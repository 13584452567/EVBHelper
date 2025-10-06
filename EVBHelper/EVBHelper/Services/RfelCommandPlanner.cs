using System;
using System.Collections.Generic;
using System.IO;

namespace EVBHelper.Services
{
    public static class RfelCommandPlanner
    {
        public static IReadOnlyList<RfelExecutionRequest> BuildFlashPipeline(RfelFlashRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.ExecutablePath))
            {
                throw new ArgumentException("rfel executable path cannot be empty", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.FirmwarePath))
            {
                throw new ArgumentException("Firmware path cannot be empty", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Address))
            {
                throw new ArgumentException("Flash address cannot be empty", nameof(request));
            }

            var workingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? Path.GetDirectoryName(Path.GetFullPath(request.FirmwarePath)) ?? Environment.CurrentDirectory
                : request.WorkingDirectory;

            var normalizedFirmwarePath = Path.GetFullPath(request.FirmwarePath);

            var commands = new List<RfelExecutionRequest>();

            if (request.InitializeDdr)
            {
                var ddrArgs = CreateVerbosityArguments(request.VerbosityLevel);
                ddrArgs.Add("ddr");
                if (!string.IsNullOrWhiteSpace(request.DdrProfile))
                {
                    ddrArgs.Add("--profile");
                    ddrArgs.Add(request.DdrProfile!);
                }

                commands.Add(new RfelExecutionRequest(request.ExecutablePath, ddrArgs, workingDirectory));
            }

            var writeArgs = CreateVerbosityArguments(request.VerbosityLevel);
            writeArgs.Add("write");
            writeArgs.Add(request.Address.Trim());
            writeArgs.Add(normalizedFirmwarePath);
            commands.Add(new RfelExecutionRequest(request.ExecutablePath, writeArgs, workingDirectory));

            if (request.ResetAfterFlash)
            {
                var resetArgs = CreateVerbosityArguments(request.VerbosityLevel);
                resetArgs.Add("reset");
                commands.Add(new RfelExecutionRequest(request.ExecutablePath, resetArgs, workingDirectory));
            }

            return commands;
        }

        public static List<string> CreateVerbosityArguments(int level)
        {
            if (level <= 0)
            {
                return new List<string>();
            }

            var count = Math.Clamp(level, 0, 3);
            return new List<string> { "-" + new string('v', count) };
        }
    }
}
