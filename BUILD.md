# Windows x64 构建

固定编辑器：**Unity 6 LTS 6000.0.23f1**（不是“最新版本”声明）。[官方版本页面](https://unity.com/releases/editor/whats-new/6000.0.23f1)。包：URP 17.0.3、Unity UI 2.0.0、Test Framework 1.4.5，详见 Packages/manifest.json 与 packages-lock.json。URP 为该编辑器自带的实际版本。首次包解析需要联网；运行构建产物不需要联网。

## 环境与首次打开

使用 Windows Unity Editor 与 Unity Hub。默认构建后端 Mono；Windows Editor 自带 Windows Mono 构建能力。若改为 IL2CPP，需要 Hub 的 Windows Build Support (IL2CPP)，Visual Studio C++ 与 Windows SDK；本项目不要求改为 IL2CPP。

1. 安装指定编辑器并通过 Hub 添加当前项目根目录。
2. 等待包解析及脚本编译完成。自动初始化创建 URP 管线、渲染器、场景与构建场景列表。未自动执行时选择 `CineScape > 初始化项目`。
3. 打开 `Assets/Scenes/Cinema.unity`，按 Play；运行时程序化生成影院和 UI，无需手工摆放模型。
4. Window > General > Test Runner，运行 EditMode 测试。
5. 点击 `CineScape > 构建 Windows x64`，或 Build Profiles 选择 Windows、x64。输出 `Builds/Windows/CineScape3D.exe`。

本项目已完成真实编辑器初始化，ProjectSettings、场景/URP 资源、.meta 与包锁文件均已生成。本机编辑器不在 Hub 默认目录，而在 `D:\CineScapeBuildTools\Unity-6000.0.23f1\Editor`。

## 自动构建

在项目根目录 PowerShell 执行（编辑器路径按安装位置调整）：

```powershell
.\Tools\Build-Windows.ps1 -UnityEditor 'C:\Program Files\Unity\Hub\Editor\6000.0.23f1\Editor\Unity.exe'
```

等效命令：

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.23f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\Users\seki9\Documents\CineScape3D' -buildTarget Win64 -executeMethod CineScape.Editor.ProjectBuilder.BuildWindows -logFile 'Builds/build.log'
```

自动 EditMode 测试（不要添加 `-quit`，Test Runner 完成后退出）：

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.23f1\Editor\Unity.exe' -batchmode -projectPath 'C:\Users\seki9\Documents\CineScape3D' -runTests -testPlatform EditMode -testResults 'Builds/editmode.xml' -logFile 'Builds/tests.log'
```

自定义输出用 `-outputPath`，构建日志记录在 Builds/build.log。发布保留 **整个**输出文件夹：EXE、CineScape3D_Data、UnityPlayer.dll、MonoBleedingEdge 和其他生成文件。可压缩整个目录分发，禁止只复制 EXE。

## 本机已验证命令

```powershell
.\Tools\Build-Windows.ps1 -UnityEditor 'D:\CineScapeBuildTools\Unity-6000.0.23f1\Editor\Unity.exe'
```

脚本自动优先使用同目录 Unity.com，确保命令行等待执行完成并拿到退出码。2026-10-08 已成功生成 Windows x64 标准 Unity 构建，日志为 `Builds/build.log`。已在 Windows 11 / Intel UHD Graphics 上进行 EXE 启动检查；Windows 10 与无 Editor 的第二台电脑运行仍需验证。

播放测试需要本地测试片，通过环境变量指定：

```powershell
$env:CINESCAPE_TEST_VIDEO_PATH = 'C:\Users\seki9\Documents\CineScape3D\Builds\test-media\test-h264-aac.mp4'
& 'D:\CineScapeBuildTools\Unity-6000.0.23f1\Editor\Unity.com' -batchmode -projectPath 'C:\Users\seki9\Documents\CineScape3D' -runTests -testPlatform PlayMode -testResults 'Builds/playmode.xml' -logFile 'Builds/playmode.log'
```

未指定测试片时该 PlayMode 测试会跳过，不能算播放验收通过。测试片由本机 FFmpeg 的 testsrc2/sine 生成，没有下载第三方影片，不随分发包提供。

当前编辑器是从经数字签名验证的官方 NSISBI 安装包提取至 D 盘，并在编辑器目录补充了同包附带的微软 VC++ 2010 x64 DLL，未改写系统运行库。`Tools/Extract-UnityEditor.py` 是针对该安装包的受限文件提取辅助工具，使用 Python 标准库；不执行安装脚本。常规用户仍推荐使用 Unity Hub 安装。

## MKV 直接播放组件

Assets/StreamingAssets/VLC 包含官方 LibVLC 3.0.21 的 DLL、plugins、许可与源码归档，随 Unity 构建复制。不可只复制 DLL 或 EXE。当前集成使用 CPU 解码与内存帧回调，不承诺 4K 高帧率硬解或 HDR 显示。

建议构建到新目录，避免旧版 FFmpeg 组件残留：

`Tools/Build-Windows.ps1 -UnityEditor 'D:\CineScapeBuildTools\Unity-6000.0.23f1\Editor\Unity.exe' -OutputPath 'Builds/Windows-Cinema/CineScape3D.exe'`

PlayMode 测试设置 CINESCAPE_TEST_VIDEO_PATH 为约 8 秒的 MKV；CINESCAPE_LONG_MKV_PATH 为约 10 分钟的 MKV；CINESCAPE_FRAME_MKV_PATH 为测试色块样片。StandaloneWindows64 测试平台验证独立播放器。


