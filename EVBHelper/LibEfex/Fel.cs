using LibEfex.common;
using LibEfex.core;
using LibEfex.io;
using LibEfex.protocols;

namespace LibEfex;

public static class Fel
{
    public static void Exec(EfexContext ctx, uint addr)
    {
        Efex.SendRequest(ctx, EfexCmd.FelExec, addr, 0);
        Efex.ReadStatus(ctx);
    }

    public static byte[] Read(EfexContext ctx, uint addr, int len)
    {
        var buffer = new byte[len];
        Efex.SendRequest(ctx, EfexCmd.FelRead, addr, (uint)len);
        EfexUsb.UsbBulkRecv(ctx, 0x81, buffer, len);
        Efex.ReadStatus(ctx);
        return buffer;
    }

    public static void Write(EfexContext ctx, uint addr, byte[] buf, int len)
    {
        Efex.SendRequest(ctx, EfexCmd.FelWrite, addr, (uint)len);
        EfexUsb.UsbBulkSend(ctx, 0x02, buf, len);
        Efex.ReadStatus(ctx);
    }
}
