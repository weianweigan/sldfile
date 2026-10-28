namespace SldFileTests;

public class SummaryTest
{
    [Fact]
    public void TestReadSummary()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, true);

        Assert.NotNull(file);
        Assert.NotNull(file.Summary);

        Assert.Equal("weigan", file.Summary.Author);
        Assert.Equal("remark from sldfile", file.Summary.Comments);
        Assert.Equal("sldfile", file.Summary.Keywords);
        Assert.Equal("weigan", file.Summary.LastSavedBy);
        Assert.Equal("sldfile title", file.Summary.Title);
        Assert.Equal("sldfile subject", file.Summary.Subject);
    }

    [Fact]
    public void TestWriteSummary()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: false))
        {
            file.Metadata.Author = "Agentic Coder";
            file.Metadata.Title = "Edited Cube";
            file.Save();
        }

        // Re-read and assert — values should be synchronized to Summary
        using (var fileRead = SldFile.SwFile.Open(temp.FilePath, true))
        {
            Assert.Equal("Agentic Coder", fileRead.Summary.Author);
            Assert.Equal("Edited Cube", fileRead.Summary.Title);
        }
    }
}
