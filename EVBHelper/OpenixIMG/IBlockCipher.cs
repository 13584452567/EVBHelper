using System;

namespace OpenixIMG
{
    public interface IBlockCipher
    {
        string AlgorithmName { get; }
        bool IsPartialBlockOkay { get; }
        int GetBlockSize();
        void Init(bool forEncryption, ICipherParameters parameters);
        int ProcessBlock(byte[] input, int inOff, byte[] output, int outOff);
        int ProcessBlock(ReadOnlySpan<byte> input, Span<byte> output);
        void Reset();
    }

    public interface ICipherParameters
    {
    }

    public class KeyParameter : ICipherParameters
    {
        private readonly byte[] key;

        public KeyParameter(byte[] key)
        {
            this.key = (byte[])key.Clone();
        }

        public byte[] GetKey()
        {
            return (byte[])key.Clone();
        }
    }
}
