using LibEfex.core;
using LibEfex.exception;
using LibEfex.protocols;
using System;

namespace LibEfex.io;

public static class EfexUsb
{
    private const int SunxiUsbVendor = 0x1f3a;
    private const int SunxiUsbProduct = 0xefe8;

    private const int AwUsbRead = 0x11;
    private const int AwUsbWrite = 0x12;

    public static int UsbBulkSend(EfexContext ctx, int ep, byte[] buf, int len)
    {
        try
        {
            return ctx.Device.BulkSend(ep, buf, len);
        }
        catch (Exception)
        {
            throw new EfexException(EfexError.UsbTransfer);
        }
    }

    public static int UsbBulkRecv(EfexContext ctx, int ep, byte[] buf, int len)
    {
        try
        {
            return ctx.Device.BulkRecv(ep, buf, len);
        }
        catch (Exception)
        {
            throw new EfexException(EfexError.UsbTransfer);
        }
    }

    public static void SendUsbRequest(EfexContext ctx, int type, int length)
    {
        var requestType = type == AwUsbRead ? (byte)0x80 : (byte)0x40;
        var request = type == AwUsbRead ? (byte)AwUsbRead : (byte)AwUsbWrite;

        try
        {
            ctx.Device.ControlTransfer(requestType, request, 0, 0, Array.Empty<byte>(), (ushort)length);
        }
        catch (Exception)
        {
            throw new EfexException(EfexError.UsbTransfer);
        }
    }
}
