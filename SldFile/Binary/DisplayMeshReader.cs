using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace SldFile.Binary;

/// <summary>
/// Decodes 3D display mesh tessellations from SolidWorks DisplayLists MFC streams.
/// Follows the 6-channel tessellation specification.
/// </summary>
internal static class DisplayMeshReader
{
    private static readonly byte[] FaceTessClass = Encoding.ASCII.GetBytes("uoTempFaceTessData_c");
    private static readonly byte[] ClassMarker = { 0xFF, 0xFF, 0x01, 0x00 };
    private static readonly byte[] Utf16Marker = { 0xFF, 0xFE, 0xFF };
    private static readonly byte[] InlineColorMarker = { 0x33, 0x80 };
    private static readonly byte[] FeatureRecordMarker = { 0x09, 0x80 };

    private const int CompactHeaderLen = 8;
    private const int ExtendedHeaderLen = 40;
    private const int ChannelHeaderLen = 16;
    private const float MetresToMillimetres = 1000.0f;

    internal sealed class ClassInterval
    {
        public string Name { get; set; } = string.Empty;
        public int ClassOffset { get; set; }
        public int ContentStart { get; set; }
        public int ContentEnd { get; set; }
    }

    internal sealed class RawTable
    {
        public uint[] Strips { get; set; } = Array.Empty<uint>();
        public Vector3[] Positions { get; set; } = Array.Empty<Vector3>();
        public Vector3[] Normals { get; set; } = Array.Empty<Vector3>();

        public List<int> Triangles()
        {
            var triangles = new List<int>();
            int baseIdx = 0;
            foreach (uint stripLen in Strips)
            {
                int len = (int)stripLen;
                for (int i = 0; i < len - 2; i++)
                {
                    if (i % 2 == 0)
                    {
                        triangles.Add(baseIdx + i);
                        triangles.Add(baseIdx + i + 1);
                        triangles.Add(baseIdx + i + 2);
                    }
                    else
                    {
                        triangles.Add(baseIdx + i);
                        triangles.Add(baseIdx + i + 2);
                        triangles.Add(baseIdx + i + 1);
                    }
                }
                baseIdx += len;
            }
            return triangles;
        }

        public int TriangleCount()
        {
            int count = 0;
            foreach (uint len in Strips)
            {
                if (len > 2) count += (int)len - 2;
            }
            return count;
        }
    }

    internal sealed class RawFace
    {
        public RawTable Table { get; set; } = new();
        public int Start { get; set; }
        public int End { get; set; }
        public int MetadataStart { get; set; }
        public int MetadataEnd { get; set; }
        public List<DisplaySurfaceReference> SurfaceReferences { get; } = new();

        public uint? FeatureSourceId()
        {
            if (SurfaceReferences.Count == 0) return null;
            uint first = SurfaceReferences[0].FeatureSourceId;
            foreach (var r in SurfaceReferences)
            {
                if (r.FeatureSourceId != first) return null;
            }
            return first;
        }
    }

    internal sealed class ChannelDesc
    {
        public uint ItemSize { get; set; }
        public uint Kind { get; set; }
        public uint Flags { get; set; }
        public int Count { get; set; }
        public int DataStart { get; set; }
        public int DataEnd { get; set; }
    }

    /// <summary>
    /// Reads and decodes the DisplayMesh from the DisplayLists stream and optional VisualStates stream.
    /// </summary>
    public static DisplayMesh? ReadMesh(byte[] displayListsPayload, byte[]? visualStatesPayload = null)
    {
        if (displayListsPayload == null || displayListsPayload.Length == 0)
            return null;

        var classes = ReadClassIntervals(displayListsPayload);
        var faces = ExtractFaces(displayListsPayload, classes);
        if (faces.Count == 0)
            return null;

        // Resolve appearances
        var featureColors = visualStatesPayload != null && visualStatesPayload.Length > 0
            ? ExtractFeatureColors(visualStatesPayload)
            : new Dictionary<uint, uint>();

        var appearances = ResolveAppearances(displayListsPayload, classes, faces, featureColors);

        return PackMesh(faces, appearances);
    }

