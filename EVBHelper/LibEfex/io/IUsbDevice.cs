using LibUsbDotNet;
using LibUsbDotNet.Main;

namespace LibEfex.io;

public interface IUsbDevice : IDisposable
{
    int BulkSend(int ep, byte[] buf, int len);
    int BulkRecv(int ep, byte[] buf, int len);
    int ControlTransfer(byte bmRequestType, byte bRequest, ushort wValue, ushort wIndex, byte[] data, ushort wLength);
}

public class LibUsbDevice(UsbDevice device) : IUsbDevice
{
    private readonly UsbDevice _device = device;

    public int BulkSend(int ep, byte[] buf, int len)
    {
        var writer = _device.OpenEndpointWriter((WriteEndpointID)ep);
        writer.Write(buf, 10000, out var bytesWritten);
        return bytesWritten;
    }

    public int BulkRecv(int ep, byte[] buf, int len)
    {
        var reader = _device.OpenEndpointReader((ReadEndpointID)ep);
        reader.Read(buf, 10000, out var bytesRead);
        return bytesRead;
    }

    public int ControlTransfer(byte bmRequestType, byte bRequest, ushort wValue, ushort wIndex, byte[] data, ushort wLength)
    {
        var setupPacket = new UsbSetupPacket(bmRequestType, bRequest, wValue, wIndex, wLength);
        _device.ControlTransfer(ref setupPacket, data, wLength, out var length);
        return length;
    }

    public void Dispose()
    {
        _device.Close();
    }
}
