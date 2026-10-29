using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Resources;
using Xunit;

namespace SldFileTests
{
    public class SolidWorksIconResolverTests
    {
        private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        [Fact]
        public void Installation_CanDetectSolidWorksOnSystem()
        {
            var inst = SolidWorksInstallation.Current;
            Assert.NotNull(inst);
            Assert.True(Directory.Exists(inst.InstallationDirectory), $"Directory does not exist: {inst.InstallationDirectory}");
            Assert.True(inst.HasStyleBlue, "swStyleBlueu.dll should be found in SolidWorks install directory");
            Assert.NotNull(inst.SwStyleBluePath);
            Assert.True(File.Exists(inst.SwStyleBluePath));
        }

        [Fact]
        public void Installation_DetectNonExistentReturnsNull()
        {
            var fake = SolidWorksInstallation.Detect(@"C:\NonExistent_Fake_SolidWorks_Dir_12345");
            Assert.Null(fake);
        }

        [Fact]
        public void IconResolver_CanExtractSpecificResource()
        {
            var inst = SolidWorksInstallation.Current;
            Assert.NotNull(inst);

            byte[]? bytes = SolidWorksIconResolver.ExtractResource(inst.SwStyleBluePath!, "PNG_FEATURES_EXTRUDE_BOSS");
            Assert.NotNull(bytes);
            Assert.True(bytes!.Length > 8);
            Assert.True(bytes.Take(8).SequenceEqual(PngHeader), "Resource must start with standard PNG header");
        }

        [Theory]
        [InlineData(FeatureKind.Extrusion, "凸台-拉伸1", "PNG_FEATURES_EXTRUDE_BOSS")]
        [InlineData(FeatureKind.Extrusion, "切除-拉伸1", "PNG_FEATURES_EXTRUDE_CUT")]
        [InlineData(FeatureKind.Cut, "切除-拉伸1", "PNG_FEATURES_EXTRUDE_CUT")]
        [InlineData(FeatureKind.Sketch, "草图1", "PNG_SKETCH_SKETCH")]
        [InlineData(FeatureKind.Sketch, "3D草图1", "PNG_SKETCH_3D_SKETCH")]
        [InlineData(FeatureKind.ReferencePlane, "前视基准面", "PNG_REFGEO_PLANE")]
        [InlineData(FeatureKind.Other, "原点", "PNG_TREE_ORIGIN")]
        [InlineData(FeatureKind.Fillet, "圆角1", "PNG_FEATURES_FILLET")]
        [InlineData(FeatureKind.Chamfer, "倒角1", "PNG_FEATURES_CHAMFER")]
        [InlineData(FeatureKind.Revolve, "旋转1", "PNG_FEATURES_REVOLVED_BOSS_BASE")]
        [InlineData(FeatureKind.HoleWizard, "异型孔向导1", "PNG_FEATURES_HOLE_WIZARD")]
        [InlineData(FeatureKind.Folder, "活页夹", "PNG_FEAT_EMPTY_FOLDER")]
        public void IconResolver_ResolvesCorrectResourceName(FeatureKind kind, string featureName, string expectedResName)
        {
            string? res = SolidWorksIconResolver.ResolveResourceName(kind, featureName);
            Assert.Equal(expectedResName, res);
        }

        [Fact]
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

        [Fact]
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
}
