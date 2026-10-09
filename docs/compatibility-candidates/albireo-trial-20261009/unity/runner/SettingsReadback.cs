using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Read only a byte-for-byte copy of Unity-authored settings and LastBuild report
// in a separate inspection project with no candidate assembly or player code.
public static class SettingsReadback
{
    [Serializable] sealed class Settings
    {
        public string unity, backend, codeGeneration, compilerConfiguration, stripping;
        public int architecture;
        public bool incrementalGC, connectProfiler;
        public bool buildReportAvailable;
        public string reportResult, reportOptions, reportPlatform, reportOutputPath;
        public int reportErrors, reportWarnings;
        public ulong reportBytes;
        public string qualification = "API readbacks from copied Unity-authored saved settings and copied LastBuild.buildreport; source hashes retained by outer runner. Candidate assembly absent. No player rebuild.";
    }
    public static void Record()
    {
        var args = Environment.GetCommandLineArgs(); string output = null;
        for (int i = 0; i + 1 < args.Length; ++i) if (args[i] == "--cilica-settings") output = args[i + 1];
        if (output == null || !Path.IsPathRooted(output) || File.Exists(output)) throw new ArgumentException("Fresh absolute settings output required.");
        var target = NamedBuildTarget.Standalone;
        var s = new Settings { unity = Application.unityVersion,
            backend = PlayerSettings.GetScriptingBackend(target).ToString(),
            codeGeneration = PlayerSettings.GetIl2CppCodeGeneration(target).ToString(),
            compilerConfiguration = PlayerSettings.GetIl2CppCompilerConfiguration(target).ToString(),
            stripping = PlayerSettings.GetManagedStrippingLevel(target).ToString(),
            architecture = PlayerSettings.GetArchitecture(target), incrementalGC = PlayerSettings.gcIncremental,
            connectProfiler = EditorUserBuildSettings.connectProfiler };
        var report = BuildReport.GetLatestReport(); s.buildReportAvailable = report != null;
        if (report != null)
        {
            var summary = report.summary;
            s.reportResult = summary.result.ToString(); s.reportOptions = summary.options.ToString();
            s.reportPlatform = summary.platform.ToString(); s.reportOutputPath = summary.outputPath;
            s.reportErrors = summary.totalErrors; s.reportWarnings = summary.totalWarnings; s.reportBytes = summary.totalSize;
        }
        File.WriteAllText(output, JsonUtility.ToJson(s, true)+"\n");
    }
    public static void InspectAll()
    {
        Record();
        ProfileInspection.Inspect();
    }
}
