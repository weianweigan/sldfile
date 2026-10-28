using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFileTests;

public class FeatureInspectionTests
{
    private readonly ITestOutputHelper _output;

    public FeatureInspectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InspectResolvedFeaturesFolders2026()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart2026, readOnly: true);
        _output.WriteLine($"2026 ResolvedFeatures folder count: {file.ResolvedFeatures.Folders.Count}");
        void PrintFolder(FeatureFolder folder, int depth)
        {
            var indent = new string(' ', depth * 2);
            _output.WriteLine($"{indent}- [{folder.ClassName}] {folder.DisplayName} (Children: {folder.Children.Count}, ChildData: {folder.ChildFeatureData.Count}, FeatureData: {folder.FeatureData?.GetType().Name})");
            foreach (var cd in folder.ChildFeatureData)
                _output.WriteLine($"{indent}   * [Data] {cd.GetType().Name} ({cd.ClassName})");
            foreach (var child in folder.Children)
                PrintFolder(child, depth + 1);
        }
        foreach (var folder in file.ResolvedFeatures.Folders)
            PrintFolder(folder, 0);
    }

    [Fact]
    public void InspectResolvedFeaturesFolders2012()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        _output.WriteLine($"2012 ResolvedFeatures folder count: {file.ResolvedFeatures.Folders.Count}");
        void PrintFolder(FeatureFolder folder, int depth)
        {
            var indent = new string(' ', depth * 2);
            _output.WriteLine($"{indent}- [{folder.ClassName}] {folder.DisplayName} (Children: {folder.Children.Count}, FeatureData: {folder.FeatureData?.GetType().Name})");
            foreach (var child in folder.Children)
                PrintFolder(child, depth + 1);
        }
        foreach (var folder in file.ResolvedFeatures.Folders)
            PrintFolder(folder, 0);
    }


    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    [Fact]
    public void VerifyEndSpecInExtrusions()
    {
        var samplesDir = @"D:\weigan\repos\sldfile\reverse_workspace\experiments\feature-matrix-20261028\samples";
        foreach (var (fileName, expectedDepth) in new[] { ("extrude_10mm.SLDPRT", 0.010), ("extrude_20mm.SLDPRT", 0.020), ("extrude_30mm.SLDPRT", 0.030) })
        {
            var path = Path.Combine(samplesDir, fileName);
            using var sw = SwFile.Open(path, readOnly: true);
            var streamData = sw.RawStreams["Contents/Config-0-ResolvedFeatures"];
            var doc = MfcDocument.Parse(streamData);
            var root = sw.ResolvedFeatures.Folders.FirstOrDefault();
            Assert.NotNull(root);
            var ext = root!.Children.FirstOrDefault(c => c.ClassName.StartsWith("moExtrusion_"));
            Assert.NotNull(ext);
            var endSpec = ext!.ChildFeatureData.OfType<EndSpecData>().FirstOrDefault();
            Assert.NotNull(endSpec);
            _output.WriteLine($"{fileName}: EndType={endSpec!.EndType}, EndDepth={endSpec.EndDepth}m (Expected {expectedDepth}m)");
            int needleOffset = IndexOf(streamData, BitConverter.GetBytes(expectedDepth));
            var ownerElem = doc.AllElements.LastOrDefault(e => e.TagOffset <= needleOffset);
            _output.WriteLine($"{fileName}: Depth {expectedDepth}m at offset {needleOffset} is inside/after [{ownerElem?.ClassName}] Name='{ownerElem?.AsWChar()}' (TagOffset={ownerElem?.TagOffset})");
            _output.WriteLine(HexDump(streamData, Math.Max(0, needleOffset - 16), 48));
        }
    }

    private static string HexDump(byte[] data, int offset, int length)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < length; i += 16)
        {
            sb.AppendFormat("{0:X4}: ", offset + i);
            for (int j = 0; j < 16; j++)
            {
                if (i + j < length)
                    sb.AppendFormat("{0:X2} ", data[offset + i + j]);
                else
                    sb.Append("   ");
            }
            sb.Append(" ");
            for (int j = 0; j < 16 && (i + j) < length; j++)
            {
                byte b = data[offset + i + j];
                sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
