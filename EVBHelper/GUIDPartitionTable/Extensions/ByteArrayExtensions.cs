using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DiskPartitionInfo.Extensions
{
    internal static class ByteArrayExtensions
    {
        internal static T ToStruct<T>(this byte[] bytes)
            where T : struct
        {
            if (bytes is null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var expectedLength = Unsafe.SizeOf<T>();
            if (bytes.Length < expectedLength)
            {
                throw new ArgumentException($"Byte buffer too small for {typeof(T)}. Expected at least {expectedLength} bytes but received {bytes.Length}.", nameof(bytes));
            }

            return MemoryMarshal.Read<T>(bytes.AsSpan());
        }
    }
}
