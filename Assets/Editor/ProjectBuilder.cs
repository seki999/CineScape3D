using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CineScape.Editor
{
    [InitializeOnLoad]
    public static class ProjectBuilder
    {
        const string ScenePath="Assets/Scenes/Cinema.unity";
        static ProjectBuilder() { EditorApplication.delayCall+=FirstOpen; }
        static void FirstOpen()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if(!File.Exists(ScenePath)) Initialize();
        }
        [MenuItem("CineScape/初始化项目")]
        public static void Initialize()
        {
            Directory.CreateDirectory("Assets/Scenes");Directory.CreateDirectory("Assets/Settings");
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/CinemaRenderer.asset");
            if(renderer==null){renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,"Assets/Settings/CinemaRenderer.asset");}
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/CinemaURP.asset");
            if(pipeline==null){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,"Assets/Settings/CinemaURP.asset");}
            pipeline.supportsHDR=false;pipeline.shadowDistance=0;pipeline.msaaSampleCount=2;
            GraphicsSettings.defaultRenderPipeline=pipeline;
            for(int i=0;i<QualitySettings.names.Length;i++) { QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=pipeline; }
            PlayerSettings.companyName="CineScape";PlayerSettings.productName="CineScape3D";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
            PlayerSettings.runInBackground=true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            // 保留旧 Input API 与 StandaloneInputModule。
            var playerSettings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=playerSettings.FindProperty("activeInputHandler");if(input!=null){input.intValue=0;playerSettings.ApplyModifiedProperties();}
            // Resources 材质保留所需 Shader，避免 Always Included 打包全部变体。
            Directory.CreateDirectory("Assets/Resources");
            foreach(var kind in new[]{"Lit","Unlit"})
            {
                var name="Universal Render Pipeline/"+kind;
                var shader=Shader.Find(name);if(shader==null)throw new InvalidOperationException("找不到 URP Shader："+name);
                var materialPath="Assets/Resources/Cinema"+kind+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
                material.enableInstancing=true;EditorUtility.SetDirty(material);
            }
            if(!File.Exists(ScenePath))
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                new GameObject("CineScape 3D").AddComponent<AppManager>();EditorSceneManager.SaveScene(scene,ScenePath);
            }
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            EditorUtility.SetDirty(pipeline);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        [MenuItem("CineScape/构建 Windows x64")]
        public static void BuildWindows()
        {
            Initialize();
            string output="Builds/Windows/CineScape3D.exe";
            var args=Environment.GetCommandLineArgs();for(int i=0;i<args.Length-1;i++)if(args[i]=="-outputPath")output=args[i+1];
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{ScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Windows 构建失败："+report.summary.result);
            Debug.Log("构建成功："+output);
        }
    }
}
