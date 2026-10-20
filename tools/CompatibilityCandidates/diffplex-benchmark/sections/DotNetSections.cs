using System;
using System.Text.Json;
using Cilica.SectionTiming;
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(JsonSerializer.Serialize(Runner.Run(args[0], int.Parse(args[1]),
            int.Parse(args[2]), args[3] == "on"), new JsonSerializerOptions { IncludeFields = true }));
    }
}
