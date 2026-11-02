using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Negative coverage for the AddExtrude support boundary established after the native audit:
/// authoring requires a 0x70/0x71 ResolvedFeatures stream, a parseable moSketchChain_c and the
/// 0x67 sketch child-count marker. Failures must not modify the pending stream.
/// </summary>
public class ExtrudeAuthoringBoundaryTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static TempTestFile CreateStrippedSketchPart()
    {
        var extPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT");
        var temp = TestFileProvider.CreateTempCopy(extPath);

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var raw = sw.ResolvedFeatures.RawData!;
            byte[] sketchOnly = new byte[0x22E0];
            Array.Copy(raw, 0, sketchOnly, 0, 0x22E0);

            ushort folderCount = BitConverter.ToUInt16(sketchOnly, 4);
            BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(sketchOnly, 4);
            sketchOnly[0x22D4] = 0x00;

            sw.SetPendingStream(ResolvedStream, sketchOnly);
            sw.Save();
        }

        return temp;
    }

    private static byte[] ReadStream(string path)
    {
        using var sw = SwFile.Open(path, readOnly: true);
        return (byte[])sw.RawStreams[ResolvedStream].Clone();
    }

    private static void RewriteStream(TempTestFile temp, Action<byte[]> mutate)
    {
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        mutate(raw);
        sw.SetPendingStream(ResolvedStream, raw);
        sw.Save();
    }

    private static int FindAscii(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    private static void AssertRejectedWithoutChangingStream(TempTestFile temp, Action<SldPart> action, string expectedMessage)
    {
        byte[] before = ReadStream(temp.FilePath);

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => action(part));
            Assert.Contains(expectedMessage, error.Message);
            part.Save();
        }

        Assert.Equal(before, ReadStream(temp.FilePath));
    }

    [Fact]
    public void AddExtrudeRejectsVersion0x6eSketchOnlyPart()
    {
        using var temp = CreateStrippedSketchPart();
        RewriteStream(temp, raw => BitConverter.GetBytes((ushort)0x6E).CopyTo(raw, 0));

        AssertRejectedWithoutChangingStream(temp, part => part.AddExtrude(0.010), "0x6E");
    }

    [Fact]
    public void AddExtrudeRejectsNative0x6eSketchOnlyPart()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", "extrude_sketch_only_0x6e.SLDPRT");
        using var temp = TestFileProvider.CreateTempCopy(path);

        // Exact Agent 5 audit probe: a valid native 0x6E sketch-only part cannot receive a
        // synthesized extrusion feature (previously produced an unloadable, locked file).
        AssertRejectedWithoutChangingStream(temp, part => part.AddExtrude(0.010), "0x6E");
    }

    [Fact]
    public void AddExtrudeRejectsUnconsumedSketchInNative0x70Part()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", "extrude_unconsumed_sketch.SLDPRT");
        using var temp = TestFileProvider.CreateTempCopy(path);

        // Exact Agent 5 audit probe: Sketch2 of this native 0x70 part has no moSketchChain_c,
        // so the previous [1..4] fallback bound wrong topology and native OpenDoc6 failed.
        AssertRejectedWithoutChangingStream(temp, part => part.AddExtrude(0.010), "moSketchChain_c");
    }

    [Fact]
    public void AddExtrudeRejectsSketchWithoutParsedChain()
    {
        using var temp = CreateStrippedSketchPart();
        RewriteStream(temp, raw =>
        {
            int chainTag = FindAscii(raw, "moSketchChain_c");
            Assert.True(chainTag > 0, "the controlled sketch sample must contain moSketchChain_c");
            raw[chainTag] = (byte)'n'; // 'm' -> 'n': tag remains well formed but is not a chain
        });

        AssertRejectedWithoutChangingStream(temp, part => part.AddExtrude(0.010), "moSketchChain_c");
    }

    [Fact]
    public void AddExtrudeRejectsMissingSketchChildCountMarker()
    {
        using var temp = CreateStrippedSketchPart();
        RewriteStream(temp, raw =>
        {
            bool corrupted = false;
            for (int i = raw.Length - 4; i >= Math.Max(0, raw.Length - 256); i--)
            {
                if (raw[i] == 0x67 && raw[i + 1] == 0 && raw[i + 2] == 0 && raw[i + 3] == 0)
                {
                    raw[i] = 0x68;
                    corrupted = true;
                    break;
                }
            }
            Assert.True(corrupted, "the controlled sketch sample must contain the 0x67 marker");
        });

        AssertRejectedWithoutChangingStream(temp, part => part.AddExtrude(0.010), "0x67");
    }

    [Fact]
    public void AddExtrudeRejects0x71SketchWithoutChildCountMarker()
    {
        var cutPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_cut_5mm.SLDPRT");
        using var temp = TestFileProvider.CreateTempCopy(cutPath);

        // Sketch1 of this 0x71 sample has a parsed chain but no 0x67 child-count marker in the
        // supported range; Sketch2 has no chain at all (covered by the next test).
        AssertRejectedWithoutChangingStream(temp, part => part.AddExtrude(0.004, targetSketchName: "草图1"), "0x67");
    }

    [Fact]
    public void AddExtrudeRejects0x71SketchWithoutParsedChain()
    {
        var cutPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_cut_5mm.SLDPRT");
        using var temp = TestFileProvider.CreateTempCopy(cutPath);

        AssertRejectedWithoutChangingStream(temp, part => part.AddExtrude(0.004, isCut: true), "moSketchChain_c");
    }
}
