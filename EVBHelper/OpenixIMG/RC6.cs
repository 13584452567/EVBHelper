//using Org.BouncyCastle.Crypto;
//using Org.BouncyCastle.Crypto.Parameters;

namespace OpenixIMG
{
    public sealed class RC6Engine : IBlockCipher
    {
        private const int WordSize = 32;
        private const int BlockSizeInBytes = 16;
        private const int Rounds = 20;
        private const int KeyScheduleSize = 2 * Rounds + 4;

        private static readonly uint P32 = 0xB7E15163;
        private static readonly uint Q32 = 0x9E3779B9;
        private static readonly int LgW = 5;

        private uint[] _roundKeys = new uint[KeyScheduleSize];
        private bool _forEncryption;

        public string AlgorithmName => "RC6";
        public bool IsPartialBlockOkay => false;

        public int GetBlockSize() => BlockSizeInBytes;

        public void Init(bool forEncryption, ICipherParameters parameters)
        {
            if (!(parameters is KeyParameter keyParams))
                throw new ArgumentException("Invalid parameters passed to RC6 init - KeyParameter required", nameof(parameters));

            _forEncryption = forEncryption;
            SetKey(keyParams.GetKey());
        }

        public int ProcessBlock(byte[] inBuf, int inOff, byte[] outBuf, int outOff)
        {
            if (_roundKeys == null)
                throw new InvalidOperationException("RC6 engine not initialized.");
            if ((inOff + BlockSizeInBytes) > inBuf.Length)
                throw new ArgumentException("Input buffer too short.");
            if ((outOff + BlockSizeInBytes) > outBuf.Length)
                throw new ArgumentException("Output buffer too short.");

            if (_forEncryption)
            {
                EncryptBlock(inBuf, inOff, outBuf, outOff);
            }
            else
            {
                DecryptBlock(inBuf, inOff, outBuf, outOff);
            }

            return BlockSizeInBytes;
        }

        public int ProcessBlock(ReadOnlySpan<byte> input, Span<byte> output)
        {
            if (_roundKeys == null)
                throw new InvalidOperationException("RC6 engine not initialized.");
            if (input.Length < BlockSizeInBytes)
                throw new ArgumentException("Input buffer too short.");
            if (output.Length < BlockSizeInBytes)
                throw new ArgumentException("Output buffer too short.");

            if (_forEncryption)
            {
                EncryptBlock(input, output);
            }
            else
            {
                DecryptBlock(input, output);
            }

            return BlockSizeInBytes;
        }

        public void Reset()
        {
            // No state to reset besides the key which is done in Init
        }

        private void SetKey(byte[] key)
        {
            int c = (key.Length + 3) / 4;
            if (c == 0) c = 1;
            uint[] L = new uint[c];

            for (int i = 0; i < key.Length; i++)
            {
                L[i / 4] |= (uint)key[i] << (8 * (i % 4));
            }

            _roundKeys[0] = P32;
            for (int i = 1; i < KeyScheduleSize; i++)
            {
                _roundKeys[i] = _roundKeys[i - 1] + Q32;
            }

            uint a = 0, b = 0;
            int i_ = 0, j = 0;
            int v = 3 * Math.Max(c, KeyScheduleSize);

            for (int s = 1; s <= v; s++)
            {
                a = _roundKeys[i_] = Rotl(_roundKeys[i_] + a + b, 3);
                b = L[j] = Rotl(L[j] + a + b, (int)(a + b));
                i_ = (i_ + 1) % KeyScheduleSize;
                j = (j + 1) % c;
            }
        }

        private void EncryptBlock(byte[] inBuf, int inOff, byte[] outBuf, int outOff)
        {
            uint a = ToUInt32(inBuf, inOff);
            uint b = ToUInt32(inBuf, inOff + 4);
            uint c = ToUInt32(inBuf, inOff + 8);
            uint d = ToUInt32(inBuf, inOff + 12);

            b += _roundKeys[0];
            d += _roundKeys[1];

            for (int i = 1; i <= Rounds; i++)
            {
                uint t = Rotl(b * (2 * b + 1), LgW);
                uint u = Rotl(d * (2 * d + 1), LgW);
                a = Rotl(a ^ t, (int)u) + _roundKeys[2 * i];
                c = Rotl(c ^ u, (int)t) + _roundKeys[2 * i + 1];

                uint temp = a;
                a = b;
                b = c;
                c = d;
                d = temp;
            }

            a += _roundKeys[2 * Rounds + 2];
            c += _roundKeys[2 * Rounds + 3];

            ToBytes(a, outBuf, outOff);
            ToBytes(b, outBuf, outOff + 4);
            ToBytes(c, outBuf, outOff + 8);
            ToBytes(d, outBuf, outOff + 12);
        }

