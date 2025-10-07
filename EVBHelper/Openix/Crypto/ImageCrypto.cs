namespace Openix.Crypto;

internal sealed class ImageCrypto : IDisposable
{
    private readonly Rc6Cipher _headerCipher;
    private readonly Rc6Cipher _fileHeaderCipher;
    private readonly Rc6Cipher _fileContentCipher;
    private readonly bool _encryptionEnabled;
    private bool _disposed;

    private ImageCrypto(bool encryptionEnabled, Rc6Cipher headerCipher, Rc6Cipher headerTableCipher, Rc6Cipher contentCipher)
    {
        _encryptionEnabled = encryptionEnabled;
        _headerCipher = headerCipher;
        _fileHeaderCipher = headerTableCipher;
        _fileContentCipher = contentCipher;
    }

    public static ImageCrypto Create(bool encryptionEnabled)
    {
        var headerKey = CreateKey(0, (byte)'i');
        var headerCipher = Rc6Cipher.Create(headerKey);

        var fileHeaderKey = CreateKey(1, (byte)'m');
        var fileHeaderCipher = Rc6Cipher.Create(fileHeaderKey);

        var fileContentKey = CreateKey(2, (byte)'g');
        var contentCipher = Rc6Cipher.Create(fileContentKey);

        return new ImageCrypto(encryptionEnabled, headerCipher, fileHeaderCipher, contentCipher);
    }

    public void DecryptHeader(Span<byte> buffer)
        => DecryptInPlace(_headerCipher, buffer);

    public void DecryptHeaderTable(Span<byte> buffer)
        => DecryptInPlace(_fileHeaderCipher, buffer);

    public void DecryptContent(Span<byte> buffer)
        => DecryptInPlace(_fileContentCipher, buffer);

    private void DecryptInPlace(Rc6Cipher cipher, Span<byte> buffer)
    {
        if (!_encryptionEnabled)
        {
            return;
        }

        Span<byte> temp = stackalloc byte[16];
        var blockCount = buffer.Length / 16;
        for (var block = 0; block < blockCount; block++)
        {
            var slice = buffer.Slice(block * 16, 16);
            cipher.DecryptBlock(slice, temp);
            temp.CopyTo(slice);
        }
    }

    private static byte[] CreateKey(byte fill, byte last)
    {
        var key = new byte[32];
        for (var i = 0; i < key.Length; i++)
        {
            key[i] = fill;
        }
        key[^1] = last;
        return key;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _headerCipher.Dispose();
        _fileHeaderCipher.Dispose();
        _fileContentCipher.Dispose();
        _disposed = true;
    }
}
