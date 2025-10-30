namespace LibEfex.protocols;

public enum EfexError
{
    /* Generic Errors */
    Success = 0,        /**< Success */
    InvalidParam = -1, /**< Invalid parameter */
    NullPtr = -2,      /**< Null pointer error */
    Memory = -3,        /**< Memory allocation error */
    NotSupport = -4,   /**< Operation not supported */

    /* USB Communication Errors */
    UsbInit = -10,             /**< USB initialization failed */
    UsbDeviceNotFound = -11, /**< Device not found */
    UsbOpen = -12,             /**< Failed to open device */
    UsbTransfer = -13,         /**< USB transfer failed */
    UsbTimeout = -14,          /**< USB transfer timeout */

    /* Protocol Errors */
    Protocol = -20,            /**< Protocol error */
    InvalidResponse = -21,    /**< Invalid response from device */
    UnexpectedStatus = -22,   /**< Unexpected status code */
    InvalidState = -23,       /**< Invalid device state */
    InvalidDeviceMode = -24, /**< Invalid device mode */

    /* Operation Errors */
    OperationFailed = -30, /**< Operation failed */
    DeviceBusy = -31,      /**< Device is busy */
    DeviceNotReady = -32, /**< Device not ready */

    /* Flash Related Errors */
    FlashAccess = -40,     /**< Flash access error */
    FlashSizeProbe = -41, /**< Flash size probing failed */
    FlashSetOnoff = -42,  /**< Failed to set flash on/off */

    /* Verification Errors */
    Verification = -50, /**< Verification failed */
    CrcMismatch = -51, /**< CRC mismatch error */

    /* File Operation Errors */
    FileOpen = -60,  /**< Failed to open file */
    FileRead = -61,  /**< Failed to read file */
    FileWrite = -62, /**< Failed to write file */
    FileSize = -63,  /**< File size error */
}
