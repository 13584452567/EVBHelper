namespace LibEfex.protocols;

public enum EfexCmd
{
    /* Common Commands */
    VerifyDevice = 0x0001,
    SwitchRole = 0x0002,
    IsReady = 0x0003,
    GetCmdSetVer = 0x0004,
    Disconnect = 0x0010,
    /* FEL Commands */
    FelWrite = 0x0101,
    FelExec = 0x0102,
    FelRead = 0x0103,
    /* FES Commands */
    FesTrans = 0x0201,
    FesRun = 0x0202,
    FesInfo = 0x0203,
    FesGetMsg = 0x0204,
    FesUnregFed = 0x0205,
    FesDown = 0x0206,
    FesUp = 0x0207,
    FesVerify = 0x0208,
    FesQueryStorage = 0x0209,
    FesFlashSetOn = 0x020A,
    FesFlashSetOff = 0x020B,
    FesVerifyValue = 0x020C,
    FesVerifyStatus = 0x020D,
    FesFlashSizeProbe = 0x020E,
    FesToolMode = 0x020F,
    FesVerifyUbootBlk = 0x0214,
    FesForceEraseFlash = 0x0220,
    FesForceEraseKey = 0x0221,
    FesQuerySecure = 0x0230,
    FesQueryInfo = 0x0231,
    FesGetChipid = 0x0232
}
