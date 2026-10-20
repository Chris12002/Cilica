using System;
using System.IO;
using UnityEngine;
using Cilica.SectionTiming;
public static class SectionHarness
{
    static string Option(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; ++i) if (args[i] == name) return args[i + 1];
        return fallback;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Main()
    {
        Result result; int exit = 0;
        try { result = Runner.Run(Option("--mode", "verify"), int.Parse(Option("--work", "0")),
            int.Parse(Option("--iterations", "4096")), Option("--timers", "on") == "on"); }
        catch (Exception error) { result = new Result { error = error.ToString() }; exit = 1; }
        try { File.WriteAllText(Option("--result", "sections.json"), JsonUtility.ToJson(result)); }
        catch (Exception error) { Debug.LogException(error); exit = 1; }
        Application.Quit(exit);
    }
}
