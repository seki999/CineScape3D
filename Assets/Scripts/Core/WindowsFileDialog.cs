using System;
using System.Runtime.InteropServices;

namespace CineScape
{
    public static class WindowsFileDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class OpenFileName
        {
            public int size = Marshal.SizeOf(typeof(OpenFileName));
            public IntPtr owner, instance;
            public string filter = "电影文件（MP4 / MKV）\0*.mp4;*.mkv\0MP4 视频\0*.mp4\0MKV 视频\0*.mkv\0所有文件\0*.*\0\0";
            public IntPtr customFilter;
            public int maxCustomFilter, filterIndex = 1;
            public IntPtr file;
            public int maxFile = 32768;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public string initialDir, title = "打开本地电影";
            public int flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;
            public short fileOffset, extensionOffset;
            public string defaultExtension = "mp4";
            public IntPtr customData, hook, templateName, reserved;
            public int reserved2, flagsEx;
        }
        [DllImport("comdlg32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        static extern bool GetOpenFileNameW([In, Out] OpenFileName dialog);
        [DllImport("comdlg32.dll")] static extern int CommDlgExtendedError();
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        public static string Open()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            // OPENFILENAME 使用可写 wchar_t 缓冲区，避免把 StringBuilder 当作结构字段封送。
            var buffer = Marshal.AllocCoTaskMem(32768 * sizeof(char));
            try
            {
                Marshal.WriteInt16(buffer,0);
                var dialog = new OpenFileName { owner = GetActiveWindow(), file = buffer };
                if(GetOpenFileNameW(dialog)) return Marshal.PtrToStringUni(buffer);
                var error = CommDlgExtendedError();
                if(error != 0) throw new InvalidOperationException("文件选择器错误：" + error);
                return null;
            }
            finally { Marshal.FreeCoTaskMem(buffer); }
#else
            throw new PlatformNotSupportedException("本地文件选择器仅支持 Windows。");
#endif
        }
    }
}
