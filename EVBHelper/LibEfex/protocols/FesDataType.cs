namespace LibEfex.protocols;

[Flags]
public enum FesDataType
{
    None = 0x0, /**< No tag */
    /* Data type tag */
    Dram = 0x7f00,         /**< DRAM configuration data tag */
    Mbr = 0x7f01,          /**< MBR partition table tag */
    Boot1 = 0x7f02,        /**< BOOT1 tag */
    Boot0 = 0x7f03,        /**< BOOT0 tag */
    Erase = 0x7f04,        /**< Erase command tag */
    FullimgSize = 0x7f10, /**< Full image size tag */
    Ext4Ubifs = 0x7ff0,   /**< EXT4/UBIFS file system tag */
    Flash = 0x8000,        /**< FLASH operation tag */
    /* Data type mask */
    DataTypeMask = 0x7fff, /**< Data type mask */

    /* Transfer tag */
    TransStart = 0x20000,  /**< Transfer start tag */
    TransFinish = 0x10000, /**< Transfer finish tag */

    /* Transfer mask */
    TransMask = 0x30000, /**< Transfer control mask */
}
