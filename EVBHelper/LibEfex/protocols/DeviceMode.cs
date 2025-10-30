namespace LibEfex.protocols;

public enum DeviceMode
{
    Null = 0x0,
    Fel = 0x1,
    Srv = 0x2,
    UpdateCool = 0x3,
    UpdateHot = 0x4,
    Efi = 0x10,
    Fes = 0x20,
    Vid = 0xef,
}
