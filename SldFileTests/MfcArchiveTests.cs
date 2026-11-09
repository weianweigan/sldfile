using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class MfcArchiveTests
{
    [Fact]
    public void MfcArchive_Primitives_RoundTrip_Symmetrically()
    {
        byte b = 0x42;
        ushort u16 = 0x1234;
        short i16 = -4321;
        uint u32 = 0xDEADBEEF;
        int i32 = -98765432;
        ulong u64 = 0x0123456789ABCDEF;
        long i64 = -123456789012345;
        float f32 = 3.14159f;
        double f64 = 2.718281828459045;
        bool bTrue = true;
        bool bFalse = false;
        string? testStr = "SolidWorks 2026 统一序列化测试";
        string? emptyStr = "";

        byte[] serialized;
        using (var writer = MfcArchive.CreateWriter())
        {
            writer.Byte(ref b);
            writer.U16(ref u16);
            writer.I16(ref i16);
            writer.U32(ref u32);
            writer.I32(ref i32);
            writer.U64(ref u64);
            writer.I64(ref i64);
            writer.F32(ref f32);
            writer.F64(ref f64);
            writer.Boolean(ref bTrue);
            writer.Boolean(ref bFalse);
            writer.WChar(ref testStr);
            writer.WChar(ref emptyStr);

            serialized = writer.ToArray();
        }

        Assert.NotEmpty(serialized);

        // Read back
        byte rb = 0;
        ushort ru16 = 0;
        short ri16 = 0;
        uint ru32 = 0;
        int ri32 = 0;
        ulong ru64 = 0;
        long ri64 = 0;
        float rf32 = 0;
        double rf64 = 0;
        bool rbTrue = false;
        bool rbFalse = true;
        string? rTestStr = null;
        string? rEmptyStr = null;

        using (var reader = MfcArchive.CreateReader(serialized))
        {
            reader.Byte(ref rb);
            reader.U16(ref ru16);
            reader.I16(ref ri16);
            reader.U32(ref ru32);
            reader.I32(ref ri32);
            reader.U64(ref ru64);
            reader.I64(ref ri64);
            reader.F32(ref rf32);
            reader.F64(ref rf64);
            reader.Boolean(ref rbTrue);
            reader.Boolean(ref rbFalse);
            reader.WChar(ref rTestStr);
            reader.WChar(ref rEmptyStr);
        }

        Assert.Equal(b, rb);
        Assert.Equal(u16, ru16);
        Assert.Equal(i16, ri16);
        Assert.Equal(u32, ru32);
        Assert.Equal(i32, ri32);
        Assert.Equal(u64, ru64);
        Assert.Equal(i64, ri64);
        Assert.Equal(f32, rf32);
        Assert.Equal(f64, rf64);
        Assert.True(rbTrue);
        Assert.False(rbFalse);
        Assert.Equal("SolidWorks 2026 统一序列化测试", rTestStr);
        Assert.Equal(string.Empty, rEmptyStr);
    }

    [Fact]
    public void MfcArchive_OpaqueObject_Guarantees_ByteAccurate_RoundTrip()
    {
        byte[] original = new byte[] { 0x01, 0x02, 0x03, 0xFF, 0x00, 0x55, 0xAA };
        var opaque = new OpaqueObject("moUnknown_c", original);

        byte[] output;
        using (var writer = MfcArchive.CreateWriter())
        {
            opaque.Serialize(writer);
            output = writer.ToArray();
        }

        Assert.Equal(original, output);

        var restored = new OpaqueObject("moUnknown_c", Array.Empty<byte>());
        using (var reader = MfcArchive.CreateReader(output))
        {
            restored.Serialize(reader);
        }

        Assert.Equal(original, restored.RawData);
    }

    [Fact]
    public void MfcArchive_WChar_Matches_MfcPrimitives_BytePattern()
    {
        string text = "拉伸凸台1";
        byte[] primBytes = MfcPrimitives.EncodeWChar(text);

        byte[] archBytes;
        using (var writer = MfcArchive.CreateWriter())
        {
            string? t = text;
            writer.WChar(ref t);
            archBytes = writer.ToArray();
        }

        Assert.Equal(primBytes, archBytes);
    }
}