    private static List<ClassInterval> ReadClassIntervals(byte[] payload)
    {
        var decls = new List<(int Offset, string Name)>();
        var markers = FindAll(payload, ClassMarker);
        foreach (int offset in markers)
        {
            if (offset + 6 > payload.Length) continue;
            ushort len = BitConverter.ToUInt16(payload, offset + 4);
            if (len < 1 || len > 128 || offset + 6 + len > payload.Length) continue;

            bool valid = true;
            for (int i = 0; i < len; i++)
            {
                byte b = payload[offset + 6 + i];
                if (!(char.IsLetterOrDigit((char)b) || b == '_' || b == '-'))
                {
                    valid = false;
                    break;
                }
            }
            if (!valid) continue;

            string name = Encoding.ASCII.GetString(payload, offset + 6, len);
            decls.Add((offset, name));
        }

        var result = new List<ClassInterval>();
        for (int i = 0; i < decls.Count; i++)
        {
            int nextOffset = (i + 1 < decls.Count) ? decls[i + 1].Offset : payload.Length;
            result.Add(new ClassInterval
            {
                Name = decls[i].Name,
                ClassOffset = decls[i].Offset,
                ContentStart = decls[i].Offset + 6 + decls[i].Name.Length,
                ContentEnd = nextOffset
            });
        }
        return result;
    }

    private static List<RawFace> ExtractFaces(byte[] payload, List<ClassInterval> classes)
    {
        var markers = FindAll(payload, FaceTessClass);
        var faces = new List<RawFace>();

        for (int i = 0; i < markers.Count; i++)
        {
            int marker = markers[i];
            int header = marker + FaceTessClass.Length;
            if (header + 8 > payload.Length) continue;

            uint triCount = BitConverter.ToUInt32(payload, header);
            uint stripCount = BitConverter.ToUInt32(payload, header + 4);

            int limit = payload.Length;
            foreach (var cls in classes)
            {
                if (cls.Name == "uoTempFaceTessData_c" && cls.ClassOffset + 6 == marker)
                {
                    limit = cls.ContentEnd;
                    break;
                }
            }
            if (limit == payload.Length && i + 1 < markers.Count)
            {
                limit = markers[i + 1];
            }

            int hLen = GetHeaderLen(payload, header);
            int start = header + hLen;

            var tables = ParseTableSequence(payload, start, limit);
            if (tables == null || tables.Count == 0) continue;

            if (tables[0].Table.TriangleCount() != triCount || tables[0].Table.Strips.Length != stripCount)
                continue;

            foreach (var (tStart, tEnd, table) in tables)
            {
                faces.Add(new RawFace
                {
                    Table = table,
                    Start = tStart,
                    End = tEnd,
                    MetadataStart = tEnd,
                    MetadataEnd = limit
                });
            }
        }

        AppendReferencedFaces(payload, classes, faces);

        faces.Sort((a, b) => a.Start.CompareTo(b.Start));
        for (int i = 0; i < faces.Count; i++)
        {
            if (i + 1 < faces.Count)
            {
                faces[i].MetadataEnd = Math.Min(faces[i].MetadataEnd, faces[i + 1].Start);
            }
            faces[i].SurfaceReferences.AddRange(ExtractSurfaceReferences(payload, faces[i].MetadataStart, faces[i].MetadataEnd));
        }

        return faces;
    }

    private static int GetHeaderLen(byte[] payload, int header)
    {
        if (header + ExtendedHeaderLen <= payload.Length)
        {
            bool isExtended = BitConverter.ToUInt32(payload, header + 8) == 1
                && BitConverter.ToUInt32(payload, header + 12) == 0
                && BitConverter.ToUInt32(payload, header + 16) == 0
                && BitConverter.ToUInt32(payload, header + 20) != 0;

            if (isExtended)
            {
                bool tailZero = true;
                for (int i = 24; i < ExtendedHeaderLen; i++)
                {
                    if (payload[header + i] != 0) { tailZero = false; break; }
                }
                if (tailZero) return ExtendedHeaderLen;
            }
        }
        return CompactHeaderLen;
    }

    private static bool HeaderAgrees(byte[] payload, int header, RawTable table)
    {
        if (header + 8 > payload.Length) return false;
        uint triCount = BitConverter.ToUInt32(payload, header);
        uint stripCount = BitConverter.ToUInt32(payload, header + 4);
        return triCount == table.TriangleCount() && stripCount == table.Strips.Length;
    }

