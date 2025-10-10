using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OpenixIMG
{
    public struct Partition
    {
        public string Name;
        public ulong Size;
        public string DownloadFile;
        public uint UserType;
        public bool KeyData;
        public bool Encrypt;
        public bool Verify;
        public bool RO;
    }

    public class OpenixPartition
    {
        private readonly bool _verbose;
        public uint DownloadFileSize { get; private set; }
        public uint MbrSize { get; private set; }
        public List<Partition> Partitions { get; } = new List<Partition>();

        public OpenixPartition(byte[] data, bool verbose = false)
        {
            _verbose = verbose;
            Parse(Encoding.ASCII.GetString(data));
        }

        private void Parse(string content)
        {
            using (var stream = new StringReader(content))
            {
                string? line;
                bool inMbrSection = false;
                bool inPartitionSection = false;
                Partition currentPartition = new Partition();

                while ((line = stream.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("//"))
                        continue;

                    if (line == "[partition_start]")
                    {
                        inPartitionSection = true;
                        inMbrSection = false;
                        continue;
                    }
                    if (line == "[mbr]")
                    {
                        inMbrSection = true;
                        inPartitionSection = false;
                        continue;
                    }
                    if (line == "[partition]")
                    {
                        inMbrSection = false;
                        if (!string.IsNullOrEmpty(currentPartition.Name))
                        {
                            Partitions.Add(currentPartition);
                        }
                        currentPartition = new Partition();
                        inPartitionSection = true;
                        continue;
                    }

                    if (inMbrSection)
                    {
                        var parts = line.Split(new[] { '=' }, 2);
                        if (parts.Length == 2 && parts[0].Trim() == "size")
                        {
                            MbrSize = (uint)ParseNumber(parts[1].Trim());
                        }
                    }
                    else if (inPartitionSection)
                    {
                        ParseLine(line, ref currentPartition);
                    }
                }

                if (inPartitionSection && !string.IsNullOrEmpty(currentPartition.Name))
                {
                    Partitions.Add(currentPartition);
                }
            }
        }

        public void PrintPartition()
        {
            var sb = new StringBuilder();
            sb.AppendLine("\nPartition details:");
            sb.AppendLine("--------------------------------------------------------------------------------------------------------");
            sb.AppendFormat("{0,-20} {1,-20} {2,-35} {3,-10} {4}\n", "Name", "Size", "Download File", "User Type", "Flags");
            sb.AppendLine("--------------------------------------------------------------------------------------------------------");

            foreach (var p in Partitions)
            {
                var flags = (p.KeyData ? "K" : "") + (p.Encrypt ? "E" : "") + (p.Verify ? "V" : "") + (p.RO ? "R" : "");
                if (string.IsNullOrEmpty(flags)) flags = "-";
                sb.AppendFormat("{0,-20} {1,-20} {2,-35} 0x{3:x4}     {4}\n",
                    p.Name, p.Size, string.IsNullOrEmpty(p.DownloadFile) ? "-" : p.DownloadFile, p.UserType, flags);
            }
            sb.AppendLine("\nFlags: K=KeyData, E=Encrypt, V=Verify, R=Read-Only");
            Console.WriteLine(sb.ToString());
        }

        private static void ParseLine(string line, ref Partition partition)
        {
            var parts = line.Split(new[] { '=' }, 2);
            if (parts.Length != 2) return;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            switch (key)
            {
                case "name": partition.Name = value; break;
                case "size": partition.Size = ParseNumber(value); break;
                case "downloadfile": partition.DownloadFile = value.Trim('"'); break;
                case "user_type": partition.UserType = (uint)ParseNumber(value); break;
                case "keydata": partition.KeyData = ParseNumber(value) != 0; break;
                case "encrypt": partition.Encrypt = ParseNumber(value) != 0; break;
                case "verify": partition.Verify = ParseNumber(value) != 0; break;
                case "ro": partition.RO = ParseNumber(value) != 0; break;
            }
        }

        private static ulong ParseNumber(string s)
        {
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ulong.Parse(s.Substring(2), NumberStyles.HexNumber);
            }
            return ulong.Parse(s);
        }
    }
}
