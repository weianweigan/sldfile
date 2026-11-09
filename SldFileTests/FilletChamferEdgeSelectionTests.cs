using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Cross-sample coverage for the measured single-edge persistent reference used by Fillet_c,
/// VarFillet_c and Chamfer_c. Persistent ids are opaque; the class word/archive GUID/record
/// structure are validated and exposed.
/// </summary>
public class FilletChamferEdgeSelectionTests
{
    private const string StreamPath = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string folder, string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", folder, name + ".SLDPRT");
        return File.Exists(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", name + ".SLDPRT");
    }

    [Theory]
    [InlineData("FilletOptions", "fillet_5mm", 0x20D0u)]
    [InlineData("FilletOptions", "fillet_5mm_resave", 0x232Du)]
    [InlineData("", "fillet_2mm", 0x38D9u)]
    [InlineData("FilletOptions", "fillet_variable_2_4mm", 0x254Au)]
    [InlineData("FilletOptions", "fillet_variable_2_4mm_resave", 0x254Au)]
    public void FilletSamplesExposeSinglePersistentEdge(string folder, string sample, uint persistentId)
    {
        using var file = SwFile.Open(SamplePath(folder, sample), readOnly: true);
        var definitions = file.Features.Select(f => f.Model).OfType<MfcFeatureModelBase>().ToArray();
        var definition = definitions.FirstOrDefault(d => d is FilletFeatureModel or VariableFilletFeatureModel);
        Assert.NotNull(definition);
        var edges = definition switch
        {
            FilletFeatureModel f => f.Edges,
            VariableFilletFeatureModel v => v.Edges,
            _ => null
        };
        var edge = Assert.Single(edges!);
        Assert.Equal(SelectionEntityType.Edge, edge.EntityType);
        Assert.Equal(4u, edge.EntityId);
        Assert.Equal(0x200u, edge.ClassWord);
        Assert.Equal(persistentId, edge.PersistentId);
        Assert.Equal(PersistentEdgeReferenceParser.ExpectedArchiveGuid, edge.ArchiveGuid);
        Assert.Equal(4, edge.RecordCount);
    }

    [Theory]
    [InlineData("chamfer_dist_dist_2_3mm")]
    [InlineData("chamfer_dist_dist_2_3mm_resave")]
    [InlineData("chamfer_dist_angle_2mm45")]
    [InlineData("chamfer_dist_angle_2mm45_resave")]
    public void ChamferSamplesExposeSinglePersistentEdge(string sample)
    {
        using var file = SwFile.Open(SamplePath("ChamferOptions", sample), readOnly: true);
        var definition = Assert.IsType<ChamferFeatureModel>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Chamfer)).Model);
        var edge = Assert.Single(definition.Edges!);
        Assert.Equal(SelectionEntityType.Edge, edge.EntityType);
        Assert.Equal(4u, edge.EntityId);
        Assert.Equal(0u, edge.ClassWord);
        Assert.Equal(0u, edge.PersistentId);
        Assert.Equal(PersistentEdgeReferenceParser.ExpectedArchiveGuid, edge.ArchiveGuid);
        Assert.Equal(4, edge.RecordCount);
    }

    [Fact]
    public void FilletEdgeReferenceTamperingIsRejected()
    {
        byte[] raw;
        using (var file = SwFile.Open(SamplePath("FilletOptions", "fillet_5mm"), readOnly: true))
            raw = (byte[])file.RawStreams[StreamPath].Clone();

        void ExpectNull(Action<byte[], int> mutate)
        {
            byte[] copy = (byte[])raw.Clone();
            var doc = MfcDocument.Parse(copy);
            var edge = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moEdgeRef_c"));
            mutate(copy, edge.DataStart);
            var mutated = MfcDocument.Parse(copy);
            var feature = mutated.AllElements.First(e => e.ClassName == "Fillet_c");
            Assert.Null(FilletChamferFeatureParser.ParseFillet(mutated, "Fillet_c", 0x51, feature.TagOffset, copy.Length));
        }

        ExpectNull((b, o) => BitConverter.GetBytes(2u).CopyTo(b, o));           // count != 1
        ExpectNull((b, o) => BitConverter.GetBytes(0x201u).CopyTo(b, o + 12));  // class word
        ExpectNull((b, o) => b[o + 20] ^= 0xFF);                                // archive GUID
        ExpectNull((b, o) => b[o + 0x26] ^= 0xFF);                              // first record marker
        ExpectNull((b, o) => BitConverter.GetBytes(5u).CopyTo(b, o + 8));       // local edge id
    }

    [Fact]
    public void VariableAndChamferEdgeReferenceTamperingIsRejected()
    {
        byte[] variable;
        using (var file = SwFile.Open(SamplePath("FilletOptions", "fillet_variable_2_4mm"), readOnly: true))
            variable = (byte[])file.RawStreams[StreamPath].Clone();
        var variableDoc = MfcDocument.Parse(variable);
        var variableEdge = Assert.Single(variableDoc.AllElements.Where(e => e.ClassName == "moEdgeRef_c"));
        byte[] variableCopy = (byte[])variable.Clone();
        BitConverter.GetBytes(0x201u).CopyTo(variableCopy, variableEdge.DataStart + 12);
        var mutatedVariable = MfcDocument.Parse(variableCopy);
        var variableFeature = mutatedVariable.AllElements.First(e => e.ClassName == "VarFillet_c");
        Assert.Null(FilletChamferFeatureParser.ParseVariableFillet(
            mutatedVariable, "VarFillet_c", 0x52, variableFeature.TagOffset, variableCopy.Length));

        byte[] chamfer;
        using (var file = SwFile.Open(SamplePath("ChamferOptions", "chamfer_dist_dist_2_3mm"), readOnly: true))
            chamfer = (byte[])file.RawStreams[StreamPath].Clone();
        var chamferDoc = MfcDocument.Parse(chamfer);
        var chamferEdge = Assert.Single(chamferDoc.AllElements.Where(e => e.ClassName == "moEdgeRef_c"));
        byte[] chamferCopy = (byte[])chamfer.Clone();
        chamferCopy[chamferEdge.DataStart + 20] ^= 0xFF;
        var mutatedChamfer = MfcDocument.Parse(chamferCopy);
        var chamferFeature = mutatedChamfer.AllElements.First(e => e.ClassName == "Chamfer_c");
        Assert.Null(FilletChamferFeatureParser.ParseChamfer(
            mutatedChamfer, "Chamfer_c", 0x52, chamferFeature.TagOffset, chamferCopy.Length));
    }
}