        private void EncryptBlock(ReadOnlySpan<byte> input, Span<byte> output)
        {
            uint a = ToUInt32(input);
            uint b = ToUInt32(input.Slice(4));
            uint c = ToUInt32(input.Slice(8));
            uint d = ToUInt32(input.Slice(12));

            b += _roundKeys[0];
            d += _roundKeys[1];

            for (int i = 1; i <= Rounds; i++)
            {
                uint t = Rotl(b * (2 * b + 1), LgW);
                uint u = Rotl(d * (2 * d + 1), LgW);
                a = Rotl(a ^ t, (int)u) + _roundKeys[2 * i];
                c = Rotl(c ^ u, (int)t) + _roundKeys[2 * i + 1];

                uint temp = a;
                a = b;
                b = c;
                c = d;
                d = temp;
            }

            a += _roundKeys[2 * Rounds + 2];
            c += _roundKeys[2 * Rounds + 3];

            ToBytes(a, output);
            ToBytes(b, output.Slice(4));
            ToBytes(c, output.Slice(8));
            ToBytes(d, output.Slice(12));
        }

        private void DecryptBlock(byte[] inBuf, int inOff, byte[] outBuf, int outOff)
        {
            uint a = ToUInt32(inBuf, inOff);
            uint b = ToUInt32(inBuf, inOff + 4);
            uint c = ToUInt32(inBuf, inOff + 8);
            uint d = ToUInt32(inBuf, inOff + 12);

            c -= _roundKeys[2 * Rounds + 3];
            a -= _roundKeys[2 * Rounds + 2];

            for (int i = Rounds; i >= 1; i--)
            {
                uint temp = d;
                d = c;
                c = b;
                b = a;
                a = temp;

                uint u = Rotl(d * (2 * d + 1), LgW);
                uint t = Rotl(b * (2 * b + 1), LgW);
                c = Rotr(c - _roundKeys[2 * i + 1], (int)t) ^ u;
                a = Rotr(a - _roundKeys[2 * i], (int)u) ^ t;
            }

            d -= _roundKeys[1];
            b -= _roundKeys[0];

            ToBytes(a, outBuf, outOff);
            ToBytes(b, outBuf, outOff + 4);
            ToBytes(c, outBuf, outOff + 8);
            ToBytes(d, outBuf, outOff + 12);
        }

        private void DecryptBlock(ReadOnlySpan<byte> input, Span<byte> output)
        {
            uint a = ToUInt32(input);
            uint b = ToUInt32(input.Slice(4));
            uint c = ToUInt32(input.Slice(8));
            uint d = ToUInt32(input.Slice(12));

            c -= _roundKeys[2 * Rounds + 3];
            a -= _roundKeys[2 * Rounds + 2];

            for (int i = Rounds; i >= 1; i--)
            {
                uint temp = d;
                d = c;
                c = b;
                b = a;
                a = temp;

                uint u = Rotl(d * (2 * d + 1), LgW);
                uint t = Rotl(b * (2 * b + 1), LgW);
                c = Rotr(c - _roundKeys[2 * i + 1], (int)t) ^ u;
                a = Rotr(a - _roundKeys[2 * i], (int)u) ^ t;
            }

            d -= _roundKeys[1];
            b -= _roundKeys[0];

            ToBytes(a, output);
            ToBytes(b, output.Slice(4));
            ToBytes(c, output.Slice(8));
            ToBytes(d, output.Slice(12));
        }

        private static uint Rotl(uint x, int n) => (x << n) | (x >> (WordSize - n));
        private static uint Rotr(uint x, int n) => (x >> n) | (x << (WordSize - n));

        private static uint ToUInt32(byte[] buf, int off)
        {
            return (uint)buf[off] | (uint)buf[off + 1] << 8 | (uint)buf[off + 2] << 16 | (uint)buf[off + 3] << 24;
        }

        private static uint ToUInt32(ReadOnlySpan<byte> buf)
        {
            return (uint)buf[0] | (uint)buf[1] << 8 | (uint)buf[2] << 16 | (uint)buf[3] << 24;
        }

        private static void ToBytes(uint val, byte[] buf, int off)
        {
            buf[off] = (byte)val;
            buf[off + 1] = (byte)(val >> 8);
            buf[off + 2] = (byte)(val >> 16);
            buf[off + 3] = (byte)(val >> 24);
        }

        private static void ToBytes(uint val, Span<byte> buf)
        {
            buf[0] = (byte)val;
            buf[1] = (byte)(val >> 8);
            buf[2] = (byte)(val >> 16);
            buf[3] = (byte)(val >> 24);
        }
    }
}
