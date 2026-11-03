using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace AITeach1.Services;

internal sealed class ChromaServer : IDisposable
{
    private Process? _process;
    private string? _databasePath;
    private HttpClient? _httpClient;

    public async Task<HttpClient> StartAsync(string databasePath, CancellationToken cancellationToken)
    {
        databasePath = Path.GetFullPath(databasePath);
        if (_process is { HasExited: false } && string.Equals(_databasePath, databasePath, StringComparison.OrdinalIgnoreCase))
        {
            return _httpClient!;
        }

        StopServer();
        Directory.CreateDirectory(databasePath);

        var port = GetFreePort();
        var startInfo = new ProcessStartInfo
        {
            FileName = "chroma",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--path");
        startInfo.ArgumentList.Add(databasePath);
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString());

        try
        {
            _process = Process.Start(startInfo);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException(
                "The ChromaDB command-line tool was not found. Install ChromaDB with Python (pip install chromadb) and ensure the chroma command is on PATH.",
                exception);
        }

        if (_process is null)
        {
            throw new InvalidOperationException("Could not start the ChromaDB server process.");
        }

        _databasePath = databasePath;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}/"),
            Timeout = TimeSpan.FromMinutes(5)
        };

        for (var attempt = 0; attempt < 60; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_process.HasExited)
            {
                var exitCode = _process.ExitCode;
                StopServer();
                throw new InvalidOperationException($"ChromaDB exited during startup (code {exitCode}). Check the ChromaDB installation.");
            }

            try
            {
                using var response = await _httpClient.GetAsync("api/v2/heartbeat", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return _httpClient;
                }
            }
            catch (HttpRequestException)
            {
                // The ChromaDB server is still starting.
            }

            await Task.Delay(500, cancellationToken);
        }

        StopServer();
        throw new InvalidOperationException("ChromaDB did not start within 30 seconds.");
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private void StopServer()
    {
        _httpClient?.Dispose();
        _httpClient = null;

        if (_process is { HasExited: false })
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
            catch (InvalidOperationException)
            {
                // The process already exited.
            }
        }

        _process?.Dispose();
        _process = null;
        _databasePath = null;
    }

    public void Dispose() => StopServer();
}
