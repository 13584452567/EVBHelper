namespace OpenixCard.Models;

internal static class LinuxPartitionDefaults
{
    public const long GptLocation = 0x100000; // bytes
    public const long Boot0Offset = 0x2000; // bytes
    public const long BootPackagesOffset = 0x1004000; // bytes

    public static long CommonCompensationInKilobytes =>
        (GptLocation >> 10) + (Boot0Offset >> 10) + (BootPackagesOffset >> 10);
}
