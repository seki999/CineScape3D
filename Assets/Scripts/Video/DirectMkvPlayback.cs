using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace CineScape
{
    // LibVLC 解码时钟驱动画面/音频，无整片转码或临时影片。
    public sealed class DirectMkvPlayback : IDisposable
    {
        const string Library="libvlc.dll";
        static IntPtr library;
        IntPtr instance, player, allocation, pixels;
        readonly object frameGate=new object();
        LockCallback lockCallback;
        UnlockCallback unlockCallback;
        DisplayCallback displayCallback;
        FormatCallback formatCallback;
        CleanupCallback cleanupCallback;
        Texture2D frame;
        int width,height,version,uploaded;
        volatile bool hasFrame;
        bool paused;
        double pendingSeek=-1;
        float aspect;
        public bool HasFrame => hasFrame;
        public bool Playing => !paused && State==3;
        public bool Ended => State==6;
        public bool Failed => State==7;
        int State => player==IntPtr.Zero ? 0 : libvlc_media_player_get_state(player);
        public double Time => player==IntPtr.Zero ? 0 : Math.Max(0,libvlc_media_player_get_time(player)/1000.0);
        public double Duration => player==IntPtr.Zero ? 0 : Math.Max(0,libvlc_media_player_get_length(player)/1000.0);
        public int AudioTracks => player==IntPtr.Zero ? 0 : Math.Max(0,libvlc_audio_get_track_count(player)-1);
        public void Open(string path,float volume,bool muted)
        {
            var startup=System.Diagnostics.Stopwatch.StartNew();
            string root=Path.Combine(Application.streamingAssetsPath,"VLC");
            if(library==IntPtr.Zero)
            {
                if(!File.Exists(Path.Combine(root,Library))) throw new FileNotFoundException("直接播放组件缺失，请解压完整程序包。");
                library=LoadLibraryExW(Path.Combine(root,Library),IntPtr.Zero,8);
                if(library==IntPtr.Zero) throw new InvalidOperationException("无法加载视频组件，Windows 错误 "+Marshal.GetLastWin32Error());
            }
            // 不读取用户 VLC 配置，也不加载播放器界面、字幕或外部脚本。
            Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH",Path.Combine(root,"plugins"));
            double dllSeconds=startup.Elapsed.TotalSeconds;
            var options=new[]{"--ignore-config","--no-video-title-show","--no-osd","--no-spu","--no-sub-autodetect-file","--quiet","--file-caching=300","--avcodec-hw=none"};
            var pointers=new IntPtr[options.Length];
            var argv=Marshal.AllocHGlobal(IntPtr.Size*pointers.Length);
            try
            {
                for(int i=0;i<options.Length;i++){pointers[i]=Utf8(options[i]);Marshal.WriteIntPtr(argv,i*IntPtr.Size,pointers[i]);}
                instance=libvlc_new(options.Length,argv);
            }
            finally {foreach(var ptr in pointers) if(ptr!=IntPtr.Zero) Marshal.FreeHGlobal(ptr);Marshal.FreeHGlobal(argv);}
            if(instance==IntPtr.Zero) throw new InvalidOperationException("视频解码器初始化失败。");
            double instanceSeconds=startup.Elapsed.TotalSeconds;
            player=libvlc_media_player_new(instance);
            if(player==IntPtr.Zero) throw new InvalidOperationException("视频播放组件初始化失败。");
            lockCallback=Lock;unlockCallback=Unlock;displayCallback=Display;formatCallback=Format;cleanupCallback=Cleanup;
            libvlc_video_set_callbacks(player,lockCallback,unlockCallback,displayCallback,IntPtr.Zero);
            libvlc_video_set_format_callbacks(player,formatCallback,cleanupCallback);
            var name=Utf8(Path.GetFullPath(path));IntPtr media;
            try {media=libvlc_media_new_path(instance,name);} finally {Marshal.FreeHGlobal(name);}
            if(media==IntPtr.Zero) throw new InvalidOperationException("无法打开本地影片。");
            try {libvlc_media_player_set_media(player,media);} finally {libvlc_media_release(media);}
            if(libvlc_media_player_play(player)!=0) throw new InvalidOperationException("视频启动失败。");
            SetVolume(volume,muted);
            Debug.Log("MKV_OPEN_INIT_SECONDS="+startup.Elapsed.TotalSeconds.ToString("F3")+"; DLL="+dllSeconds.ToString("F3")+"; INSTANCE="+instanceSeconds.ToString("F3"));
        }
        static IntPtr Utf8(string value)
        {
            var bytes=Encoding.UTF8.GetBytes(value+"\0");var result=Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes,0,result,bytes.Length);return result;
        }
        uint Format(ref IntPtr opaque,IntPtr chroma,ref uint videoWidth,ref uint videoHeight,IntPtr pitches,IntPtr lines)
        {
            // 回调来自原生线程：只操作内存，不调用 Unity。
            try
            {
                lock(frameGate)
                {
                    FreePixels();
                    aspect=videoWidth/(float)Math.Max(1,videoHeight);
                    double scale=Math.Min(1,Math.Min(1920.0/videoWidth,1080.0/videoHeight));
                    width=Math.Max(8,((int)(videoWidth*scale)+7)&~7);height=Math.Max(1,(int)(videoHeight*scale));
                    int paddedHeight=(height+31)&~31;
                    allocation=Marshal.AllocHGlobal(checked(width*paddedHeight*4+31));
                    pixels=new IntPtr((allocation.ToInt64()+31)&~31L);
                    Marshal.Copy(Encoding.ASCII.GetBytes("RV32"),0,chroma,4);
                    videoWidth=(uint)width;videoHeight=(uint)height;
                    Marshal.WriteInt32(pitches,width*4);Marshal.WriteInt32(lines,paddedHeight);
                    return 1;
                }
            }
            catch {return 0;}
        }
        IntPtr Lock(IntPtr opaque,IntPtr planes){Monitor.Enter(frameGate);Marshal.WriteIntPtr(planes,pixels);return IntPtr.Zero;}
        void Unlock(IntPtr opaque,IntPtr picture,IntPtr planes){Monitor.Exit(frameGate);}
        void Display(IntPtr opaque,IntPtr picture){lock(frameGate){version++;hasFrame=true;}}
        void Cleanup(IntPtr opaque){lock(frameGate) FreePixels();}
        void FreePixels(){if(allocation!=IntPtr.Zero) Marshal.FreeHGlobal(allocation);allocation=pixels=IntPtr.Zero;}
        public void Upload(VideoScreenRenderer screen)
        {
            if(pendingSeek>=0 && State==3) {libvlc_media_player_set_time(player,(long)(pendingSeek*1000));pendingSeek=-1;}
            lock(frameGate)
            {
                if(pixels==IntPtr.Zero || version==uploaded) return;
                if(frame==null || frame.width!=width || frame.height!=height)
                {
                    if(frame!=null) UnityEngine.Object.Destroy(frame);
                    frame=new Texture2D(width,height,TextureFormat.BGRA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                }
                frame.LoadRawTextureData(pixels,width*height*4);frame.Apply(false,false);
                screen.Present(frame,aspect);uploaded=version;
            }
        }
        public void Pause(bool value){paused=value;libvlc_media_player_set_pause(player,value?1:0);}
        public void Seek(double seconds)
        {
            if(Ended)
            {
                libvlc_media_player_stop(player);paused=false;pendingSeek=Math.Max(0,seconds);
                libvlc_media_player_play(player);
            }
            else libvlc_media_player_set_time(player,(long)(Math.Max(0,seconds)*1000));
        }
        public void SetVolume(float value,bool mute)
        {
            if(player==IntPtr.Zero)return;
            libvlc_audio_set_volume(player,Mathf.RoundToInt(Mathf.Clamp01(value)*100));libvlc_audio_set_mute(player,mute?1:0);
        }
        public void Dispose()
        {
            // Stop 等待解码线程结束；不能持有 frameGate 等它，避免回调死锁。
            if(player!=IntPtr.Zero){libvlc_media_player_stop(player);libvlc_media_player_release(player);player=IntPtr.Zero;}
            if(instance!=IntPtr.Zero){libvlc_release(instance);instance=IntPtr.Zero;}
            lock(frameGate) FreePixels();
            if(frame!=null){UnityEngine.Object.Destroy(frame);frame=null;}
            GC.KeepAlive(lockCallback);GC.KeepAlive(unlockCallback);GC.KeepAlive(displayCallback);GC.KeepAlive(formatCallback);GC.KeepAlive(cleanupCallback);
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr LockCallback(IntPtr opaque,IntPtr planes);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void UnlockCallback(IntPtr opaque,IntPtr picture,IntPtr planes);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void DisplayCallback(IntPtr opaque,IntPtr picture);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint FormatCallback(ref IntPtr opaque,IntPtr chroma,ref uint width,ref uint height,IntPtr pitches,IntPtr lines);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void CleanupCallback(IntPtr opaque);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryExW(string path,IntPtr file,uint flags);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr libvlc_new(int argc,IntPtr argv);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_release(IntPtr instance);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr libvlc_media_new_path(IntPtr instance,IntPtr path);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_media_release(IntPtr media);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr libvlc_media_player_new(IntPtr instance);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_media_player_release(IntPtr player);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_media_player_set_media(IntPtr player,IntPtr media);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int libvlc_media_player_play(IntPtr player);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_media_player_stop(IntPtr player);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_media_player_set_pause(IntPtr player,int pause);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int libvlc_media_player_get_state(IntPtr player);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern long libvlc_media_player_get_time(IntPtr player);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern long libvlc_media_player_get_length(IntPtr player);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_media_player_set_time(IntPtr player,long time);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int libvlc_audio_get_track_count(IntPtr player);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern int libvlc_audio_set_volume(IntPtr player,int volume);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_audio_set_mute(IntPtr player,int mute);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_video_set_callbacks(IntPtr player,LockCallback @lock,UnlockCallback unlock,DisplayCallback display,IntPtr opaque);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_video_set_format_callbacks(IntPtr player,FormatCallback format,CleanupCallback cleanup);
    }
}
