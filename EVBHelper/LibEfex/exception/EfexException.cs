using LibEfex.protocols;

namespace LibEfex.exception;

public class EfexException(EfexError error) : Exception(GetErrorMessage(error))
{
    public EfexError ErrorCode { get; } = error;

    private static string GetErrorMessage(EfexError error)
    {
        return error switch
        {
            EfexError.Success => "Success",
            EfexError.InvalidParam => "Invalid parameter",
            EfexError.NullPtr => "Null pointer error",
            EfexError.Memory => "Memory allocation error",
            EfexError.NotSupport => "Operation not supported",
            EfexError.UsbInit => "USB initialization failed",
            EfexError.UsbDeviceNotFound => "Device not found",
            EfexError.UsbOpen => "Failed to open device",
            EfexError.UsbTransfer => "USB transfer failed",
            EfexError.UsbTimeout => "USB transfer timeout",
            EfexError.Protocol => "Protocol error",
            EfexError.InvalidResponse => "Invalid response from device",
            EfexError.UnexpectedStatus => "Unexpected status code",
            EfexError.InvalidState => "Invalid device state",
            EfexError.InvalidDeviceMode => "Invalid device mode",
            EfexError.OperationFailed => "Operation failed",
            EfexError.DeviceBusy => "Device is busy",
            EfexError.DeviceNotReady => "Device not ready",
            EfexError.FlashAccess => "Flash access error",
            EfexError.FlashSizeProbe => "Flash size probing failed",
            EfexError.FlashSetOnoff => "Failed to set flash on/off",
            EfexError.Verification => "Verification failed",
            EfexError.CrcMismatch => "CRC mismatch error",
            EfexError.FileOpen => "Failed to open file",
            EfexError.FileRead => "Failed to read file",
            EfexError.FileWrite => "Failed to write file",
            EfexError.FileSize => "File size error",
            _ => "Unknown error"
        };
    }
}
