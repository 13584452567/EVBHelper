using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EVBHelper.Services
{
    public sealed class RfelCliService : IRfelCliService
    {
        public async Task<RfelExecutionResult> ExecuteAsync(
            RfelExecutionRequest request,
            IProgress<RfelLogEvent>? logProgress,
            CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.ExecutablePath))
            {
                throw new ArgumentException("rfel executable path cannot be empty", nameof(request));
            }

            var executablePath = ResolveExecutable(request.ExecutablePath);
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                WorkingDirectory = !string.IsNullOrWhiteSpace(request.WorkingDirectory)
                    ? request.WorkingDirectory
                    : Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            foreach (var argument in request.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            var outputLines = new List<string>();
            var errorLines = new List<string>();
            var stopwatch = Stopwatch.StartNew();
            var wasCancelled = false;
            Exception? capturedException = null;

            try
            {
                using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                var stdOutTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var stdErrTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                process.OutputDataReceived += (_, args) =>
                {
                    if (args.Data is null)
                    {
                        stdOutTcs.TrySetResult(true);
                        return;
                    }

                    outputLines.Add(args.Data);
                    logProgress?.Report(new RfelLogEvent(RfelLogLevel.Info, args.Data));
                };

                process.ErrorDataReceived += (_, args) =>
                {
                    if (args.Data is null)
                    {
                        stdErrTcs.TrySetResult(true);
                        return;
                    }

                    errorLines.Add(args.Data);
                    logProgress?.Report(new RfelLogEvent(RfelLogLevel.Error, args.Data));
                };

                if (!process.Start())
                {
                    throw new InvalidOperationException("Unable to start rfel process");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await using var registration = cancellationToken.Register(static state =>
                {
                    if (state is Process p && !p.HasExited)
                    {
                        try
                        {
                            p.Kill(entireProcessTree: true);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }, process);

                try
                {
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    wasCancelled = true;
                    throw;
                }
                finally
                {
                    await Task.WhenAll(stdOutTcs.Task, stdErrTcs.Task).ConfigureAwait(false);
                }

                stopwatch.Stop();

                return new RfelExecutionResult(
                    request,
                    process.ExitCode,
                    stopwatch.Elapsed,
                    outputLines,
                    errorLines,
                    wasCancelled,
                    null);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return new RfelExecutionResult(
                    request,
                    null,
                    stopwatch.Elapsed,
                    outputLines,
                    errorLines,
                    wasCancelled: true,
                    null);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                capturedException = ex;
                logProgress?.Report(new RfelLogEvent(RfelLogLevel.Error, ex.Message));
                return new RfelExecutionResult(
                    request,
                    null,
                    stopwatch.Elapsed,
                    outputLines,
                    errorLines,
                    wasCancelled,
                    capturedException);
            }
        }

        public async Task<RfelPipelineResult> FlashAsync(
            RfelFlashRequest request,
            IProgress<RfelLogEvent>? logProgress,
            CancellationToken cancellationToken)
        {
            var steps = new List<RfelExecutionResult>();
            var commands = RfelCommandPlanner.BuildFlashPipeline(request);

            foreach (var command in commands)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await ExecuteAsync(command, logProgress, cancellationToken).ConfigureAwait(false);
                steps.Add(result);

                if (!result.Success)
                {
                    break;
                }
            }

            return new RfelPipelineResult(steps);
        }

        public Task<RfelExecutionResult> CheckVersionAsync(
            string executablePath,
            int verbosity,
            IProgress<RfelLogEvent>? logProgress,
            CancellationToken cancellationToken)
        {
            var arguments = RfelCommandPlanner.CreateVerbosityArguments(verbosity);
            arguments.Add("version");
            return ExecuteAsync(new RfelExecutionRequest(executablePath, arguments), logProgress, cancellationToken);
        }

        public Task<RfelExecutionResult> ResetAsync(
            string executablePath,
            int verbosity,
            IProgress<RfelLogEvent>? logProgress,
            CancellationToken cancellationToken)
        {
            var arguments = RfelCommandPlanner.CreateVerbosityArguments(verbosity);
            arguments.Add("reset");
            return ExecuteAsync(new RfelExecutionRequest(executablePath, arguments), logProgress, cancellationToken);
        }

        private static string ResolveExecutable(string executablePath)
        {
            if (File.Exists(executablePath))
            {
                return executablePath;
            }

            if (OperatingSystem.IsWindows())
            {
                var withExtension = executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? executablePath
                    : executablePath + ".exe";

                if (File.Exists(withExtension))
                {
                    return withExtension;
                }
            }

            var environmentPath = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(environmentPath))
            {
                throw new FileNotFoundException($"Unable to locate rfel executable: {executablePath}");
            }

            foreach (var pathSegment in environmentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(pathSegment, executablePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                if (OperatingSystem.IsWindows())
                {
                    var windowsCandidate = candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? candidate
                        : candidate + ".exe";
                    if (File.Exists(windowsCandidate))
                    {
                        return windowsCandidate;
                    }
                }
            }

            throw new FileNotFoundException($"rfel executable not found in PATH: {executablePath}");
        }
    }
}
