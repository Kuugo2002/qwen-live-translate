# 俄语实时翻译助手

**简体中文** | [English](README.en.md)

适用于 Windows 的系统声音实时翻译工具。捕获电脑正在播放的俄语声音，调用 **Qwen3.8-LiveTranslate-Flash-Realtime**，逐步显示中文字幕和俄语原文。

适用于观看俄语视频、收听网络直播，以及辅助理解线上会议等场景。

## 功能

- **系统声音采集**：选择扬声器或耳机，采集该播放设备的声音，无需对着麦克风播放。
- **流式中文字幕**：接收模型返回的增量译文，实时更新。
- **原文对照**：显示识别到的俄语原文，并与对应译文关联。
- **悬浮字幕**：置顶显示，支持拖动位置和调整字号。
- **记录导出**：将时间、中文译文和原文保存为 UTF-8 文本。
- **本机加密密钥**：通过 Windows DPAPI 加密保存 API Key。

## 环境要求

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 / 11，64 位 |
| 网络 | 能连接千问实时翻译 API |
| 模型权限 | 可调用指定模型的千问 API Key |
| 源码构建 | .NET 10 SDK、PowerShell |
| 便携版运行 | 无需额外安装 .NET |

模型 ID：`qwen3.8-livetranslate-flash-realtime`。输入源语言自动识别，目标语言固定为中文，默认只请求文字输出。

## 构建与运行

安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)，然后在项目目录运行：

```powershell
# 构建 Windows x64 便携版，包含 .NET 运行时
./build.ps1 -Portable
```

输出目录：`dist/俄语实时翻译助手/`。双击其中的 `俄语实时翻译助手.exe`。

也可以直接从源码启动：

```powershell
dotnet run --project QwenLiveTranslate/QwenLiveTranslate.csproj
```

不带 `-Portable` 的构建需要运行电脑安装 .NET 10 Desktop Runtime：

```powershell
./build.ps1
```

编译产物不纳入 Git 源码仓库。

## 使用方法

1. 选择与播放器实际输出一致的声音设备。
2. 输入自己的千问 API Key；首次开始翻译时会在本机加密保存。
3. 点击 **开始翻译**，播放俄语音频或视频。
4. 查看主窗口字幕，或点击 **悬浮字幕** 显示置顶字幕。
5. 点击 **停止**，等待最后一段翻译完成。
6. 如需保留内容，关闭程序前点击 **导出记录**。

默认接口：

```text
wss://maas.qianwenaiapi.com/api-ws/v1/realtime
```

如需使用其他地域或平台的兼容接口，可展开 **接口设置** 修改地址；API Key 必须与接口服务匹配。程序会自动添加模型查询参数。

## 隐私与费用

- 开始翻译后，**所选播放设备的全部声音**会发送给千问云端，包括其他应用同时播放的声音。
- API 调用费用由模型服务商收取，取决于账户套餐及实际用量。
- API Key 保存在 `%LOCALAPPDATA%\QwenLiveTranslate\secret.bin`，使用当前 Windows 用户的 DPAPI 加密。
- 可通过环境变量 `DASHSCOPE_API_KEY` 提供密钥；已有本机加密密钥时优先读取本机密钥。
- 仓库不包含 API Key。请勿提交自己的密钥、录音、字幕记录或本机配置。
- 翻译记录保存在当前进程内存中，需要时手动导出。

## 已知限制

- 当前采集整个播放设备，不按单个应用过滤声音。
- 模型自动识别源语言，听到其他语言时也可能翻译。
- 实际延迟和准确率取决于网络、音质、背景声、语速及模型分段。
- 部分受保护内容、独占音频模式可能无法采集。
- 悬浮字幕可能无法覆盖独占全屏应用，可改用窗口或无边框模式。
- 连接中断时显示错误并停止，需要手动重新开始。
- 桌面界面目前为中文；英文版文档不代表英文界面。

## 项目结构

```text
QwenLiveTranslate/       WPF 界面、系统声音采集、实时 API 和字幕处理
docs/                   使用说明和验证记录
build.ps1               Windows x64 发布脚本
global.json             .NET SDK 版本范围
README.md               中文说明
README.en.md            English documentation
```

## 验证

构建后运行离线自检，需要本机有可用播放设备：

```powershell
& './dist/俄语实时翻译助手/俄语实时翻译助手.exe' --self-test
```

使用自己的测试音频进行真实接口验证（会产生 API 费用）：

```powershell
& './dist/俄语实时翻译助手/俄语实时翻译助手.exe' --probe --wav 'C:\audio\russian.wav'

# 实际播放测试音频，再回环采集并翻译
& './dist/俄语实时翻译助手/俄语实时翻译助手.exe' --probe --wav 'C:\audio\russian.wav' --loopback
```

测试结果写入运行目录下的 `test-results/`。测试音频和本机密钥不会随源码提供。已完成短音频的真实回环翻译验证，尚未进行数小时连续运行测试。

## 更多文档

- [详细使用说明](docs/usage.zh-CN.md)
- [验证记录](docs/validation.zh-CN.md)
- [千问实时翻译官方文档](https://platform.qianwenai.com/docs/developer-guides/speech/realtime-translation)

技术栈：.NET 10、WPF、NAudio 2.2.1、WebSocket。
