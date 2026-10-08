# 素材与依赖许可记录

本项目影院全部由本仓库 C# 代码生成 Unity 基础几何，没有下载第三方模型、照片、纹理、音乐或示例电影。用户自行选择拥有使用权的本地影片。

| 项目 | 来源 | 许可说明 |
|---|---|---|
| Unity Editor / Player / VideoPlayer | https://unity.com/legal | Unity 软件许可；发布前依适用 Unity 条款确认授权 |
| Universal Render Pipeline 17.0.3 | https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0 | Unity 包许可证；以安装包 LICENSE.md / Third Party Notices 为准 |
| Unity UI 2.0.0 | https://docs.unity3d.com/Packages/com.unity.ugui@2.0 | 以包 LICENSE.md 为准 |
| Unity Test Framework 1.4.5 | https://docs.unity3d.com/Packages/com.unity.test-framework@1.4 | 仅编辑器测试；以包许可证及 NUnit 第三方声明为准 |
| 微软雅黑等字体 | Windows 系统安装字体 | 仅动态调用用户系统字体，不复制或再分发字体文件 |
| Roslyn / .NET SDK | https://github.com/dotnet/roslyn | Roslyn MIT；仅开发静态检查工具，不打包至 Player |

Unity 包已在指定编辑器解析，分发目录的 ThirdPartyNotices 保存 URP、Core、UI、Burst 的包许可证和已有第三方声明。Player 使用官方 VideoLAN LibVLC 3.0.21 直接播放 MKV。libvlc / libvlccore 为 LGPL 2.1 或后续版本，插件可能有其他许可证；StreamingAssets/VLC 内含 COPYING、COPYING.LIB、NOTICE 和官方 VLC 源码归档（含 contrib 构建配方与依赖来源）。组件可替换。程序不包含 FFmpeg 转码 EXE、libmpv、编辑器、测试影片或开发解包工具。开发用 FFmpeg 仅用于生成测试样片。微软 VC++ 2010 DLL 仅用于本机编辑器加载，其来源是 Unity 官方安装包附带的微软签名运行库。

`Tools/Extract-UnityEditor.py` 自行实现 NSISBI 文件记录与 LZMA 提取，格式参考 https://gist.github.com/Slaynash/57b3a911e4da092e2b2664046700fe7d ，不复制其 C# 解码器，不执行安装包命令。开发使用的 7-Zip 来自 https://www.7-zip.org/ ，olefile 来自 Python 包仓库；均不随 Player 分发。

项目自身源代码尚未指定对外开源许可证。


