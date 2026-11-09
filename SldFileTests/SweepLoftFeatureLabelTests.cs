using System.Collections.Generic;
using System.Text;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Phase 3 labels are recognized as design-tree feature nodes, but no payload
/// parser exists yet. The mapper must leave FeatureData null instead of
/// manufacturing default values for an unverified layout.
/// </summary>
public class SweepLoftFeatureLabelTests
{
    [Theory]
    [InlineData("moSweep_c")]
    [InlineData("moLoft_c")]
    [InlineData("moBlend_c")]
    [InlineData("moShell_c")]
    [InlineData("moDraft_c")]
    [InlineData("moRib_c")]
    [InlineData("moCombineBodies_c")]
    [InlineData("moSplitBody_c")]
    [InlineData("moDeleteBody_c")]
    public void KnownPhase3Label_IsRecognizedWithoutFeatureData(string className)
    {
        byte[] stream = BuildResolvedFeaturesStream(className, "Feature1");

        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(MfcDocument.Parse(stream), model);

        var folder = Assert.Single(model.Folders);
        Assert.Equal(className, folder.ClassName);
        Assert.Equal("Feature1", folder.DisplayName);
        Assert.Null(folder.FeatureData);
    }

    [Fact]
    public void UnknownFeatureClass_IsNotPromotedToFolderNode()
    {
        byte[] stream = BuildResolvedFeaturesStream("moUnknownPhase3_c", "Feature1");

        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(MfcDocument.Parse(stream), model);

        Assert.Empty(model.Folders);
    }

    [Fact]
    public void MappingResolvedFeatures_DoesNotModifyInputStream()
    {
        byte[] stream = BuildResolvedFeaturesStream("moSweep_c", "Sweep1");
        byte[] before = (byte[])stream.Clone();

        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(MfcDocument.Parse(stream), model);

        Assert.Equal(before, stream);
        Assert.Single(model.Folders);
    }

    private static byte[] BuildResolvedFeaturesStream(string className, string displayName)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(className);
        var bytes = new List<byte> { 0x0F, 0x00 }; // Part stream version
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) });
        bytes.AddRange(ascii);

        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, (byte)displayName.Length });
        bytes.AddRange(Encoding.Unicode.GetBytes(displayName));
        bytes.AddRange(new byte[] { 0x00, 0x00 });

        bytes.AddRange(new byte[64]);
        return bytes.ToArray();
    }
}
