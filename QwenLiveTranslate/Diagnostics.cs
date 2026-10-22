using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.CoreAudioApi;

namespace QwenLiveTranslate;

public static class Diagnostics
{
    public static async Task<int> RunAsync(string[] args)
    {
        var report = new List<string>();
        int code = 0;
        try
        {
            if (args.Contains("--self-test"))
            {
                var store = new TranscriptStore();
                var rows = new List<Transcript>();
                store.Added += rows.Add;
                void Apply(string json) { using var doc = JsonDocument.Parse(json); store.Apply(doc.RootElement); }
                Apply("""{"type":"conversation.item.input_audio_transcription.delta","item_id":"ru1","delta":"Привет"}""");
                Apply("""{"type":"conversation.item.created","previous_item_id":"ru1","item":{"id":"zh1","role":"assistant"}}""");
                Apply("""{"type":"response.text.delta","item_id":"zh1","delta":"你"}""");
                Apply("""{"type":"response.text.delta","item_id":"zh1","delta":"好"}""");
                Apply("""{"type":"response.text.done","item_id":"zh1","text":"你好！"}""");
                Apply("""{"type":"conversation.item.input_audio_transcription.completed","item_id":"ru1","transcript":"Привет!"}""");
                if (rows.Count != 1 || rows[0].Chinese != "你好！" || rows[0].Russian != "Привет!") throw new Exception("字幕流合并与原译文关联失败。");
                Apply("""{"type":"conversation.item.created","previous_item_id":"zh1","item":{"id":"ru2","role":"assistant","content":[{"type":"input_audio"}]}}""");
                Apply("""{"type":"conversation.item.input_audio_transcription.delta","item_id":"ru2","delta":"Сегодня"}""");
                Apply("""{"type":"conversation.item.created","previous_item_id":"ru2","item":{"id":"zh2","role":"assistant","content":[]}}""");
                Apply("""{"type":"response.text.delta","item_id":"zh2","delta":"今天"}""");
                if (rows.Count != 2 || rows[1].Chinese != "今天" || rows[1].Russian != "Сегодня" || rows[0].Chinese != "你好！") throw new Exception("连续语句的原译文关联失败。");
                report.Add("PASS: 流式增量、最终文本覆盖、原译文关联。");
                var settings = new Settings();
                if (!settings.GetUri().Query.Contains(Settings.Model)) throw new Exception("模型 URI 错误。");
                settings.Endpoint = "http://invalid.test";
                try { settings.GetUri(); throw new Exception("未拒绝明文接口。"); } catch (InvalidOperationException) { }
                report.Add("PASS: 模型地址及加密连接校验。");
                var devices = SystemAudio.Devices();
                if (devices.Count == 0) throw new Exception("未发现系统声音设备。");
                using var audio = new SystemAudio(devices[0].Id);
                audio.Start();
                await Task.Delay(1200);
                report.Add($"PASS: WASAPI 16kHz 单声道 PCM16 采集启动，{devices.Count} 个播放设备。");
            }
            if (args.Contains("--probe"))
            {
                var key = Settings.LoadKey();
                if (key.Length == 0) throw new Exception("未配置密钥。");
                await using var client = new RealtimeClient();
                var translated = "";
                var types = new HashSet<string>();
                var transcriptStore = new TranscriptStore();
                var transcriptRows = new List<Transcript>();
                transcriptStore.Added += transcriptRows.Add;
                client.Event += e =>
                {
                    transcriptStore.Apply(e);
                    var type = e.GetProperty("type").GetString() ?? "";
                    types.Add(type);
                    if (type == "session.updated") report.Add("PASS: 模型会话已配置为中文文本输出。");
                    if (type == "response.text.delta") translated += e.GetProperty("delta").GetString();
                    if (type == "response.text.done" && e.TryGetProperty("text", out var text)) { translated = text.GetString() ?? translated; report.Add("译文：" + translated); }
                };
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var configuration = Settings.Load();
                var endpointIndex = Array.IndexOf(args, "--endpoint");
                if (endpointIndex >= 0) configuration.Endpoint = args[endpointIndex + 1];
                await client.ConnectAsync(configuration, key, timeout.Token);
                var index = Array.IndexOf(args, "--wav");
                if (index >= 0)
                {
                    using var input = new AudioFileReader(args[index + 1]);
                    if (args.Contains("--loopback"))
                    {
                        using var enumerator = new MMDeviceEnumerator();
                        using var device = enumerator.GetDevice(SystemAudio.DefaultDeviceId());
                        using var capture = new SystemAudio(device.ID);
                        using var player = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);
                        player.Init(input);
                        capture.Start();
                        using var captureStop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                        var pump = capture.PumpAsync(client, _ => { }, captureStop.Token);
                        player.Play();
                        while (player.PlaybackState == PlaybackState.Playing) await Task.Delay(100, timeout.Token);
                        await Task.Delay(2000, timeout.Token);
                        captureStop.Cancel();
                        try { await pump; } catch (OperationCanceledException) when (captureStop.IsCancellationRequested) { }
                        report.Add("PASS: 测试音频经 Windows 播放设备输出，再通过 WASAPI 回环采集上传。");
                    }
                    else
                    {
                        ISampleProvider source = input;
                        if (source.WaveFormat.Channels == 2) source = new StereoToMonoSampleProvider(source);
                        if (source.WaveFormat.Channels != 1) throw new Exception("测试音频必须为单声道或双声道。");
                        source = new WdlResamplingSampleProvider(source, 16000);
                        var pcm = new SampleToWaveProvider16(source);
                        var block = new byte[3200];
                        int read;
                        while ((read = pcm.Read(block, 0, block.Length)) > 0)
                        {
                            await client.SendAudioAsync(block[..read], timeout.Token);
                            await Task.Delay(100, timeout.Token);
                        }
                        for (var i = 0; i < 15; i++) { await client.SendAudioAsync(new byte[3200], timeout.Token); await Task.Delay(100, timeout.Token); }
                    }
                }
                await client.FinishAsync();
                if (index >= 0 && !Regex.IsMatch(translated, @"\p{IsCJKUnifiedIdeographs}")) throw new Exception("未收到中文翻译。事件：" + string.Join(", ", types));
                if (index >= 0 && (transcriptRows.Count == 0 || transcriptRows.Any(row => string.IsNullOrWhiteSpace(row.Russian) || string.IsNullOrWhiteSpace(row.Chinese)))) throw new Exception("测试音频的原译文没有正确关联。");
                if (index >= 0) report.Add($"PASS: {transcriptRows.Count} 段真实原文和译文逐一关联。");
                if (index >= 0) report.Add("PASS: 真实音频流翻译：" + translated);
                report.Add("PASS: session.finish 收尾完成。");
                report.Add("Events: " + string.Join(", ", types));
            }
        }
        catch (Exception ex) { code = 1; report.Add("FAIL: " + ex.Message); }
        Directory.CreateDirectory("test-results");
        await File.WriteAllLinesAsync(Path.Combine("test-results", args.Contains("--probe") ? "probe.txt" : "self-test.txt"), report);
        return code;
    }
}
