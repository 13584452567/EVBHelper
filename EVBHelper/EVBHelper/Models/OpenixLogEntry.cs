using System;
using Openix.Logging;

namespace EVBHelper.Models;

public sealed record OpenixLogEntry(DateTimeOffset Timestamp, string Message, OpenixLogLevel Level)
{
    public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Message}";
}
