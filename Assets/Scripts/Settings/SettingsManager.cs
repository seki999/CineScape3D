using System;
using System.IO;
using UnityEngine;

namespace CineScape
{
    [Serializable]
    public class AppSettings
    {
        public string language = "zh-CN";
        public int quality = 1;
        public bool fullscreen;
        public float defaultVolume = .8f;
        public float volume = .8f;
        public float defaultDistance = .5f;
        public float lastDistance = .5f;
        public bool rememberDistance = true;
        public bool autoHide = true;
        public float keystone = 1;
        public void Normalize()
        {
            language = "zh-CN";
            quality = Mathf.Clamp(quality, 0, 2);
            defaultVolume = Safe(defaultVolume, .8f);
            volume = Safe(volume, .8f);
            defaultDistance = Safe(defaultDistance, .5f);
            lastDistance = Safe(lastDistance, .5f);
            keystone = Safe(keystone, 1);
        }
        static float Safe(float n, float fallback) => float.IsNaN(n) || float.IsInfinity(n) ? fallback : Mathf.Clamp01(n);
    }

    public class SettingsManager
    {
        public AppSettings Data { get; private set; }
        public string LastError { get; private set; }
        public string Path { get; }
        public SettingsManager(string path = null)
        {
            Path = path ?? System.IO.Path.Combine(Application.persistentDataPath, "settings.json");
            try
            {
                Data = new AppSettings();
                if (File.Exists(Path)) JsonUtility.FromJsonOverwrite(File.ReadAllText(Path), Data);
                Data.Normalize();
            }
            catch (Exception e) { Data = new AppSettings(); LastError = "配置读取失败，已恢复默认值：" + e.Message; }
        }
        public void Save()
        {
            try
            {
                Data.Normalize();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                var temp = Path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(Data, true));
                if (File.Exists(Path)) File.Replace(temp, Path, null); else File.Move(temp, Path);
                LastError = null;
            }
            catch (Exception e) { LastError = "无法保存配置：" + e.Message; Debug.LogWarning(LastError); }
        }
    }
}
