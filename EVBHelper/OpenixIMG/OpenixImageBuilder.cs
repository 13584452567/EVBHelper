using System.Globalization;
using System.Text;

namespace OpenixIMG
{
    public sealed class OpenixImageBuilder
    {
        private readonly IBlockCipher _headerCipher = new RC6Engine();
        private readonly IBlockCipher _fileHeadersCipher = new RC6Engine();
        private readonly IBlockCipher _fileContentCipher = new RC6Engine();

        public OpenixImageBuilder()
        {
            InitCrypto();
        }

        public void BuildFromFolder(string folder, string outputFile)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                throw new DirectoryNotFoundException(folder);
            }

            var cfgPath = Path.Combine(folder, "image.cfg");
            if (!File.Exists(cfgPath))
            {
                throw new FileNotFoundException("image.cfg not found", cfgPath);
            }

            var cfg = File.ReadAllText(cfgPath, Encoding.ASCII);
            var model = ParseConfig(cfg);

            var files = new List<(ConfigFileEntry entry, string path, uint size)>();
            foreach (var e in model.Files)
            {
                var path = Path.Combine(folder, e.Filename.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Missing file: {e.Filename}", path);
                }
                var len = checked((uint)new FileInfo(path).Length);
                files.Add((e, path, len));
            }

            var header = new ImageHeader();
            header.Initialize(model.Version, model.Pid, model.Vid, model.HardwareId, model.FirmwareId, (uint)files.Count);

            var fileHeaders = new FileHeader[files.Count];

            uint contentOffset = 1024 + (uint)files.Count * 1024;
            if ((contentOffset & 0x1FF) != 0)
            {
                contentOffset = (contentOffset & ~0x1FFu) + 0x200u;
            }

            uint runningOffset = contentOffset;
            for (int i = 0; i < files.Count; i++)
            {
                var (e, p, size) = files[i];
                var fh = new FileHeader();
                fh.Initialize(e.Filename, e.MainType, e.SubType, size, runningOffset);
                fileHeaders[i] = fh;
                var stored = fh.v1.stored_length;
                runningOffset = checked(runningOffset + stored);
            }

            header.image_size = runningOffset;

