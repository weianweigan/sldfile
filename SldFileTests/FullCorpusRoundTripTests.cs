using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class FullCorpusRoundTripTests
{
    private static readonly string TestFilesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "TestFiles");

    [Fact]
    public void MfcArchive_DefinitionStreams_RoundTrip_ByteAccurately()
    {
        // Tests Definition streams across corpus using MfcArchive
        var modelFiles = GetSampleFiles().Take(15);
        int tested = 0;

        foreach (var file in modelFiles)
        {
            var streams = ReadAllStreams(file);
            foreach (var kvp in streams)
            {
                if (kvp.Key.EndsWith("Definition", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] data = kvp.Value;
                    var opaque = new OpaqueObject("DefinitionRoot", data);

                    byte[] output;
                    using (var writer = MfcArchive.CreateWriter(data.Length))
                    {
                        opaque.Serialize(writer);
                        output = writer.ToArray();
                    }

                    Assert.Equal(data, output);
                    tested++;
                }
            }
        }

        Assert.True(tested > 0, "At least one Definition stream should be verified.");
    }

    [Fact]
    public void MfcArchive_ResolvedFeaturesStreams_RoundTrip_ByteAccurately()
    {
        var modelFiles = GetSampleFiles().Take(20);
        int tested = 0;

        foreach (var file in modelFiles)
        {
            var streams = ReadAllStreams(file);
            foreach (var kvp in streams)
            {
                if (kvp.Key.EndsWith("-ResolvedFeatures", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] data = kvp.Value;
                    var opaque = new OpaqueObject("ResolvedFeaturesRoot", data);

                    byte[] output;
                    using (var writer = MfcArchive.CreateWriter(data.Length))
                    {
                        opaque.Serialize(writer);
                        output = writer.ToArray();
                    }

                    Assert.Equal(data, output);
                    tested++;
                }
            }
        }

        Assert.True(tested > 0, "At least one ResolvedFeatures stream should be verified.");
    }

    private static IEnumerable<string> GetSampleFiles()
    {
        if (!Directory.Exists(TestFilesDir))
            return Enumerable.Empty<string>();

        return Directory.EnumerateFiles(TestFilesDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".SLDDRW", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, byte[]> ReadAllStreams(string file)
    {
        try
        {
            using var sw = SldFile.SwFile.Open(file, readOnly: true);
            var prop = typeof(SldFile.SwFile).GetProperty("RawStreams",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (prop?.GetValue(sw) is Dictionary<string, byte[]> dict)
            {
                return new Dictionary<string, byte[]>(dict, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch { }
        return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
    }
}
