using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AITeach1.Services;

internal sealed record IndexProgress(string Message, int Percent);

internal sealed class RagIndexBuilder : IDisposable
{
    private const int BatchSize = 24;
    private const string CollectionName = "ait_each_documents";
    private readonly ChromaServer _chromaServer = new();
    private readonly OllamaEmbeddingClient _ollama = new();

    public async Task<int> BuildAsync(
        string folderPath,
        IProgress<IndexProgress> progress,
        CancellationToken cancellationToken)
    {
        folderPath = Path.GetFullPath(folderPath);
        var databasePath = Path.Combine(folderPath, "chroma_db");
        var httpClient = await _chromaServer.StartAsync(databasePath, cancellationToken);

        progress.Report(new IndexProgress("Scanning for .doc, .docx, .pdf, and .txt files…", 2));
        var files = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(file => IsSupported(file) && !IsInsideDatabase(file, databasePath))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            throw new InvalidOperationException("No .doc, .docx, .pdf, or .txt files were found in the selected folder or its subfolders.");
        }

        var chunks = new List<IndexChunk>();
        var warnings = new List<string>();
        for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = files[fileIndex];
            progress.Report(new IndexProgress($"Reading {Path.GetFileName(filePath)} ({fileIndex + 1}/{files.Length})…", 5 + fileIndex * 20 / files.Length));

            try
            {
                var text = await Task.Run(() => DocumentTextExtractor.Extract(filePath), cancellationToken);
                var relativePath = Path.GetRelativePath(folderPath, filePath);
                var fileChunks = DocumentTextExtractor.SplitIntoChunks(text)
                    .Select((chunk, index) => new IndexChunk(
                        CreateId(relativePath, index),
                        chunk,
                        relativePath,
                        index))
                    .ToArray();
                chunks.AddRange(fileChunks);
                if (fileChunks.Length == 0)
                {
                    warnings.Add($"No text extracted from {relativePath}");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                warnings.Add($"Skipped {Path.GetRelativePath(folderPath, filePath)}: {exception.Message}");
            }
        }

        if (chunks.Count == 0)
        {
            var warningText = warnings.Count == 0 ? string.Empty : $"\n{string.Join("\n", warnings.Take(5))}";
            throw new InvalidOperationException($"No readable text was found in the supported files.{warningText}");
        }

        var collectionId = await GetOrCreateCollectionAsync(httpClient, cancellationToken);
        var firstBatch = chunks.Take(BatchSize).ToArray();
        progress.Report(new IndexProgress($"Checking Ollama and embedding the first {firstBatch.Length} chunks…", 25));
        var firstEmbeddings = await _ollama.EmbedAsync(firstBatch.Select(chunk => chunk.Text).ToArray(), cancellationToken);

        await DeleteFolderDocumentsAsync(httpClient, collectionId, folderPath, cancellationToken);
        await UpsertAsync(httpClient, collectionId, folderPath, firstBatch, firstEmbeddings, cancellationToken);

        var completedChunks = firstBatch.Length;
        for (var start = BatchSize; start < chunks.Count; start += BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = chunks.Skip(start).Take(BatchSize).ToArray();
            progress.Report(new IndexProgress(
                $"Creating embeddings ({completedChunks}/{chunks.Count} chunks)…",
                25 + completedChunks * 70 / chunks.Count));

            var embeddings = await _ollama.EmbedAsync(batch.Select(chunk => chunk.Text).ToArray(), cancellationToken);
            await UpsertAsync(httpClient, collectionId, folderPath, batch, embeddings, cancellationToken);
            completedChunks += batch.Length;
        }

        foreach (var warning in warnings.Take(5))
        {
            progress.Report(new IndexProgress(warning, 98));
        }

        progress.Report(new IndexProgress(
            $"Index complete: {files.Length} files scanned, {chunks.Count} chunks stored in {databasePath}.",
            100));
        return chunks.Count;
    }

    private static bool IsSupported(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() is ".doc" or ".docx" or ".pdf" or ".txt";

    private static bool IsInsideDatabase(string filePath, string databasePath)
    {
        var fullFilePath = Path.GetFullPath(filePath);
        var fullDatabasePath = Path.GetFullPath(databasePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullFilePath.StartsWith(fullDatabasePath, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateId(string relativePath, int chunkIndex)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{relativePath.ToUpperInvariant()}:{chunkIndex}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static async Task<string> GetOrCreateCollectionAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/v2/tenants/default_tenant/databases/default_database/collections",
            new { name = CollectionName, get_or_create = true },
            cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"ChromaDB could not create the collection: {responseBody}");
        }

        using var json = JsonDocument.Parse(responseBody);
        return json.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("ChromaDB did not return a collection ID.");
    }

    private static async Task DeleteFolderDocumentsAsync(
        HttpClient httpClient,
        string collectionId,
        string folderPath,
        CancellationToken cancellationToken)
    {
        var endpoint = $"api/v2/tenants/default_tenant/databases/default_database/collections/{collectionId}/delete";
        using var response = await httpClient.PostAsJsonAsync(
            endpoint,
            new { where = new Dictionary<string, object> { ["folder"] = folderPath } },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"ChromaDB could not replace the previous folder index: {body}");
        }
    }

    private static async Task UpsertAsync(
        HttpClient httpClient,
        string collectionId,
        string folderPath,
        IReadOnlyList<IndexChunk> chunks,
        IReadOnlyList<float[]> embeddings,
        CancellationToken cancellationToken)
    {
        var endpoint = $"api/v2/tenants/default_tenant/databases/default_database/collections/{collectionId}/upsert";
        var payload = new
        {
            ids = chunks.Select(chunk => chunk.Id).ToArray(),
            embeddings,
            documents = chunks.Select(chunk => chunk.Text).ToArray(),
            metadatas = chunks.Select(chunk => new Dictionary<string, object>
            {
                ["folder"] = folderPath,
                ["source"] = chunk.Source,
                ["chunk_index"] = chunk.ChunkIndex
            }).ToArray()
        };

        using var response = await httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"ChromaDB could not store the document chunks: {body}");
        }
    }

    public void Dispose()
    {
        _chromaServer.Dispose();
        _ollama.Dispose();
    }

    private sealed record IndexChunk(string Id, string Text, string Source, int ChunkIndex);
}
