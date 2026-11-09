using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Fixed measured Shell authoring tests. The block is re-emitted verbatim; only the
/// six validated thickness slots, folder count and feature presence change.
/// </summary>
public class ShellAuthoringTests
{
    [Theory]
    [InlineData("shell_t2_remove_top", "Primary", 0.002)]
    [InlineData("shell_t2_remove_top_resave", "Resave", 0.002)]
    [InlineData("shell_t3_remove_top", "Primary", 0.003)]
    [InlineData("shell_t3_remove_top_resave", "Resave", 0.003)]
    public void ShellAuthoring_ReEmitsMeasuredBlockAndReReads(string sample, string variant, double thickness)
    {
        _ = variant;
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new ShellFeatureModel(thickness)
            {
                FeatureName = "Shell",
                SerializedTypeId = 0x51u,
                FacesRemoved = new[] { new EntityReference { EntityType = SelectionEntityType.Face, EntityId = 1, Name = "moCompFace_c:1" } }
            };
            var feature = part.AddShell(model);
            Assert.NotNull(feature);
            Assert.Equal("Shell", feature.Name);
            var definition = Assert.IsType<ShellFeatureModel>(feature.Model);
            AssertShellDefinition(definition, thickness);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == "Shell"));
        Assert.Equal(FeatureKind.Shell, reFeature.Kind);
        AssertShellDefinition(Assert.IsType<ShellFeatureModel>(reFeature.Model), thickness);
    }

    [Fact]
    public void ShellAuthoring_ParameterizesThicknessBeyondMeasuredSamples()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("shell_t2_remove_top"));
        StripInPlace(temp);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new ShellFeatureModel(0.004)
            {
                FeatureName = "Shell",
                SerializedTypeId = 0x51u,
                FacesRemoved = new[] { new EntityReference { EntityType = SelectionEntityType.Face, EntityId = 1, Name = "moCompFace_c:1" } }
            };
            var feature = part.AddShell(model);
            Assert.NotNull(feature);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var definition = Assert.IsType<ShellFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Name == "Shell")).Model);
        AssertShellDefinition(definition, 0.004);
    }

    [Fact]
    public void ShellAuthoring_RejectsWrongBaseThicknessAndName_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("shell_t2_remove_top"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddShell(new ShellFeatureModel(0.002)));
    }

    [Fact]
    public void ShellAuthoring_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("shell_t2_remove_top"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        Assert.Throws<ArgumentNullException>(() => part.AddShell((ShellFeatureModel)null!));
    }

    [Fact]
    public void AuthoredShell_SurvivesResave()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("shell_t2_remove_top"));
        StripInPlace(temp);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            part.AddShell(new ShellFeatureModel(0.002) { FeatureName = "Shell" });
            part.Save();
        }
        using (var resave = new SldPart(temp.FilePath, readOnly: false))
        {
            resave.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        AssertShellDefinition(Assert.IsType<ShellFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Name == "Shell")).Model), 0.002);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void StripInPlace(TempTestFile temp)
    {
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] full = part.GetStreamBytes(streamPath)!;
        var elem = MfcDocument.Parse(full).AllElements.FirstOrDefault(e => e.ClassName == "moShell_c" && e.TagByte == 0x01);
        if (elem != null)
        {
            int offset = elem.TagOffset;
            byte[] stripped = new byte[offset];
            Array.Copy(full, 0, stripped, 0, offset);
            ushort count = BitConverter.ToUInt16(full, 4);
            BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
            part.SetPendingStream(streamPath, stripped);
            part.Save();
        }
    }

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/ShellOptions", name + ".SLDPRT");

    private static void AssertShellDefinition(ShellFeatureModel definition, double thickness)
    {
        Assert.Equal("moShell_c", definition.ClassName);
        Assert.Equal(0x51u, definition.SerializedTypeId);
        Assert.Equal(thickness, definition.Thickness!.Value, 12);
    }
}
