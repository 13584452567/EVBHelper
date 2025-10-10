using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace OpenixCard.Crypto;

internal sealed class Rc6Cipher : IDisposable
{
    private const int BlockSize = 16;

    private readonly RC6Engine _engine = new();
    private readonly KeyParameter _keyParameter;
    private readonly byte[] _inputBuffer = new byte[BlockSize];
    private readonly byte[] _outputBuffer = new byte[BlockSize];
    private bool _disposed;

    private Rc6Cipher(byte[] key)
    {
        _keyParameter = new KeyParameter(key);
    }

    public static Rc6Cipher Create(ReadOnlySpan<byte> key)
    {
        if (key.IsEmpty)
        {
            throw new ArgumentException("Key must not be empty", nameof(key));
        }

        return new Rc6Cipher(key.ToArray());
    }

    public void EncryptBlock(ReadOnlySpan<byte> input, Span<byte> output)
        => ProcessBlock(input, output, true);

    public void DecryptBlock(ReadOnlySpan<byte> input, Span<byte> output)
        => ProcessBlock(input, output, false);

    private void ProcessBlock(ReadOnlySpan<byte> input, Span<byte> output, bool forEncryption)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Rc6Cipher));
        }
        if (input.Length < BlockSize)
        {
            throw new ArgumentException($"RC6 requires {BlockSize}-byte blocks.", nameof(input));
        }
        if (output.Length < BlockSize)
        {
            throw new ArgumentException($"RC6 requires {BlockSize}-byte blocks.", nameof(output));
        }

        input[..BlockSize].CopyTo(_inputBuffer);
        _engine.Init(forEncryption, _keyParameter);
        _engine.ProcessBlock(_inputBuffer, 0, _outputBuffer, 0);
        _outputBuffer.CopyTo(output);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
