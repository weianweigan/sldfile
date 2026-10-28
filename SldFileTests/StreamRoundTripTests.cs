using SldFile.Binary;

namespace SldFileTests;

public class StreamRoundTripTests
{
    [Fact]
    public void Header2_RoundTrip_FileReopen_Succeeds()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: false))
        {
            Assert.NotEmpty(file.Header2.Users);
            Assert.NotEmpty(file.Header2.LogEntries);
            file.Save();
        }

        // Should open without errors after save
        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.NotEmpty(file.Header2.Users);
            Assert.NotEmpty(file.Header2.LogEntries);
        }
    }

    [Fact]
    public void ResolvedFeatures_RoundTrip_FileReopen_Succeeds()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: false))
        {
            Assert.NotEmpty(file.ResolvedFeatures.Folders);
            file.Save();
        }

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.NotEmpty(file.ResolvedFeatures.Folders);
        }
    }

    [Fact]
    public void CombinedRoundTrip_BothStreams_RemainConsistent()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);

        int userCount, logCount, folderCount;
        ushort rfVersion;

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: false))
        {
            userCount = file.Header2.Users.Count;
            logCount = file.Header2.LogEntries.Count;
            rfVersion = file.ResolvedFeatures.StreamVersion;
            folderCount = FlattenFolders(file.ResolvedFeatures.Folders).Count;

            file.Save();
        }

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.Equal(userCount, file.Header2.Users.Count);
            Assert.Equal(logCount, file.Header2.LogEntries.Count);
            Assert.Equal(rfVersion, file.ResolvedFeatures.StreamVersion);
            Assert.Equal(folderCount, FlattenFolders(file.ResolvedFeatures.Folders).Count);
        }
    }

    [Fact]
    public void RoundTrip_AllSampleFiles()
    {
        var files = new[]
        {
            TestFileProvider.CubePart,
            TestFileProvider.CylinderPart,
            TestFileProvider.CubeDrawing,
            TestFileProvider.Assembly
        };

        foreach (var source in files)
        {
            using var temp = TestFileProvider.CreateTempCopy(source);

            using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: false))
            {
                Assert.NotEmpty(file.Header2.Users);
                Assert.NotEmpty(file.Header2.LogEntries);
                file.Save();
            }

            using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: true))
            {
                Assert.NotEmpty(file.Header2.Users);
                Assert.NotEmpty(file.Header2.LogEntries);
                Assert.NotEmpty(file.Header2.ExternalReferences);
            }
        }
    }

    [Fact]
    public void RoundTrip_OriginalFileShouldStillLoad()
    {
        // Verify the original test file is still loadable after all roundtrip testing
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        Assert.NotEmpty(file.Header2.Users);
        Assert.NotEmpty(file.Header2.LogEntries);
        Assert.NotEmpty(file.ResolvedFeatures.Folders);
    }

    private static List<FeatureFolder> FlattenFolders(IReadOnlyList<FeatureFolder> folders)
    {
        var result = new List<FeatureFolder>();
        foreach (var f in folders)
        {
            result.Add(f);
            result.AddRange(FlattenFolders(f.Children));
        }
        return result;
    }
}
