using System;
using System.Text;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class MfcReaderTests
{
    [Fact]
    public void MfcReader_ReadPrimitives_ParsesCorrectly()
    {
        using var writer = new MfcStreamWriter();
        writer.WriteU8(0x42);
        writer.WriteU16(1234);
        writer.WriteU32(567890);
        writer.WriteU64(9876543210UL);
        writer.WriteF32(3.14f);
        writer.WriteF64(2.71828);

        byte[] bytes = writer.ToArray();
        var reader = new MfcReader(bytes);

        Assert.Equal(0x42, reader.ReadU8());
        Assert.Equal((ushort)1234, reader.ReadU16());
        Assert.Equal(567890u, reader.ReadU32());
        Assert.Equal(9876543210UL, reader.ReadU64());
        Assert.Equal(3.14f, reader.ReadF32(), 2);
        Assert.Equal(2.71828, reader.ReadF64(), 4);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void MfcReader_ReadWChar_RoundTripsWithWriter()
    {
        using var writer = new MfcStreamWriter();
        writer.WriteWChar("SolidWorks");
        writer.WriteWChar(string.Empty);
        writer.WriteWChar(null);

        byte[] bytes = writer.ToArray();
        var reader = new MfcReader(bytes);

        Assert.Equal("SolidWorks", reader.ReadWChar());
        Assert.Equal(string.Empty, reader.ReadWChar());
        Assert.Equal(string.Empty, reader.ReadWChar());
    }

    [Fact]
    public void MfcReader_OverRead_ReturnsSafeDefaults()
    {
        var reader = new MfcReader(new byte[] { 0x01 });

        Assert.Equal(1, reader.ReadU8());
        Assert.Equal((ushort)0, reader.ReadU16());
        Assert.Equal(0u, reader.ReadU32());
        Assert.Null(reader.ReadWChar());
        Assert.False(reader.HasMore);
    }

    [Fact]
    public void MfcElement_CreateReader_BoundsMatchElementData()
    {
        using var writer = new MfcStreamWriter();
        writer.WriteMfcTag("testClass_c");
        writer.WriteU32(0x12345678);
        writer.WriteWChar("Payload");

        byte[] data = writer.ToArray();
        var doc = MfcDocument.Parse(data);

        var elem = doc.FindFirst("testClass_c");
        Assert.NotNull(elem);

        var reader = elem!.CreateReader();
        Assert.Equal(0x12345678u, reader.ReadU32());
        Assert.Equal("Payload", reader.ReadWChar());
    }

    [Fact]
    public void MfcDocument_ParentChildLinks_EstablishedCorrectly()
    {
        using var writer = new MfcStreamWriter();
        writer.WriteMfcTag("ParentClass");
        writer.WriteU16(10);
        writer.WriteMfcTag("ChildClass");
        writer.WriteU32(20);

        byte[] data = writer.ToArray();
        var doc = MfcDocument.Parse(data);

        var parent = doc.FindFirst("ParentClass");
        Assert.NotNull(parent);
        Assert.True(parent!.Index >= 0);

        var child = doc.FindChildOf(parent, "ChildClass");
        Assert.NotNull(child);
        Assert.Equal(parent, child!.Parent);
        Assert.Contains(child, parent.Children);
    }
}
