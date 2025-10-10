using System.Globalization;
using System.Linq;
using System.Text;
using OpenixCard.Exceptions;
using OpenixCard.Models;

namespace OpenixCard.Services;

internal sealed class FexToCfgConverter : IFexToCfgConverter
{
    private const string PartitionTableFex = "sys_partition.fex";

    public Task<string> SaveAsync(UnpackContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var metadata = ResolveMetadata(context.OutputDirectory);
        var partitions = ParsePartitionTable(metadata.FexPath, cancellationToken);

        if (partitions.Count == 0)
        {
            throw new OperatorError($"在 {metadata.FexPath} 中未找到任何分区定义。");
        }

        var tableType = DeterminePartitionTableType(partitions);
        var cfgContent = BuildCfg(metadata.ImageName, partitions, tableType);

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(context.OutputDirectory);
        File.WriteAllText(metadata.TargetCfgPath, cfgContent, Encoding.UTF8);

        return Task.FromResult(metadata.TargetCfgPath);
    }

    public Task<PartitionSizeInfo> CalculateSizeAsync(UnpackContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var metadata = ResolveMetadata(context.OutputDirectory);
        var partitions = ParsePartitionTable(metadata.FexPath, cancellationToken);

        var entries = partitions
            .Where(p => !string.Equals(p.Name, "UDISK", StringComparison.OrdinalIgnoreCase))
            .Select(p => new PartitionEntry
            {
                Name = p.Name,
                SizeInKilobytes = p.SizeSectors / 2,
                Source = p.DownloadFile ?? "blank.fex"
            })
            .ToList();

        var totalKilobytes = entries.Sum(e => e.SizeInKilobytes) + LinuxPartitionDefaults.CommonCompensationInKilobytes;

        var info = new PartitionSizeInfo
        {
            SizeInKilobytes = totalKilobytes,
            SizeInMegabytes = totalKilobytes / 1024d,
            Partitions = entries
        };

        return Task.FromResult(info);
    }

