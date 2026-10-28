namespace SldFileTests;

public class StreamIntegrationTest
{
    [Fact]
    public void CanReadAndWriteNewStreams()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".SLDPRT");
        File.Copy(TestFileProvider.CubePart, tempFile, true);

        try
        {
            using (var file = SldFile.SwFile.Open(tempFile, readOnly: false))
            {
                Assert.NotNull(file.Header2);
                Assert.NotEmpty(file.Header2.Users);
                Assert.NotEmpty(file.Header2.LogEntries);
                Assert.True(file.Header2.Users.Count > 0);

                Assert.NotNull(file.ModelStamps);
                Assert.True(file.ModelStamps.Timestamp1 != 0);

                Assert.True(file.ConfigurationProperties.Count > 0);
                Assert.True(file.ConfigurationProperties.ContainsKey("Config-0-Properties"));

                Assert.True(file.ZlbContents.Count > 0);

                file.Save();
            }

            using (var file2 = SldFile.SwFile.Open(tempFile, readOnly: true))
            {
                Assert.NotEmpty(file2.Header2.Users);
                Assert.NotEmpty(file2.Header2.LogEntries);
                Assert.True(file2.Header2.Users.Count > 0);
                Assert.True(file2.ModelStamps.Timestamp1 != 0);
                Assert.True(file2.ConfigurationProperties.Count > 0);
                Assert.True(file2.ZlbContents.Count > 0);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