    private static List<(int Start, int End, RawTable Table)>? ParseTableSequence(byte[] payload, int at, int limit)
    {
        var first = ParseTable(payload, at);
        if (first == null) return null;
        var (fTable, fCursor) = first.Value;
        if (fCursor > limit) return null;

        var result = new List<(int, int, RawTable)> { (at, fCursor, fTable) };
        int cursor = fCursor;

        byte[] needle = { 4, 0, 0, 0 };
        while (cursor + ChannelHeaderLen <= limit)
        {
            int rel = IndexOf(payload, needle, cursor, limit - cursor);
            if (rel < 0) break;

            cursor = rel;
            var next = ParseTable(payload, cursor);
            if (next != null && next.Value.Cursor <= limit)
            {
                result.Add((cursor, next.Value.Cursor, next.Value.Table));
                cursor = next.Value.Cursor;
            }
            else
            {
                cursor += 4;
            }
        }
        return result;
    }

    private static (RawTable Table, int Cursor)? ParseTable(byte[] bytes, int at)
    {
        var channels = new List<ChannelDesc>(6);
        int cursor = at;
        for (int i = 0; i < 6; i++)
        {
            var ch = ReadChannel(bytes, cursor);
            if (ch == null) return null;
            cursor = ch.DataEnd;
            channels.Add(ch);
        }

        var stripsCh = channels[0];
        var posCh = channels[1];
        var normCh = channels[2];

        if (stripsCh.ItemSize != 4 || stripsCh.Kind != 8 || stripsCh.Flags != 2) return null;
        if (posCh.ItemSize != 12 || posCh.Kind != 100 || posCh.Flags != 2) return null;
        if (normCh.ItemSize != 12 || normCh.Kind != 100 || normCh.Flags != 2) return null;

        var stripLengths = new uint[stripsCh.Count];
        int vTotal = 0;
        for (int i = 0; i < stripsCh.Count; i++)
        {
            stripLengths[i] = BitConverter.ToUInt32(bytes, stripsCh.DataStart + i * 4);
            vTotal += (int)stripLengths[i];
        }

        var positions = ReadVectors(bytes, posCh);
        var normals = ReadVectors(bytes, normCh);
        if (positions == null || normals == null) return null;

        if (stripLengths.Length == 0 || positions.Length == 0 || vTotal != positions.Length)
            return null;

        if (normals.Length > 0 && normals.Length != positions.Length)
            return null;

        if (!AuxiliaryChannelsAgree(bytes, stripLengths, channels))
            return null;

        var table = new RawTable
        {
            Strips = stripLengths,
            Positions = positions,
            Normals = normals
        };
        return (table, cursor);
    }

    private static ChannelDesc? ReadChannel(byte[] bytes, int at)
    {
        if (at + ChannelHeaderLen > bytes.Length) return null;
        uint itemSize = BitConverter.ToUInt32(bytes, at);
        uint kind = BitConverter.ToUInt32(bytes, at + 4);
        uint flags = BitConverter.ToUInt32(bytes, at + 8);
        int count = (int)BitConverter.ToUInt32(bytes, at + 12);

        int start = at + ChannelHeaderLen;
        long endLong = (long)start + (long)itemSize * count;
        if (endLong > bytes.Length || endLong < start) return null;

        return new ChannelDesc
        {
            ItemSize = itemSize,
            Kind = kind,
            Flags = flags,
            Count = count,
            DataStart = start,
            DataEnd = (int)endLong
        };
    }

    private static Vector3[]? ReadVectors(byte[] bytes, ChannelDesc channel)
    {
        var result = new Vector3[channel.Count];
        for (int i = 0; i < channel.Count; i++)
        {
            int at = channel.DataStart + i * 12;
            if (at + 12 > bytes.Length) return null;
            float x = BitConverter.ToSingle(bytes, at);
            float y = BitConverter.ToSingle(bytes, at + 4);
            float z = BitConverter.ToSingle(bytes, at + 8);

            if (float.IsNaN(x) || float.IsInfinity(x) ||
                float.IsNaN(y) || float.IsInfinity(y) ||
                float.IsNaN(z) || float.IsInfinity(z))
                return null;

            result[i] = new Vector3(x, y, z);
        }
        return result;
    }

