using ProtonEcuToolkit.Core.Kwp;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotnet run -- <COM_PORT>");
    return 1;
}

var path = args[0];
const int maxCycles = 5;
var cycles = 0;
var done = new TaskCompletionSource();

using var session = new KwpSession();

session.StateChanged += (state, detail) =>
{
    Console.WriteLine(detail is not null ? $"[state] {state} ({detail})" : $"[state] {state}");
};

session.PidsUpdated += (readings) =>
{
    cycles++;
    Console.WriteLine($"\n[pids cycle {cycles}/{maxCycles}]");
    foreach (var r in readings)
    {
        if (r.Error is not null)
        {
            Console.WriteLine($"  {r.Name,-16} ERROR: {r.Error}  (raw={r.RawHex})");
        }
        else
        {
            Console.WriteLine($"  {r.Name,-16} {r.Value:F2} {r.Unit}  (raw={r.RawHex})");
        }
    }

    if (cycles >= maxCycles)
    {
        done.TrySetResult();
    }
};

try
{
    await session.ConnectAsync(path);
    await done.Task;
}
finally
{
    await session.DisconnectAsync();
}

return 0;
