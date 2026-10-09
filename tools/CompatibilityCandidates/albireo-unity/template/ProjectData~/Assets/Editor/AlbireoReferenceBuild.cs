using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Explicit -executeMethod only. No InitializeOnLoad hooks and no candidate calls.
public static class AlbireoReferenceBuild
{
    [Serializable] sealed class Settings
    {
        public string unity, target = "StandaloneOSX", backend = "IL2CPP", optimization = "Release/OptimizeSpeed";
        public string stripping = "Minimal", result;
        public string valuesKind = "Requested configuration; capture emitted flags/options and API readbacks separately.";
        public int architecture;
        public bool development = true, incrementalGC = true, automaticProfilerConnection = false;
    }
    public static void Build()
    {
        string output = "";
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; ++i) if (args[i] == "--cilica-output") output = args[i + 1];
        if (!Path.IsPathRooted(output) || !output.EndsWith(".app", StringComparison.Ordinal) || Directory.Exists(output))
            throw new ArgumentException("Use a new absolute .app output path.");
        var target = NamedBuildTarget.Standalone;
        PlayerSettings.productName = "Cilica Albireo Decode reference"; PlayerSettings.companyName = "Cilica";
        PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSpeed);
        PlayerSettings.SetIl2CppCompilerConfiguration(target, Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Minimal);
        PlayerSettings.SetArchitecture(target, 1); PlayerSettings.gcIncremental = true;
        EditorUserBuildSettings.connectProfiler = false;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/AlbireoReference.unity"); AssetDatabase.SaveAssets();
        var settings = new Settings { unity = Application.unityVersion, architecture = PlayerSettings.GetArchitecture(target) };
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output + ".settings.json", JsonUtility.ToJson(settings, true));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/AlbireoReference.unity" },
            locationPathName = output, target = BuildTarget.StandaloneOSX, options = BuildOptions.Development });
        settings.result = report.summary.result.ToString(); File.WriteAllText(output + ".settings.json", JsonUtility.ToJson(settings, true));
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Unity build failed: " + settings.result);
    }
}
