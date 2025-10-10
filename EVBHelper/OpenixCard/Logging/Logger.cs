using System.Diagnostics.CodeAnalysis;

namespace OpenixCard.Logging;

internal static class Logger
{
    private static readonly object SyncRoot = new();

    public static void Info(string message) => Write("[OpenixCard INFO] ", message, ConsoleColor.Cyan);
    public static void Data(string message) => Write(string.Empty, message, ConsoleColor.Green);
    public static void Warning(string message) => Write("[OpenixCard WARN] ", message, ConsoleColor.Yellow);
    public static void Error(string message) => Write("[OpenixCard ERROR] ", message, ConsoleColor.Red);
    public static void Debug(string message) => Write("[OpenixCard DEBUG] ", message, ConsoleColor.Gray);

    private static void Write(string prefix, string message, ConsoleColor color)
    {
        lock (SyncRoot)
        {
            var (currentForeground, currentBackground) = (Console.ForegroundColor, Console.BackgroundColor);
            try
            {
                Console.ForegroundColor = color;
                if (!string.IsNullOrEmpty(prefix))
                {
                    Console.Write(prefix);
                }
                Console.WriteLine(message);
            }
            finally
            {
                Console.ForegroundColor = currentForeground;
                Console.BackgroundColor = currentBackground;
            }
        }
    }

    [DoesNotReturn]
    public static void Fatal(Exception ex)
    {
        Error(ex.Message);
        Environment.ExitCode = -1;
        throw ex;
    }
}