    private static bool AuxiliaryChannelsAgree(byte[] bytes, uint[] strips, List<ChannelDesc> channels)
    {
        if (channels.Count < 6) return false;
        var b = channels[3];
        var c = channels[4];
        var d = channels[5];

        if (b.ItemSize != 4 || b.Kind != 8 || b.Flags != 2) return false;
        if (c.ItemSize != 4 || c.Kind != 8 || c.Flags != 2) return false;
        if (d.ItemSize != 1 || d.Kind != 8 || d.Flags != 2) return false;

        int totalEndpoints = 0;
        var endpointLists = new uint[strips.Length];
        for (int i = 0; i < strips.Length; i++)
        {
            if (strips[i] < 1) return false;
            endpointLists[i] = strips[i] * 2 - 2;
            totalEndpoints += (int)endpointLists[i];
        }

        if (c.Count != strips.Length) return false;
        for (int i = 0; i < c.Count; i++)
        {
            uint stored = BitConverter.ToUInt32(bytes, c.DataStart + i * 4);
            if (stored != endpointLists[i]) return false;
        }

        bool countsAgree = (b.Count == 0 && d.Count == 0) || (b.Count == totalEndpoints && d.Count == totalEndpoints);
        if (!countsAgree) return false;

        for (int i = 0; i < b.Count; i++)
        {
            float f = BitConverter.ToSingle(bytes, b.DataStart + i * 4);
            if (float.IsNaN(f) || float.IsInfinity(f)) return false;
        }

        return true;
    }

    private static void AppendReferencedFaces(byte[] payload, List<ClassInterval> classes, List<RawFace> faces)
    {
        var tags = new HashSet<ushort>();
        foreach (var face in faces)
        {
            foreach (int width in new[] { CompactHeaderLen, ExtendedHeaderLen })
            {
                int at = face.Start - (width + 2);
                if (at < 0) continue;

                ushort tag = BitConverter.ToUInt16(payload, at);
                if ((tag & 0x8000) == 0 || tag == 0xFFFF || tag == 0x8000) continue;

                bool insideFaceClass = false;
                foreach (var cls in classes)
                {
                    if (cls.Name == "uoTempFaceTessData_c" && cls.ContentStart <= at && face.End <= cls.ContentEnd)
                    {
                        insideFaceClass = true;
                        break;
                    }
                }

                if (insideFaceClass && GetHeaderLen(payload, at + 2) == width && HeaderAgrees(payload, at + 2, face.Table))
                {
                    tags.Add(tag);
                }
            }
        }

        if (tags.Count != 1) return;
        ushort targetTag = 0;
        foreach (var t in tags) targetTag = t;

        int first = int.MaxValue;
        var known = new Dictionary<int, int>();
        foreach (var face in faces)
        {
            first = Math.Min(first, face.Start);
            known[face.Start] = face.End;
        }
        if (first == int.MaxValue) return;

        byte tagB0 = (byte)(targetTag & 0xFF);
        byte tagB1 = (byte)((targetTag >> 8) & 0xFF);

        int p = first;
        while (p + 2 < payload.Length)
        {
            if (payload[p] == tagB0 && payload[p + 1] == tagB1)
            {
                int header = p + 2;
                int start = header + GetHeaderLen(payload, header);
                if (known.TryGetValue(start, out int knownEnd))
                {
                    p = knownEnd;
                    continue;
                }

                int limit = payload.Length;
                foreach (var cls in classes)
                {
                    if (cls.ClassOffset > p)
                    {
                        limit = cls.ClassOffset;
                        break;
                    }
                }

                var tableResult = ParseTable(payload, start);
                if (tableResult != null && tableResult.Value.Cursor <= limit && HeaderAgrees(payload, header, tableResult.Value.Table))
                {
                    faces.Add(new RawFace
                    {
                        Table = tableResult.Value.Table,
                        Start = start,
                        End = tableResult.Value.Cursor,
                        MetadataStart = tableResult.Value.Cursor,
                        MetadataEnd = limit
                    });
                    p = tableResult.Value.Cursor;
                    continue;
                }
                p += 2;
            }
            else
            {
                p++;
            }
        }
    }

