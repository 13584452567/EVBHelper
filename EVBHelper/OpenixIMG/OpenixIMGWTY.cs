using System.Runtime.InteropServices;
using System.Text;

namespace OpenixIMG
{
    public enum OutputFormat
    {
        UNIMG,
        IMGREPACKER
    }

    public static class OpenixIMGWTY
    {
        public const string IMAGEWTY_MAGIC = "IMAGEWTY";
        public const int IMAGEWTY_MAGIC_LEN = 8;
        public const uint IMAGEWTY_VERSION = 0x100234;
        public const int IMAGEWTY_FILEHDR_LEN = 1024;
        public const int IMAGEWTY_FHDR_MAINTYPE_LEN = 8;
        public const int IMAGEWTY_FHDR_SUBTYPE_LEN = 16;
        public const int IMAGEWTY_FHDR_FILENAME_LEN = 256;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct ImageHeaderV1
    {
        public uint pid;
        public uint vid;
        public uint hardware_id;
        public uint firmware_id;
        public uint val1;
        public uint val1024;
        public uint num_files;
        public uint val1024_2;
        public uint val0;
        public uint val0_2;
        public uint val0_3;
        public uint val0_4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct ImageHeaderV3
    {
        public uint unknown;
        public uint pid;
        public uint vid;
        public uint hardware_id;
        public uint firmware_id;
        public uint val1;
        public uint val1024;
        public uint num_files;
        public uint val1024_2;
        public uint val0;
        public uint val0_2;
        public uint val0_3;
        public uint val0_4;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, CharSet = CharSet.Ansi)]
    public struct ImageHeader
    {
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = OpenixIMGWTY.IMAGEWTY_MAGIC_LEN)]
        public byte[] magic;

        [FieldOffset(8)]
        public uint header_version;

        [FieldOffset(12)]
        public uint header_size;

        [FieldOffset(16)]
        public uint ram_base;

        [FieldOffset(20)]
        public uint version;

        [FieldOffset(24)]
        public uint image_size;

        [FieldOffset(28)]
        public uint image_header_size;

        [FieldOffset(32)]
        public ImageHeaderV1 v1;

        [FieldOffset(32)]
        public ImageHeaderV3 v3;

        public void Initialize(uint version, uint pid, uint vid, uint hardware_id, uint firmware_id, uint num_files)
        {
            magic = Encoding.ASCII.GetBytes(OpenixIMGWTY.IMAGEWTY_MAGIC);
            header_version = 0x0100; // Default to v1
            header_size = 0x50;
            ram_base = 0x04D00000;
            this.version = version;
            image_size = 0; // To be filled later
            image_header_size = 1024;

            v1.pid = pid;
            v1.vid = vid;
            v1.hardware_id = hardware_id;
            v1.firmware_id = firmware_id;
            v1.val1 = 1;
            v1.val1024 = 1024;
            v1.num_files = num_files;
            v1.val1024_2 = 1024;
            v1.val0 = 0;
            v1.val0_2 = 0;
            v1.val0_3 = 0;
            v1.val0_4 = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct FileHeaderV1
    {
        public uint unknown_3;
        public uint stored_length;
        public uint original_length;
        public uint offset;
        public uint unknown;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = OpenixIMGWTY.IMAGEWTY_FHDR_FILENAME_LEN)]
        public string filename;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct FileHeaderV3
    {
        public uint unknown_0;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = OpenixIMGWTY.IMAGEWTY_FHDR_FILENAME_LEN)]
        public string filename;
        public uint stored_length;
        public uint pad1;
        public uint original_length;
        public uint pad2;
        public uint offset;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, CharSet = CharSet.Ansi)]
    public struct FileHeader
    {
        [FieldOffset(0)]
        public uint filename_len;

        [FieldOffset(4)]
        public uint total_header_size;

        [FieldOffset(8)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = OpenixIMGWTY.IMAGEWTY_FHDR_MAINTYPE_LEN)]
        public byte[] maintype;

        [FieldOffset(16)]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = OpenixIMGWTY.IMAGEWTY_FHDR_SUBTYPE_LEN)]
        public byte[] subtype;

        [FieldOffset(32)]
        public FileHeaderV1 v1;

        [FieldOffset(32)]
        public FileHeaderV3 v3;

        public void Initialize(string filename, string maintype, string subtype, uint size, uint offset)
        {
            filename_len = OpenixIMGWTY.IMAGEWTY_FHDR_FILENAME_LEN;
            total_header_size = OpenixIMGWTY.IMAGEWTY_FILEHDR_LEN;

            this.maintype = new byte[OpenixIMGWTY.IMAGEWTY_FHDR_MAINTYPE_LEN];
            Encoding.ASCII.GetBytes(maintype, 0, Math.Min(maintype.Length, this.maintype.Length), this.maintype, 0);

            this.subtype = new byte[OpenixIMGWTY.IMAGEWTY_FHDR_SUBTYPE_LEN];
            Encoding.ASCII.GetBytes(subtype, 0, Math.Min(subtype.Length, this.subtype.Length), this.subtype, 0);

            v1.filename = filename;
            v1.offset = offset;
            v1.stored_length = size;
            v1.original_length = size;

            if ((v1.stored_length & 0x1FF) != 0)
            {
                v1.stored_length &= ~0x1FFu;
                v1.stored_length += 0x200;
            }

            v1.unknown_3 = 0;
            v1.unknown = 0;
        }
    }
}
