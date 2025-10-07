using System;

namespace EVBHelper.Models.Gpt;

internal static class Crc32Helper
{
    private static readonly uint[] Table = CreateTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        const uint seed = 0xFFFF_FFFF;
        var crc = seed;

        foreach (var b in data)
        {
            var index = (byte)((crc ^ b) & 0xFF);
            crc = (crc >> 8) ^ Table[index];
        }

        return crc ^ seed;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        const uint polynomial = 0xEDB88320;

        for (uint i = 0; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                if ((value & 1) != 0)
                {
                    value = (value >> 1) ^ polynomial;
                }
                else
                {
                    value >>= 1;
                }
            }

            table[i] = value;
        }

        return table;
    }
}
