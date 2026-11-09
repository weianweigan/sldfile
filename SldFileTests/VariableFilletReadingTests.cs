using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Controlled reading coverage for the SW2026 variable-radius fillet layout
/// (VarFillet_c / TypeId 0x52) and regression coverage for the constant-radius fillet.
/// </summary>
public class VariableFilletReadingTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "FilletOptions", name + ".SLDPRT");

    private static byte[] ReadStream(string path)
    {
        using var file = SwFile.Open(path, readOnly: true);
        return (byte[])file.RawStreams[ResolvedStream].Clone();
    }

    [Theory]
    [InlineData("fillet_variable_2_4mm")]
    [InlineData("fillet_variable_2_4mm_resave")]
    public void NativeVariableFillet_ParsesTwoControlPointRadii(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Fillet && f.TypeName == "VarFillet_c"));
        Assert.Equal(0x52, feature.Id);
        Assert.Equal("VariableFillet", feature.Name);

        var definition = Assert.IsType<VariableFilletFeatureModel>(feature.Model);
        Assert.Equal("VarFillet_c", definition.ClassName);
        Assert.Equal(0x52u, definition.TypeId);
        Assert.Equal(new[] { 0.002, 0.004 }, definition.ControlPointRadii!.ToArray());
        Assert.Equal(2, definition.ControlPointCount);
        Assert.Equal(0.002, definition.GetControlPointRadiusAtIndex(0)!.Value, 12);
        Assert.Equal(0.004, definition.GetControlPointRadiusAtIndex(1)!.Value, 12);
        Assert.Null(definition.GetControlPointRadiusAtIndex(2));
        Assert.True(definition.IsResolved(nameof(VariableFilletFeatureModel.ControlPointRadii)));
        Assert.True(definition.IsResolved(nameof(VariableFilletFeatureModel.ControlPointCount)));
        var edge = Assert.Single(definition.Edges!);
        Assert.Equal(SelectionEntityType.Edge, edge.EntityType);
        Assert.Equal(4u, edge.EntityId);
        Assert.Equal(0x200u, edge.ClassWord);
        Assert.Equal(0x254Au, edge.PersistentId);
        Assert.Equal(PersistentEdgeReferenceParser.ExpectedArchiveGuid, edge.ArchiveGuid);
        Assert.Equal(4, edge.RecordCount);
        Assert.True(definition.IsResolved(nameof(VariableFilletFeatureModel.Edges)));

        // Not established by the controlled layout: keep unresolved instead of copying native defaults.
        Assert.Null(definition.DefaultRadius);
        Assert.Null(definition.DefaultDistance);
        Assert.Null(definition.TransitionType);
        Assert.Null(definition.OverflowType);
        Assert.Null(definition.FilletEdgeCount);
    }

    [Fact]
    public void VariableFillet_MainAndResaveProduceTheSameDefinition()
    {
        VariableFilletFeatureModel Read(string name)
        {
            using var file = SwFile.Open(SamplePath(name), readOnly: true);
            return Assert.IsType<VariableFilletFeatureModel>(
                Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Fillet && f.TypeName == "VarFillet_c")).Model);
        }

        var main = Read("fillet_variable_2_4mm");
        var resave = Read("fillet_variable_2_4mm_resave");
        Assert.Equal(main.ControlPointRadii!.ToArray(), resave.ControlPointRadii!.ToArray());
        Assert.Equal(main.ControlPointCount, resave.ControlPointCount);
        Assert.Equal(main.IsResolved(nameof(VariableFilletFeatureModel.ControlPointRadii)),
            resave.IsResolved(nameof(VariableFilletFeatureModel.ControlPointRadii)));
    }

    [Theory]
    [InlineData("fillet_5mm")]
    [InlineData("fillet_5mm_resave")]
    public void ConstantFillet_ReadingRemainsUnchanged(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Fillet && f.TypeName == "Fillet_c"));
        Assert.Equal(0x51, feature.Id);
        var definition = Assert.IsType<FilletFeatureModel>(feature.Model);
        Assert.Equal(0.005, definition.DefaultRadius!.Value, 12);
        Assert.True(definition.IsResolved(nameof(FilletFeatureModel.DefaultRadius)));
    }

    [Fact]
    public void ParsersStrictlyDistinguishConstantAndVariableLayouts()
    {
        const string variableSample = "fillet_variable_2_4mm";
        const string constantSample = "fillet_5mm";

        byte[] variable = ReadStream(SamplePath(variableSample));
        var variableDoc = MfcDocument.Parse(variable);
        var variableFeature = variableDoc.AllElements.First(e => e.ClassName == "VarFillet_c");
        Assert.NotNull(FilletChamferFeatureParser.ParseVariableFillet(
            variableDoc, "VarFillet_c", 0x52, variableFeature.TagOffset, variable.Length));
        Assert.Null(FilletChamferFeatureParser.ParseFillet(
            variableDoc, "VarFillet_c", 0x52, variableFeature.TagOffset, variable.Length));

        byte[] constant = ReadStream(SamplePath(constantSample));
        var constantDoc = MfcDocument.Parse(constant);
        var constantFeature = constantDoc.AllElements.First(e => e.ClassName == "Fillet_c");
        Assert.NotNull(FilletChamferFeatureParser.ParseFillet(
            constantDoc, "Fillet_c", 0x51, constantFeature.TagOffset, constant.Length));
        Assert.Null(FilletChamferFeatureParser.ParseVariableFillet(
            constantDoc, "Fillet_c", 0x51, constantFeature.TagOffset, constant.Length));

        // Class/TypeId/layout guards.
        Assert.Null(FilletChamferFeatureParser.ParseVariableFillet(
            variableDoc, "Fillet_c", 0x51, variableFeature.TagOffset, variable.Length));
        Assert.Null(FilletChamferFeatureParser.ParseVariableFillet(
            variableDoc, "VarFillet_c", 0x51, variableFeature.TagOffset, variable.Length));
        Assert.Null(FilletChamferFeatureParser.ParseVariableFillet(
            variableDoc, "VarFillet_c", 0x52, variable.Length, variable.Length));
        Assert.Null(FilletChamferFeatureParser.ParseVariableFillet(
            variableDoc, "VarFillet_c", 0x52, -1, variable.Length));
    }

    [Fact]
    public void VariableFilletTamperedRadiusCopiesAreRejected()
    {
        byte[] original = ReadStream(SamplePath("fillet_variable_2_4mm"));
        var doc = MfcDocument.Parse(original);
        var feature = doc.AllElements.First(e => e.ClassName == "VarFillet_c");
        var radiusDim = doc.AllElements.First(e => e.ClassName == "ThreeDRadiusDim_c");
        var vertexRef = doc.AllElements.First(e => e.ClassName == "moVertexRef_c");

        Assert.NotNull(FilletChamferFeatureParser.ParseVariableFillet(
            doc, "VarFillet_c", 0x52, feature.TagOffset, original.Length));

        void Tamper(int offset)
        {
            byte[] copy = (byte[])original.Clone();
            BitConverter.GetBytes(0.003).CopyTo(copy, offset);
            var tampered = MfcDocument.Parse(copy);
            var tamperedFeature = tampered.AllElements.First(e => e.ClassName == "VarFillet_c");
            Assert.Null(FilletChamferFeatureParser.ParseVariableFillet(
                tampered, "VarFillet_c", 0x52, tamperedFeature.TagOffset, copy.Length));
        }

        Tamper(radiusDim.DataStart + 318);   // first radius positive copy
        Tamper(radiusDim.DataStart + 38);    // first radius named value
        Tamper(vertexRef.DataStart + 904);   // second radius positive copy
        Tamper(vertexRef.DataStart + 864);   // second radius named value

        // Negative copy of the first radius in the vertex block.
        byte[] neg = (byte[])original.Clone();
        BitConverter.GetBytes(-0.003).CopyTo(neg, vertexRef.DataStart + 1136);
        var negDoc = MfcDocument.Parse(neg);
        var negFeature = negDoc.AllElements.First(e => e.ClassName == "VarFillet_c");
        Assert.Null(FilletChamferFeatureParser.ParseVariableFillet(
            negDoc, "VarFillet_c", 0x52, negFeature.TagOffset, neg.Length));

        // Parameter names are part of the layout discriminator.
        byte[] renamed = (byte[])original.Clone();
        renamed[radiusDim.DataStart + 14] = (byte)'9'; // D0 -> D9
        var renamedDoc = MfcDocument.Parse(renamed);
        var renamedFeature = renamedDoc.AllElements.First(e => e.ClassName == "VarFillet_c");
        Assert.Null(FilletChamferFeatureParser.ParseVariableFillet(
            renamedDoc, "VarFillet_c", 0x52, renamedFeature.TagOffset, renamed.Length));

        byte[] renamedSecond = (byte[])original.Clone();
        renamedSecond[vertexRef.DataStart + 838] = (byte)'2'; // D01 -> D21
        var renamedSecondDoc = MfcDocument.Parse(renamedSecond);
        var renamedSecondFeature = renamedSecondDoc.AllElements.First(e => e.ClassName == "VarFillet_c");
        Assert.Null(FilletChamferFeatureParser.ParseVariableFillet(
            renamedSecondDoc, "VarFillet_c", 0x52, renamedSecondFeature.TagOffset, renamedSecond.Length));    }

    [Fact]
    public void VariableFilletUnknownLayoutAndStreamVersionReturnNull()
    {
        byte[] original = ReadStream(SamplePath("fillet_variable_2_4mm"));
        var doc = MfcDocument.Parse(original);
        var feature = doc.AllElements.First(e => e.ClassName == "VarFillet_c");

        byte[] wrongVersion = (byte[])original.Clone();
        BitConverter.GetBytes((ushort)0x71).CopyTo(wrongVersion, 0);
        var wrongDoc = MfcDocument.Parse(wrongVersion);
        var wrongFeature = wrongDoc.AllElements.First(e => e.ClassName == "VarFillet_c");
        Assert.Null(FilletChamferFeatureParser.ParseVariableFillet(
            wrongDoc, "VarFillet_c", 0x52, wrongFeature.TagOffset, wrongVersion.Length));

        // Removing the vertex-reference class tag breaks the measured object sequence.
        byte[] missingVertex = (byte[])original.Clone();
        int vertexAscii = FindAscii(missingVertex, "moVertexRef_c");
        Assert.True(vertexAscii > 0);
        missingVertex[vertexAscii] = (byte)'n';
        var missingDoc = MfcDocument.Parse(missingVertex);
        var missingFeature = missingDoc.AllElements.First(e => e.ClassName == "VarFillet_c");
        Assert.Null(FilletChamferFeatureParser.ParseVariableFillet(
            missingDoc, "VarFillet_c", 0x52, missingFeature.TagOffset, missingVertex.Length));

        // Parsing must not mutate the source buffer.
        Assert.Equal(original, doc.SourceData);
        Assert.NotNull(FilletChamferFeatureParser.ParseVariableFillet(
            MfcDocument.Parse(original), "VarFillet_c", 0x52, feature.TagOffset, original.Length));
    }

    private static int FindAscii(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        for (int i = 0; i <= data.Length - pattern.Length; i++)
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
