using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace OpenixCard.Crypto;

internal static class TwofishCipher
{
    private const int BlockSize = 16;

    public static void EncryptBlock(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key)
        => ProcessBlock(input, output, key, true);

    public static void DecryptBlock(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key)
        => ProcessBlock(input, output, key, false);

    private static void ProcessBlock(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key, bool forEncryption)
    {
        if (input.Length < BlockSize)
        {
            throw new ArgumentException("Twofish works on 16-byte blocks.", nameof(input));
        }
        if (output.Length < BlockSize)
        {
            throw new ArgumentException("Twofish works on 16-byte blocks.", nameof(output));
        }
        if (key.Length is not 16 and not 24 and not 32)
        {
            throw new ArgumentException("Twofish keys must be 128, 192, or 256 bits long.", nameof(key));
        }

        var engine = new TwofishEngine();
        engine.Init(forEncryption, new KeyParameter(key.ToArray()));

        var inBuffer = input[..BlockSize].ToArray();
        var outBuffer = new byte[BlockSize];
        engine.ProcessBlock(inBuffer, 0, outBuffer, 0);
        outBuffer.CopyTo(output);
    }
}
