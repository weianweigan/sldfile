using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Phase 3.2 / 3.3 model-shape tests. No samples exist yet, so the definitions must
/// expose no values and no resolution flags until a validated layout is decoded.
/// Registry/Mapper are intentionally not modified in this round; these tests pin the
/// current "recognized but unparsed" and "reject unknown layout" behavior.
/// </summary>
public class ShellDraftRibCombineSplitModelTests
{
    public static IEnumerable<object[]> Definitions()
    {
        yield return new object[] { new ShellFeatureDefinitionData(), "moShell_c" };
        yield return new object[] { new DraftFeatureDefinitionData(), "moDraft_c" };
        yield return new object[] { new RibFeatureDefinitionData(), "moRib_c" };
        yield return new object[] { new CombineBodiesFeatureDefinitionData(), "moCombineBodies_c" };
        yield return new object[] { new SplitBodyFeatureDefinitionData(), "moSplitBody_c" };
        yield return new object[] { new DeleteBodyFeatureDefinitionData(), "moDeleteBody_c" };
    }

    [Theory]
    [MemberData(nameof(Definitions))]
    public void NewDefinition_HasNoResolvedValuesOrFlags(IFeatureData definition, string expectedClassName)
    {
        Assert.Equal(expectedClassName, definition.ClassName);
        Assert.Equal(0u, definition.TypeId);

        object resolution = definition.GetType().GetProperty("Resolution")!.GetValue(definition)!;
        foreach (var property in resolution.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType != typeof(bool))
                continue;
            Assert.False((bool)property.GetValue(resolution)!,
                $"{definition.ClassName}.{property.Name} must default to false");
        }

        foreach (var property in definition.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name == nameof(IFeatureData.ClassName) ||
                property.Name == nameof(IFeatureData.TypeId) ||
                property.Name == "Resolution")
                continue;

            // Every parameter/reference/type-code field is nullable and unset.
            Assert.Null(property.GetValue(definition));
        }
    }

    [Fact]
    public void AuthoringInputs_DoNotFlipResolutionFlags()
    {
        var shell = new ShellFeatureDefinitionData
        {
            Thickness = 0.002,
            Direction = 0,
            SerializedTypeId = 0x56,
            FacesRemoved = new[] { new EntityReference { EntityId = 7, EntityType = SelectionEntityType.Face } }
        };
        Assert.Equal(0x56u, shell.TypeId);
        Assert.False(shell.Resolution.SerializedTypeId);
        Assert.False(shell.Resolution.Thickness);
        Assert.False(shell.Resolution.Direction);
        Assert.False(shell.Resolution.FacesRemoved);

        var combine = new CombineBodiesFeatureDefinitionData
        {
            OperationType = 0,
            MainBody = new EntityReference { EntityId = 1, EntityType = SelectionEntityType.Body }
        };
        Assert.False(combine.Resolution.OperationType);
        Assert.False(combine.Resolution.MainBody);
        Assert.Equal(0u, combine.TypeId);
    }

    [Theory]
    [InlineData("moShell_c")]
    [InlineData("moDraft_c")]
    [InlineData("moRib_c")]
    [InlineData("moCombineBodies_c")]
    [InlineData("moSplitBody_c")]
    [InlineData("moDeleteBody_c")]
    public void Registry_HasNoPhase32Parser_Yet(string className)
    {
        var document = MfcDocument.Parse(BuildTaggedStream(className, "Feature1"));
        var element = Assert.Single(document.AllElements.Where(e => e.ClassName == className));

        Assert.False(FeatureDataRegistry.TryParse(element, out var data));
        Assert.Null(data);
    }

    [Theory]
    [InlineData("moShell_c")]
    [InlineData("moDraft_c")]
    [InlineData("moRib_c")]
    [InlineData("moCombineBodies_c")]
    [InlineData("moSplitBody_c")]
    [InlineData("moDeleteBody_c")]
    public void Mapper_RecognizesNodeButLeavesFeatureDataNull_WithoutModifyingStream(string className)
    {
        byte[] stream = BuildResolvedFeaturesStream(className, "Feature1");
        byte[] before = (byte[])stream.Clone();

        var model = new ResolvedFeaturesData();
        ResolvedFeaturesSchemaMapper.Map(MfcDocument.Parse(stream), model);

        var folder = Assert.Single(model.Folders);
        Assert.Equal(className, folder.ClassName);
        Assert.Null(folder.FeatureData);
        Assert.Equal(before, stream);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static byte[] BuildTaggedStream(string className, string displayName)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(className);
        var bytes = new List<byte> { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) };
        bytes.AddRange(ascii);
        bytes.AddRange(WChar(displayName));
        bytes.AddRange(new byte[48]);
        return bytes.ToArray();
    }

    private static byte[] BuildResolvedFeaturesStream(string className, string displayName)
    {
        var bytes = new List<byte> { 0x0F, 0x00 }; // Part stream version
        bytes.AddRange(BuildTaggedStream(className, displayName));
        return bytes.ToArray();
    }

    private static byte[] WChar(string value)
    {
        var bytes = new List<byte> { 0xFF, 0xFE, 0xFF, (byte)value.Length };
        bytes.AddRange(Encoding.Unicode.GetBytes(value));
        bytes.AddRange(new byte[] { 0x00, 0x00 });
        return bytes.ToArray();
    }
}
