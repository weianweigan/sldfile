using SldFile.Mfc;
using System.Text;

namespace SldFileTests;

public class MfcPrimitivesTests
{
    // ═══════════════════════════════════════════════════════════════
    // IsMfcTag
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IsMfcTag_ValidClassTag_ReturnsTrue()
    {
        // FF FF 01 00 = new class instance
        var data = new byte[] { 0xFF, 0xFF, 0x01, 0x00, 0x08, 0x00 };
        Assert.True(MfcPrimitives.IsMfcTag(data, 0));
    }

    [Fact]
    public void IsMfcTag_ArrayElementTag_ReturnsTrue()
    {
        // FF FF 00 00 = array element / inline block
        var data = new byte[] { 0xFF, 0xFF, 0x00, 0x00, 0x08, 0x00 };
        Assert.True(MfcPrimitives.IsMfcTag(data, 0));
    }

    [Fact]
    public void IsMfcTag_BadPrefix_ReturnsFalse()
    {
        var data = new byte[] { 0xAA, 0xBB, 0x01, 0x00, 0x08, 0x00 };
        Assert.False(MfcPrimitives.IsMfcTag(data, 0));
    }

    [Fact]
    public void IsMfcTag_BadTagByte_ReturnsFalse()
    {
        // tag_byte must be 0x00 or 0x01
        var data = new byte[] { 0xFF, 0xFF, 0x02, 0x00, 0x08, 0x00 };
        Assert.False(MfcPrimitives.IsMfcTag(data, 0));
    }

