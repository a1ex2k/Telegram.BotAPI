using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Telegram.BotAPI.Benchmarks;

/// <summary>
/// A loopback HTTP/1.1 server that streams chunked Telegram Bot API responses
/// over real TCP sockets. It intentionally avoids HttpMessageHandler/StringContent,
/// which would not exercise the HttpClient transport response stream.
/// </summary>
internal sealed class MockTelegramHttpServer : IAsyncDisposable
{
    private const int ChunkSize = 512;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource shutdown = new();
    private readonly byte[] responseBody;
    private readonly Task acceptLoop;

    public MockTelegramHttpServer(byte[] responseBody)
    {
        ArgumentNullException.ThrowIfNull(responseBody);
        this.responseBody = responseBody;

        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        BaseAddress = new Uri($"http://127.0.0.1:{port}/");
        acceptLoop = AcceptLoopAsync();
    }

    public Uri BaseAddress { get; }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(shutdown.Token)
                    .ConfigureAwait(false);
                _ = HandleClientAsync(client);
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (shutdown.IsCancellationRequested)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        await using (NetworkStream stream = client.GetStream())
        {
            try
            {
                using StreamReader reader = new(
                    stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024, leaveOpen: true);

                // The benchmark issues body-less GET requests to getUpdates.
                string? requestLine = await reader.ReadLineAsync(shutdown.Token).ConfigureAwait(false);
                if (requestLine is null)
                    return;

                string? header;
                do
                {
                    header = await reader.ReadLineAsync(shutdown.Token).ConfigureAwait(false);
                }
                while (!string.IsNullOrEmpty(header));

                byte[] headers = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: application/json; charset=utf-8\r\n" +
                    "Transfer-Encoding: chunked\r\n" +
                    "Connection: close\r\n\r\n");

                await stream.WriteAsync(headers, shutdown.Token).ConfigureAwait(false);

                for (int offset = 0; offset < responseBody.Length; offset += ChunkSize)
                {
                    int count = Math.Min(ChunkSize, responseBody.Length - offset);
                    byte[] chunkHeader = Encoding.ASCII.GetBytes($"{count:X}\r\n");

                    await stream.WriteAsync(chunkHeader, shutdown.Token).ConfigureAwait(false);
                    await stream.WriteAsync(
                        responseBody.AsMemory(offset, count), shutdown.Token).ConfigureAwait(false);
                    await stream.WriteAsync("\r\n"u8.ToArray(), shutdown.Token).ConfigureAwait(false);
                    await stream.FlushAsync(shutdown.Token).ConfigureAwait(false);
                }

                await stream.WriteAsync("0\r\n\r\n"u8.ToArray(), shutdown.Token).ConfigureAwait(false);
                await stream.FlushAsync(shutdown.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is IOException or SocketException or OperationCanceledException)
            {
                // A client may cancel a benchmark operation while the response is being streamed.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        shutdown.Cancel();
        listener.Stop();

        try
        {
            await acceptLoop.ConfigureAwait(false);
        }
        finally
        {
            shutdown.Dispose();
        }
    }
}
