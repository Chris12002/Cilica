using System;
using System.Collections.Generic;
using System.IO;
using UnityEditorInternal;
using UnityEngine;

// Owned postprocessor in a separate empty Editor project. No candidate assembly.
public static class ProfileInspection
{
    const string Marker = "Cilica.Albireo.DecodeSelectedRoots";
    [Serializable] public sealed class Sample
    {
        public int frame, thread, sample, children;
        public string threadName;
        public double milliseconds;
        public long gcAllocationBytes;
        public int gcAllocationSamples;
    }
    [Serializable] public sealed class Report
    {
        public string unityVersion, rawFile, marker = Marker, error;
        public int firstFrame, lastFrame, threadViews, sampleCount, markerSamples;
        public Sample[] samples;
        public string qualification = "Development player diagnostic marker capture; source construction and two Decode calls per Probe.Run. Not pure Decode timing or a competitive comparison.";
    }
    static string Option(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; ++i) if (args[i] == name) return args[i + 1];
        throw new ArgumentException("Required argument: " + name);
    }
    public static void Inspect()
    {
        string input = Option("--cilica-profile"), output = Option("--cilica-inspection");
        if (!Path.IsPathRooted(input) || !File.Exists(input) || new FileInfo(input).Length > 64 * 1024 * 1024)
            throw new ArgumentException("A bounded existing absolute profile path is required.");
        if (!Path.IsPathRooted(output) || File.Exists(output) || !Directory.Exists(Path.GetDirectoryName(output)))
            throw new ArgumentException("A fresh absolute result path is required.");
        var report = new Report { unityVersion = Application.unityVersion, rawFile = Path.GetFullPath(input) };
        var found = new List<Sample>();
        try
        {
            ProfilerDriver.ClearAllFrames();
            if (!ProfilerDriver.LoadProfile(input, false)) throw new InvalidOperationException("Profiler could not load the raw capture.");
            report.firstFrame = ProfilerDriver.firstFrameIndex; report.lastFrame = ProfilerDriver.lastFrameIndex;
            if (report.firstFrame < 0 || report.lastFrame < report.firstFrame || report.lastFrame - report.firstFrame > 1024)
                throw new InvalidOperationException("Unexpected captured frame range.");
            for (int frame = report.firstFrame; frame <= report.lastFrame; ++frame)
            {
                for (int thread = 0; thread < 256; ++thread)
                {
                    using (var view = ProfilerDriver.GetRawFrameDataView(frame, thread))
                    {
                        if (!view.valid) break;
                        ++report.threadViews;
                        if (view.sampleCount < 0 || view.sampleCount > 1000000 || report.sampleCount > 5000000 - view.sampleCount)
                            throw new InvalidOperationException("Profiler sample inspection bound.");
                        report.sampleCount += view.sampleCount;
                        for (int i = 0; i < view.sampleCount; ++i)
                        {
                            if (view.GetSampleName(i) != Marker) continue;
                            int children = view.GetSampleChildrenCountRecursive(i);
                            if (children < 0 || children > view.sampleCount - i - 1)
                                throw new InvalidOperationException("Invalid marker child range.");
                            var sample = new Sample { frame = frame, thread = thread, sample = i,
                                threadName = view.threadName, milliseconds = view.GetSampleTimeMs(i), children = children };
                            for (int child = i + 1; child <= i + children; ++child)
                            {
                                if (view.GetSampleName(child) != "GC.Alloc" || view.GetSampleMetadataCount(child) < 1) continue;
                                sample.gcAllocationBytes += view.GetSampleMetadataAsLong(child, 0);
                                ++sample.gcAllocationSamples;
                            }
                            found.Add(sample);
                            if (found.Count > 3) throw new InvalidOperationException("More than three selected marker samples.");
                        }
                    }
                }
            }
            report.markerSamples = found.Count;
            if (found.Count != 3) throw new InvalidOperationException("Expected exactly three selected marker samples.");
        }
        catch (Exception error)
        {
            report.error = error.ToString();
            throw;
        }
        finally
        {
            report.samples = found.ToArray(); report.markerSamples = found.Count;
            File.WriteAllText(output, JsonUtility.ToJson(report, true) + "\n");
        }
    }
}
