//using Org.BouncyCastle.Crypto;
//using Org.BouncyCastle.Crypto.Engines;
//using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenixIMG
{
    public class OpenixPacker
    {
    private readonly IBlockCipher _headerCipher = new RC6Engine();
    private readonly IBlockCipher _fileHeadersCipher = new RC6Engine();

    private readonly IBlockCipher _fileContentCipher = new RC6Engine();
        private readonly bool _verbose;
        private bool _imageLoaded;
        private string _imageFilePath = "";
        private byte[] _imageData = Array.Empty<byte>();
        private ImageHeader _imageHeader;
        private FileHeader[] _fileHeaders = Array.Empty<FileHeader>();
    private bool _isEncrypted = false;
    private OutputFormat _outputFormat = OutputFormat.UNIMG;
    private byte[] _fileHeadersRaw = Array.Empty<byte>();

        public OpenixPacker(bool verbose = false)
        {
            _verbose = verbose;
            _imageLoaded = false;
            InitializeCrypto();
        }

        // Exposed read-only properties for consumers (UI/services)
        public bool IsImageLoaded => _imageLoaded;
        public bool IsEncrypted => _isEncrypted;
        public string ImageFilePath => _imageFilePath;
        public ImageHeader ImageHeader => _imageHeader;
        public FileHeader[] FileHeaders => _fileHeaders;
        public byte[] FileHeadersRaw => _fileHeadersRaw;

        private void InitializeCrypto()
        {
            var headerKey = GetKeyParameter(_headerCipher);
            if (headerKey != null) _headerCipher.Init(false, headerKey);

            var fileHeadersKey = GetKeyParameter(_fileHeadersCipher);
            if (fileHeadersKey != null) _fileHeadersCipher.Init(false, fileHeadersKey);

            var fileContentKey = GetKeyParameter(_fileContentCipher);
            if (fileContentKey != null) _fileContentCipher.Init(false, fileContentKey);
        }

        public bool LoadImage(string filePath)
        {
            _imageFilePath = filePath;
            try
            {
                _imageData = File.ReadAllBytes(_imageFilePath);
                if (_imageData.Length < 1024) return false;

                var headerBytes = new byte[1024];
                Array.Copy(_imageData, 0, headerBytes, 0, 1024);
                // 检测是否为明文头（是否包含 magic）
                var magic = Encoding.ASCII.GetString(headerBytes, 0, OpenixIMGWTY.IMAGEWTY_MAGIC_LEN);
                _isEncrypted = magic != OpenixIMGWTY.IMAGEWTY_MAGIC;

                if (_isEncrypted)
                {
                    // 加密镜像：先解密头部再解析
                    ProcessData(_headerCipher, headerBytes, 0, 1024);
                    if (_verbose) Console.WriteLine("Detected encrypted image. Decrypting header.");
                }
                else if (_verbose)
                {
                    Console.WriteLine("Detected plaintext image header.");
                }

                _imageHeader = ByteArrayToStructure<ImageHeader>(headerBytes);

                uint numFiles = _imageHeader.header_version == 0x0300 ? _imageHeader.v3.num_files : _imageHeader.v1.num_files;
                if (numFiles > 0)
                {
                    var fileHeadersBytes = new byte[numFiles * 1024];
                    Array.Copy(_imageData, 1024, fileHeadersBytes, 0, fileHeadersBytes.Length);
                    if (_isEncrypted)
                    {
                        ProcessData(_fileHeadersCipher, fileHeadersBytes, 0, fileHeadersBytes.Length);
                    }

                    _fileHeadersRaw = fileHeadersBytes;

                    _fileHeaders = new FileHeader[numFiles];
                    for (int i = 0; i < numFiles; i++)
                    {
                        var singleHeaderBytes = new byte[1024];
                        Array.Copy(fileHeadersBytes, i * 1024, singleHeaderBytes, 0, 1024);
                        _fileHeaders[i] = ByteArrayToStructure<FileHeader>(singleHeaderBytes);
                    }
                }

                _imageLoaded = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading image: {ex.Message}");
                return false;
            }
        }

        public byte[]? GetDecryptedData()
        {
            if (!_imageLoaded) return null;
            return _imageData;
        }

        public void SetOutputFormat(OutputFormat format)
        {
            _outputFormat = format;
        }

        public bool UnpackImage(string outputDir)
        {
            if (!_imageLoaded) return false;

            // 重建输出目录
            try
            {
                if (Directory.Exists(outputDir))
                {
                    Directory.Delete(outputDir, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: failed to clear output directory: {ex.Message}");
            }
            Directory.CreateDirectory(outputDir);

            uint numFiles = _imageHeader.header_version == 0x0300 ? _imageHeader.v3.num_files : _imageHeader.v1.num_files;

            for (int i = 0; i < numFiles; i++)
            {
                try
                {
                    var fileHeader = _fileHeaders[i];
                    string? filename;
                    uint originalLength, offset, storedLength;

                    if (_imageHeader.header_version == 0x0300)
                    {
                        filename = fileHeader.v3.filename;
                        originalLength = fileHeader.v3.original_length;
                        offset = fileHeader.v3.offset;
                        storedLength = fileHeader.v3.stored_length;
                    }
                    else
                    {
                        filename = fileHeader.v1.filename;
                        originalLength = fileHeader.v1.original_length;
                        offset = fileHeader.v1.offset;
                        storedLength = fileHeader.v1.stored_length;
                    }

                    if (string.IsNullOrEmpty(filename)) continue;

                    string maintype = Encoding.ASCII.GetString(fileHeader.maintype).TrimEnd('\0', ' ');
                    string subtype = Encoding.ASCII.GetString(fileHeader.subtype).TrimEnd('\0', ' ');

                    if (_outputFormat == OutputFormat.UNIMG)
                    {
                        // UNIMG：输出头文件和内容文件到根目录
                        var hdrName = $"{maintype}_{subtype}.hdr";
                        var contName = $"{maintype}_{subtype}";

                        var hdrPath = Path.Combine(outputDir, hdrName);
                        var contPath = Path.Combine(outputDir, contName);

                        // 写 header：使用解密后的原始 1024 字节
                        if (_fileHeadersRaw != null && _fileHeadersRaw.Length >= (i + 1) * 1024)
                        {
                            var oneHdr = new byte[1024];
                            Array.Copy(_fileHeadersRaw, i * 1024, oneHdr, 0, 1024);
                            File.WriteAllBytes(hdrPath, oneHdr);
                        }
                        else
                        {
                            // 兜底：从结构体再序列化
                            var oneHdr = StructureToByteArray(fileHeader, 1024);
                            File.WriteAllBytes(hdrPath, oneHdr);
                        }

                        // 写内容
                        var fileData = ExtractFileContent(offset, storedLength, originalLength);
                        File.WriteAllBytes(contPath, fileData);

                        if (_verbose)
                        {
                            Console.WriteLine($"[{i:D3}] {maintype}/{subtype} -> {contName} ({originalLength}, {storedLength})");
                        }
                    }
                    else // IMGREPACKER
                    {
                        var outRel = filename.TrimStart('/', '\\');
                        var outPath = Path.Combine(outputDir, outRel);

                        var parent = Path.GetDirectoryName(outPath);
                        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

                        var fileData = ExtractFileContent(offset, storedLength, originalLength);
                        File.WriteAllBytes(outPath, fileData);

                        if (_verbose)
                        {
                            Console.WriteLine($"[{i:D3}] extract {outRel}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error unpacking file {i}: {ex.Message}");
                }
            }

            // 生成 image.cfg
            try
            {
                GenerateImageCfg(outputDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: failed to generate image.cfg: {ex.Message}");
            }

            return true;
        }

        public byte[]? GetFileDataByFilename(string filename)
        {
            if (!_imageLoaded) return null;

            uint numFiles = _imageHeader.header_version == 0x0300 ? _imageHeader.v3.num_files : _imageHeader.v1.num_files;
            for (int i = 0; i < numFiles; i++)
            {
                var fileHeader = _fileHeaders[i];
                string? currentFilename = (_imageHeader.header_version == 0x0300) ? fileHeader.v3.filename : fileHeader.v1.filename;

                if (currentFilename != null && currentFilename.TrimEnd('\0') == filename)
                {
                    uint originalLength = (_imageHeader.header_version == 0x0300) ? fileHeader.v3.original_length : fileHeader.v1.original_length;
                    uint offset = (_imageHeader.header_version == 0x0300) ? fileHeader.v3.offset : fileHeader.v1.offset;
                    uint storedLength = (_imageHeader.header_version == 0x0300) ? fileHeader.v3.stored_length : fileHeader.v1.stored_length;

                    var outData = new byte[originalLength];
                    if (_isEncrypted)
                    {
                        var encryptedData = new byte[storedLength];
                        Array.Copy(_imageData, (int)offset, encryptedData, 0, (int)storedLength);
                        ProcessData(_fileContentCipher, encryptedData, 0, (int)storedLength);
                        Array.Copy(encryptedData, 0, outData, 0, (int)originalLength);
                    }
                    else
                    {
                        Array.Copy(_imageData, (int)offset, outData, 0, (int)originalLength);
                    }
                    return outData;
                }
            }
            return null;
        }

        // 参考 C++ decryptImage 实现完整镜像解密输出
        public bool DecryptImage(string outputFile)
        {
            try
            {
                if (!_imageLoaded)
                {
                    Console.WriteLine("Error: no image file loaded!");
                    return false;
                }

                // 复制一份数据用于就地解密
                var decryptedImageData = new byte[_imageData.Length];
                Array.Copy(_imageData, decryptedImageData, _imageData.Length);

                if (_isEncrypted)
                {
                    // 处理 Header：先用原始加密头，再对副本进行解密
                    ProcessData(_headerCipher, decryptedImageData, 0, 1024);

                    // 从解密后的头获取文件数
                    var headerBytes = new byte[1024];
                    Array.Copy(decryptedImageData, 0, headerBytes, 0, 1024);
                    var decHeader = ByteArrayToStructure<ImageHeader>(headerBytes);
                    uint numFiles = decHeader.header_version == 0x0300 ? decHeader.v3.num_files : decHeader.v1.num_files;

                    // 处理文件头区
                    int fhdrOffset = 1024;
                    int fhdrLength = checked((int)(numFiles * 1024));
                    if (fhdrLength > 0)
                    {
                        ProcessData(_fileHeadersCipher, decryptedImageData, fhdrOffset, fhdrLength);
                    }

                    // 从已解密的文件头读取每个条目，按 offset 定位解密内容区
                    for (int i = 0; i < numFiles; i++)
                    {
                        var oneHdr = new byte[1024];
                        Array.Copy(decryptedImageData, fhdrOffset + i * 1024, oneHdr, 0, 1024);
                        var fileHdr = ByteArrayToStructure<FileHeader>(oneHdr);

                        uint storedLength = decHeader.header_version == 0x0300 ? fileHdr.v3.stored_length : fileHdr.v1.stored_length;
                        uint offset = decHeader.header_version == 0x0300 ? fileHdr.v3.offset : fileHdr.v1.offset;

                        if (storedLength > 0)
                        {
                            ProcessData(_fileContentCipher, decryptedImageData, (int)offset, (int)storedLength);
                        }
                    }
                }

                // 写出解密镜像
                File.WriteAllBytes(outputFile, decryptedImageData);
                if (_verbose) Console.WriteLine($"Successfully decrypted image to {outputFile}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decrypting image: {ex.Message}");
                return false;
            }
        }

        private void ProcessData(IBlockCipher cipher, byte[] data, int offset, int length)
        {
            int blockSize = cipher.GetBlockSize();
            for (int i = offset; i < offset + length; i += blockSize)
            {
                cipher.ProcessBlock(data, i, data, i);
            }
        }

        private byte[] ExtractFileContent(uint offset, uint storedLength, uint originalLength)
        {
            var fileData = new byte[originalLength];
            if (_isEncrypted)
            {
                var encryptedData = new byte[storedLength];
                Array.Copy(_imageData, (int)offset, encryptedData, 0, (int)storedLength);
                ProcessData(_fileContentCipher, encryptedData, 0, (int)storedLength);
                Array.Copy(encryptedData, 0, fileData, 0, (int)originalLength);
            }
            else
            {
                Array.Copy(_imageData, (int)offset, fileData, 0, (int)originalLength);
            }
            return fileData;
        }

        private void GenerateImageCfg(string outputDir)
        {
            uint numFiles = _imageHeader.header_version == 0x0300 ? _imageHeader.v3.num_files : _imageHeader.v1.num_files;
            uint pid = _imageHeader.header_version == 0x0300 ? _imageHeader.v3.pid : _imageHeader.v1.pid;
            uint vid = _imageHeader.header_version == 0x0300 ? _imageHeader.v3.vid : _imageHeader.v1.vid;
            uint hardwareId = _imageHeader.header_version == 0x0300 ? _imageHeader.v3.hardware_id : _imageHeader.v1.hardware_id;
            uint firmwareId = _imageHeader.header_version == 0x0300 ? _imageHeader.v3.firmware_id : _imageHeader.v1.firmware_id;

            var sb = new StringBuilder();

            // Header comments
            sb.AppendLine(";/**************************************************************************/");
            sb.AppendLine($"; {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("; generated by OpenixIMG");
            sb.AppendLine($"; {_imageFilePath}");
            sb.AppendLine(";/**************************************************************************/");
            sb.AppendLine();

            // DIR_DEF
            sb.AppendLine("[DIR_DEF]");
            sb.AppendLine("INPUT_DIR = \"../\"");
            sb.AppendLine();

            // FILELIST
            sb.AppendLine("[FILELIST]");
            for (int i = 0; i < numFiles; i++)
            {
                var fh = _fileHeaders[i];
                string? filename = _imageHeader.header_version == 0x0300 ? fh.v3.filename : fh.v1.filename;
                string maintype = Encoding.ASCII.GetString(fh.maintype).TrimEnd('\0', ' ');
                string subtype = Encoding.ASCII.GetString(fh.subtype).TrimEnd('\0', ' ');

                string contFilename;
                if (_outputFormat == OutputFormat.UNIMG)
                {
                    contFilename = $"{maintype}_{subtype}";
                }
                else
                {
                    contFilename = (filename ?? string.Empty).TrimStart('/', '\\');
                }

                sb.Append("{ ");
                sb.Append($"filename = \"{contFilename}\", ");
                sb.Append($"maintype = \"{maintype}\", ");
                sb.Append($"subtype = \"{subtype}\", ");
                sb.AppendLine("},");
            }
            sb.AppendLine();

            // IMAGE_CFG
            sb.AppendLine("[IMAGE_CFG]");
            sb.AppendLine($"version = 0x{_imageHeader.version:x}");
            sb.AppendLine($"pid = 0x{pid:x}");
            sb.AppendLine($"vid = 0x{vid:x}");
            sb.AppendLine($"hardwareid = 0x{hardwareId:x}");
            sb.AppendLine($"firmwareid = 0x{firmwareId:x}");
            sb.AppendLine($"imagename = {_imageFilePath}");
            sb.AppendLine("filelist = FILELIST");
            sb.AppendLine($"encrypt = {(_isEncrypted ? "1" : "0")}");
            sb.AppendLine();

            var cfgPath = Path.Combine(outputDir, "image.cfg");
            File.WriteAllText(cfgPath, sb.ToString(), Encoding.ASCII);
        }

        private static byte[] StructureToByteArray<T>(T obj, int size) where T : struct
        {
            var buffer = new byte[size];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(obj, handle.AddrOfPinnedObject(), false);
            }
            finally
            {
                handle.Free();
            }
            return buffer;
        }

        private KeyParameter? GetKeyParameter(IBlockCipher cipher)
        {
            if (cipher == _headerCipher)
            {
                var key = new byte[32];
                key[31] = (byte)'i';
                return new KeyParameter(key);
            }
            if (cipher == _fileHeadersCipher)
            {
                var key = new byte[32];
                for (int i = 0; i < 31; i++) key[i] = 1;
                key[31] = (byte)'m';
                return new KeyParameter(key);
            }
            if (cipher == _fileContentCipher)
            {
                // 与 C++ RC6 文件内容 key 对齐：末字节为 'g'
                var key = new byte[32];
                for (int i = 0; i < 31; i++) key[i] = 2;
                key[31] = (byte)'g';
                return new KeyParameter(key);
            }
            return null;
        }

        private static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
