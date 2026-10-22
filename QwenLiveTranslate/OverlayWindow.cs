using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QwenLiveTranslate;

public sealed class OverlayWindow : Window
{
    readonly TextBlock subtitle = new() { Text = "等待中文翻译…", FontSize = 30, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, Margin = new Thickness(18, 8, 18, 18) };
    public OverlayWindow()
    {
        Title = "悬浮字幕"; Width = 860; Height = 180; MinWidth = 400; MinHeight = 110;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.CanResizeWithGrip;
        Left = (SystemParameters.WorkArea.Width - Width) / 2; Top = SystemParameters.WorkArea.Bottom - Height - 45;
        var border = new Border { Background = new SolidColorBrush(Color.FromArgb(225, 19, 29, 43)), CornerRadius = new CornerRadius(12) };
        var dock = new DockPanel();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
        var label = new TextBlock { Text = "拖动移动字幕", Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
        bar.Children.Add(label);
        AddButton(bar, "A−", () => subtitle.FontSize = Math.Max(16, subtitle.FontSize - 2));
        AddButton(bar, "A+", () => subtitle.FontSize = Math.Min(64, subtitle.FontSize + 2));
        AddButton(bar, "关闭", Hide);
        DockPanel.SetDock(bar, Dock.Top); dock.Children.Add(bar); dock.Children.Add(subtitle); border.Child = dock; Content = border;
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }
    static void AddButton(Panel panel, string text, Action action)
    { var b = new Button { Content = text, Padding = new Thickness(9, 3, 9, 3) }; b.Click += (_, _) => action(); panel.Children.Add(b); }
    public void Update(string text) => subtitle.Text = text;
}
