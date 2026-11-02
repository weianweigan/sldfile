using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;
using Xunit.Abstractions;

namespace SldFileTests;

/// <summary>
/// Task 1.2: 旋转族 (moRevolution_c) 逆向工程探查测试。
/// 用于分析 revolve_360.SLDPRT 的流结构，定位 moRevolution_c 节点的字段偏移。
/// </summary>
public class RevolveFeatureReverseTests
{
    private readonly ITestOutputHelper _output;

    public RevolveFeatureReverseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GetFeatureSamplePath(string fileName)
    {
        var testFilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", fileName);
        if (File.Exists(testFilesPath)) return testFilesPath;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"Sample '{fileName}' not found.");
    }

    private static List<FeatureFolder> FlattenFolders(IReadOnlyList<FeatureFolder> folders)
    {
        var result = new List<FeatureFolder>();
        foreach (var f in folders) { result.Add(f); result.AddRange(FlattenFolders(f.Children)); }
        return result;
    }

    private static string HexDump(byte[] data, int offset, int length)
    {
        var sb = new StringBuilder();
        length = Math.Min(length, data.Length - offset);
        for (int i = 0; i < length; i += 16)
        {
            sb.AppendFormat("{0:X4}: ", offset + i);
            for (int j = 0; j < 16; j++)
                sb.Append((i + j < length) ? $"{data[offset + i + j]:X2} " : "   ");
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

    private static int FindBytes(byte[] haystack, byte[] needle, int startFrom = 0)
    {
        for (int i = startFrom; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
                if (haystack[i + j] != needle[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 1: 列出 revolve_360.SLDPRT 的所有 MFC 文件夹结构
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Revolve_Step1_InspectFolderTree()
    {
        var path = GetFeatureSamplePath("revolve_360.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);

        _output.WriteLine($"=== revolve_360.SLDPRT Feature Tree ===");
        _output.WriteLine($"StreamVersion: 0x{file.ResolvedFeatures.StreamVersion:X4}");
        _output.WriteLine($"FileType: {file.ResolvedFeatures.FileType}");
        _output.WriteLine($"FolderCount (top): {file.ResolvedFeatures.Folders.Count}");
        _output.WriteLine("");

        void Print(FeatureFolder f, int depth)
        {
            var indent = new string(' ', depth * 2);
            _output.WriteLine($"{indent}[{f.Header.TypeId:X2}] {f.ClassName} | '{f.DisplayName}' | TagOff={f.TagOffset} | DataSize={(f.FeatureData != null ? GetRawLen(f.FeatureData) : 0)} | Children={f.Children.Count} | ChildData={f.ChildFeatureData.Count}");
            foreach (var cd in f.ChildFeatureData)
                _output.WriteLine($"{indent}   * {cd.GetType().Name} ({cd.ClassName})");
            foreach (var child in f.Children)
                Print(child, depth + 1);
        }

        foreach (var folder in file.ResolvedFeatures.Folders)
            Print(folder, 0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 2: 定位 moRevolution_c 节点的原始字节并做 hex dump
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Revolve_Step2_HexDumpRevolutionNode()
    {
        var path = GetFeatureSamplePath("revolve_360.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);

        // 读取原始流
        var stream = file.RawStreams["Contents/Config-0-ResolvedFeatures"];
        _output.WriteLine($"Stream length: {stream.Length} bytes");

        // 在流中搜索 "moRevolution_c" 类标签
        byte[] classTag = Encoding.ASCII.GetBytes("moRevolution_c");
        int tagPos = FindBytes(stream, classTag);
        _output.WriteLine($"'moRevolution_c' found at offset: {(tagPos >= 0 ? tagPos.ToString() : "NOT FOUND")}");

        if (tagPos >= 0)
        {
            // MFC 类标签 prefix: FF FF 01 00 [u16 len] [name]
            int tagStart = tagPos - 4; // FF FF 01 00
            _output.WriteLine($"\n=== MFC class tag at 0x{tagStart:X4} ===");
            _output.WriteLine(HexDump(stream, Math.Max(0, tagStart), Math.Min(classTag.Length + 32, stream.Length - tagStart)));

            // 节点数据起始 = tag + name（含4字节前缀+2字节长度+name长度）
            int nodeDataStart = tagPos + classTag.Length;
            _output.WriteLine($"\n=== Node RawData starts at 0x{nodeDataStart:X4} ===");
            _output.WriteLine($"Next 512 bytes:");
            _output.WriteLine(HexDump(stream, nodeDataStart, Math.Min(512, stream.Length - nodeDataStart)));
        }

        // 搜索所有 moRevolution 相关标签
        _output.WriteLine("\n=== All 'Revolution' occurrences in stream ===");
        byte[] searchPattern = Encoding.ASCII.GetBytes("Revolution");
        int pos = 0;
        while (pos < stream.Length)
        {
            int found = FindBytes(stream, searchPattern, pos);
            if (found < 0) break;
            _output.WriteLine($"  Offset 0x{found:X4}: {HexDump(stream, Math.Max(0, found - 4), 40).Trim()}");
            pos = found + 1;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 3: 搜索已知的 360° = 2π 弧度在流中的位置
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Revolve_Step3_Locate360DegreeAngleField()
    {
        var path = GetFeatureSamplePath("revolve_360.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        var stream = file.RawStreams["Contents/Config-0-ResolvedFeatures"];

        // 360° = 2π ≈ 6.283185307179586 in IEEE 754 double
        double twoPi = 2.0 * Math.PI;
        byte[] twoPiBytes = BitConverter.GetBytes(twoPi);
        _output.WriteLine($"Searching for 2π = {twoPi:R} → bytes: {BitConverter.ToString(twoPiBytes)}");

        int found = FindBytes(stream, twoPiBytes);
        if (found >= 0)
        {
            _output.WriteLine($"Found 2π at offset 0x{found:X4} ({found})");
            _output.WriteLine($"\n=== Context around 2π (offset 0x{Math.Max(0, found - 64):X4}) ===");
            _output.WriteLine(HexDump(stream, Math.Max(0, found - 64), 200));
        }
        else
        {
            _output.WriteLine("2π NOT FOUND in stream.");

            // 尝试搜索接近 2π 的值（浮点精度）
            for (int i = 0; i <= stream.Length - 8; i++)
            {
                double val = BitConverter.ToDouble(stream, i);
                if (Math.Abs(val - twoPi) < 1e-10)
                {
                    _output.WriteLine($"Near-match at offset 0x{i:X4}: {val:R}");
                }
            }
        }

        // 同时搜索 π (180°) 用于对比
        double pi = Math.PI;
        byte[] piBytes = BitConverter.GetBytes(pi);
        _output.WriteLine($"\nSearching for π = {pi:R} → bytes: {BitConverter.ToString(piBytes)}");
        int piFound = FindBytes(stream, piBytes);
        if (piFound >= 0)
        {
            _output.WriteLine($"Found π at offset 0x{piFound:X4}");
            _output.WriteLine(HexDump(stream, Math.Max(0, piFound - 16), 64));
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 4: 解析 revolve_360 的完整特征树，打印所有 ChildFeatureData
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Revolve_Step4_FullStreamAnnotation()
    {
        var path = GetFeatureSamplePath("revolve_360.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        var stream = file.RawStreams["Contents/Config-0-ResolvedFeatures"];

        _output.WriteLine($"Total stream bytes: {stream.Length}");
        _output.WriteLine($"\n=== First 128 bytes (stream header) ===");
        _output.WriteLine(HexDump(stream, 0, Math.Min(128, stream.Length)));

        // 列出所有 MFC 类标签在流中的位置
        _output.WriteLine("\n=== All MFC class tags (FF FF 01 00) in stream ===");
        byte[] mfcTagPrefix = { 0xFF, 0xFF, 0x01, 0x00 };
        int pos = 0;
        while (pos < stream.Length - 10)
        {
            int found = FindBytes(stream, mfcTagPrefix, pos);
            if (found < 0) break;

            // 读取类名长度 (u16LE at found+4)
            if (found + 6 < stream.Length)
            {
                ushort nameLen = BitConverter.ToUInt16(stream, found + 4);
                if (nameLen > 0 && nameLen < 128 && found + 6 + nameLen <= stream.Length)
                {
                    string name = Encoding.ASCII.GetString(stream, found + 6, nameLen);
                    // 过滤非ASCII
                    if (name.All(c => c >= 32 && c <= 126))
                        _output.WriteLine($"  0x{found:X4}: '{name}'");
                }
            }
            pos = found + 1;
        }

        // 打印 MFC 文件夹树
        _output.WriteLine("\n=== Parsed Feature Folder Tree ===");
        void Print(FeatureFolder f, int depth)
        {
            var indent = new string(' ', depth * 2);
            _output.WriteLine($"{indent}[{f.Header.TypeId:X2}] {f.ClassName} | '{f.DisplayName}' | Tag@{f.TagOffset:X4}");
            foreach (var cd in f.ChildFeatureData)
            {
                _output.WriteLine($"{indent}   >> {cd.GetType().Name} [{cd.ClassName}] RawLen={GetRawLen(cd)}");
            }
            foreach (var child in f.Children) Print(child, depth + 1);
        }
        foreach (var folder in file.ResolvedFeatures.Folders) Print(folder, 0);
    }

    private static int GetRawLen(IFeatureData fd)
    {
        var rawField = fd.GetType().GetField("RawData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        var raw = rawField?.GetValue(fd) as byte[];
        return raw?.Length ?? 0;
    }

    private static byte[]? GetRaw(IFeatureData fd)
    {
        var rawField = fd.GetType().GetField("RawData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        return rawField?.GetValue(fd) as byte[];
    }

    // ═══════════════════════════════════════════════════════════════════
    // Step 5: 扫描所有 double 值（寻找角度/深度等参数）
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Revolve_Step5_ScanDoubleValuesNearRevolutionTag()
    {
        var path = GetFeatureSamplePath("revolve_360.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        var stream = file.RawStreams["Contents/Config-0-ResolvedFeatures"];

        // 找到 moRevolution_c 或 moRevolution_ct 标签
        byte[] revTag = Encoding.ASCII.GetBytes("moRevolution_c");
        int tagPos = FindBytes(stream, revTag);
        if (tagPos < 0)
        {
            _output.WriteLine("moRevolution_c tag NOT found");
            // 查找 moRevolution_ct
            revTag = Encoding.ASCII.GetBytes("moRevolution_ct");
            tagPos = FindBytes(stream, revTag);
        }

        if (tagPos < 0)
        {
            _output.WriteLine("No moRevolution tag found at all.");
            return;
        }

        _output.WriteLine($"moRevolution tag at 0x{tagPos:X4} ('{Encoding.ASCII.GetString(revTag)}')");
        int dataStart = tagPos + revTag.Length;

        // 找下一个 MFC 标签作为节点结束
        byte[] nextTag = { 0xFF, 0xFF, 0x01, 0x00 };
        int nextTagPos = FindBytes(stream, nextTag, dataStart + 4);
        int nodeEnd = (nextTagPos > 0) ? nextTagPos : Math.Min(dataStart + 2048, stream.Length);

        _output.WriteLine($"Node data: 0x{dataStart:X4} to 0x{nodeEnd:X4} ({nodeEnd - dataStart} bytes)");
        _output.WriteLine("\n=== Full node hex dump ===");
        _output.WriteLine(HexDump(stream, dataStart, nodeEnd - dataStart));

        // 扫描所有8字节对齐的 double 值
        _output.WriteLine("\n=== Double candidates (all 8-byte aligned) ===");
        for (int i = dataStart; i <= nodeEnd - 8; i += 1)
        {
            double val = BitConverter.ToDouble(stream, i);
            // 过滤有意义的值（0.001到1000之间，或接近π的倍数）
            bool isAngle = Math.Abs(val % Math.PI) < 1e-6 && val > 0 && val <= 2 * Math.PI + 0.001;
            bool isLen = val > 0.0001 && val < 10.0; // 可能的长度（米）
            bool isFlag = val == 0.0 || val == 1.0;
            if (isAngle || isLen)
                _output.WriteLine($"  @0x{i:X4}: {val:G17}  {(isAngle ? "[ANGLE?]" : "")}{(isLen ? "[LENGTH?]" : "")}");
        }
    }
}