            using var fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);

            var headerBuf = StructureToBytes(header, 1024);
            var fhBuf = new byte[fileHeaders.Length * 1024];
            for (int i = 0; i < fileHeaders.Length; i++)
            {
                var one = StructureToBytes(fileHeaders[i], 1024);
                Buffer.BlockCopy(one, 0, fhBuf, i * 1024, 1024);
            }

            if (model.Encrypt)
            {
                ProcessBlocks(_headerCipher, headerBuf, 0, headerBuf.Length);
                if (fhBuf.Length > 0)
                {
                    ProcessBlocks(_fileHeadersCipher, fhBuf, 0, fhBuf.Length);
                }
            }

            fs.Write(headerBuf, 0, headerBuf.Length);
            fs.Write(fhBuf, 0, fhBuf.Length);

            var written = (uint)(fs.Position);
            if (written < contentOffset)
            {
                var pad = new byte[contentOffset - written];
                fs.Write(pad, 0, pad.Length);
            }

            for (int i = 0; i < files.Count; i++)
            {
                var data = File.ReadAllBytes(files[i].path);
                if (data.Length != fileHeaders[i].v1.original_length)
                {
                    Array.Resize(ref data, (int)fileHeaders[i].v1.original_length);
                }

                var stored = (int)fileHeaders[i].v1.stored_length;
                if (stored != data.Length)
                {
                    Array.Resize(ref data, stored);
                }

                if (model.Encrypt && stored > 0)
                {
                    ProcessBlocks(_fileContentCipher, data, 0, stored);
                }

                fs.Write(data, 0, data.Length);
            }
        }

        private void InitCrypto()
        {
            var key = new byte[32];
            key[31] = (byte)'i';
            _headerCipher.Init(true, new KeyParameter(key));

            key = new byte[32];
            for (int i = 0; i < 31; i++) key[i] = 1;
            key[31] = (byte)'m';
            _fileHeadersCipher.Init(true, new KeyParameter(key));

            key = new byte[32];
            for (int i = 0; i < 31; i++) key[i] = 2;
            key[31] = (byte)'g';
            _fileContentCipher.Init(true, new KeyParameter(key));
        }

        private static void ProcessBlocks(IBlockCipher cipher, byte[] data, int offset, int length)
        {
            int block = cipher.GetBlockSize();
            for (int i = offset; i < offset + length; i += block)
            {
                cipher.ProcessBlock(data, i, data, i);
            }
        }

        private static byte[] StructureToBytes<T>(T obj, int size) where T : struct
        {
            var buf = new byte[size];
            var span = new Span<byte>(buf);
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(obj, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }
            return buf;
        }

        private static ConfigModel ParseConfig(string text)
        {
            var model = new ConfigModel();
            var files = new List<ConfigFileEntry>();

            using var sr = new StringReader(text);
            string? line;
            string section = string.Empty;
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("//")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line;
                    continue;
                }

                if (section == "[FILELIST]")
                {
                    if (line.StartsWith("{"))
                    {
                        var entryText = new StringBuilder();
                        entryText.AppendLine(line);
                        while (!line.Contains("}"))
                        {
                            line = sr.ReadLine();
                            if (line == null) break;
                            entryText.AppendLine(line);
                        }
                        var e = ParseFileEntry(entryText.ToString());
                        files.Add(e);
                    }
                }
                else if (section == "[IMAGE_CFG]")
                {
                    var kv = SplitKv(line);
                    if (kv == null) continue;
                    switch (kv.Value.key)
                    {
                        case "version": model.Version = ParseUInt(kv.Value.value); break;
                        case "pid": model.Pid = ParseUInt(kv.Value.value); break;
                        case "vid": model.Vid = ParseUInt(kv.Value.value); break;
                        case "hardwareid": model.HardwareId = ParseUInt(kv.Value.value); break;
                        case "firmwareid": model.FirmwareId = ParseUInt(kv.Value.value); break;
                        case "encrypt": model.Encrypt = ParseUInt(kv.Value.value) != 0; break;
                    }
                }
            }

            model.Files = files;
            return model;
        }

        private static ConfigFileEntry ParseFileEntry(string block)
        {
            string? filename = null, maintype = null, subtype = null;
            using var sr = new StringReader(block);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                var kv = SplitKv(line);
                if (kv == null) continue;
                switch (kv.Value.key)
                {
                    case "filename": filename = TrimQuotes(kv.Value.value); break;
                    case "maintype": maintype = TrimQuotes(kv.Value.value); break;
                    case "subtype": subtype = TrimQuotes(kv.Value.value); break;
                }
            }

            if (string.IsNullOrEmpty(filename)) throw new InvalidDataException("filename missing in FILELIST entry");
            return new ConfigFileEntry(filename!, maintype ?? string.Empty, subtype ?? string.Empty);
        }

        private static (string key, string value)? SplitKv(string line)
        {
            var idx = line.IndexOf('=');
            if (idx <= 0) return null;
            var key = line.Substring(0, idx).Trim();
            var value = line.Substring(idx + 1).Trim();
            return (key, value);
        }

        private static uint ParseUInt(string s)
        {
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return uint.Parse(s.AsSpan(2), NumberStyles.HexNumber);
            }
            return uint.Parse(s, CultureInfo.InvariantCulture);
        }

        private static string TrimQuotes(string s)
        {
            s = s.Trim();
            if (s.StartsWith('"') && s.EndsWith('"') && s.Length >= 2)
            {
                return s.Substring(1, s.Length - 2);
            }
            return s;
        }

        private sealed record ConfigModel
        {
            public uint Version { get; set; }
            public uint Pid { get; set; }
            public uint Vid { get; set; }
            public uint HardwareId { get; set; }
            public uint FirmwareId { get; set; }
            public bool Encrypt { get; set; }
            public List<ConfigFileEntry> Files { get; set; } = new();
        }

        private sealed record ConfigFileEntry(string Filename, string MainType, string SubType);
    }
}
