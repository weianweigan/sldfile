using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Resources;
using SldFile.Tests.Integration.Framework;
using Xunit;

namespace SldFile.Tests.Integration.Tests;

public class SolidWorksIconResolverIntegrationTests
{
    private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [SolidWorksFact]
    public void Installation_CanDetectSolidWorksOnSystem()
    {
        var inst = SolidWorksInstallation.Current;
        Assert.NotNull(inst);
        Assert.True(Directory.Exists(inst.InstallationDirectory), $"Directory does not exist: {inst.InstallationDirectory}");
        Assert.True(inst.HasStyleBlue, "swStyleBlueu.dll should be found in SolidWorks install directory");
        Assert.NotNull(inst.SwStyleBluePath);
        Assert.True(File.Exists(inst.SwStyleBluePath));
    }

    [SolidWorksFact]
    public void IconResolver_CanExtractSpecificResource()
    {
        var inst = SolidWorksInstallation.Current;
        Assert.NotNull(inst);

        byte[]? bytes = SolidWorksIconResolver.ExtractResource(inst.SwStyleBluePath!, "PNG_FEATURES_EXTRUDE_BOSS");
        Assert.NotNull(bytes);
        Assert.True(bytes!.Length > 8);
        Assert.True(bytes.Take(8).SequenceEqual(PngHeader), "Resource must start with standard PNG header");
    }

    [SolidWorksFact]
    public void IconResolver_GetFeatureIconReturnsValidPng()
    {
        byte[]? extrude = SolidWorksIconResolver.GetFeatureIcon(FeatureKind.Extrusion, "凸台-拉伸1", 20);
        Assert.NotNull(extrude);
        Assert.True(extrude!.Take(8).SequenceEqual(PngHeader));

        byte[]? cut = SolidWorksIconResolver.GetFeatureIcon(FeatureKind.Extrusion, "切除-拉伸1", 20);
        Assert.NotNull(cut);
        Assert.True(cut!.Take(8).SequenceEqual(PngHeader));

        byte[]? sketch = SolidWorksIconResolver.GetFeatureIcon(FeatureKind.Sketch, "草图1", 20);
        Assert.NotNull(sketch);
        Assert.True(sketch!.Take(8).SequenceEqual(PngHeader));

        byte[]? plane = SolidWorksIconResolver.GetFeatureIcon(FeatureKind.ReferencePlane, "前视基准面", 20);
        Assert.NotNull(plane);
        Assert.True(plane!.Take(8).SequenceEqual(PngHeader));

        byte[]? origin = SolidWorksIconResolver.GetFeatureIcon("原点", 20);
        Assert.NotNull(origin);
        Assert.True(origin!.Take(8).SequenceEqual(PngHeader));
    }

    [SolidWorksFact]
    public void IconResolver_DifferentSizesResolveAppropriately()
    {
        byte[]? small = SolidWorksIconResolver.GetFeatureIcon(FeatureKind.Extrusion, "凸台-拉伸1", 20);
        byte[]? medium = SolidWorksIconResolver.GetFeatureIcon(FeatureKind.Extrusion, "凸台-拉伸1", 32);
        byte[]? large = SolidWorksIconResolver.GetFeatureIcon(FeatureKind.Extrusion, "凸台-拉伸1", 40);

        Assert.NotNull(small);
        Assert.NotNull(medium);
        Assert.NotNull(large);

        // Medium (32x32) has larger byte count than small (20x20)
        Assert.True(medium!.Length > small!.Length);
    }
}