    private static List<DisplaySurfaceReference> ExtractSurfaceReferences(byte[] payload, int start, int end)
    {
        var refs = new List<DisplaySurfaceReference>();
        var strings = ExtractUtf16Strings(payload, start, end);
        foreach (var text in strings)
        {
            string trimmed = text.Trim();
            var parts = trimmed.Split(',');
            if (parts.Length >= 3 && parts[0].StartsWith("mo") && parts[0].EndsWith("SurfIdRep_c"))
            {
                if (uint.TryParse(parts[1], out uint featId) && featId != 0 && featId != uint.MaxValue)
                {
                    if (uint.TryParse(parts[2], out uint localSurfId))
                    {
                        refs.Add(new DisplaySurfaceReference
                        {
                            Identity = trimmed,
                            FeatureSourceId = featId,
                            LocalSurfaceId = localSurfId
                        });
                    }
                }
            }
        }
        return refs;
    }

    private static List<string> ExtractUtf16Strings(byte[] payload, int start, int end)
    {
        var result = new List<string>();
        int at = start;
        int limit = Math.Min(end, payload.Length);

        while (at + 4 <= limit)
        {
            if (payload[at] == Utf16Marker[0] && payload[at + 1] == Utf16Marker[1] && payload[at + 2] == Utf16Marker[2])
            {
                int charCount = payload[at + 3];
                int textEnd = at + 4 + charCount * 2;
                if (textEnd <= limit)
                {
                    try
                    {
                        string s = Encoding.Unicode.GetString(payload, at + 4, charCount * 2);
                        result.Add(s);
                    }
                    catch { }
                    at = textEnd;
                    continue;
                }
            }
            at++;
        }
        return result;
    }

    private static Dictionary<uint, uint> ExtractFeatureColors(byte[] visualStates)
    {
        var result = new Dictionary<uint, uint>();
        var classes = ReadClassIntervals(visualStates);
        var markers = FindAll(visualStates, FeatureRecordMarker);

        const int FeatureRecordLen = 36;
        const int FeatureMarkerOffset = 30;

        foreach (int marker in markers)
        {
            int at = marker - FeatureMarkerOffset;
            if (at < 0 || at + FeatureRecordLen > visualStates.Length) continue;

            bool inFeatureClass = false;
            foreach (var cls in classes)
            {
                if (cls.Name == "moCompFeature_c" && cls.ContentStart <= at && at + FeatureRecordLen <= cls.ContentEnd)
                {
                    inFeatureClass = true;
                    break;
                }
            }
            if (!inFeatureClass) continue;

            uint version = BitConverter.ToUInt32(visualStates, at);
            if (version != 0x00004268) continue;

            if (BitConverter.ToUInt32(visualStates, at + 12) != 1 ||
                BitConverter.ToUInt32(visualStates, at + 16) != 1 ||
                BitConverter.ToUInt32(visualStates, at + 20) != 2)
                continue;

            // Prefix check
            if (visualStates[at + 24] != 0x07 || visualStates[at + 25] != 0x80 ||
                visualStates[at + 26] != 0x01 || visualStates[at + 27] != 0x00 ||
                visualStates[at + 28] != 0x00 || visualStates[at + 29] != 0x00)
                continue;

            uint source = BitConverter.ToUInt32(visualStates, at + 4);
            uint packedColor = BitConverter.ToUInt32(visualStates, at + 32);

            if (source != 0 && source != uint.MaxValue)
            {
                result[source] = packedColor;
            }
        }
        return result;
    }

