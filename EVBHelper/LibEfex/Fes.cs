using LibEfex.common;
using LibEfex.core;
using LibEfex.io;
using LibEfex.protocols;
using System.Runtime.InteropServices;

namespace LibEfex;

public static class Fes
{
    public static uint QueryStorage(EfexContext ctx)
    {
        Efex.SendRequest(ctx, EfexCmd.FesQueryStorage, 0, 0);
        return (uint)Efex.ReadStatus(ctx);
    }

    public static uint QuerySecure(EfexContext ctx)
    {
        Efex.SendRequest(ctx, EfexCmd.FesQuerySecure, 0, 0);
        return (uint)Efex.ReadStatus(ctx);
    }

    public static uint ProbeFlashSize(EfexContext ctx)
    {
        Efex.SendRequest(ctx, EfexCmd.FesFlashSizeProbe, 0, 0);
        return (uint)Efex.ReadStatus(ctx);
    }

    public static void FlashSetOnoff(EfexContext ctx, uint storageType, bool onOff)
    {
        var cmd = onOff ? EfexCmd.FesFlashSetOn : EfexCmd.FesFlashSetOff;
        Efex.SendRequest(ctx, cmd, storageType, 0);
        Efex.ReadStatus(ctx);
    }

    public static byte[] GetChipId(EfexContext ctx)
    {
        var buffer = new byte[16];
        Efex.SendRequest(ctx, EfexCmd.FesGetChipid, 0, 0);
        EfexUsb.UsbBulkRecv(ctx, 0x81, buffer, buffer.Length);
        Efex.ReadStatus(ctx);
        return buffer;
    }

    public static void Down(EfexContext ctx, byte[] buf, int len, uint addr, FesDataType type)
    {
        var request = new EfexRequest
        {
            Cmd = EfexCmd.FesDown,
            Addr = addr,
            Len = (uint)len,
            DataType = (uint)type,
            Magic = 0x43555741 // "AWUC"
        };

        var buffer = new byte[Marshal.SizeOf(request)];
        var ptr = Marshal.AllocHGlobal(buffer.Length);
        Marshal.StructureToPtr(request, ptr, false);
        Marshal.Copy(ptr, buffer, 0, buffer.Length);
        Marshal.FreeHGlobal(ptr);

        EfexUsb.SendUsbRequest(ctx, 0x12, buffer.Length);
        EfexUsb.UsbBulkSend(ctx, 0x02, buffer, buffer.Length);
        EfexUsb.UsbBulkSend(ctx, 0x02, buf, len);
        Efex.ReadStatus(ctx);
    }

    public static byte[] Up(EfexContext ctx, int len, uint addr, FesDataType type)
    {
        var buffer = new byte[len];
        var request = new EfexRequest
        {
            Cmd = EfexCmd.FesUp,
            Addr = addr,
            Len = (uint)len,
            DataType = (uint)type,
            Magic = 0x43555741 // "AWUC"
        };

        var reqBuffer = new byte[Marshal.SizeOf(request)];
        var ptr = Marshal.AllocHGlobal(reqBuffer.Length);
        Marshal.StructureToPtr(request, ptr, false);
        Marshal.Copy(ptr, reqBuffer, 0, reqBuffer.Length);
        Marshal.FreeHGlobal(ptr);

        EfexUsb.SendUsbRequest(ctx, 0x12, reqBuffer.Length);
        EfexUsb.UsbBulkSend(ctx, 0x02, reqBuffer, reqBuffer.Length);
        EfexUsb.UsbBulkRecv(ctx, 0x81, buffer, len);
        Efex.ReadStatus(ctx);
        return buffer;
    }
}
