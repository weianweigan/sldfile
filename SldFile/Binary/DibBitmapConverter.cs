using System;

namespace SldFile.Binary;

/// <summary>
/// Converts SolidWorks DIB preview stream data into standard BMP format.
/// SolidWorks stores preview images in the 'Preview' stream as a 4-byte length prefix
/// followed by a Device-Independent Bitmap (BITMAPINFOHEADER + palette + pixel data),
/// without the 14-byte BITMAPFILEHEADER.
/// </summary>
internal static class DibBitmapConverter
{
    private const ushort BmpSignature = 0x4D42; // "BM"
    private const int FileHeaderSize = 14;
    private const int MinInfoHeaderSize = 40;

    /// <summary>
    /// Converts a raw SolidWorks Preview stream (4-byte length prefix + DIB) into a valid BMP byte array.
    /// Returns null if the data is invalid or too short.
    /// </summary>
    public static byte[]? ConvertToBmp(byte[]? previewStream)
    {
        if (previewStream == null || previewStream.Length < 4 + MinInfoHeaderSize)
            return null;

        uint declaredSize = BitConverter.ToUInt32(previewStream, 0);
        if (declaredSize < MinInfoHeaderSize)
            return null;

        int dibLength = (int)Math.Min((long)declaredSize, previewStream.Length - 4);
        if (dibLength < MinInfoHeaderSize)
            return null;

        // Parse BITMAPINFOHEADER at offset 4
        uint biSize = BitConverter.ToUInt32(previewStream, 4);
        if (biSize < MinInfoHeaderSize || biSize > dibLength)
            return null;

        int biWidth = BitConverter.ToInt32(previewStream, 8);
        int biHeight = BitConverter.ToInt32(previewStream, 12);
        ushort biPlanes = BitConverter.ToUInt16(previewStream, 16);
        ushort biBitCount = BitConverter.ToUInt16(previewStream, 18);
        uint biCompression = BitConverter.ToUInt32(previewStream, 20);
        uint biClrUsed = BitConverter.ToUInt32(previewStream, 36);

        // Sanity check
        if (biPlanes != 1 || biWidth == 0 || biHeight == 0)
            return null;

        // Determine color table / palette size
        int paletteEntries = 0;
        if (biClrUsed > 0)
        {
            paletteEntries = (int)biClrUsed;
        }
        else if (biBitCount <= 8)
        {
            paletteEntries = 1 << biBitCount;
        }

        int paletteSize = paletteEntries * 4;
        if (biCompression == 3 && biBitCount is 16 or 32 && biSize == 40)
        {
            // BI_BITFIELDS with standard 40-byte header has 3 DWORD color masks
            paletteSize = 12;
        }

        int pixelOffset = FileHeaderSize + (int)biSize + paletteSize;
        int totalBmpSize = FileHeaderSize + dibLength;

        byte[] bmp = new byte[totalBmpSize];

        // 14-byte BITMAPFILEHEADER
        bmp[0] = 0x42; // 'B'
        bmp[1] = 0x4D; // 'M'
        bmp[2] = (byte)totalBmpSize;
        bmp[3] = (byte)(totalBmpSize >> 8);
        bmp[4] = (byte)(totalBmpSize >> 16);
        bmp[5] = (byte)(totalBmpSize >> 24);
        bmp[6] = 0; // reserved1
        bmp[7] = 0;
        bmp[8] = 0; // reserved2
        bmp[9] = 0;
        bmp[10] = (byte)pixelOffset;
        bmp[11] = (byte)(pixelOffset >> 8);
        bmp[12] = (byte)(pixelOffset >> 16);
        bmp[13] = (byte)(pixelOffset >> 24);

        // Copy DIB payload directly after BITMAPFILEHEADER
        Array.Copy(previewStream, 4, bmp, FileHeaderSize, dibLength);

        return bmp;
    }
}
