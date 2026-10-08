using UnityEngine;

namespace CineScape
{
    public class AppManager : MonoBehaviour
    {
        SettingsManager settings;
        CameraDistanceController distance;
        VideoPlaybackController video;
        float lastSave;
        float savedDistance, savedVolume;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartApplication()
        {
            if(FindFirstObjectByType<AppManager>() == null) new GameObject("CineScape 3D").AddComponent<AppManager>();
        }
        void Awake()
        {
            settings = new SettingsManager();
            var cinema = new GameObject("程序化真实影院").AddComponent<CinemaSceneController>(); cinema.Generate();
            var cameraObject = new GameObject("观影摄像机");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.fieldOfView = 58; camera.nearClipPlane = .1f; camera.farClipPlane = 70;
            cameraObject.AddComponent<AudioListener>();
            distance = cameraObject.AddComponent<CameraDistanceController>();
            distance.Initialize(cinema.ScreenCenter,CinemaSceneController.Depth,CinemaSceneController.ScreenWidth,
                settings.Data.rememberDistance ? settings.Data.lastDistance : settings.Data.defaultDistance);
            distance.SetKeystone(settings.Data.keystone);
            var renderer = gameObject.AddComponent<VideoScreenRenderer>(); renderer.Initialize(cinema.VideoSurface);
            video = gameObject.AddComponent<VideoPlaybackController>(); video.Initialize(renderer); video.Volume = settings.Data.volume;
            QualitySettingsController.Apply(settings.Data.quality);
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow; Screen.fullScreen = settings.Data.fullscreen;
            gameObject.AddComponent<PlayerUIController>().Initialize(video,distance,settings);
            savedDistance = distance.Value; savedVolume = video.Volume;
        }
        void Update()
        {
            if(UnityEngine.Time.unscaledTime-lastSave < 1) return;
            lastSave = UnityEngine.Time.unscaledTime;
            if(!Mathf.Approximately(savedDistance,distance.Value) || !Mathf.Approximately(savedVolume,video.Volume)) Save();
        }
        void Save()
        {
            settings.Data.lastDistance = savedDistance = distance.Value;
            settings.Data.volume = savedVolume = video.Volume; settings.Save();
        }
        void OnApplicationQuit() { Save(); }
        void OnApplicationPause(bool paused) { if(paused && settings != null) Save(); }
    }
}
