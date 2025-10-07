namespace Openix.Cli;

internal sealed class CommandOptions
{
    public bool Unpack { get; init; }
    public bool Dump { get; init; }
    public bool Pack { get; init; }
    public bool GenerateCfg { get; init; }
    public bool ReportSize { get; init; }
    public IReadOnlyList<string> Inputs { get; init; } = Array.Empty<string>();
}
