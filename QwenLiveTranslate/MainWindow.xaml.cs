using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace QwenLiveTranslate;

public partial class MainWindow : Window
{
    readonly Settings settings = Settings.Load();
    readonly ObservableCollection<Transcript> rows = new();
    TranscriptStore store = new();
    readonly OverlayWindow overlay = new();
    RealtimeClient? client;
    SystemAudio? audio;
    CancellationTokenSource? running;
    Task? pump;
    bool busy, stopping, closing, allowClose;
    public MainWindow()
    {
        InitializeComponent();
        KeyBox.Password = Settings.LoadKey(); EndpointBox.Text = settings.Endpoint;
        HistoryList.ItemsSource = rows;
        BindStore(); RefreshDevices();
    }
    void BindStore()
    {
        store.Added += row => { rows.Add(row); HistoryScroll.ScrollToBottom(); };
        store.Translated += row => { if (row.Chinese.Length > 0) { CurrentText.Text = row.Chinese; overlay.Update(row.Chinese); } HistoryScroll.ScrollToBottom(); };
    }
    void RefreshDevices()
    {
        try
        {
            var devices = SystemAudio.Devices();
            DeviceBox.ItemsSource = devices;
            var preferred = string.IsNullOrEmpty(settings.DeviceId) && devices.Count > 0 ? SystemAudio.DefaultDeviceId() : settings.DeviceId;
            DeviceBox.SelectedItem = devices.FirstOrDefault(d => d.Id == preferred) ?? devices.FirstOrDefault();
            if (devices.Count == 0) StatusText.Text = "未发现播放设备，请连接扬声器或耳机后刷新。";
        }
        catch (Exception ex) { StatusText.Text = "无法列出声音设备：" + ex.Message; }
    }
    void Refresh_Click(object sender, RoutedEventArgs e) { if (!busy) RefreshDevices(); }
    async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (string.IsNullOrWhiteSpace(KeyBox.Password)) { StatusText.Text = "请先填写 API 密钥。"; return; }
        if (DeviceBox.SelectedItem is not AudioDevice selected) { StatusText.Text = "请先选择声音设备。"; return; }
        SetBusy(true); running = new();
        try
        {
            settings.Endpoint = EndpointBox.Text.Trim(); settings.DeviceId = selected.Id;
            settings.GetUri(); settings.Save(KeyBox.Password.Trim());
            // A new store gives every connection its own item namespace, preserving visible history.
            store = new(); BindStore();
            StatusText.Text = "正在连接千问实时翻译…";
            var session = new RealtimeClient(); client = session;
            session.Event += ev => Dispatcher.BeginInvoke(() => { if (ReferenceEquals(client, session)) OnEvent(ev); });
            session.Failed += ex => Dispatcher.BeginInvoke(async () => { if (ReferenceEquals(client, session) && !stopping) await StopAsync("连接中断：" + FriendlyError(ex), false); });
            await session.ConnectAsync(settings, KeyBox.Password, running.Token);
            audio = new SystemAudio(selected.Id); audio.Start();
            StatusText.Text = "翻译中 · 播放俄语音频，中文会逐步显示 · 仅翻译所选播放设备的声音";
            StopButton.IsEnabled = true;
            pump = PumpAsync(session, audio, running.Token);
        }
        catch (Exception ex) { await StopAsync("启动失败：" + FriendlyError(ex), false); }
    }
    async Task PumpAsync(RealtimeClient session, SystemAudio source, CancellationToken ct)
    {
        try { await source.PumpAsync(session, level => Dispatcher.BeginInvoke(() => LevelBar.Value = level), ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { _ = Dispatcher.InvokeAsync(async () => await StopAsync("翻译停止：" + FriendlyError(ex), false)); }
    }
    void OnEvent(JsonElement ev)
    {
        store.Apply(ev);
        if (ev.GetProperty("type").GetString() == "conversation.item.input_audio_transcription.failed") StatusText.Text = "此段原文识别失败，正在继续接收声音。";
        if (ev.GetProperty("type").GetString() == "response.done" && ev.TryGetProperty("response", out var response) && response.TryGetProperty("status", out var status) && status.GetString() == "failed")
            StatusText.Text = "本段翻译失败：" + (response.TryGetProperty("status_details", out var details) ? details.ToString() : "请稍后重新开始。");
    }
    async void Stop_Click(object sender, RoutedEventArgs e) => await StopAsync("已停止 · 记录可导出", true);
    async Task StopAsync(string message, bool graceful)
    {
        if (stopping) return;
        stopping = true; StopButton.IsEnabled = false;
        StatusText.Text = graceful ? "正在结束，等待最后一段字幕…" : message;
        try
        {
            running?.Cancel();
            if (pump is not null) await pump;
            audio?.Dispose(); audio = null;
            if (client is not null)
            {
                if (graceful) { try { await client.FinishAsync(); } catch { message = "已停止 · 最后一段未确认完成，已显示的记录仍可导出"; } }
                await client.DisposeAsync(); client = null;
            }
        }
        catch (Exception ex) { message = "停止时遇到问题：" + FriendlyError(ex); }
        finally
        {
            running?.Dispose(); running = null; pump = null;
            LevelBar.Value = 0; SetBusy(false); stopping = false; StatusText.Text = message;
        }
    }
    static string FriendlyError(Exception ex) => ex is OperationCanceledException ? "请求超时，请检查网络和接口地址。" : ex.Message;
    void SetBusy(bool value)
    {
        busy = value; StartButton.IsEnabled = !value; DeviceBox.IsEnabled = !value;
        EndpointBox.IsEnabled = !value; KeyBox.IsEnabled = !value;
    }
    void Overlay_Click(object sender, RoutedEventArgs e) { if (overlay.IsVisible) overlay.Hide(); else overlay.Show(); }
    void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (busy) { StatusText.Text = "请停止翻译后再清空记录。"; return; }
        rows.Clear(); store.Clear(); CurrentText.Text = "记录已清空，等待下一次翻译。"; overlay.Update("等待中文翻译…");
    }
    void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "文本文件 (*.txt)|*.txt", FileName = "俄译中记录-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt" };
        if (dialog.ShowDialog() != true) return;
        try { File.WriteAllText(dialog.FileName, string.Join(Environment.NewLine + Environment.NewLine, rows.Select(r => $"[{r.Time}]\n中文：{r.Chinese}\n原文：{r.Russian}")), new UTF8Encoding(true)); StatusText.Text = "记录已导出。"; }
        catch (Exception ex) { StatusText.Text = "导出失败：" + ex.Message; }
    }
    async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        await System.Windows.Threading.Dispatcher.Yield();
        // Let an in-flight connect or stop complete before disposing shared resources.
        running?.Cancel();
        while (stopping || (busy && pump is null)) await Task.Delay(50);
        await StopAsync("已关闭", true);
        overlay.Close(); allowClose = true; Close();
    }
}
