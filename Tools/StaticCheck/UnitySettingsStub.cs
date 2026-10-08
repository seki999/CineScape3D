// 仅用于独立配置文件 I/O 检查，不模拟 Unity 渲染或编译环境。
using System;
using System.Text.Json;
namespace UnityEngine
{
    public static class Application { public static string persistentDataPath => System.IO.Path.GetTempPath(); }
    public static class Mathf { public static int Clamp(int v,int a,int b)=>Math.Clamp(v,a,b);public static float Clamp01(float v)=>Math.Clamp(v,0,1); }
    public static class Debug { public static void LogWarning(object message)=>Console.WriteLine(message); }
    public static class JsonUtility
    {
        static JsonSerializerOptions Options=new JsonSerializerOptions{IncludeFields=true,WriteIndented=true};
        public static T FromJson<T>(string text)=>JsonSerializer.Deserialize<T>(text,Options);
        public static void FromJsonOverwrite(string text,object target)
        {
            var loaded=JsonSerializer.Deserialize(text,target.GetType(),Options);
            if(loaded==null)throw new ArgumentException("JSON object required");
            foreach(var field in target.GetType().GetFields())field.SetValue(target,field.GetValue(loaded));
        }
        public static string ToJson(object data,bool pretty)=>JsonSerializer.Serialize(data,Options);
    }
}
