#if !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Cilica.CandidateValidation;
using UnityEngine;
using UnityEngine.Profiling;

// Owned adapter. Import/build/player execution requires separate scoped approval.
// No candidate execution in the Editor, and no player work without explicit mode.
public sealed class SegmentUnityHarness : MonoBehaviour
{
    [Serializable] public sealed class Result
    {
        public string mode, inputFingerprint, unityVersion, platform, error, profileFile;
        public string workload = "Owned segment composition only; no CircularBuffer candidate source";
        public string qualification = "Diagnostic Development IL2CPP capture; not competitive performance timing.";
        public int scenarioCount = 6, checksum, iterations, warmIterations;
        public int[] values, profileScenarios, sampleChecksums;
        public long frequency;
        public long[] elapsedTicks;
    }
    static readonly int[] ProfileScenarios = { 2, 3, 5 };
    Result result;
    string output, raw = "";
    int frame, sample, expected;
    bool profiling, finished;

    static string Option(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; ++i) if (args[i] == name) return args[i + 1];
        return "";
    }
    static string FreshOutput(string value)
    {
        if (!Path.IsPathRooted(value) || !Directory.Exists(Path.GetDirectoryName(value)) || File.Exists(value))
            throw new ArgumentException("Use a fresh absolute output file in an existing evidence directory.");
        return Path.GetFullPath(value);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if (Option("--cilica-mode").Length == 0) { Application.Quit(0); return; }
        new GameObject("Owned segment composition reference").AddComponent<SegmentUnityHarness>();
    }
    void Start()
    {
        result = new Result { mode = Option("--cilica-mode"), inputFingerprint = Option("--cilica-fingerprint"),
            unityVersion = Application.unityVersion, platform = Application.platform.ToString(), frequency = Stopwatch.Frequency };
        try
        {
            if (result.mode != "verify" && result.mode != "profile") throw new ArgumentException("Mode must be verify or profile.");
            if (result.inputFingerprint.Length != 64) throw new ArgumentException("Input fingerprint required.");
            foreach (char c in result.inputFingerprint) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))
                throw new ArgumentException("Input fingerprint must be lowercase SHA-256.");
            output = FreshOutput(Option("--cilica-result"));
            if (result.mode == "profile")
            {
#if !DEVELOPMENT_BUILD
                throw new InvalidOperationException("Binary CPU profile requires the reviewed Development build.");
#else
                raw = FreshOutput(Option("--cilica-profile"));
                if (!raw.EndsWith(".raw", StringComparison.Ordinal) || raw == output)
                    throw new ArgumentException("Profile must be a distinct fresh .raw output.");
                result.iterations = int.Parse(Option("--cilica-iterations"), CultureInfo.InvariantCulture);
                if (result.iterations < 1 || result.iterations > 128) throw new ArgumentException("Iterations must be 1..128.");
#endif
            }
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            // Retains all 48 scalar observations: six lengths plus 42 values.
            var values = new List<int>();
            for (int scenario = 0; scenario < 6; ++scenario)
            {
                int length = SegmentReferenceCases.Run(scenario, -1);
                if (length < 1 || length > 4096) throw new InvalidOperationException("Transcript bound.");
                for (int index = -1; index < length; ++index)
                {
                    values.Add(scenario); values.Add(index);
                    values.Add(index == -1 ? length : SegmentReferenceCases.Run(scenario, index));
                }
            }
            result.values = values.ToArray();
            foreach (int value in result.values) result.checksum = unchecked(result.checksum * 31 + value);
            if (result.mode == "verify") { Finish(null); return; }
            result.profileScenarios = ProfileScenarios;
            result.elapsedTicks = new long[3]; result.sampleChecksums = new int[3];
            result.warmIterations = 8; expected = RunBatch(1);
            if (RunBatch(result.warmIterations) != unchecked(expected * result.warmIterations))
                throw new InvalidOperationException("Warm observations changed.");
            // File-backed CPU profiling; no automatic Editor connection requested.
            Profiler.maxUsedMemory = 32 * 1024 * 1024;
            Profiler.logFile = raw; Profiler.enableBinaryLog = true; Profiler.enabled = true;
            result.profileFile = raw; profiling = true;
        }
        catch (Exception error) { Finish(error); }
    }
    static int RunBatch(int count)
    {
        int sum = 0;
        for (int i = 0; i < count; ++i)
            foreach (int scenario in ProfileScenarios) sum = unchecked(sum + SegmentReferenceCases.Run(scenario, 3));
        return sum;
    }
    void Update()
    {
        if (finished || !profiling) return;
        try
        {
            ++frame;
            if (frame == 1) return; // Let Unity publish a profiling frame before the selected work.
            if (sample < 3)
            {
                Profiler.BeginSample("Cilica.SegmentComposition.SelectedRoots");
                long start = Stopwatch.GetTimestamp(); int sum;
                try { sum = RunBatch(result.iterations); }
                finally { Profiler.EndSample(); }
                result.elapsedTicks[sample] = Stopwatch.GetTimestamp() - start;
                result.sampleChecksums[sample++] = sum;
                if (sum != unchecked(expected * result.iterations)) throw new InvalidOperationException("Profile observations changed.");
                if (File.Exists(raw) && new FileInfo(raw).Length > 64 * 1024 * 1024)
                    throw new InvalidOperationException("Profile file exceeded soft 64 MiB bound.");
                return;
            }
            if (frame < 7) return; // Retain frame-boundary flushes before closing the log.
            Profiler.enabled = false; Profiler.logFile = ""; Profiler.enableBinaryLog = false; profiling = false;
            if (!File.Exists(raw) || new FileInfo(raw).Length == 0) throw new InvalidOperationException("No binary CPU profile captured.");
            if (new FileInfo(raw).Length > 64 * 1024 * 1024)
                throw new InvalidOperationException("Closed profile exceeded soft 64 MiB bound.");
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    void Finish(Exception error)
    {
        if (finished) return; finished = true;
        if (profiling) { Profiler.enabled = false; Profiler.logFile = ""; Profiler.enableBinaryLog = false; profiling = false; }
        if (error != null) result.error = error.ToString();
        try
        {
            if (output == null) throw new InvalidOperationException("No validated result path.");
            File.WriteAllText(output, JsonUtility.ToJson(result) + "\n");
        }
        catch (Exception writeError) { UnityEngine.Debug.LogException(writeError); Application.Quit(1); return; }
        Application.Quit(error == null ? 0 : 1);
    }
}
#endif
