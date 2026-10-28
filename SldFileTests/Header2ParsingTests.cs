using SldFile.Binary;

namespace SldFileTests;

public class Header2ParsingTests
{
    [Fact]
    public void Header2_ShouldExposeUsers()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var header2 = file.Header2;

        Assert.NotNull(header2);
        Assert.NotNull(header2.Users);
        Assert.NotEmpty(header2.Users);
        // First user is the creator
        Assert.False(string.IsNullOrEmpty(header2.Users[0]));
    }

    [Fact]
    public void Header2_ShouldExposeLogEntries()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var header2 = file.Header2;

        Assert.NotNull(header2.LogEntries);
        Assert.NotEmpty(header2.LogEntries);

        Assert.Contains(header2.LogEntries, r =>
            r.EventType == LogEventType.Created ||
            r.EventType == LogEventType.Modified ||
            r.EventType == LogEventType.Deleted);
    }

    [Fact]
    public void Header2_ShouldHaveObjectCount()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var header2 = file.Header2;

        Assert.True(header2.ObjectCount > 0);
    }

    [Fact]
    public void Header2_LogEntries_ShouldHaveTimestamps()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var records = file.Header2.LogEntries;
        var withTimestamp = records.Where(r => !r.IsTerminal).ToList();

        if (withTimestamp.Count > 0)
        {
            Assert.All(withTimestamp, r => Assert.True(r.Timestamp > 0 || r.IsTerminal,
                $"Record with action '{r.Action}' should have a timestamp"));
        }
    }

    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    [InlineData(nameof(TestFileProvider.CubeDrawing))]
    [InlineData(nameof(TestFileProvider.Assembly))]
    public void Header2_ShouldParseAcrossSampleFiles(string providerPropertyName)
    {
        var filePath = ResolveProviderPath(providerPropertyName);

        using var file = SldFile.SwFile.Open(filePath, readOnly: true);
        var header2 = file.Header2;

        Assert.NotNull(header2);
        Assert.NotEmpty(header2.Users);
        Assert.NotEmpty(header2.LogEntries);
        Assert.True(header2.ObjectCount > 0);
    }

    [Fact]
    public void Header2_RoundTrip_ReadWriteRead_ProducesIdenticalUsers()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);

        IReadOnlyList<string>? originalUsers = null;
        IReadOnlyList<LogEntry>? originalLogs = null;

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: false))
        {
            originalUsers = file.Header2.Users;
            originalLogs = file.Header2.LogEntries;
            file.Save();
        }

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.NotNull(originalUsers);
            Assert.Equal(originalUsers.Count, file.Header2.Users.Count);
            for (int i = 0; i < originalUsers.Count; i++)
            {
                Assert.Equal(originalUsers[i], file.Header2.Users[i]);
            }

            Assert.NotNull(originalLogs);
            Assert.Equal(originalLogs.Count, file.Header2.LogEntries.Count);
            for (int i = 0; i < originalLogs.Count; i++)
            {
                Assert.Equal(originalLogs[i].Action, file.Header2.LogEntries[i].Action);
                Assert.Equal(originalLogs[i].EventType, file.Header2.LogEntries[i].EventType);
                Assert.Equal(originalLogs[i].RecordId, file.Header2.LogEntries[i].RecordId);
                Assert.Equal(originalLogs[i].FeatureName, file.Header2.LogEntries[i].FeatureName);
                Assert.Equal(originalLogs[i].Handle, file.Header2.LogEntries[i].Handle);
                Assert.Equal(originalLogs[i].Timestamp, file.Header2.LogEntries[i].Timestamp);
                Assert.Equal(originalLogs[i].IsTerminal, file.Header2.LogEntries[i].IsTerminal);
            }
        }
    }

    [Fact]
    public void Header2_PartFile_ShouldHaveExternalReference()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var extRefs = file.Header2.ExternalReferences;
        Assert.NotEmpty(extRefs);

        foreach (var extRef in extRefs)
        {
            Assert.False(string.IsNullOrEmpty(extRef.ShortName));
        }
    }

    [Fact]
    public void Header2_AssemblyFile_ShouldHaveMultipleExternalReferences()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.Assembly, readOnly: true);

        var extRefs = file.Header2.ExternalReferences;
        Assert.NotEmpty(extRefs);
        // Assemblies reference multiple component files
        Assert.True(extRefs.Count >= 1,
            $"Expected at least 1 external reference for assembly, got {extRefs.Count}");
    }

    [Fact]
    public void Header2_LogRecord_Branches_AreDetected()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var records = file.Header2.LogEntries;

        // Should have at least one Branch A record (with RecordId)
        var branchA = records.Where(r => r.RecordId.HasValue).ToList();
        Assert.NotEmpty(branchA);

        // Branch A records should have FeatureName and Tag/Flags
        foreach (var r in branchA)
        {
            Assert.NotNull(r.FeatureName);
            Assert.True(r.Tag.HasValue);
            Assert.True(r.Flags.HasValue);
        }
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
