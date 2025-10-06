using EVBHelper.Services;
using System;

namespace EVBHelper.Models
{
    public sealed record LogMessage(DateTimeOffset Timestamp, string Message, RfelLogLevel Level)
    {
        public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Message}";
    }
}
