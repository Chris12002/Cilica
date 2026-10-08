using System;
using System.Diagnostics;
using System.IO;
using Cilica.CandidateBenchmark;
using UnityEngine;

// No Editor initialization hooks. Runs only when the built player starts.
public static class BenchmarkHarness
{
    [Serializable] public sealed class Result
    {
        public string mode, unityVersion, platform, error;
        public int[] values;
        public int checksum, iterations, warmIterations;
        public long frequency;
        public long[] elapsedTicks;
        public double warmMilliseconds;
    }
    static string Option(string name, string fallback)
    {
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i+1<args.Length;i++) if(args[i]==name) return args[i+1];
        return fallback;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Main()
    {
        var r=new Result {mode=Option("--cilica-mode","verify"), unityVersion=Application.unityVersion,
            platform=Application.platform.ToString(),frequency=Stopwatch.Frequency};
        int exit=0;
        try
        {
            if(r.mode=="verify")
            {
                int count=BenchProbe.Run(0,-1);if(count!=523)throw new Exception("Unexpected transcript length: "+count);
                r.values=new int[count];for(int i=0;i<count;i++)r.values[i]=BenchProbe.Run(0,-i-2);
                r.checksum=BenchProbe.Run(0,1);
            }
            else if(r.mode=="cold")
            {
                long start=Stopwatch.GetTimestamp();r.checksum=BenchProbe.Run(0,1);
                r.elapsedTicks=new[]{Stopwatch.GetTimestamp()-start};r.iterations=1;
            }
            else if(r.mode=="steady")
            {
                r.iterations=int.Parse(Option("--cilica-iterations","4096"));
                if(r.iterations<1 || r.iterations>32768)throw new Exception("Iteration bound");
                long start=Stopwatch.GetTimestamp();
                do {if(BenchProbe.Run(0,8)!=49168)throw new Exception("Warm checksum");r.warmIterations+=8;}
                while((Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency<300);
                r.warmMilliseconds=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
                r.elapsedTicks=new long[7];
                for(int i=0;i<7;i++)
                {
                    start=Stopwatch.GetTimestamp();int sum=BenchProbe.Run(0,r.iterations);
                    r.elapsedTicks[i]=Stopwatch.GetTimestamp()-start;
                    if(sum!=unchecked(6146*r.iterations))throw new Exception("Sample checksum");r.checksum=sum;
                }
            }
            else if(r.mode!="noop")throw new Exception("Unknown mode");
            if((r.mode=="verify" || r.mode=="cold") && r.checksum!=6146)throw new Exception("Checksum mismatch");
        }
        catch(Exception e){r.error=e.ToString();exit=1;}
        string json=JsonUtility.ToJson(r);UnityEngine.Debug.Log("CILICA_RESULT "+json);
        try {string file=Option("--cilica-result","");if(file.Length!=0)File.WriteAllText(file,json+"\n");}
        catch(Exception e){UnityEngine.Debug.LogException(e);exit=1;}
        Application.Quit(exit);
    }
}
