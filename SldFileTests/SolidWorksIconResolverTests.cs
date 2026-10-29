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
        [Fact]
        public void Installation_DetectNonExistentReturnsNull()
        {
            var fake = SolidWorksInstallation.Detect(@"C:\NonExistent_Fake_SolidWorks_Dir_12345");
            Assert.Null(fake);
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
        public void IconResolver_WithoutSolidWorks_DoesNotThrow()
        {
            // Verify that extracting from non-existent DLL returns null safely
            byte[]? icon = SolidWorksIconResolver.ExtractResource(@"C:\NonExistent_Fake_Dir\swStyleBlueu.dll", "PNG_FEATURES_EXTRUDE_BOSS");
            Assert.Null(icon);
        }
    }
}
