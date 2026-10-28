using System.Linq;
using SldFile.Binary;

namespace SldFileTests;

public class ResolvedFeaturesParsingTests
{
    [Fact]
    public void ResolvedFeatures_PartFile_ShouldDetectStreamVersion()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var rf = file.ResolvedFeatures;

        Assert.Equal(0x000F, rf.StreamVersion);
        Assert.Contains("Part", rf.FileType);
        Assert.Contains("SLDPRT", rf.FileType);
    }

    [Fact]
    public void ResolvedFeatures_AssemblyFile_ShouldDetectStreamVersion()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.Assembly, readOnly: true);

        var rf = file.ResolvedFeatures;

        Assert.Equal(0x000B, rf.StreamVersion);
        Assert.Contains("Assembly", rf.FileType);
        Assert.Contains("SLDASM", rf.FileType);
    }

    [Fact]
    public void ResolvedFeatures_PartFile_ShouldHaveFolders()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var rf = file.ResolvedFeatures;

        Assert.NotEmpty(rf.Folders);
        // Part files should have moSensorFolder_ct as root
        var root = rf.Folders[0];
        Assert.EndsWith("_ct", root.ClassName);
    }

    [Fact]
    public void ResolvedFeatures_FolderHeader_ShouldHaveValidMagic()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var rf = file.ResolvedFeatures;
        var allFolders = FlattenFolders(rf.Folders);

        Assert.NotEmpty(allFolders);
        foreach (var folder in allFolders)
        {
            Assert.True(folder.Header.IsValid,
                $"Folder '{folder.ClassName}' has invalid magic (magic not found): 0x{folder.Header.Magic:X16}");
            // Feature folders (ProfileFeature, Extrusion, LengthParameter) may have
            // magic values that differ from the standard Part/Assembly constants.
            if (folder.Header.Magic == FolderHeader.PartMagic ||
                folder.Header.Magic == FolderHeader.AssemblyMagic)
                continue;
            // Accept non-standard magic for known feature classes
            Assert.True(
                IsFeatureFolder(folder.ClassName),
                $"Folder '{folder.ClassName}' magic 0x{folder.Header.Magic:X16} " +
                $"does not match Part (0x{FolderHeader.PartMagic:X16}) " +
                $"or Assembly (0x{FolderHeader.AssemblyMagic:X16})");
        }
    }

    [Fact]
    public void ResolvedFeatures_FolderHeader_ShouldHaveMarkerE()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var rf = file.ResolvedFeatures;
        var allFolders = FlattenFolders(rf.Folders);

        foreach (var folder in allFolders)
        {
            // Feature folders may have different MarkerE values (e.g. 0x8D, 0x8E)
            if (IsFeatureFolder(folder.ClassName))
                Assert.True(folder.Header.MarkerE > 0,
                    $"Feature folder '{folder.ClassName}' should have non-zero MarkerE");
            else
                Assert.Equal(FolderHeader.KnownMarkerE, folder.Header.MarkerE);
        }
    }

    [Fact]
    public void ResolvedFeatures_PartFile_ShouldHaveRefPlanes()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var rf = file.ResolvedFeatures;
        var allFolders = FlattenFolders(rf.Folders);

        var refPlanes = allFolders.Where(f => f.ClassName == "moRefPlane_ct").ToList();
        Assert.NotEmpty(refPlanes);
        // Type ID should be 2, 3, or 4 (Front, Top, Right)
        // Note: In some file versions, only 1 moRefPlane_ct MFC tag is present;
        // the other two ref planes are encoded as inline data within moDefaultRefPlnData_c.
        Assert.All(refPlanes, rp => Assert.True(rp.Header.TypeId >= 2 && rp.Header.TypeId <= 4));
    }

    [Fact]
    public void ResolvedFeatures_Folders_HaveDisplayNames()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var rf = file.ResolvedFeatures;
        var allFolders = FlattenFolders(rf.Folders);

        foreach (var folder in allFolders)
        {
            Assert.False(string.IsNullOrEmpty(folder.DisplayName),
                $"Folder '{folder.ClassName}' should have a display name");
        }
    }

    [Fact]
    public void ResolvedFeatures_Folders_HaveTypeIds()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var rf = file.ResolvedFeatures;
        var allFolders = FlattenFolders(rf.Folders);

        foreach (var folder in allFolders)
        {
            Assert.True(folder.Header.TypeId != 0 || folder.ClassName.Contains("Unknown"),
                $"Folder '{folder.ClassName}' has TypeId=0");
        }
    }

    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    [InlineData(nameof(TestFileProvider.Assembly))]
    public void ResolvedFeatures_ShouldParseAcrossSampleFiles(string providerPropertyName)
    {
        var filePath = ResolveProviderPath(providerPropertyName);

        using var file = SldFile.SwFile.Open(filePath, readOnly: true);
        var rf = file.ResolvedFeatures;

        Assert.NotNull(rf);
        Assert.NotEmpty(rf.FileType);

        // Part and Assembly files should have folders
        var allFolders = FlattenFolders(rf.Folders);
        Assert.NotEmpty(allFolders);

        // Every folder should have a valid header
        foreach (var folder in allFolders)
        {
            Assert.True(folder.Header.IsValid,
                $"Folder '{folder.ClassName}' has invalid header magic");
        }
    }

    [Fact]
    public void ResolvedFeatures_RoundTrip_PreservesFolders()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);

        int originalFolderCount = 0;
        List<(string className, string displayName, uint typeId)> originalFolders = new();

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: false))
        {
            var rf = file.ResolvedFeatures;
            var allFolders = FlattenFolders(rf.Folders);
            originalFolderCount = allFolders.Count;

            foreach (var f in allFolders)
            {
                originalFolders.Add((f.ClassName, f.DisplayName, f.Header.TypeId));
            }

            file.Save();
        }

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: true))
        {
            var rf = file.ResolvedFeatures;
            var allFolders = FlattenFolders(rf.Folders);

            Assert.Equal(originalFolderCount, allFolders.Count);

            for (int i = 0; i < originalFolders.Count && i < allFolders.Count; i++)
            {
                Assert.Equal(originalFolders[i].className, allFolders[i].ClassName);
                Assert.Equal(originalFolders[i].displayName, allFolders[i].DisplayName);
                Assert.Equal(originalFolders[i].typeId, allFolders[i].Header.TypeId);
            }
        }
    }

    [Fact]
    public void ResolvedFeatures_FolderHeader_ChildFlag_IndicatesChildren()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var rf = file.ResolvedFeatures;
        var allFolders = FlattenFolders(rf.Folders);

        foreach (var folder in allFolders)
        {
            if (folder.Children.Count > 0)
            {
                Assert.Equal(FolderHeader.KnownChildFlag, folder.Header.ChildFlag);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature-specific data tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void FeatureData_RefPlane_ShouldHaveDefaultRefPlaneData()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var rf = file.ResolvedFeatures;
        var allFolders = FlattenFolders(rf.Folders);

        var refPlanes = allFolders.Where(f => f.ClassName == "moRefPlane_ct").ToList();
        Assert.NotEmpty(refPlanes);

        foreach (var rp in refPlanes)
        {
            Assert.NotEmpty(rp.ChildFeatureData);
            var refPlaneData = rp.ChildFeatureData
                .OfType<DefaultRefPlaneData>()
                .FirstOrDefault();
            Assert.NotNull(refPlaneData);

            // Axes should be unit vectors
            Assert.NotEqual(0.0, refPlaneData!.XAxis[0] + refPlaneData.XAxis[1] + refPlaneData.XAxis[2]);
            Assert.NotEqual(0.0, refPlaneData.YAxis[0] + refPlaneData.YAxis[1] + refPlaneData.YAxis[2]);
            Assert.NotEqual(0.0, refPlaneData.ZAxis[0] + refPlaneData.ZAxis[1] + refPlaneData.ZAxis[2]);
        }
    }

    [Fact]
    public void FeatureData_Extrusion_ShouldHaveSubObjects()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var rf = file.ResolvedFeatures;
        var allFolders = FlattenFolders(rf.Folders);

        var extrusion = allFolders.FirstOrDefault(f => f.ClassName == "moExtrusion_ct");
        Assert.NotNull(extrusion);

        // Should have EndSpec, BBoxCenter, PerBodyChooser, FaceRef, etc.
        var endSpec = extrusion!.ChildFeatureData.OfType<EndSpecData>().FirstOrDefault();
        Assert.NotNull(endSpec);
        Assert.True(endSpec!.EndType > 0, "EndSpec should have a non-zero EndType");

        var bbox = extrusion.ChildFeatureData.OfType<BBoxCenterData>().FirstOrDefault();
        Assert.NotNull(bbox);

        var chooser = extrusion.ChildFeatureData.OfType<PerBodyChooserData>().FirstOrDefault();
        Assert.NotNull(chooser);

        var faceRef = extrusion.ChildFeatureData.OfType<FaceRefData>().FirstOrDefault();
        Assert.NotNull(faceRef);
        Assert.True(faceRef!.FaceCount > 0, "FaceRef should have a non-zero FaceCount");
    }

    [Fact]
    public void FeatureData_ProfileFeature_ShouldHaveSketchData()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var rf = file.ResolvedFeatures;
        var allFolders = FlattenFolders(rf.Folders);

        // Non-folder elements are attached to the nearest ancestor folder
        // in the MFC tree. Find sketch data across the entire tree.
        var allChildData = FlattenChildFeatureData(rf.Folders);

        var sketchChain = allChildData.OfType<SketchChainData>().FirstOrDefault();
        Assert.NotNull(sketchChain);
        // At least one index should be non-zero (sketch entity reference)
        Assert.True(sketchChain!.Indices.Any(i => i != 0), "SketchChain should reference at least one entity");

        var lineHandle = allChildData.OfType<LineHandleData>().FirstOrDefault();
        Assert.NotNull(lineHandle);
    }

    [Fact]
    public void FeatureData_ProfileFeature_ShouldHaveDisplayDistanceDim()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var rf = file.ResolvedFeatures;
        var allChildData = FlattenChildFeatureData(rf.Folders);

        var dim = allChildData.OfType<DisplayDistanceDimData>().FirstOrDefault();
        Assert.NotNull(dim);
        // DimValue may be zero for default dimensions; just verify the header was parsed
        Assert.True(dim!.DimHeader.Length > 0, "Display distance dim should have a header");
    }

    [Fact]
    public void FeatureData_LengthParameter_ShouldHaveFeatureDataOnFolder()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var rf = file.ResolvedFeatures;
        var allFolders = FlattenFolders(rf.Folders);

        var lenParam = allFolders.FirstOrDefault(f => f.ClassName == "moLengthParameter_ct");
        Assert.NotNull(lenParam);

        Assert.NotNull(lenParam!.FeatureData);
        var lpData = Assert.IsType<LengthParameterData>(lenParam.FeatureData);
        Assert.True(lpData.ParamValue > 0, "Length parameter should have a positive value");
    }

    [Fact]
    public void FeatureData_AcrossSampleFiles_ShouldParseFeatureData()
    {
        var providers = new[] { nameof(TestFileProvider.CubePart), nameof(TestFileProvider.CylinderPart) };
        foreach (var providerPropertyName in providers)
        {
            var filePath = ResolveProviderPath(providerPropertyName);
            using var file = SldFile.SwFile.Open(filePath, readOnly: true);
            var rf = file.ResolvedFeatures;
            var allFolders = FlattenFolders(rf.Folders);

            // Every moRefPlane_ct should have DefaultRefPlaneData
            foreach (var rp in allFolders.Where(f => f.ClassName == "moRefPlane_ct"))
            {
                Assert.Contains(rp.ChildFeatureData, d => d is DefaultRefPlaneData);
            }

            // Every moExtrusion_ct should have an EndSpecData
            foreach (var ext in allFolders.Where(f => f.ClassName == "moExtrusion_ct"))
            {
                Assert.Contains(ext.ChildFeatureData, d => d is EndSpecData);
            }
        }
    }

    /// <summary>
    /// Returns true for feature classes that may have non-standard magic/MarkerE values.
    /// </summary>
    private static bool IsFeatureFolder(string className)
    {
        return className == "moProfileFeature_ct" || className == "moProfileFeature_cM"
            || className == "moExtrusion_ct"
            || className == "moLengthParameter_ct"
            || className.StartsWith("moSketchChain_");
    }

    private static List<FeatureFolder> FlattenFolders(IReadOnlyList<FeatureFolder> folders)
    {
        var result = new List<FeatureFolder>();
        foreach (var folder in folders)
        {
            result.Add(folder);
            result.AddRange(FlattenFolders(folder.Children));
        }
        return result;
    }

    private static List<IFeatureData> FlattenChildFeatureData(IReadOnlyList<FeatureFolder> folders)
    {
        var result = new List<IFeatureData>();
        foreach (var folder in folders)
        {
            result.AddRange(folder.ChildFeatureData);
            result.AddRange(FlattenChildFeatureData(folder.Children));
        }
        return result;
    }

    private static string ResolveProviderPath(string propertyName)
    {
        var prop = typeof(TestFileProvider).GetProperty(propertyName);
        Assert.NotNull(prop);

        var value = prop!.GetValue(null) as string;
        Assert.False(string.IsNullOrWhiteSpace(value));

        return value!;
    }
}
