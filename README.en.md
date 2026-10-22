# Russian Live Translation Assistant

[简体中文](README.md) | **English**

A Windows desktop application that captures Russian audio playing on your computer and displays streaming Chinese subtitles using **Qwen3.8-LiveTranslate-Flash-Realtime**.

Use it while watching Russian videos, listening to live streams, or following online meetings.

## Features

- **System audio capture** from a selected speaker or headphone output using WASAPI loopback.
- **Streaming Chinese subtitles** updated as translation fragments arrive.
- **Original transcripts** paired with the corresponding translations.
- **Always-on-top subtitle window** with dragging and font size controls.
- **Text export** containing timestamps, translations, and original transcripts.
- **Locally encrypted API keys** protected by Windows DPAPI.

## Requirements

| Item | Requirement |
| --- | --- |
| Operating system | Windows 10 / 11, 64-bit |
| Network | Access to the Qwen realtime translation API |
| Model access | A Qwen API key with access to the specified model |
| Building from source | .NET 10 SDK and PowerShell |
| Portable build | No separate .NET installation required |

Model ID: `qwen3.8-livetranslate-flash-realtime`. The source language is detected automatically. The target language is Chinese, and only text output is requested by default.

## Automated builds and downloads

[![Windows build](https://github.com/Kuugo2002/qwen-live-translate/actions/workflows/build-windows.yml/badge.svg)](https://github.com/Kuugo2002/qwen-live-translate/actions/workflows/build-windows.yml)

Pushes to `main`, pull requests targeting `main`, and **Run workflow** on the Actions page trigger a Windows x64 portable build.

1. Open [Build Windows app](https://github.com/Kuugo2002/qwen-live-translate/actions/workflows/build-windows.yml).
2. Open a successful run and download `qwen-live-translate-windows-x64` under **Artifacts** (GitHub sign-in required).
3. Extract the download and the application archive inside it, then open `俄语实时翻译助手.exe`.

The package includes the .NET runtime and documentation. Artifacts are retained for 14 days. CI requires no API key and makes no cloud model calls; its offline self-test checks transcript handling and endpoint validation while skipping audio hardware checks on the runner.

## Build and run

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then run from the project directory:

```powershell
# Build a Windows x64 portable application with the .NET runtime included
./build.ps1 -Portable
```

The application is generated in `dist/俄语实时翻译助手/`. Open `俄语实时翻译助手.exe` in that folder.

Alternatively, run directly from source:

```powershell
dotnet run --project QwenLiveTranslate/QwenLiveTranslate.csproj
```

Building without `-Portable` requires the .NET 10 Desktop Runtime on the target computer:

```powershell
./build.ps1
```

Generated binaries are excluded from the Git source repository.

## Usage

1. Select the audio output device used by your player.
2. Enter your own Qwen API key. It is encrypted and saved locally when you start translation.
3. Click **开始翻译** (Start translation), then play Russian audio or video.
4. Read subtitles in the main window, or click **悬浮字幕** (Floating subtitles).
5. Click **停止** (Stop) and wait for the final translation to finish.
6. Click **导出记录** (Export history) before closing if you want to keep the transcript.

Default endpoint:

```text
wss://maas.qianwenaiapi.com/api-ws/v1/realtime
```

Expand **接口设置** (Endpoint settings) to use another supported region or compatible service endpoint. The API key must match the service. The application adds the model query parameter automatically.

## Privacy and costs

- While translation is active, **all audio from the selected output device** is sent to Qwen's cloud service, including sound played by other applications.
- API charges are determined by the provider, your account plan, and actual usage.
- The API key is saved at `%LOCALAPPDATA%\QwenLiveTranslate\secret.bin`, encrypted for the current Windows user using DPAPI.
- You may provide `DASHSCOPE_API_KEY` as an environment variable. An existing locally encrypted key takes precedence.
- This repository contains no API key. Do not commit credentials, recordings, transcript exports, or local configuration.
- Translation history stays in process memory unless you export it.

## Limitations

- Captures the entire selected output device rather than an individual application.
- Other languages may also be translated because source language detection is automatic.
- Latency and accuracy depend on network conditions, audio quality, background sound, speech rate, and model segmentation.
- Protected content or exclusive audio modes may not be captured.
- The subtitle window may not appear above exclusive fullscreen applications; use windowed or borderless mode.
- Connection failures stop translation and require a manual restart.
- The desktop interface is currently Chinese. This English README does not imply an English UI.

## Project structure

```text
QwenLiveTranslate/       WPF UI, system audio capture, realtime API, transcript handling
docs/                   User guide and validation notes in Chinese
build.ps1               Windows x64 publishing script
global.json             .NET SDK version policy
README.md               Chinese documentation
README.en.md            English documentation
```

## Validation

After building, run the offline self-test on a computer with an available audio output device:

```powershell
& './dist/俄语实时翻译助手/俄语实时翻译助手.exe' --self-test
```

Test against the real API using your own audio file and key. These commands incur API charges:

```powershell
& './dist/俄语实时翻译助手/俄语实时翻译助手.exe' --probe --wav 'C:\audio\russian.wav'

# Play the file through Windows, capture it through loopback, and translate
& './dist/俄语实时翻译助手/俄语实时翻译助手.exe' --probe --wav 'C:\audio\russian.wav' --loopback
```

Results are written to `test-results/` in the working directory. Test audio and local credentials are not distributed with the source. Short audio has passed real loopback translation testing; hours-long continuous operation has not been tested.

## More documentation

- [Detailed user guide (Chinese)](docs/usage.zh-CN.md)
- [Validation notes (Chinese)](docs/validation.zh-CN.md)
- [Official Qwen realtime translation documentation](https://platform.qianwenai.com/docs/developer-guides/speech/realtime-translation)

Built with .NET 10, WPF, NAudio 2.2.1, and WebSocket.
