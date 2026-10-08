using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BenchmarkBuild
{
    static string Option(string name,string fallback)
    {
        var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]==name)return args[i+1];return fallback;
    }
    [Serializable] class Settings
    {
        public string unity, target, backend, optimization, stripping;
        public int architecture;
        public bool incrementalGC, development;
        public string result;
    }
    public static void Build()
    {
        string target=Option("--cilica-target","mac");
        if(target!="mac" && target!="web")throw new Exception("Unknown target");
        string output=Path.GetFullPath(Option("--cilica-output","Build/DiffPlex.app"));
        var named=target=="mac"?NamedBuildTarget.Standalone:NamedBuildTarget.WebGL;
        var buildTarget=target=="mac"?BuildTarget.StandaloneOSX:BuildTarget.WebGL;
        PlayerSettings.productName="Cilica DiffPlex comparison";
        PlayerSettings.companyName="Cilica";
        PlayerSettings.SetScriptingBackend(named,ScriptingImplementation.IL2CPP);
        PlayerSettings.SetIl2CppCodeGeneration(named,Il2CppCodeGeneration.OptimizeSpeed);
        PlayerSettings.SetIl2CppCompilerConfiguration(named,Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetManagedStrippingLevel(named,ManagedStrippingLevel.Minimal);
        PlayerSettings.gcIncremental=true;
        if(target=="mac")PlayerSettings.SetArchitecture(named,1);
        // Scene creation uses Unity's API; no hand-authored scene YAML.
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene,"Assets/Benchmark.unity");
        AssetDatabase.SaveAssets();
        var settings=new Settings{unity=Application.unityVersion,target=target,backend="IL2CPP",optimization="Release/OptimizeSpeed",
            stripping="Minimal",architecture=target=="mac"?PlayerSettings.GetArchitecture(named):0,incrementalGC=PlayerSettings.gcIncremental,development=false};
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output+".settings.json",JsonUtility.ToJson(settings,true));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Benchmark.unity"},locationPathName=output,
            target=buildTarget,options=BuildOptions.None});
        settings.result=report.summary.result.ToString();File.WriteAllText(output+".settings.json",JsonUtility.ToJson(settings,true));
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
    }
}