    private static List<(string? Name, uint? Color, string? Source)> ResolveAppearances(
        byte[] payload,
        List<ClassInterval> classes,
        List<RawFace> faces,
        Dictionary<uint, uint> featureColors)
    {
        var resolved = new List<(string? Name, uint? Color, string? Source)>(faces.Count);
        for (int i = 0; i < faces.Count; i++) resolved.Add((null, null, null));

        // 1. Body-level properties
        int prevBodyEnd = 0;
        foreach (var cls in classes)
        {
            if (cls.Name == "uoBodyPropInfo_c")
            {
                var defs = ExtractInlineDefinitions(payload, cls.ContentStart, cls.ContentEnd);
                if (defs.Count == 1)
                {
                    var (name, color) = defs[0];
                    for (int i = 0; i < faces.Count; i++)
                    {
                        if (prevBodyEnd <= faces[i].Start && faces[i].End <= cls.ClassOffset)
                        {
                            resolved[i] = (name, color, "Body");
                        }
                    }
                }
                prevBodyEnd = cls.ContentEnd;
            }
        }

        // 2. Feature-level colors
        for (int i = 0; i < faces.Count; i++)
        {
            uint? featId = faces[i].FeatureSourceId();
            if (featId.HasValue && featureColors.TryGetValue(featId.Value, out uint color))
            {
                resolved[i] = (null, color, "Feature");
            }
        }

        // 3. Face-local properties
        for (int i = 0; i < faces.Count; i++)
        {
            ClassInterval? faceCls = null;
            foreach (var cls in classes)
            {
                if (cls.Name == "uoTempFaceTessData_c" && cls.ContentStart <= faces[i].Start && faces[i].Start < cls.ContentEnd)
                {
                    faceCls = cls;
                    break;
                }
            }
            if (faceCls != null)
            {
                int endLimit = Math.Min(faces[i].MetadataEnd, faceCls.ContentEnd);
                var defs = ExtractInlineDefinitions(payload, faces[i].MetadataStart, endLimit);
                if (defs.Count == 1)
                {
                    var (name, color) = defs[0];
                    resolved[i] = (name, color, "Face");
                }
            }
        }

        return resolved;
    }

    private static List<(string Name, uint Color)> ExtractInlineDefinitions(byte[] payload, int start, int end)
    {
        var list = new List<(string, uint)>();
        int limit = Math.Min(end, payload.Length);
        int at = start;

        while (at + 2 <= limit)
        {
            if (payload[at] == InlineColorMarker[0] && payload[at + 1] == InlineColorMarker[1])
            {
                int packedOffset = at + 2;
                if (packedOffset + 4 <= limit)
                {
                    uint color = BitConverter.ToUInt32(payload, packedOffset);
                    int nameHeader = packedOffset + 16;
                    if (nameHeader + 4 <= limit &&
                        payload[nameHeader] == Utf16Marker[0] &&
                        payload[nameHeader + 1] == Utf16Marker[1] &&
                        payload[nameHeader + 2] == Utf16Marker[2])
                    {
                        int charCount = payload[nameHeader + 3];
                        int nameEnd = nameHeader + 4 + charCount * 2;
                        if (nameEnd <= limit)
                        {
                            string name = Encoding.Unicode.GetString(payload, nameHeader + 4, charCount * 2).Trim();
                            if (!string.IsNullOrEmpty(name))
                            {
                                list.Add((name, color));
                            }
                        }
                    }
                }
            }
            at++;
        }
        return list;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
    private struct FloatIntUnion
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public float FloatValue;
        [System.Runtime.InteropServices.FieldOffset(0)]
        public int IntValue;
    }

    private static int SingleToInt32Bits(float value)
    {
        var union = new FloatIntUnion { FloatValue = value };
        return union.IntValue;
    }

    private static List<(int Start, int End)> DeriveOutlineEdges(Vector3[] positions, List<int> triangles)
    {
        // Canonical index map using bit-level float representation
        var firstIndex = new Dictionary<(int, int, int), int>(positions.Length);
        var canonical = new int[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var p = positions[i];
            float px = p.X == 0f ? 0f : p.X;
            float py = p.Y == 0f ? 0f : p.Y;
            float pz = p.Z == 0f ? 0f : p.Z;
            var key = (SingleToInt32Bits(px),
                       SingleToInt32Bits(py),
                       SingleToInt32Bits(pz));

            if (!firstIndex.TryGetValue(key, out int cIdx))
            {
                cIdx = i;
                firstIndex[key] = cIdx;
            }
            canonical[i] = cIdx;
        }

        var order = new List<(int A, int B)>();
        var uses = new Dictionary<(int A, int B), int>();

        for (int t = 0; t < triangles.Count; t += 3)
        {
            int i0 = triangles[t];
            int i1 = triangles[t + 1];
            int i2 = triangles[t + 2];

            int c0 = canonical[i0];
            int c1 = canonical[i1];
            int c2 = canonical[i2];

            AddEdge(c0, c1);
            AddEdge(c1, c2);
            AddEdge(c2, c0);
        }

        void AddEdge(int a, int b)
        {
            if (a == b) return;
            var edgeKey = a < b ? (a, b) : (b, a);
            if (uses.TryGetValue(edgeKey, out int count))
            {
                uses[edgeKey] = count + 1;
            }
            else
            {
                uses[edgeKey] = 1;
                order.Add(edgeKey);
            }
        }

        var outlines = new List<(int, int)>();
        foreach (var edge in order)
        {
            if (uses[edge] == 1)
            {
                outlines.Add(edge);
            }
        }
        return outlines;
    }

