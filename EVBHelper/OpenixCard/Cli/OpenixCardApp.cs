using OpenixCard.Logging;
using OpenixCard.Services;
using OpenixCard.Utilities;

namespace OpenixCard.Cli;

internal sealed class OpenixCardApp
{
    private readonly OpenixCardService _service;

    public OpenixCardApp(OpenixCardService service)
    {
        _service = service;
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        ShowLogo();

        if (!CommandLineParser.TryParse(args, out var options, out var error))
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine(error);
            }
            return -1;
        }

        try
        {
            await _service.ExecuteAsync(options, cancellationToken);
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
            if (Environment.GetEnvironmentVariable("OPENIX_DEBUG") == "1")
            {
                Logger.Debug(ex.ToString());
            }
            return -1;
        }
    }

    private static void ShowLogo()
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"|_| Version: {GitMetadata.CommitHash} Commit: {GitMetadata.Version}");
        Console.ResetColor();
        Console.WriteLine();
    }
}
