using System;
using EVBHelper.Services;

namespace EVBHelper.Models
{
    public sealed record LogMessage(DateTimeOffset Timestamp, string Message, RfelLogLevel Level)
    {
        public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Message}";
    }
}
