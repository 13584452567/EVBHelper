using System.Runtime.InteropServices;

namespace LibEfex.protocols;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct EfexRequest
{
    public EfexCmd Cmd;
    public uint Addr;
    public uint Len;
    public uint DataType;
    public ushort TransType;
    public ushort Reserved;
    public uint NextCmd;
    public uint Reserved2;
    public uint Magic;
    public uint Checksum;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct EfexResponse
{
    public uint Magic;
    public uint Status;
    public uint Checksum;
}
