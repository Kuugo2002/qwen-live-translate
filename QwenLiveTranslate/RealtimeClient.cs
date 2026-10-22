using System.IO;
using System.Net.WebSockets;
using System.Text.Json;

namespace QwenLiveTranslate;

public sealed class RealtimeClient : IAsyncDisposable
{
    readonly ClientWebSocket socket = new();
    readonly SemaphoreSlim sendLock = new(1, 1);
    readonly CancellationTokenSource lifetime = new();
    readonly TaskCompletionSource updated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    Task? receiver;
    public event Action<JsonElement>? Event;
    public event Action<Exception>? Failed;
    public async Task ConnectAsync(Settings settings, string key, CancellationToken ct)
    {
        socket.Options.SetRequestHeader("Authorization", "Bearer " + key.Trim());
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await socket.ConnectAsync(settings.GetUri(), timeout.Token);
        receiver = ReceiveAsync();
        await SendAsync(new
        {
            type = "session.update",
            session = new
            {
                output_modalities = new[] { "text" },
                translation = new { language = "zh" },
                audio = new { input = new { turn_detection = new { type = "server_vad", threshold = 0.5 } } }
            }
        }, timeout.Token);
        await updated.Task.WaitAsync(timeout.Token);
    }
    public Task SendAudioAsync(byte[] pcm, CancellationToken ct) => SendAsync(new { type = "input_audio_buffer.append", audio = Convert.ToBase64String(pcm) }, ct);
    async Task SendAsync(object data, CancellationToken ct)
    {
        await sendLock.WaitAsync(ct);
        try { await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(data).AsMemory(), WebSocketMessageType.Text, true, ct); }
        finally { sendLock.Release(); }
    }
    async Task ReceiveAsync()
    {
        try
        {
            var buffer = new byte[32768];
            while (!lifetime.IsCancellationRequested)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), lifetime.Token);
                    if (result.MessageType == WebSocketMessageType.Close) throw new IOException("服务端已断开连接，请重新开始翻译。");
                    message.Write(buffer, 0, result.Count);
                    if (message.Length > 4 * 1024 * 1024) throw new IOException("服务端消息过大。");
                } while (!result.EndOfMessage);
                using var doc = JsonDocument.Parse(message.ToArray());
                var ev = doc.RootElement;
                var type = ev.GetProperty("type").GetString();
                if (type == "error") throw new InvalidOperationException(ev.GetProperty("error").ToString());
                if (type == "session.updated") updated.TrySetResult();
                Event?.Invoke(ev.Clone());
                if (type == "session.finished") { finished.TrySetResult(); return; }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            updated.TrySetException(ex);
            finished.TrySetException(ex);
            Failed?.Invoke(ex);
        }
    }
    public async Task FinishAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        await SendAsync(new { type = "session.finish" }, timeout.Token);
        await finished.Task.WaitAsync(timeout.Token);
    }
    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        socket.Abort();
        if (receiver is not null) await receiver;
        socket.Dispose();
        lifetime.Dispose();
        sendLock.Dispose();
    }
}
