using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Reading coverage for the measured thin extrusion class (moExtruThin_c / TypeId 0x52).
/// Samples: ExtrudeOptions/extrude_thin_{one_dir,midplane,two_dir,cap}_1mm plus native resaves.
/// </summary>
public class ExtrudeThinOptionReadingTests
{
    private static string SamplePath(string sample) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", sample + ".SLDPRT");

    [Theory]
    [InlineData("extrude_thin_one_dir_1mm", 0u, 0.001, false, false)]
    [InlineData("extrude_thin_one_dir_1mm_resave", 0u, 0.001, false, false)]
    [InlineData("extrude_thin_midplane_1mm", 2u, 0.001, false, false)]
    [InlineData("extrude_thin_midplane_1mm_resave", 2u, 0.001, false, false)]
    [InlineData("extrude_thin_two_dir_1_2mm", 3u, 0.002, false, true)]
    [InlineData("extrude_thin_two_dir_1_2mm_resave", 3u, 0.002, false, true)]
    [InlineData("extrude_thin_cap_1mm", 0u, 0.001, true, false)]
    [InlineData("extrude_thin_cap_1mm_resave", 0u, 0.001, true, false)]
    public void ThinSamples_ReadMeasuredWallOptions(
        string sample, uint wallType, double wallThicknessDirection2, bool capEnds, bool bothDirections)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion));
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(feature.Definition);

        Assert.True(definition.IsThinFeature);
        Assert.Equal("moExtruThin_c", definition.ClassName);
        Assert.Equal(0x52u, definition.TypeId);
        Assert.Equal(0.010, definition.Depth, 12);
        Assert.Equal(wallType, definition.ThinWallType);
        Assert.Equal(0.001, definition.WallThickness, 12);
        Assert.Equal(wallThicknessDirection2, definition.WallThicknessDirection2, 12);
        Assert.Equal(capEnds, definition.CapEnds);
        Assert.Equal(0, definition.CapThickness);
        Assert.Equal(bothDirections, definition.BothDirections);
        Assert.Equal(bothDirections ? 0.005 : 0, definition.DepthDirection2, 12);

        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.IsThinFeature)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.ThinWallType)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.WallThickness)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.WallThicknessDirection2)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.CapEnds)));
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.Depth)));
        // No sample establishes a non-zero cap thickness or a thin draft/outward value.
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.CapThickness)));
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftWhileExtruding)));
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureDefinitionData.DraftOutward)));
    }

    private static bool TryParseThin(byte[] stream, out ExtrudeFeatureDefinitionData? definition, uint typeId = 0x52)
    {
        var doc = MfcDocument.Parse(stream);
        MfcElement thin = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtruThin_c"));
        var folder = new FeatureFolder
        {
            ClassName = "moExtruThin_c",
            DisplayName = "Thin",
            TagOffset = thin.TagOffset,
            Header = new FolderHeader { TypeId = typeId }
        };
        return ExtrudeThinFeatureParser.TryParse(
            doc, folder, BitConverter.ToUInt16(stream, 0), thin.TagOffset, stream.Length, out definition);
    }

    private static byte[] LoadThinStream(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        return (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
    }

    private static MfcElement SingleElement(byte[] stream, string className) =>
        Assert.Single(MfcDocument.Parse(stream).AllElements.Where(e => e.ClassName == className));

    [Fact]
    public void ThinWallTypeValueOutsideMeasuredEnumIsRejected()
    {
        byte[] stream = LoadThinStream("extrude_thin_one_dir_1mm");
        var radial = SingleElement(stream, "moDisplayRadialDim_c");
        stream[radial.TagOffset - 8] = 7; // ThinWallType: no sample for 1/4/7
        Assert.False(TryParseThin(stream, out var definition));
        Assert.Null(definition);
    }

    [Fact]
    public void ThinCapFlagOutsideZeroOrOneIsRejected()
    {
        byte[] stream = LoadThinStream("extrude_thin_cap_1mm");
        var radial = SingleElement(stream, "moDisplayRadialDim_c");
        stream[radial.TagOffset - 28] = 2; // CapEnds must remain a measured 0/1 flag
        Assert.False(TryParseThin(stream, out var definition));
        Assert.Null(definition);
    }

    [Fact]
    public void ThinMissingMarkerOrTailIsRejected()
    {
        var radial = SingleElement(LoadThinStream("extrude_thin_one_dir_1mm"), "moDisplayRadialDim_c");

        byte[] noMarker = LoadThinStream("extrude_thin_one_dir_1mm");
        noMarker[radial.TagOffset - 9] = 0;
        Assert.False(TryParseThin(noMarker, out _));

        byte[] dirtyTail = LoadThinStream("extrude_thin_one_dir_1mm");
        dirtyTail[radial.TagOffset - 1] = 1;
        Assert.False(TryParseThin(dirtyTail, out _));
    }

    [Fact]
    public void ThinMissingWallParameterIsRejected()
    {
        byte[] stream = LoadThinStream("extrude_thin_one_dir_1mm");
        byte[] d6 = { 0xFF, 0xFE, 0xFF, 0x02, 0x44, 0x00, 0x36, 0x00 };
        int index = Find(stream, d6, 0x1C00);
        Assert.True(index > 0);
        stream[index + 6] = 0x38; // D6 -> D8: neither D5/D6 contract is complete
        Assert.False(TryParseThin(stream, out _));
    }

    [Fact]
    public void ThinDuplicateWallParameterIsRejected()
    {
        byte[] stream = LoadThinStream("extrude_thin_one_dir_1mm");
        var d5 = ExtrudeAdditionalOptionParser.FindNamedParameters(stream, 0x1C00, stream.Length, "D5");
        Assert.Single(d5);
        byte[] duplicated = stream.Concat(stream.Skip(d5[0].Offset).Take(64)).ToArray();
        Assert.False(TryParseThin(duplicated, out _));
    }

    [Fact]
    public void ThinTwoDirectionRequiresDirection2Parameter()
    {
        byte[] stream = LoadThinStream("extrude_thin_two_dir_1_2mm");
        byte[] d2 = { 0xFF, 0xFE, 0xFF, 0x02, 0x44, 0x00, 0x32, 0x00 };
        int index = Find(stream, d2, 0x1C00);
        Assert.True(index > 0);
        stream[index + 6] = 0x38; // D2 -> D8; the two-direction depth would be unexplained
        Assert.False(TryParseThin(stream, out _));
    }

    [Fact]
    public void ThinSingleDirectionRejectsInjectedDirection2Parameter()
    {
        byte[] stream = LoadThinStream("extrude_thin_one_dir_1mm");
        byte[] twoDir = LoadThinStream("extrude_thin_two_dir_1_2mm");
        var d2 = ExtrudeAdditionalOptionParser.FindNamedParameters(twoDir, 0x1C00, twoDir.Length, "D2");
        Assert.Single(d2);
        byte[] injected = stream.Concat(twoDir.Skip(d2[0].Offset).Take(64)).ToArray();
        Assert.False(TryParseThin(injected, out _));
    }

    [Fact]
    public void ThinUnknownVersionOrTypeIdIsRejected()
    {
        byte[] stream = LoadThinStream("extrude_thin_one_dir_1mm");
        byte[] wrongVersion = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0x71).CopyTo(wrongVersion, 0);
        Assert.False(TryParseThin(wrongVersion, out _));
        Assert.False(TryParseThin(stream, out _, typeId: 0x4F));
    }

    private static int Find(byte[] data, byte[] pattern, int start)
    {
        for (int i = Math.Max(0, start); i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
