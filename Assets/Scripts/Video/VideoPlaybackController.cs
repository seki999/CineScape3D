using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace CineScape
{
    // 后续其他解码器可以实现此接口，场景与摄像机无需改写。
    public interface IVideoPlayback
    {
        void Open(string path);
        void Toggle();
        void Stop();
        void Seek(double seconds);
        double Time { get; }
        double Duration { get; }
    }
    public class VideoPlaybackController : MonoBehaviour, IVideoPlayback
    {
        VideoPlayer player;
        AudioSource audioSource;
        VideoScreenRenderer screen;
        bool seeking;
        double queuedSeek = -1;
        float loadingSince;
        string currentPath;
        bool ended;
        DirectMkvPlayback direct;
        public string Status { get; private set; } = "打开视频开始观看";
        public bool Loading { get; private set; }
        public bool Ready => direct != null ? direct.HasFrame : player != null && player.isPrepared;
        public bool Playing => Ready && (direct != null ? direct.Playing : player.isPlaying);
        public bool HasFile { get; private set; }
        public double Time => Ready ? (direct != null ? direct.Time : player.time) : 0;
        public double Duration => direct != null ? direct.Duration : Ready && !double.IsNaN(player.length) && !double.IsInfinity(player.length) ? player.length : 0;
        public int AudioTracks => direct != null ? direct.AudioTracks : Ready ? player.audioTrackCount : 0;
        public bool UsesDirectDecoder => direct != null;
        public float Volume { get => audioSource.volume; set { audioSource.volume = Mathf.Clamp01(value);direct?.SetVolume(audioSource.volume,audioSource.mute); } }
        public bool Muted { get => audioSource.mute; set { audioSource.mute = value;direct?.SetVolume(audioSource.volume,value); } }
        public void Initialize(VideoScreenRenderer renderer)
        {
            screen = renderer;
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.spatialBlend = 0;
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false; player.isLooping = false; player.waitForFirstFrame = true;
            player.skipOnDrop = true; player.source = VideoSource.Url;
            player.renderMode = VideoRenderMode.RenderTexture; player.targetTexture = screen.Texture;
            player.aspectRatio = VideoAspectRatio.FitInside;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount = 1; player.EnableAudioTrack(0,true); player.SetTargetAudioSource(0,audioSource);
            player.timeUpdateMode = VideoTimeUpdateMode.DSPTime;
            player.prepareCompleted += Prepared; player.errorReceived += Failed;
            player.seekCompleted += SeekDone; player.loopPointReached += Ended;
        }
        public void Open(string path)
        {
            if(string.IsNullOrWhiteSpace(path)) return;
            Stop();
            try
            {
                if(!File.Exists(path)) { currentPath=null;HasFile=false;Status = "视频文件不存在。"; return; }
                currentPath = path;
                HasFile = true; Loading = true; loadingSince = UnityEngine.Time.realtimeSinceStartup;
                var extension=Path.GetExtension(path);
                if(string.Equals(extension,".mkv",StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension,".mp4",StringComparison.OrdinalIgnoreCase))
                {
                    direct = new DirectMkvPlayback();
                    Status = "正在打开视频…";
                    direct.Open(path,Volume,Muted);
                }
                else Prepare(path);
            }
            catch(Exception e) { Failed(player,e.Message); }
        }
        void Prepare(string path)
        {
            loadingSince=UnityEngine.Time.realtimeSinceStartup;
            player.url = new Uri(Path.GetFullPath(path)).AbsoluteUri;
            Status="正在加载视频…";player.Prepare();
        }
        void Prepared(VideoPlayer p) { if(!Loading)return;Loading = false; Status = ""; p.Play(); }
        void Failed(VideoPlayer p,string error) { currentPath=null;Stop(); Status = "视频加载失败，请检查文件是否完整或尝试其他影片。\n" + error; }
        void Ended(VideoPlayer p) { ended=true;p.Pause(); Status = "播放结束"; }
        public void Toggle()
        {
            if(Loading) return;
            if(!Ready) { if(!string.IsNullOrEmpty(currentPath)) Open(currentPath);return; }
            if(direct != null)
            {
                if(ended) Open(currentPath);
                else direct.Pause(direct.Playing);
                Status="";return;
            }
            if(player.isPlaying) player.Pause();
            else { if(ended) Seek(0); player.Play(); }
            Status = "";
        }
        public void Stop()
        {
            if(player != null) player.Stop();
            if(direct != null) { direct.Dispose();direct=null; }
            seeking = false; ended=false;queuedSeek = -1; HasFile = !string.IsNullOrEmpty(currentPath); Loading = false;
            if(screen != null) screen.Clear(); Status = HasFile ? "已停止，点击播放重新开始" : "打开视频开始观看";
        }
        public void Seek(double seconds)
        {
            if(!Ready || (direct == null && !player.canSetTime) || Duration <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            ended=false;
            seconds = Math.Max(0,Math.Min(Duration - .05,seconds));
            if(direct != null) { direct.Seek(seconds);Status="";return; }
            if(seeking) { queuedSeek = seconds; return; }
            seeking = true; player.time = seconds;
        }
        void SeekDone(VideoPlayer p) { seeking = false; if(queuedSeek >= 0) { var t=queuedSeek; queuedSeek=-1; Seek(t); } }
        void Update()
        {
            if(direct != null)
            {
                try
                {
                    direct.Upload(screen);
                    if(direct.Failed) { Failed(player,"无法解码这个视频文件。");return; }
                    if(Loading && direct.HasFrame) { Loading=false;Status=""; }
                    if(direct.Ended && !ended)
                    {
                        if(!direct.HasFrame) { Failed(player,"文件没有可播放的视频画面。");return; }
                        ended=true;Loading=false;Status="播放结束";
                    }
                }
                catch(Exception e) { Failed(player,e.Message); }
            }
            if(Loading && UnityEngine.Time.realtimeSinceStartup-loadingSince > 30) Failed(player,"准备超过 30 秒，请尝试其他兼容文件。");
        }
        void OnDestroy()
        {
            if(player == null) return;
            player.prepareCompleted -= Prepared; player.errorReceived -= Failed;
            player.seekCompleted -= SeekDone; player.loopPointReached -= Ended;
            player.Stop();if(direct != null) { direct.Dispose();direct=null; }
        }
    }
}
