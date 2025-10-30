using LibEfex.io;
using LibEfex.protocols;

namespace LibEfex.core;

public class EfexContext : IDisposable
{
    public IUsbDevice Device { get; }
    public DeviceMode Mode { get; set; }

    public EfexContext(IUsbDevice device)
    {
        Device = device;
    }

    public void Dispose()
    {
        Device.Dispose();
    }
}