    private static PartitionTableType DeterminePartitionTableType(IEnumerable<PartitionDefinition> partitions)
    {
        foreach (var partition in partitions)
        {
            var downloadFile = partition.DownloadFile?.Trim('"');
            if (string.Equals(partition.Name, "boot-resource", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(downloadFile, "boot-resource.fex", StringComparison.OrdinalIgnoreCase))
            {
                return PartitionTableType.Hybrid;
            }
        }

        return PartitionTableType.Gpt;
    }

    private static string BuildCfg(string imageName, IReadOnlyList<PartitionDefinition> partitions, PartitionTableType tableType)
    {
        var sb = new StringBuilder();
        sb.Append("image ").Append(imageName).Append(".img {\n");

        AppendHdImageSection(sb, tableType);
        AppendBootSections(sb);
        AppendPartitions(sb, partitions);

        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendHdImageSection(StringBuilder sb, PartitionTableType tableType)
    {
        sb.Append('\t').Append("hdimage{\n");
        sb.Append("\t\tpartition-table-type = \"")
            .Append(tableType switch
            {
                PartitionTableType.Hybrid => "hybrid",
                PartitionTableType.Mbr => "mbr",
                _ => "gpt"
            })
            .Append("\"\n");
        sb.Append("\t\tgpt-location = ")
            .Append(LinuxPartitionDefaults.GptLocation / 0x100000)
            .Append('M')
            .Append('\n');
        sb.Append("\t}\n\n");
    }

    private static void AppendBootSections(StringBuilder sb)
    {
        sb.Append("\tpartition boot0 {\n")
          .Append("\t\tin-partition-table = \"no\"\n")
          .Append("\t\timage = \"boot0_sdcard.fex\"\n")
          .Append("\t\toffset = ")
          .Append(LinuxPartitionDefaults.Boot0Offset / 0x400)
          .Append('K')
          .Append('\n')
          .Append("\t}\n\n");

        sb.Append("\tpartition boot-packages {\n")
          .Append("\t\tin-partition-table = \"no\"\n")
          .Append("\t\timage = \"boot_package.fex\"\n")
          .Append("\t\toffset = ")
          .Append(LinuxPartitionDefaults.BootPackagesOffset / 0x400)
          .Append('K')
          .Append('\n')
          .Append("\t}\n\n");
    }

    private static void AppendPartitions(StringBuilder sb, IReadOnlyList<PartitionDefinition> partitions)
    {
        foreach (var partition in partitions)
        {
            if (string.Equals(partition.Name, "UDISK", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            sb.Append("\tpartition ").Append(partition.Name).Append(" {\n");

            if (string.Equals(partition.Name, "boot-resource", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(partition.DownloadFile, "boot-resource.fex", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("\t\tpartition-type = 0xC\n");
            }

            var imageName = string.IsNullOrWhiteSpace(partition.DownloadFile)
                ? "blank.fex"
                : partition.DownloadFile.Trim('"');

            sb.Append("\t\timage = \"").Append(imageName).Append("\"\n");
            sb.Append("\t\tsize = ").Append(partition.SizeSectors / 2).Append('K').Append('\n');
            sb.Append("\t}\n\n");
        }
    }

    private static PartitionFileMetadata ResolveMetadata(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("目录路径不能为空", nameof(directory));
        }

        var normalized = Path.TrimEndingDirectorySeparator(directory);
        var folderName = Path.GetFileName(normalized);
        if (string.IsNullOrEmpty(folderName))
        {
            folderName = new DirectoryInfo(normalized).Name;
        }

        var firstDot = folderName.IndexOf('.');
        var imageName = firstDot > 0 ? folderName[..firstDot] : folderName;

        var cfgFileName = imageName.Contains('.') ?
            imageName[..imageName.LastIndexOf('.')] + ".cfg" :
            imageName + ".cfg";

        if (string.IsNullOrWhiteSpace(cfgFileName))
        {
            cfgFileName = "sys_partition.cfg";
        }

        var fexPath = Path.Combine(directory, PartitionTableFex);
        if (!File.Exists(fexPath))
        {
            throw new FileOpenException(fexPath);
        }

        return new PartitionFileMetadata
        {
            ImageName = imageName,
            FexPath = fexPath,
            TargetCfgPath = Path.Combine(directory, cfgFileName)
        };
    }

    private static List<PartitionDefinition> ParsePartitionTable(string fexPath, CancellationToken cancellationToken)
    {
        var partitions = new List<PartitionDefinition>();
        PartitionBuilder? builder = null;
        var inPartitionRegion = false;

        foreach (var rawLine in File.ReadLines(fexPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = StripComment(rawLine);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.Trim();

            if (trimmed.Length > 0 && trimmed[0] == '[')
            {
                if (string.Equals(trimmed, "[partition_start]", StringComparison.OrdinalIgnoreCase))
                {
                    inPartitionRegion = true;
                    continue;
                }

                if (string.Equals(trimmed, "[partition_end]", StringComparison.OrdinalIgnoreCase))
                {
                    if (builder != null)
                    {
                        partitions.Add(builder.Build());
                        builder = null;
                    }
                    inPartitionRegion = false;
                    continue;
                }

                if (inPartitionRegion && trimmed.StartsWith("[partition", StringComparison.OrdinalIgnoreCase))
                {
                    if (builder != null)
                    {
                        partitions.Add(builder.Build());
                    }
                    builder = new PartitionBuilder();
                    continue;
                }

                if (builder != null)
                {
                    partitions.Add(builder.Build());
                    builder = null;
                }

                continue;
            }

            if (!inPartitionRegion || builder is null)
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();
            builder.Set(key, value);
        }

        if (builder != null)
        {
            partitions.Add(builder.Build());
        }

        return partitions;
    }

    private static string StripComment(string line)
    {
        var index = line.IndexOf(';');
        if (index < 0)
        {
            return line;
        }

        var inQuote = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                inQuote = !inQuote;
            }

            if (ch == ';' && !inQuote)
            {
                return line[..i];
            }
        }

        return line;
    }

    private sealed class PartitionFileMetadata
    {
        public required string ImageName { get; init; }
        public required string FexPath { get; init; }
        public required string TargetCfgPath { get; init; }
    }

    private sealed class PartitionBuilder
    {
        private string? _name;
        private long _sizeSectors;
        private string? _downloadFile;
        private long? _userType;

        public void Set(string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "name":
                    _name = value.Trim('"');
                    break;
                case "size":
                    _sizeSectors = ParseNumber(value);
                    break;
                case "downloadfile":
                    _downloadFile = value.Trim().Trim('"');
                    break;
                case "user_type":
                    _userType = ParseNumber(value);
                    break;
            }
        }

        public PartitionDefinition Build()
        {
            if (string.IsNullOrEmpty(_name))
            {
                throw new OperatorError("分区项缺少 name 字段。");
            }

            return new PartitionDefinition
            {
                Name = _name,
                SizeSectors = _sizeSectors,
                DownloadFile = _downloadFile,
                UserType = _userType
            };
        }

        private static long ParseNumber(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return long.Parse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            throw new OperatorError($"无法解析数值: {value}");
        }
    }
}
