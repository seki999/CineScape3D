using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CineScape.Tests
{
    public class PlaybackTests
    {
        [UnityTest]
        public IEnumerator CinemaCentralSeatingAndRectangularProjection()
        {
            yield return null;
            var cinema=UnityEngine.Object.FindFirstObjectByType<CinemaSceneController>();
            int seats=0,central=0;
            foreach(Transform child in cinema.transform)
                if(child.name=="坐垫"){seats++;if(Mathf.Abs(child.position.x)<.01f)central++;}
            Assert.That(seats,Is.EqualTo(117));Assert.That(central,Is.EqualTo(9));
            var distance=UnityEngine.Object.FindFirstObjectByType<CameraDistanceController>();
            float originalDistance=distance.Value,originalCorrection=distance.Keystone;
            var camera=distance.GetComponent<UnityEngine.Camera>();
            try
            {
                foreach(float value in new[]{.2f,.65f,.9f})
                {
                    distance.Set(value);distance.SetKeystone(1);yield return new WaitForSecondsRealtime(.8f);
                    var top=camera.WorldToViewportPoint(cinema.ScreenCenter+new Vector3(-8,4.5f,0));
                    var bottom=camera.WorldToViewportPoint(cinema.ScreenCenter+new Vector3(-8,-4.5f,0));
                    Assert.That(top.x,Is.EqualTo(bottom.x).Within(.001));
                    Assert.That(camera.transform.position.x,Is.EqualTo(0));
                }
                string folder=Environment.GetEnvironmentVariable("CINESCAPE_SCREENSHOT_DIR");
                if(!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                    var movie=UnityEngine.Object.FindFirstObjectByType<VideoPlaybackController>();
                    var moviePath=Environment.GetEnvironmentVariable("CINESCAPE_TEST_VIDEO_PATH");
                    if(!string.IsNullOrEmpty(moviePath)&&File.Exists(moviePath))
                    {
                        movie.Open(moviePath);float readyDeadline=Time.realtimeSinceStartup+10;
                        while(!movie.Ready&&Time.realtimeSinceStartup<readyDeadline)yield return null;
                        Assert.That(movie.Ready,Is.True,movie.Status);
                        yield return new WaitForSecondsRealtime(.2f);movie.Toggle();
                    }
                    foreach(float value in new[]{.25f,.65f})
                    {
                        distance.Set(value);yield return new WaitForSecondsRealtime(.8f);
                        var target=new RenderTexture(1600,900,24);target.Create();
                        var previous=RenderTexture.active;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
                        try
                        {
                            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                            RenderPipeline.SubmitRenderRequest(camera,request);
                            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
                            File.WriteAllBytes(Path.Combine(folder,"cinema-"+Mathf.RoundToInt(value*100)+".png"),image.EncodeToPNG());
                        }
                        finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(image);target.Release();UnityEngine.Object.Destroy(target);}
                    }
                    movie.Stop();
                }
            }
            finally{distance.Set(originalDistance);distance.SetKeystone(originalCorrection);}
        }
        [UnityTest]
        public IEnumerator LongMkvStartsAndSeeksWithoutConversion()
        {
            var path=Environment.GetEnvironmentVariable("CINESCAPE_LONG_MKV_PATH");
            if(string.IsNullOrEmpty(path)||!File.Exists(path)) Assert.Ignore("需要长 MKV 样片。");
            yield return null;
            var video=UnityEngine.Object.FindFirstObjectByType<VideoPlaybackController>();
            float start=Time.realtimeSinceStartup;
            video.Open(path);
            while(!video.Ready && video.Loading && Time.realtimeSinceStartup-start<10) yield return null;
            float elapsed=Time.realtimeSinceStartup-start;
            Assert.That(video.Ready,Is.True,video.Status);Assert.That(video.UsesDirectDecoder,Is.True);
            Assert.That(elapsed,Is.LessThan(10));Assert.That(video.Duration,Is.GreaterThan(590));
            Debug.Log("MKV_FIRST_FRAME_SECONDS="+elapsed.ToString("F3")+"; DURATION="+video.Duration.ToString("F1"));
            double target=video.Duration-3;
            video.Seek(target);
            float deadline=Time.realtimeSinceStartup+10;
            while(video.Time<target-1 && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(video.Time,Is.GreaterThan(target-1));
            video.Stop();Assert.That(video.Ready,Is.False);
        }
        [UnityTest]
        public IEnumerator DirectMkvFrameOrientationAndLetterbox()
        {
            var path=Environment.GetEnvironmentVariable("CINESCAPE_FRAME_MKV_PATH");
            if(string.IsNullOrEmpty(path)||!File.Exists(path)) Assert.Ignore("需要色块 MKV 样片。");
            yield return null;
            var video=UnityEngine.Object.FindFirstObjectByType<VideoPlaybackController>();
            video.Open(path);float deadline=Time.realtimeSinceStartup+10;
            while(!video.Ready && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(video.Ready,Is.True,video.Status);
            yield return new WaitForSecondsRealtime(.3f);
            var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                RenderTexture.active=video.GetComponent<VideoScreenRenderer>().Texture;
                texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();
                var top=texture.GetPixel(480,850);var bottom=texture.GetPixel(480,230);
                Assert.That(top.r,Is.GreaterThan(.7f),"上方应为红色，检查 RGB 通道与垂直方向。");
                Assert.That(top.b,Is.LessThan(.2f));Assert.That(bottom.b,Is.GreaterThan(.7f));
                Assert.That(texture.GetPixel(480,50).maxColorComponent,Is.LessThan(.1f),"宽银幕下方应留黑。");
                Assert.That(texture.GetPixel(480,1030).maxColorComponent,Is.LessThan(.1f),"宽银幕上方应留黑。");
            }
            finally {RenderTexture.active=previous;UnityEngine.Object.Destroy(texture);video.Stop();}
        }
        [UnityTest]
        public IEnumerator MkvCancellationAndCleanup()
        {
            var path=Environment.GetEnvironmentVariable("CINESCAPE_TEST_VIDEO_PATH");
            if(string.IsNullOrEmpty(path)||!File.Exists(path)||!string.Equals(Path.GetExtension(path),".mkv",StringComparison.OrdinalIgnoreCase))
                Assert.Ignore("此检查需要 MKV 测试片。");
            var sourceLength=new FileInfo(path).Length;
            var sourceWrite=File.GetLastWriteTimeUtc(path);
            yield return null;
            var video=UnityEngine.Object.FindFirstObjectByType<VideoPlaybackController>();
            video.Open(path);Assert.That(video.Loading,Is.True);
            video.Stop();Assert.That(video.Loading,Is.False);Assert.That(video.Ready,Is.False);
            Assert.That(new FileInfo(path).Length,Is.EqualTo(sourceLength));
            Assert.That(File.GetLastWriteTimeUtc(path),Is.EqualTo(sourceWrite));
            string damaged=Path.Combine(Application.temporaryCachePath,Guid.NewGuid()+".mkv");
            File.WriteAllText(damaged,"not a video");
            try
            {
                video.Open(damaged);
                float deadline=Time.realtimeSinceStartup+10;
                while(video.Loading&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(video.Loading,Is.False);Assert.That(video.Status,Does.Contain("失败"));
            }
            finally {video.Stop();File.Delete(damaged);}
        }
        [UnityTest]
        public IEnumerator LocalMoviePlaybackAndDistance()
        {
            var path=Environment.GetEnvironmentVariable("CINESCAPE_TEST_VIDEO_PATH");
            if(string.IsNullOrEmpty(path)||!File.Exists(path)) Assert.Ignore("设置 CINESCAPE_TEST_VIDEO_PATH 指向 H.264/AAC 测试片。");
            yield return null;
            var video=UnityEngine.Object.FindFirstObjectByType<VideoPlaybackController>();
            var distance=UnityEngine.Object.FindFirstObjectByType<CameraDistanceController>();
            Assert.That(video,Is.Not.Null);Assert.That(distance,Is.Not.Null);
            float originalDistance=distance.Value;
            video.Open(path);
            float deadline=Time.realtimeSinceStartup+30;
            while(!video.Ready && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(video.Ready,Is.True,video.Status);
            yield return new WaitForSecondsRealtime(1);
            Assert.That(video.Playing,Is.True,video.Status);Assert.That(video.Time,Is.GreaterThan(.1));
            Assert.That(video.AudioTracks,Is.GreaterThan(0));
            var screen=video.GetComponent<VideoScreenRenderer>();
            var snapshot=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                RenderTexture.active=screen.Texture;
                snapshot.ReadPixels(new Rect(0,0,1920,1080),0,0);snapshot.Apply();
                float maximum=0;
                for(int x=300;x<1700;x+=200)for(int y=200;y<1000;y+=200)
                {var c=snapshot.GetPixel(x,y);maximum=Mathf.Max(maximum,c.r,c.g,c.b);}
                Assert.That(maximum,Is.GreaterThan(.1f),"视频纹理仍是全黑。");
            }
            finally {RenderTexture.active=previous;UnityEngine.Object.Destroy(snapshot);}
            double before=video.Time;
            distance.Set(1);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(video.Playing,Is.True);Assert.That(video.Time,Is.GreaterThan(before+.1));
            Assert.That(distance.transform.position.z,Is.InRange(5.53f-.0001f,29.05f+.0001f));
            var center=distance.GetComponent<UnityEngine.Camera>().WorldToViewportPoint(new Vector3(0,5.5f,1.05f));
            Assert.That(center.y,Is.EqualTo(.5f).Within(.001f));
            float originalKeystone=distance.Keystone;
            distance.SetKeystone(0);yield return new WaitForSecondsRealtime(.15f);
            distance.SetKeystone(1);yield return new WaitForSecondsRealtime(.15f);
            Assert.That(video.Playing,Is.True);Assert.That(video.Time,Is.GreaterThan(before+.2));
            distance.SetKeystone(originalKeystone);
            video.Toggle();double paused=video.Time;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(video.Playing,Is.False);Assert.That(video.Time,Is.EqualTo(paused).Within(.1));
            video.Seek(3);
            deadline=Time.realtimeSinceStartup+10;
            while(Math.Abs(video.Time-3)>.2&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(video.Time,Is.EqualTo(3).Within(.2));
            video.Stop();Assert.That(video.Ready,Is.False);
            video.Toggle();deadline=Time.realtimeSinceStartup+30;
            while(!video.Playing&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(video.Playing,Is.True,video.Status);
            deadline=Time.realtimeSinceStartup+15;
            while(video.Playing&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(video.Status,Does.Contain("播放结束"));Assert.That(video.Ready,Is.True);
            video.Open(Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".mp4"));
            Assert.That(video.Status,Does.Contain("不存在"));Assert.That(video.Loading,Is.False);
            distance.Set(originalDistance);
        }
    }
}