    private static Vector3[] DeriveNormals(Vector3[] positions, List<int> triangles)
    {
        var sums = new Vector3[positions.Length];
        for (int t = 0; t < triangles.Count; t += 3)
        {
            int i0 = triangles[t];
            int i1 = triangles[t + 1];
            int i2 = triangles[t + 2];

            Vector3 v0 = positions[i0];
            Vector3 v1 = positions[i1];
            Vector3 v2 = positions[i2];

            Vector3 cross = Vector3.Cross(v1 - v0, v2 - v0);
            sums[i0] += cross;
            sums[i1] += cross;
            sums[i2] += cross;
        }

        var normals = new Vector3[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 n = sums[i];
            if (n.LengthSquared() > 0)
            {
                normals[i] = Vector3.Normalize(n);
            }
            else
            {
                normals[i] = Vector3.UnitZ;
            }
        }
        return normals;
    }

    private static DisplayMesh PackMesh(
        List<RawFace> faces,
        List<(string? Name, uint? Color, string? Source)> appearances)
    {
        var allPositions = new List<Vector3>();
        var allNormals = new List<Vector3>();
        var allIndices = new List<int>();
        var allEdges = new List<(int, int)>();
        var parts = new List<DisplayMeshPart>();

        Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int f = 0; f < faces.Count; f++)
        {
            var face = faces[f];
            var table = face.Table;
            var triangles = table.Triangles();
            int vStart = allPositions.Count;
            int tStart = allIndices.Count / 3;
            int eStart = allEdges.Count;

            // Scale to mm
            for (int i = 0; i < table.Positions.Length; i++)
            {
                var p = table.Positions[i] * MetresToMillimetres;
                allPositions.Add(p);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            bool normalsDerived = table.Normals.Length == 0;
            var normals = normalsDerived
                ? DeriveNormals(table.Positions, triangles)
                : table.Normals;

            allNormals.AddRange(normals);

            // Re-index triangles
            for (int i = 0; i < triangles.Count; i++)
            {
                allIndices.Add(triangles[i] + vStart);
            }

            // Outline edges
            var outlines = DeriveOutlineEdges(table.Positions, triangles);
            foreach (var (e0, e1) in outlines)
            {
                allEdges.Add((e0 + vStart, e1 + vStart));
            }

            var app = appearances[f];
            parts.Add(new DisplayMeshPart
            {
                Id = string.Format(System.Globalization.CultureInfo.InvariantCulture, "displaylist:0:{0}", f),
                StreamIndex = 0,
                TableIndex = f,
                TableOffset = face.Start,
                VertexStart = vStart,
                VertexCount = table.Positions.Length,
                TriangleStart = tStart,
                TriangleCount = triangles.Count / 3,
                EdgeStart = eStart,
                EdgeCount = outlines.Count,
                StripCount = table.Strips.Length,
                NormalsDerived = normalsDerived,
                Color = app.Color,
                ColorSource = app.Source,
                AppearanceName = app.Name,
                SurfaceReferences = face.SurfaceReferences.AsReadOnly()
            });
        }

        return new DisplayMesh
        {
            Positions = allPositions.AsReadOnly(),
            Normals = allNormals.AsReadOnly(),
            TriangleIndices = allIndices.AsReadOnly(),
            OutlineEdges = allEdges.AsReadOnly(),
            Parts = parts.AsReadOnly(),
            Min = min,
            Max = max
        };
    }

    private static List<int> FindAll(byte[] haystack, byte[] needle)
    {
        var result = new List<int>();
        if (needle.Length == 0 || haystack.Length < needle.Length) return result;

        int at = 0;
        while (at <= haystack.Length - needle.Length)
        {
            int found = IndexOf(haystack, needle, at, haystack.Length - at);
            if (found < 0) break;
            result.Add(found);
            at = found + 1;
        }
        return result;
    }

    internal static int IndexOf(byte[] haystack, byte[] needle, int start, int count)
    {
        int end = Math.Min(start + count, haystack.Length) - needle.Length;
        for (int i = start; i <= end; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }
}
