using System.Net.Http.Json;
using System.Text.Json;

namespace AITeach1.Services;

internal sealed class OllamaEmbeddingClient : IDisposable
{
    private const string ModelName = "nomic-embed-text";
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:11434/"),
        Timeout = TimeSpan.FromMinutes(5)
    };

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/embed",
                new { model = ModelName, input = texts },
                cancellationToken);

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var hint = responseText.Contains("model", StringComparison.OrdinalIgnoreCase)
                    ? $"Make sure Ollama has the {ModelName} model."
                    : "Check that Ollama is running.";
                throw new InvalidOperationException($"Ollama returned {(int)response.StatusCode}: {responseText}\n{hint}");
            }

            using var json = JsonDocument.Parse(responseText);
            if (!json.RootElement.TryGetProperty("embeddings", out var embeddingsElement))
            {
                throw new InvalidOperationException("Ollama's response did not include embeddings.");
            }

            var embeddings = embeddingsElement.EnumerateArray()
                .Select(vector => vector.EnumerateArray().Select(value => value.GetSingle()).ToArray())
                .ToArray();

            if (embeddings.Length != texts.Count)
            {
                throw new InvalidOperationException("Ollama returned a different number of embeddings than input chunks.");
            }

            return embeddings;
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "Cannot connect to Ollama at http://127.0.0.1:11434. Start Ollama and make sure the nomic-embed-text model is available.",
                exception);
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
