using DiskPartitionInfo.Extensions;
using DiskPartitionInfo.Gpt;
using GptPartitionStruct = DiskPartitionInfo.Models.GptPartitionEntry;
using GptStruct = DiskPartitionInfo.Models.GuidPartitionTable;

namespace DiskPartitionInfo.FluentApi
{
    internal partial class GptReader : IGptReader, IGptReaderLocation
    {
        private static readonly int[] SectorSizeCandidates = new[] { 512, 4096 };

        private readonly record struct HeaderReadResult(
            GptStruct Header,
            int SectorSize,
            bool IsPrimary,
            bool HeaderChecksumValid);

        private bool _usePrimary = true;

        /// <inheritdoc/>
        public IGptReader Primary()
        {
            _usePrimary = true;
            return this;
        }

        /// <inheritdoc/>
        public IGptReader Secondary()
        {
            _usePrimary = false;
            return this;
        }

        /// <inheritdoc/>
        public GuidPartitionTable FromPath(string path)
        {
            using var stream = File.OpenRead(path);

            return FromStream(stream);
        }

        /// <inheritdoc/>
        public GuidPartitionTable FromStream(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (!stream.CanSeek)
                throw new NotSupportedException("GPT reader requires a seekable stream.");

            var headerResult = ReadBestHeader(stream, _usePrimary);
            var partitions = ReadPartitions(stream, headerResult.Header, headerResult.SectorSize, out var partitionArrayChecksumValid);

            return new GuidPartitionTable(
                headerResult.Header,
                partitions,
                headerResult.IsPrimary,
                headerResult.SectorSize,
                headerResult.HeaderChecksumValid,
                partitionArrayChecksumValid);
        }

        private static HeaderReadResult ReadBestHeader(Stream stream, bool preferPrimary)
        {
            HeaderReadResult? fallback = null;
            foreach (var isPrimary in preferPrimary ? new[] { true, false } : new[] { false, true })
            {
                foreach (var sectorSize in SectorSizeCandidates)
                {
                    if (!TryReadHeader(stream, isPrimary, sectorSize, out var candidate))
                        continue;

                    if (candidate.HeaderChecksumValid)
                        return candidate;

                    fallback ??= candidate;
                }
            }

            if (fallback.HasValue)
                return fallback.Value;

            throw new InvalidDataException("Unable to locate a GPT header with a valid signature.");
        }

        private static bool TryReadHeader(Stream stream, bool usePrimary, int sectorSize, out HeaderReadResult result)
        {
            var originalPosition = stream.Position;
            var headerBuffer = new byte[sectorSize];

            try
            {
                var targetPosition = usePrimary
                    ? sectorSize
                    : checked(stream.Length - sectorSize);

                if (targetPosition < 0 || targetPosition > stream.Length)
                {
                    result = default;
                    return false;
                }

                stream.Seek(targetPosition, SeekOrigin.Begin);
                stream.ReadExactly(headerBuffer, 0, sectorSize);

                var header = headerBuffer.ToStruct<GptStruct>();

                if (!IsSignatureValid(header.Signature))
                {
                    result = default;
                    return false;
                }

                var checksumValid = ValidateHeaderChecksum(headerBuffer, header.HeaderSize, header.HeaderCrc32);

                result = new HeaderReadResult(header, sectorSize, usePrimary, checksumValid);
                return true;
            }
            catch (EndOfStreamException)
            {
                result = default;
                return false;
            }
            catch (IOException)
            {
                result = default;
                return false;
            }
            finally
            {
                stream.Seek(originalPosition, SeekOrigin.Begin);
            }
        }

        private static bool IsSignatureValid(char[] signature)
            => new string(signature).Equals("EFI PART", StringComparison.Ordinal);

        private static bool ValidateHeaderChecksum(byte[] headerBytes, uint headerSize, uint expectedCrc)
        {
            if (headerSize == 0 || headerSize > headerBytes.Length)
                return false;

            var span = headerBytes.AsSpan(0, (int)headerSize);
            Span<byte> buffer = stackalloc byte[(int)headerSize];
            span.CopyTo(buffer);
            buffer.Slice(16, 4).Clear();

            var computed = ComputeCrc32(buffer);
            return computed == expectedCrc;
        }

        private static List<GptPartitionStruct> ReadPartitions(Stream stream, GptStruct gpt, int sectorSize, out bool checksumValid)
        {
            if (gpt.PartitionsCount == 0 || gpt.PartitionEntryLength == 0)
            {
                checksumValid = gpt.PartitionsArrayCrc32 == 0;
                return new List<GptPartitionStruct>();
            }

            var entrySize = checked((int)gpt.PartitionEntryLength);
            var partitionCount = checked((int)gpt.PartitionsCount);
            var totalLength = checked(entrySize * partitionCount);

            var tableBytes = new byte[totalLength];

            stream.Seek((long)gpt.PartitionsArrayLba * sectorSize, SeekOrigin.Begin);
            stream.ReadExactly(tableBytes, 0, totalLength);

            checksumValid = ComputeCrc32(tableBytes) == gpt.PartitionsArrayCrc32;

            var partitions = new List<GptPartitionStruct>(partitionCount);
            var entryBuffer = new byte[entrySize];

            for (var offset = 0; offset < totalLength; offset += entrySize)
            {
                Buffer.BlockCopy(tableBytes, offset, entryBuffer, 0, entrySize);
                partitions.Add(entryBuffer.ToStruct<GptPartitionStruct>());
            }

            return partitions;
        }

        private static uint ComputeCrc32(ReadOnlySpan<byte> data)
        {
            const uint seed = 0xFFFF_FFFF;
            var crc = seed;

            foreach (var b in data)
            {
                var index = (byte)((crc ^ b) & 0xFF);
                crc = (crc >> 8) ^ Crc32Table[index];
            }

            return crc ^ seed;
        }

        private static readonly uint[] Crc32Table = CreateCrc32Table();

        private static uint[] CreateCrc32Table()
        {
            var table = new uint[256];
            const uint polynomial = 0xEDB88320;

            for (uint i = 0; i < table.Length; ++i)
            {
                var entry = i;
                for (var bit = 0; bit < 8; ++bit)
                {
                    if ((entry & 1) != 0)
                        entry = (entry >> 1) ^ polynomial;
                    else
                        entry >>= 1;
                }

                table[i] = entry;
            }

            return table;
        }
    }
}
