using System.Reflection;

namespace OpenixCard.Utilities;

internal static class GitMetadata
{
    public static string CommitHash { get; } =
        Environment.GetEnvironmentVariable("OPENIX_COMMIT")
        ?? Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    public static string Version { get; } =
        Environment.GetEnvironmentVariable("OPENIX_VERSION")
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "0.0.0";
}
