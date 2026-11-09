using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Reading tests for the measured 2026 DeleteBody sample. The sample is the API
/// default-selection result (first body deleted, second kept); only body count/id and
/// the type code are claimed. KeepBodies is not isolated by a controlled variant.
/// </summary>
public class DeleteBodyFeatureReadingTests
{
    [Theory]
    [InlineData("delete_body_second")]
    [InlineData("delete_body_second_resave")]
    public void DeleteBodySample_DecodesBodyReference(string name)
    {
        using var file = SwFile.Open(Resolve(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Name == "DeleteBody"));
        Assert.Equal(FeatureKind.DeleteBody, feature.Kind);
        var definition = Assert.IsType<DeleteBodyFeatureModel>(feature.Model);

        Assert.Equal("moDeleteBody_c", definition.ClassName);
        Assert.Equal(0x57u, definition.SerializedTypeId);
        Assert.Equal(0x57u, definition.TypeId);
        var body = Assert.Single(definition.Bodies!);
        Assert.Equal(SelectionEntityType.Body, body.EntityType);
        Assert.True(body.EntityId > 0);
        Assert.Equal(1, definition.BodiesCount);
        Assert.Null(definition.KeepBodies);

        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.True(definition.Resolution.Bodies);
        Assert.False(definition.Resolution.KeepBodies);
    }

    [Fact]
    public void DeleteBodyParser_RejectsUnknownLayout()
    {
        byte[] stream = BuildDeleteBodyStream(new byte[64]);
        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(MfcDocument.Parse(stream), model);

        var folder = Assert.Single(model.Folders);
        Assert.Equal("moDeleteBody_c", folder.ClassName);
        Assert.Null(folder.FeatureData);

        var element = MfcDocument.Parse(stream).AllElements.First(e => e.ClassName == "moDeleteBody_c");
        Assert.False(FeatureDataRegistry.TryParse(element, out var data));
        Assert.Null(data);
    }

    [Fact]
    public void DeleteBodyParser_DoesNotModifyInputStream()
    {
        using var file = SwFile.Open(Resolve("delete_body_second"), readOnly: true);
        var stream = file.RawStreams.First(k => k.Key.EndsWith("-ResolvedFeatures")).Value;
        byte[] before = (byte[])stream.Clone();
        var element = MfcDocument.Parse(stream).AllElements.First(e => e.ClassName == "moDeleteBody_c");

        Assert.NotNull(DeleteBodyFeatureModel.Parse(element));
        Assert.Equal(before, stream);
    }

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/CombineSplitOptions", name + ".SLDPRT");

    private static byte[] BuildDeleteBodyStream(byte[] payload)
    {
        byte[] ascii = Encoding.ASCII.GetBytes("moDeleteBody_c");
        var bytes = new List<byte> { 0x71, 0x00, 0x00, 0x00, 0x17, 0x00 };
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) });
        bytes.AddRange(ascii);
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, 0x0A });
        bytes.AddRange(Encoding.Unicode.GetBytes("DeleteBody"));
        bytes.AddRange(payload);
        return bytes.ToArray();
    }
}
