using System.Collections.ObjectModel;
using GptPartitionStruct = DiskPartitionInfo.Models.GptPartitionEntry;
using GptStruct = DiskPartitionInfo.Models.GuidPartitionTable;

namespace DiskPartitionInfo.Gpt
{
    public class GuidPartitionTable
    {
        private readonly GptStruct _gpt;
        private readonly IReadOnlyCollection<PartitionEntry> _partitions;
        private readonly bool _isPrimaryHeader;
        private readonly int _sectorSize;
        private readonly bool _isHeaderChecksumValid;
        private readonly bool _isPartitionArrayChecksumValid;

        public bool HasValidSignature()
            => new string(_gpt.Signature).Equals("EFI PART", StringComparison.Ordinal);

        public bool IsPrimaryHeader
            => _isPrimaryHeader;

        public int SectorSize
            => _sectorSize;

        public bool IsHeaderChecksumValid
            => _isHeaderChecksumValid;

        public bool IsPartitionArrayChecksumValid
            => _isPartitionArrayChecksumValid;

        public bool HasConsistentMetadata
            => HasValidSignature() && IsHeaderChecksumValid && IsPartitionArrayChecksumValid;

        public ulong PrimaryHeaderLocation
            => _gpt.PrimaryHeaderLocation;

        public ulong SecondaryHeaderLocation
            => _gpt.SecondaryHeaderLocation;

        public ulong FirstUsableLba
            => _gpt.FirstUsableLba;

        public ulong LastUsableLba
            => _gpt.LastUsableLba;

        public Guid DiskGuid
            => _gpt.DiskGuid;

        public IReadOnlyCollection<PartitionEntry> Partitions
            => _partitions;

        internal GuidPartitionTable(
            GptStruct gpt,
            IEnumerable<GptPartitionStruct> partitions,
            bool isPrimaryHeader,
            int sectorSize,
            bool isHeaderChecksumValid,
            bool isPartitionArrayChecksumValid)
        {
            _gpt = gpt;
            _isPrimaryHeader = isPrimaryHeader;
            _sectorSize = sectorSize;
            _isHeaderChecksumValid = isHeaderChecksumValid;
            _isPartitionArrayChecksumValid = isPartitionArrayChecksumValid;

            _partitions = new ReadOnlyCollection<PartitionEntry>(partitions
                .Select(p => new PartitionEntry(p))
                .ToList());
        }
    }
}
