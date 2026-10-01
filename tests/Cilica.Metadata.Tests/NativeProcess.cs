using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Cilica.Metadata.Tests;

/// <summary>Disposable test-owned processes only, with time and captured-output bounds.</summary>
internal static class NativeProcess
{
    internal sealed record Result(int ExitCode, string Stdout, string Stderr);
    internal const int MaximumOutputCharacters = 262_144;

    internal static async Task<Result> Run(string executable, IEnumerable<string> arguments, string directory,
        string input = "", TimeSpan? timeout = null)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory, UseShellExecute = false,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new InvalidOperationException("Could not start the test-owned native process.");
        var overflow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<string> stdout = Read(process.StandardOutput), stderr = Read(process.StandardError);
        Task writing = WriteInput();
        Task completion = Task.WhenAll(process.WaitForExitAsync(), stdout, stderr, writing);
        try
        {
            Task completed = await Task.WhenAny(completion, overflow.Task).WaitAsync(timeout ?? TimeSpan.FromSeconds(30));
            await completed;
            if (overflow.Task.IsCompleted) await overflow.Task;
            return new(process.ExitCode, await stdout, await stderr);
        }
        catch
        {
            // This handle belongs to this harness; never terminate by port/name.
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { } // Exit can race termination.
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            try { await completion.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException) { }
            throw;
        }

        async Task WriteInput()
        {
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
        }

        async Task<string> Read(StreamReader reader)
        {
            var text = new StringBuilder(); var buffer = new char[4_096];
            int count;
            while ((count = await reader.ReadAsync(buffer)) != 0)
            {
                if (text.Length + count > MaximumOutputCharacters)
                {
                    overflow.TrySetException(new InvalidOperationException("Native process exceeded the captured-output bound."));
                    // Continue draining while the owner terminates the process.
                    continue;
                }
                text.Append(buffer, 0, count);
            }
            return text.ToString();
        }
    }
}