    [Fact]
    public void IsMfcTag_TooShort_ReturnsFalse()
    {
        var data = new byte[] { 0xFF, 0xFF, 0x01 };
        Assert.False(MfcPrimitives.IsMfcTag(data, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // IsEndMarker
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IsEndMarker_ValidSentinel_ReturnsTrue()
    {
        var data = new byte[] { 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00 };
        Assert.True(MfcPrimitives.IsEndMarker(data, 0));
    }

    [Fact]
    public void IsEndMarker_NonSentinel_ReturnsFalse()
    {
        var data = new byte[] { 0xFF, 0xFF, 0x00, 0x00, 0x01, 0x00, 0x00 };
        Assert.False(MfcPrimitives.IsEndMarker(data, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // IsWCharPrefix
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IsWCharPrefix_ValidPrefix_ReturnsTrue()
    {
        var data = new byte[] { 0xFF, 0xFE, 0xFF, 0x05 };
        Assert.True(MfcPrimitives.IsWCharPrefix(data, 0));
    }

    [Fact]
    public void IsWCharPrefix_BadPrefix_ReturnsFalse()
    {
        var data = new byte[] { 0xFF, 0xFF, 0xFF, 0x05 };
        Assert.False(MfcPrimitives.IsWCharPrefix(data, 0));
    }

    // ═══════════════════════════════════════════════════════════════
    // ReadMfcTag
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ReadMfcTag_ReadsClassName()
    {
        var name = "moHeader_c";
        var nameBytes = Encoding.ASCII.GetBytes(name);
        var data = new byte[6 + nameBytes.Length];
        data[0] = 0xFF; data[1] = 0xFF; data[2] = 0x01; data[3] = 0x00;
        data[4] = (byte)nameBytes.Length; data[5] = 0x00;
        Array.Copy(nameBytes, 0, data, 6, nameBytes.Length);

        int offset = 0;
        var tag = MfcPrimitives.ReadMfcTag(data, ref offset);

        Assert.NotNull(tag);
        Assert.Equal("moHeader_c", tag.ClassName);
        Assert.Equal(0x01, tag.TagByte);
        Assert.Equal(0, tag.TagOffset);
        Assert.Equal(6 + nameBytes.Length, offset);
        Assert.Equal(offset, tag.DataStart);
    }

    [Fact]
    public void ReadMfcTag_CtSuffix_ResolvedCorrectly()
    {
        // Class name "moRefPlane_c" with trailing 't' + 0x80 => "moRefPlane_ct"
        var baseName = "moRefPlane_c";
        var nameBytes = Encoding.ASCII.GetBytes(baseName);
        var data = new byte[6 + nameBytes.Length + 2];
        data[0] = 0xFF; data[1] = 0xFF; data[2] = 0x01; data[3] = 0x00;
        data[4] = (byte)nameBytes.Length; data[5] = 0x00;
        Array.Copy(nameBytes, 0, data, 6, nameBytes.Length);
        var pos = 6 + nameBytes.Length;
        data[pos] = 0x74;     // 't'
        data[pos + 1] = 0x80; // separator

        int offset = 0;
        var tag = MfcPrimitives.ReadMfcTag(data, ref offset);

        Assert.NotNull(tag);
        Assert.Equal("moRefPlane_ct", tag.ClassName);
        Assert.Equal(pos + 2, tag.DataStart);
    }

    [Fact]
    public void ReadMfcTag_CMSuffix_ResolvedCorrectly()
    {
        // Class name "moSensorFolder_c" with trailing 'M' + 0x80 => "moSensorFolder_cM"
        var baseName = "moSensorFolder_c";
        var nameBytes = Encoding.ASCII.GetBytes(baseName);
        var data = new byte[6 + nameBytes.Length + 2];
        data[0] = 0xFF; data[1] = 0xFF; data[2] = 0x01; data[3] = 0x00;
        data[4] = (byte)nameBytes.Length; data[5] = 0x00;
        Array.Copy(nameBytes, 0, data, 6, nameBytes.Length);
        var pos = 6 + nameBytes.Length;
        data[pos] = 0x4D;     // 'M'
        data[pos + 1] = 0x80; // separator

        int offset = 0;
        var tag = MfcPrimitives.ReadMfcTag(data, ref offset);

        Assert.NotNull(tag);
        Assert.Equal("moSensorFolder_cM", tag.ClassName);
    }

    // ═══════════════════════════════════════════════════════════════
    // ReadWChar
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ReadWChar_ReadsUnicodeString()
    {
        var s = "Hello";
        var utf16 = Encoding.Unicode.GetBytes(s);
        var data = new byte[4 + utf16.Length];
        data[0] = 0xFF; data[1] = 0xFE; data[2] = 0xFF;
        data[3] = (byte)s.Length; // char count
        Array.Copy(utf16, 0, data, 4, utf16.Length);

        int offset = 0;
        var result = MfcPrimitives.ReadWChar(data, ref offset);

        Assert.Equal(s, result);
        Assert.Equal(data.Length, offset);
    }

    [Fact]
    public void ReadWChar_EmptyString_ReturnsEmpty()
    {
        var data = new byte[] { 0xFF, 0xFE, 0xFF, 0x00 };

        int offset = 0;
        var result = MfcPrimitives.ReadWChar(data, ref offset);

        Assert.Equal(string.Empty, result);
        Assert.Equal(4, offset);
    }

    [Fact]
    public void ReadWChar_BadPrefix_ReturnsNull()
    {
        var data = new byte[] { 0xAA, 0xBB, 0xCC, 0x05, 0x00, 0x00 };

        int offset = 0;
        var result = MfcPrimitives.ReadWChar(data, ref offset);

        Assert.Null(result);
        Assert.Equal(0, offset); // offset unchanged
    }

    // ═══════════════════════════════════════════════════════════════
    // Numeric Readers — Endianness
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ReadU16_ReadsLittleEndian()
    {
        // 0x0102 in little-endian = 0x0201
        var data = new byte[] { 0x02, 0x01 };
        int offset = 0;
        Assert.Equal((ushort)0x0102, MfcPrimitives.ReadU16(data, ref offset));
    }

    [Fact]
    public void ReadU32_ReadsLittleEndian()
    {
        var data = new byte[] { 0x78, 0x56, 0x34, 0x12 };
        int offset = 0;
        Assert.Equal(0x12345678u, MfcPrimitives.ReadU32(data, ref offset));
    }

    [Fact]
    public void ReadU64_ReadsLittleEndian()
    {
        var data = new byte[] { 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01 };
        int offset = 0;
        Assert.Equal(0x0123456789ABCDEFul, MfcPrimitives.ReadU64(data, ref offset));
    }

    [Fact]
    public void ReadF32_ReadsCorrectValue()
    {
        float expected = 1.5f;
        var data = BitConverter.GetBytes(expected);
        int offset = 0;
        Assert.Equal(expected, MfcPrimitives.ReadF32(data, ref offset), 0.0001f);
    }

    [Fact]
    public void ReadF64_ReadsCorrectValue()
    {
        double expected = 3.14159265358979;
        var data = BitConverter.GetBytes(expected);
        int offset = 0;
        Assert.Equal(expected, MfcPrimitives.ReadF64(data, ref offset), 0.000000001);
    }

    [Fact]
    public void ReadGuid_FormatsCorrectly()
    {
        // GUID {00112233-4455-6677-8899-AABBCCDDEEFF} in little-endian
        var data = new byte[16];
        data[0] = 0x33; data[1] = 0x22; data[2] = 0x11; data[3] = 0x00; // a (LE)
        data[4] = 0x55; data[5] = 0x44; // b (LE)
        data[6] = 0x77; data[7] = 0x66; // c (LE)
        data[8] = 0x88; data[9] = 0x99; // d[0..1]
        data[10] = 0xAA; data[11] = 0xBB; data[12] = 0xCC; data[13] = 0xDD; data[14] = 0xEE; data[15] = 0xFF;

        int offset = 0;
        var guid = MfcPrimitives.ReadGuid(data, ref offset);

        Assert.Equal("00112233-4455-6677-8899-AABBCCDDEEFF", guid);
    }

    // ═══════════════════════════════════════════════════════════════
    // ScanMfcTags
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ScanMfcTags_FindsAllTags()
    {
        var writer = new SldFile.Binary.MfcStreamWriter();
        writer.WriteMfcTag("moHeader_c");
        writer.WriteMfcTag("suObList");
        var data = writer.ToArray();

        var tags = MfcPrimitives.ScanMfcTags(data);

        Assert.Equal(2, tags.Count);
        Assert.Equal("moHeader_c", tags[0].ClassName);
        Assert.Equal("suObList", tags[1].ClassName);
    }

    [Fact]
    public void ScanMfcTags_CorrectlyPositionsTags()
    {
        var writer = new SldFile.Binary.MfcStreamWriter();
        writer.WriteMfcTag("moLogs_c");
        writer.WriteU16(1);
        writer.WriteU32(100);
        writer.WriteMfcTag("moStamp_c");
        writer.WriteU32(12345);
        writer.WriteMfcTag("moExtObject_c");

        var data = writer.ToArray();
        var tags = MfcPrimitives.ScanMfcTags(data);

        Assert.Equal(3, tags.Count);

        // Verify tag positions and class names
        Assert.Equal("moLogs_c", tags[0].ClassName);
        Assert.Equal(0, tags[0].TagOffset);

        Assert.Equal("moStamp_c", tags[1].ClassName);
        Assert.True(tags[1].TagOffset > tags[0].DataStart);

        Assert.Equal("moExtObject_c", tags[2].ClassName);
        Assert.True(tags[2].TagOffset > tags[1].TagOffset);

        // Each tag's DataEnd = next tag's start (or EOF for last)
        Assert.Equal(tags[1].TagOffset, tags[0].DataEnd);
        Assert.Equal(tags[2].TagOffset, tags[1].DataEnd);
        Assert.Equal(data.Length, tags[2].DataEnd);
    }

    [Fact]
    public void ScanMfcTags_WithMultipleClasses_FindsAll()
    {
        var writer = new SldFile.Binary.MfcStreamWriter();
        writer.WriteMfcTag("moSensorFolder_ct");
        writer.WriteBytes(new byte[] { 0xFF, 0xFE, 0xFF, 0x05, 0x48, 0x00, 0x65, 0x00, 0x6C, 0x00, 0x6C, 0x00, 0x6F, 0x00 });
        writer.WriteBytes(new byte[90]);
        writer.WriteMfcTag("moRefPlane_ct");
        writer.WriteBytes(new byte[] { 0xFF, 0xFE, 0xFF, 0x04, 0x46, 0x00, 0x72, 0x00, 0x6F, 0x00, 0x6E, 0x00 });
        writer.WriteBytes(new byte[90]);
        writer.WriteMfcTag("moDefaultRefPlnData_c");
        writer.WriteBytes(new byte[100]);

        var data = writer.ToArray();
        var tags = MfcPrimitives.ScanMfcTags(data);

        Assert.Equal(3, tags.Count);
        Assert.Contains(tags, t => t.ClassName == "moSensorFolder_ct");
        Assert.Contains(tags, t => t.ClassName == "moRefPlane_ct");
        Assert.Contains(tags, t => t.ClassName == "moDefaultRefPlnData_c");
        // Verify DataEnd ordering
        Assert.Equal(tags[1].TagOffset, tags[0].DataEnd);
        Assert.Equal(tags[2].TagOffset, tags[1].DataEnd);
    }

    [Fact]
    public void ScanMfcTags_HandlesEmptyStream()
    {
        var data = Array.Empty<byte>();
        var tags = MfcPrimitives.ScanMfcTags(data);
        Assert.Empty(tags);
    }

    [Fact]
    public void ScanMfcTags_SkipsEndMarkers()
    {
        var writer = new SldFile.Binary.MfcStreamWriter();
        writer.WriteMfcTag("moHeader_c");
        // End marker: FF FF 00 00 00 00
        writer.WriteBytes(new byte[] { 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00 });

        var data = writer.ToArray();
        var tags = MfcPrimitives.ScanMfcTags(data);

        Assert.Single(tags);
        Assert.Equal("moHeader_c", tags[0].ClassName);
    }

    // ═══════════════════════════════════════════════════════════════
    // ClassifyStreamType
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ClassifyStreamType_Part_ReturnsCorrectString()
    {
        var result = MfcPrimitives.ClassifyStreamType(0x000F);
        Assert.Contains("Part", result);
        Assert.Contains("SLDPRT", result);
    }

    [Fact]
    public void ClassifyStreamType_Assembly_ReturnsCorrectString()
    {
        var result = MfcPrimitives.ClassifyStreamType(0x000B);
        Assert.Contains("Assembly", result);
        Assert.Contains("SLDASM", result);
    }

    [Fact]
    public void ClassifyStreamType_Unknown_ReturnsUnknown()
    {
        var result = MfcPrimitives.ClassifyStreamType(0x0000);
        Assert.Equal("Unknown", result);
    }

    // ═══════════════════════════════════════════════════════════════
    // OleDate conversion
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void OleDateToDateTime_DefaultTimestamp_IsEpoch()
    {
        // 2.0 = 1900-01-01 (OLE date default)
        var dt = MfcPrimitives.OleDateToDateTime(2.0);
        Assert.NotNull(dt);
        Assert.Equal(1900, dt!.Value.Year);
        Assert.Equal(1, dt.Value.Month);
        Assert.Equal(1, dt.Value.Day);
    }

    [Fact]
    public void OleDateToDateTime_InvalidValue_ReturnsNull()
    {
        // Very large OLE dates can throw
        var dt = MfcPrimitives.OleDateToDateTime(1e50);
        Assert.Null(dt);
    }
}
