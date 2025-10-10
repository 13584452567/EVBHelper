namespace OpenixCard.Logging;

/// <summary>
/// Represents a log message emitted during OpenixCard operations.
/// </summary>
/// <param name="Level">The severity of the message.</param>
/// <param name="Message">The textual content of the message.</param>
public readonly record struct OpenixLogMessage(OpenixLogLevel Level, string Message)
{
    public static OpenixLogMessage Info(string message) => new(OpenixLogLevel.Info, message);
    public static OpenixLogMessage Data(string message) => new(OpenixLogLevel.Data, message);
    public static OpenixLogMessage Warning(string message) => new(OpenixLogLevel.Warning, message);
    public static OpenixLogMessage Error(string message) => new(OpenixLogLevel.Error, message);
    public static OpenixLogMessage Debug(string message) => new(OpenixLogLevel.Debug, message);
}

/// <summary>
/// Defines the available log levels for OpenixCard operations.
/// </summary>
public enum OpenixLogLevel
{
    Info,
    Data,
    Warning,
    Error,
    Debug
}
