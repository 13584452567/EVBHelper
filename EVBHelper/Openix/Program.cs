using Openix.Cli;
using Openix.Services;

namespace Openix;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        var app = new OpenixCardApp(ServiceFactory.Create());
        var exitCode = await app.RunAsync(args, cts.Token);
        return exitCode;
    }
}
