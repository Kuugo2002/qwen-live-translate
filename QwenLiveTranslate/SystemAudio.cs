using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace QwenLiveTranslate;

public sealed record AudioDevice(string Id, string Name)
{
    public override string ToString() => Name;
}
public sealed class SystemAudio : IDisposable
{
    readonly MMDevice device;
    readonly WasapiLoopbackCapture capture;
    readonly BufferedWaveProvider buffer;
    readonly object gate = new();
    Exception? failure;
    public static List<AudioDevice> Devices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Select(d => { using (d) return new AudioDevice(d.ID, d.FriendlyName); }).ToList();
    }
    public static string DefaultDeviceId()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var current = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return current.ID;
    }
    public SystemAudio(string deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        device = string.IsNullOrEmpty(deviceId) ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) : enumerator.GetDevice(deviceId);
        capture = new WasapiLoopbackCapture(device) { WaveFormat = new WaveFormat(16000, 16, 1) };
        buffer = new BufferedWaveProvider(capture.WaveFormat) { BufferDuration = TimeSpan.FromSeconds(5), ReadFully = true };
        capture.DataAvailable += (_, e) => { lock (gate) { try { buffer.AddSamples(e.Buffer, 0, e.BytesRecorded); } catch (Exception ex) { failure = ex; } } };
        capture.RecordingStopped += (_, e) => { if (e.Exception is not null) failure = e.Exception; };
    }
    public void Start() => capture.StartRecording();
    public async Task PumpAsync(RealtimeClient client, Action<double> level, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        while (await timer.WaitForNextTickAsync(ct))
        {
            var pcm = new byte[3200];
            lock (gate)
            {
                if (failure is not null) throw new IOException("声音采集失败或网络太慢，请重新开始。", failure);
                buffer.Read(pcm, 0, pcm.Length);
            }
            double peak = 0;
            for (var i = 0; i < pcm.Length; i += 2) peak = Math.Max(peak, Math.Abs((double)BitConverter.ToInt16(pcm, i)) / 32768);
            level(peak);
            await client.SendAudioAsync(pcm, ct);
        }
    }
    public void Dispose() { capture.StopRecording(); capture.Dispose(); device.Dispose(); }
}
