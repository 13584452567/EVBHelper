using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace EVBHelper.Services
{
    public enum RfelLogLevel
    {
        Info,
        Warning,
        Error
    }

    public sealed record RfelLogEvent(RfelLogLevel Level, string Message)
    {
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.Now;
    }

    public sealed record RfelExecutionRequest(string ExecutablePath, IReadOnlyList<string> Arguments, string? WorkingDirectory = null)
    {
        public string CommandDisplayText => $"{Path.GetFileNameWithoutExtension(ExecutablePath)} {string.Join(' ', Arguments)}".Trim();
    }

    public sealed class RfelExecutionResult
    {
        public RfelExecutionResult(
            RfelExecutionRequest request,
            int? exitCode,
            TimeSpan duration,
            IReadOnlyList<string> standardOutput,
            IReadOnlyList<string> standardError,
            bool wasCancelled,
            Exception? exception)
        {
            Request = request;
            ExitCode = exitCode;
            Duration = duration;
            StandardOutput = new ReadOnlyCollection<string>(standardOutput is IList<string> outputList
                ? outputList
                : new List<string>(standardOutput));
            StandardError = new ReadOnlyCollection<string>(standardError is IList<string> errorList
                ? errorList
                : new List<string>(standardError));
            WasCancelled = wasCancelled;
            Exception = exception;
        }

        public RfelExecutionRequest Request { get; }

        public int? ExitCode { get; }

        public TimeSpan Duration { get; }

        public bool WasCancelled { get; }

        public Exception? Exception { get; }

        public IReadOnlyList<string> StandardOutput { get; }

        public IReadOnlyList<string> StandardError { get; }

        public bool Success => !WasCancelled && Exception is null && ExitCode == 0;

        public string DescribeOutcome()
        {
            if (WasCancelled)
            {
                return "Operation cancelled";
            }

            if (Exception is not null)
            {
                return $"Exception: {Exception.Message}";
            }

            return ExitCode == 0
                ? "Succeeded"
                : $"Exit code {ExitCode}";
        }
    }

    public sealed class RfelFlashRequest
    {
        public string ExecutablePath { get; init; } = string.Empty;

        public string FirmwarePath { get; init; } = string.Empty;

        public string Address { get; init; } = string.Empty;

        public bool InitializeDdr { get; init; }

        public string? DdrProfile { get; init; }

        public bool ResetAfterFlash { get; init; }

        public int VerbosityLevel { get; init; }

        public string? WorkingDirectory { get; init; }
    }

    public sealed class RfelPipelineResult
    {
        public RfelPipelineResult(IReadOnlyList<RfelExecutionResult> steps)
        {
            Steps = steps;
        }

        public IReadOnlyList<RfelExecutionResult> Steps { get; }

        public bool Success => Steps.All(static step => step.Success);

        public bool WasCancelled => Steps.Any(static step => step.WasCancelled);

        public RfelExecutionResult? FirstFailure => Steps.FirstOrDefault(static step => !step.Success);
    }

    public interface IRfelCliService
    {
        System.Threading.Tasks.Task<RfelExecutionResult> ExecuteAsync(
            RfelExecutionRequest request,
            IProgress<RfelLogEvent>? logProgress,
            System.Threading.CancellationToken cancellationToken);

        System.Threading.Tasks.Task<RfelPipelineResult> FlashAsync(
            RfelFlashRequest request,
            IProgress<RfelLogEvent>? logProgress,
            System.Threading.CancellationToken cancellationToken);

        System.Threading.Tasks.Task<RfelExecutionResult> CheckVersionAsync(
            string executablePath,
            int verbosity,
            IProgress<RfelLogEvent>? logProgress,
            System.Threading.CancellationToken cancellationToken);

        System.Threading.Tasks.Task<RfelExecutionResult> ResetAsync(
            string executablePath,
            int verbosity,
            IProgress<RfelLogEvent>? logProgress,
            System.Threading.CancellationToken cancellationToken);
    }
}
