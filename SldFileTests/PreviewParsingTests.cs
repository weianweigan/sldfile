using System;
using System.IO;
using SldFile;
using Xunit;

namespace SldFileTests;

public class PreviewParsingTests
{
    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    [InlineData(nameof(TestFileProvider.CubePart2026))]
    [InlineData(nameof(TestFileProvider.CylinderPart2026))]
    public void GetPreviewBitmap_ValidSample_ProducesValidBmpHeader(string propertyName)
    {
        string filePath = ResolvePath(propertyName);
        using var file = SwFile.Open(filePath, readOnly: true);

        byte[]? bmp = file.GetPreviewBitmap();
        Assert.NotNull(bmp);
        Assert.True(bmp!.Length > 54, "BMP file should be larger than headers (14 + 40 bytes)");

        // 1. Check 'BM' signature
        Assert.Equal(0x42, bmp[0]); // 'B'
        Assert.Equal(0x4D, bmp[1]); // 'M'

        // 2. Check declared total file size
        int declaredFileSize = BitConverter.ToInt32(bmp, 2);
        Assert.Equal(bmp.Length, declaredFileSize);

        // 3. Check reserved fields are 0
        Assert.Equal(0, BitConverter.ToInt16(bmp, 6));
        Assert.Equal(0, BitConverter.ToInt16(bmp, 8));

        // 4. Check pixel offset
        int pixelOffset = BitConverter.ToInt32(bmp, 10);
        Assert.True(pixelOffset >= 54, $"Pixel offset ({pixelOffset}) should be at least 54 bytes");
        Assert.True(pixelOffset < bmp.Length, $"Pixel offset ({pixelOffset}) should be within file length ({bmp.Length})");

        // 5. Check DIB BITMAPINFOHEADER at offset 14
        int biSize = BitConverter.ToInt32(bmp, 14);
        Assert.True(biSize >= 40, $"biSize ({biSize}) must be at least 40");

        int biWidth = BitConverter.ToInt32(bmp, 18);
        int biHeight = BitConverter.ToInt32(bmp, 22);
        short biPlanes = BitConverter.ToInt16(bmp, 26);
        short biBitCount = BitConverter.ToInt16(bmp, 28);

        Assert.True(biWidth > 0, $"Image width ({biWidth}) must be positive");
        Assert.True(biHeight != 0, $"Image height ({biHeight}) cannot be 0");
        Assert.Equal(1, biPlanes);
        Assert.True(biBitCount is 1 or 4 or 8 or 16 or 24 or 32, $"Unsupported bit count: {biBitCount}");
    }

    [Fact]
    public void GetRawPreviewDib_ReturnsRawLengthPrefixedDib()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        byte[]? dib = file.GetRawPreviewDib();

        Assert.NotNull(dib);
        Assert.True(dib!.Length > 44);

        uint declaredLen = BitConverter.ToUInt32(dib, 0);
        Assert.True(declaredLen > 40);
        Assert.True(declaredLen <= dib.Length - 4);

        uint biSize = BitConverter.ToUInt32(dib, 4);
        Assert.Equal(40u, biSize);
    }

    private static string ResolvePath(string propertyName)
    {
        var prop = typeof(TestFileProvider).GetProperty(propertyName);
        Assert.NotNull(prop);
        return (string)prop!.GetValue(null)!;
    }
}
