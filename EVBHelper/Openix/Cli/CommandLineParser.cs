using System.Text;

namespace Openix.Cli;

internal static class CommandLineParser
{
    private static readonly string HelpText = BuildHelpText();

    public static bool TryParse(string[] args, out CommandOptions options, out string? error)
    {
        var builder = new CommandOptionsBuilder();
        error = null;

        if (args.Length == 0)
        {
            options = builder.Build();
            error = HelpText;
            return false;
        }

        var remaining = new List<string>();

        foreach (var arg in args)
            {
                if (arg.StartsWith('-') && !arg.StartsWith("--", StringComparison.Ordinal) && arg.Length > 2)
                {
                    if (!TryHandleBundledShortOptions(arg.AsSpan(1), builder, out var bundleError))
                    {
                        options = builder.Build();
                        error = bundleError;
                        return false;
                    }
                    continue;
                }

            switch (arg)
            {
                case "-u":
                case "--unpack":
                    builder.Unpack();
                    break;
                case "-d":
                case "--dump":
                    builder.Dump();
                    break;
                case "-c":
                case "--cfg":
                    builder.GenerateCfg();
                    break;
                case "-p":
                case "--pack":
                    builder.Pack();
                    break;
                case "-s":
                case "--size":
                    builder.ReportSize();
                    break;
                case "-h":
                case "--help":
                    options = builder.Build();
                    error = HelpText;
                    return false;
                default:
                    if (arg.StartsWith('-'))
                    {
                        options = builder.Build();
                        error = $"δ֪ѡ��: {arg}\n\n" + HelpText;
                        return false;
                    }
                    remaining.Add(arg);
                    break;
            }
        }

        builder.SetInputs(remaining);
        options = builder.Build();

        if (options.Inputs.Count == 0)
        {
            error = HelpText;
            return false;
        }

        if (!builder.HasMode)
        {
            error = "����ָ������һ���������� (-u/-d/-p/-s)��\n\n" + HelpText;
            return false;
        }

        return true;
    }

    private static bool TryHandleBundledShortOptions(ReadOnlySpan<char> optionsSpan, CommandOptionsBuilder builder, out string? error)
    {
        error = null;
        var helpRequested = false;

        foreach (var optionChar in optionsSpan)
        {
            switch (optionChar)
            {
                case 'u':
                    builder.Unpack();
                    break;
                case 'd':
                    builder.Dump();
                    break;
                case 'c':
                    builder.GenerateCfg();
                    break;
                case 'p':
                    builder.Pack();
                    break;
                case 's':
                    builder.ReportSize();
                    break;
                case 'h':
                    helpRequested = true;
                    break;
                default:
                    error = $"δ֪ѡ��: -{optionChar}\n\n" + HelpText;
                    return false;
            }
        }

        if (helpRequested)
        {
            error = HelpText;
            return false;
        }

        return true;
    }

    public static string GetHelp() => HelpText;

    private static string BuildHelpText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("OpenixCard - Allwinner ���񹤾�");
        sb.AppendLine();
        sb.AppendLine("Usage: Openix <options> <�����ļ���Ŀ¼>");
        sb.AppendLine();
        sb.AppendLine("ѡ��:");
        sb.AppendLine("  -u, --unpack       ��� Allwinner ����Ŀ¼");
        sb.AppendLine("  -d, --dump         �� Allwinner ����ת��Ϊ��׼���� (�������+���)");
        sb.AppendLine("  -c, --cfg          ���ɷ����� cfg �ļ� (���� --unpack һ��ʹ��)");
        sb.AppendLine("  -p, --pack         ��Ŀ¼���´�� Allwinner ����");
        sb.AppendLine("  -s, --size         ���� Allwinner ������ʵ��С");
        sb.AppendLine("  -h, --help         ��ʾ������Ϣ");
        sb.AppendLine();
        sb.AppendLine("ʾ��:");
        sb.AppendLine("  Openix -u  firmware.img");
        sb.AppendLine("  Openix -u -c firmware.img");
        sb.AppendLine("  Openix -d  firmware.img");
        sb.AppendLine("  Openix -p  unpacked_dir");
        sb.AppendLine("  Openix -s  firmware.img");
        return sb.ToString();
    }

    private sealed class CommandOptionsBuilder
    {
        private bool _unpack;
        private bool _dump;
        private bool _pack;
        private bool _cfg;
        private bool _size;
        private IReadOnlyList<string> _inputs = Array.Empty<string>();

        public bool HasMode => _unpack || _dump || _pack || _size;

        public void Unpack() => _unpack = true;
        public void Dump() => _dump = true;
        public void Pack() => _pack = true;
        public void GenerateCfg() => _cfg = true;
        public void ReportSize() => _size = true;

        public void SetInputs(IReadOnlyList<string> inputs) => _inputs = inputs;

        public CommandOptions Build() => new()
        {
            Unpack = _unpack,
            Dump = _dump,
            Pack = _pack,
            GenerateCfg = _cfg,
            ReportSize = _size,
            Inputs = _inputs
        };
    }
}
