using LibEfex.core;
using LibEfex.exception;
using LibEfex.io;
using LibEfex.protocols;
using System.Runtime.InteropServices;

namespace LibEfex.common;

public static class Efex
{
    public static void SendRequest(EfexContext ctx, EfexCmd type, uint addr, uint length)
    {
        var request = new EfexRequest
        {
            Cmd = type,
            Addr = addr,
            Len = length,
            Magic = 0x43555741 // "AWUC"
        };

        var buffer = new byte[Marshal.SizeOf(request)];
        var ptr = Marshal.AllocHGlobal(buffer.Length);
        Marshal.StructureToPtr(request, ptr, false);
        Marshal.Copy(ptr, buffer, 0, buffer.Length);
        Marshal.FreeHGlobal(ptr);

        EfexUsb.SendUsbRequest(ctx, 0x12, buffer.Length);
        EfexUsb.UsbBulkSend(ctx, 0x02, buffer, buffer.Length);
    }

    public static int ReadStatus(EfexContext ctx)
    {
        var response = new byte[12];
        EfexUsb.UsbBulkRecv(ctx, 0x81, response, response.Length);

        var ptr = Marshal.AllocHGlobal(response.Length);
        Marshal.Copy(response, 0, ptr, response.Length);
        var status = Marshal.PtrToStructure<EfexResponse>(ptr);
        Marshal.FreeHGlobal(ptr);

        // Check magic
        if (status.Magic != 0x41575553) // "AWUS" in little endian
        {
            throw new EfexException(EfexError.InvalidResponse);
        }

        return (int)status.Status;
    }

    public static DeviceMode GetDeviceMode(EfexContext ctx)
    {
        return ctx.Mode;
    }

    public static string GetDeviceModeStr(EfexContext ctx)
    {
        return ctx.Mode.ToString();
    }

    public static void Init(EfexContext ctx)
    {
        // Nothing to do here for now
    }

    public static string StrError(int errorCode)
    {
        return new EfexException((EfexError)errorCode).Message;
    }
}
