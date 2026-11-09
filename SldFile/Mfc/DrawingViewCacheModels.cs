using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldFile.Drawing;

namespace SldFile.Mfc;

// Wire evidence: uiViewBucket_c in mfc_classes.json; observed cached-edge and
// placement records in the 2012/2026 cube and D8001507933 samples, cross-checked
// against reverse_workspace/libopenswx/src/drawing_view_geometry.cc and
// drawing_sheet_geometry.cc. Unknown intervening fields are retained, not fitted.
internal sealed class DrawingCachedPolylineModel : IMfcSerializable
{
    internal uint Type;
    internal DrawingPreviewPoint[] Points = Array.Empty<DrawingPreviewPoint>();
    internal bool Active => Type == 0x321 || Type == 0x323 || Type == 0x331 || Type == 0x333;
    internal bool Hidden => Type == 0x331 || Type == 0x333;
    internal static bool Known(uint type) => type == 0x301 || type == 0x303 || type == 0x311 || type == 0x313 ||
        type == 0x321 || type == 0x323 || type == 0x331 || type == 0x333;
    public void Serialize(MfcArchive ar)
    {
        ar.U32(ref Type);
        if (!Known(Type)) throw new InvalidDataException();
        uint count = (uint)Points.Length; ar.U32(ref count);
        if (count < 2 || count > 4096 || ar.IsLoading && count > ar.Remaining / 24) throw new InvalidDataException();
        if (ar.IsLoading) Points = new DrawingPreviewPoint[count];
        for (int i = 0; i < Points.Length; i++)
        {
            var p = Points[i]; DrawingPreviewRecordModel.Point(ar, ref p); Points[i] = p;
            if (!Finite(p.X) || !Finite(p.Y) || !Finite(p.Z)) throw new InvalidDataException();
        }
    }
    private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && Math.Abs(v) <= 1e6;
}

internal sealed class DrawingViewPlacementModel : IMfcSerializable
{
    internal string Name = string.Empty;
    internal bool HasRotation;
    internal double[] Matrix = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
    internal DrawingPreviewPoint Translation;
    internal double Scale;
    internal byte[] UnresolvedFields = new byte[213];
    internal double[] Bounds = new double[11];
    public void Serialize(MfcArchive ar)
    {
        ar.Boolean(ref HasRotation);
        if (HasRotation)
            for (int i = 0; i < Matrix.Length; i++) ar.F64(ref Matrix[i]);
        DrawingPreviewRecordModel.Point(ar, ref Translation); ar.F64(ref Scale);
        int unknownLength = HasRotation ? 213 : 69;
        if (ar.IsLoading)
        {
            UnresolvedFields = ar.ReadBytes(unknownLength);
            if (UnresolvedFields.Length != unknownLength) throw new EndOfStreamException();
        }
        else ar.WriteBytes(UnresolvedFields);
        for (int i = 0; i < Bounds.Length; i++) ar.F64(ref Bounds[i]);
        if (!Valid()) throw new InvalidDataException();
    }
    private bool Valid()
    {
        if (Scale <= 0 || Scale > 1000 || double.IsNaN(Scale) || Bounds.Any(v => double.IsNaN(v) || double.IsInfinity(v)) || Math.Abs(Bounds[10]) > 1e-12) return false;
        if (Bounds[0] >= Bounds[2] || Bounds[1] >= Bounds[3] || Bounds[4] >= Bounds[6] || Bounds[5] >= Bounds[7]) return false;
        double[] insets = { Bounds[4] - Bounds[0], Bounds[5] - Bounds[1], Bounds[2] - Bounds[6], Bounds[3] - Bounds[7] };
        if (insets.Any(v => v < 0.0001 || v > 0.02)) return false;
        for (int row = 0; row < 3; row++)
        {
            double norm = 0;
            for (int col = 0; col < 3; col++) norm += Matrix[row * 3 + col] * Matrix[row * 3 + col];
            if (double.IsNaN(norm) || Math.Abs(norm - 1) > 1e-8) return false;
            for (int other = 0; other < row; other++)
            {
                double dot = 0;
                for (int col = 0; col < 3; col++) dot += Matrix[row * 3 + col] * Matrix[other * 3 + col];
                if (Math.Abs(dot) > 1e-8) return false;
            }
        }
        return true;
    }
    internal DrawingPreviewPoint Project(DrawingPreviewPoint p) => new DrawingPreviewPoint(
        Scale * (p.X * Matrix[0] + p.Y * Matrix[3] + p.Z * Matrix[6]) + Translation.X,
        Scale * (p.X * Matrix[1] + p.Y * Matrix[4] + p.Z * Matrix[7]) + Translation.Y);
}

internal sealed class DrawingViewCacheArchive : IMfcSerializable
{
    internal sealed class Fragment
    {
        internal int Offset, Length;
        internal IMfcSerializable Model = null!;
        internal string Owner = string.Empty;
        internal string ReferencedDocument = string.Empty;
    }
    private byte[] _source = Array.Empty<byte>();
    internal readonly List<Fragment> Fragments = new List<Fragment>();
    internal bool IsDefinition;
    internal string[] ViewNames = Array.Empty<string>();
    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            int cursor = 0;
            foreach (var f in Fragments.OrderBy(f => f.Offset))
            {
                ar.WriteBytes(_source.Skip(cursor).Take(f.Offset - cursor).ToArray());
                f.Model.Serialize(ar); cursor = f.Offset + f.Length;
            }
            ar.WriteBytes(_source.Skip(cursor).ToArray()); return;
        }
        _source = ar.ReadRemainingBytes(); Fragments.Clear();
        if (IsDefinition) ReadPlacements(); else ReadBuckets();
    }

    private bool TryRecord(int offset, int end, IMfcSerializable model, out int consumed)
    {
        consumed = 0;
        try
        {
            using var input = MfcArchive.CreateReader(_source, offset);
            model.Serialize(input); consumed = input.Position;
            if (consumed > end - offset) return false;
            var bytes = MfcArchive.Serialize(model);
            return bytes.Length == consumed && _source.AsSpan(offset, consumed).SequenceEqual(bytes);
        }
        catch (Exception e) when (e is InvalidDataException || e is EndOfStreamException || e is OverflowException) { return false; }
    }

    private List<(int Offset, string Value)> Strings(int begin, int end)
    {
        var result = new List<(int, string)>();
        for (int i = begin; i <= end - 4; i++)
        {
            if (_source[i] != 255 || _source[i + 1] != 254 || _source[i + 2] != 255) continue;
            try
            {
                using var ar = MfcArchive.CreateReader(_source, i);
                string? value = null; DrawingPreviewRecordModel.String(ar, ref value);
                if (ar.Position <= end - i && !string.IsNullOrEmpty(value)) result.Add((i, value!));
            }
            catch (Exception e) when (e is InvalidDataException || e is EndOfStreamException || e is OverflowException) { }
        }
        return result;
    }

    private void ReadPlacements()
    {
        var names = Strings(0, _source.Length).Where(s => ViewNames.Contains(s.Value, StringComparer.Ordinal)).ToList();
        var currentNames = names.Where(n => n.Offset >= 2 && _source[n.Offset - 2] == 9 && _source[n.Offset - 1] == 0x89).ToList();
        if (currentNames.Count > 0) names = currentNames;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i <= _source.Length - 190; i++)
        {
            // Native Serialize omits all three identity rotation matrices. The
            // leading flag and both forms are independently checked against COM.
            if (_source[i] > 1) continue;
            int boundsOffset = i + (_source[i] == 1 ? 318 : 102);
            if (boundsOffset > _source.Length - 88) continue;
            // Cheap exact structural gates before deserializing the full placement.
            double x0 = BitConverter.ToDouble(_source, boundsOffset), x1 = BitConverter.ToDouble(_source, boundsOffset + 16);
            double inset = BitConverter.ToDouble(_source, boundsOffset + 32) - x0;
            if (!(x0 < x1) || inset < 0.0001 || inset > 0.02) continue;
            var owner = names.LastOrDefault(n => n.Offset < boundsOffset);
            if (owner.Value == null || boundsOffset - owner.Offset > 100000 || seen.Contains(owner.Value)) continue;
            var placement = new DrawingViewPlacementModel { Name = owner.Value };
            if (!TryRecord(i, _source.Length, placement, out int length)) continue;
            Fragments.Add(new Fragment { Offset = i, Length = length, Model = placement, Owner = owner.Value });
            seen.Add(owner.Value); i += length - 1;
        }
    }

    private bool ValidBucket(int start)
    {
        if (start < 0 || start > _source.Length - 42) return false;
        double x = BitConverter.ToDouble(_source, start), y = BitConverter.ToDouble(_source, start + 8), z = BitConverter.ToDouble(_source, start + 16);
        double norm = x * x + y * y + z * z, scale = BitConverter.ToDouble(_source, start + 32);
        return !double.IsNaN(norm) && Math.Abs(norm - 1) < 1e-8 && scale > 0 && scale < 1e6;
    }

    private void ReadBuckets()
    {
        // The class-array header is at 0 or after the observed 16-byte legacy prefix.
        int first = -1, tagOffset = -1;
        foreach (int candidate in new[] { 4, 20 })
        {
            if (candidate >= _source.Length) continue;
            using var ar = MfcArchive.CreateReader(_source, candidate);
            if (ar.ReadClassTag(out var name, out _, out _) != MfcTagKind.NewClass || name != "uiViewBucket_c") continue;
            tagOffset = candidate; first = candidate + ar.Position; break;
        }
        if (!ValidBucket(first)) return;
        var starts = new List<int> { first };
        for (int i = first + 42; i <= _source.Length - 44; i++)
            if (_source[i] == 1 && _source[i + 1] == 128 && ValidBucket(i + 2)) { starts.Add(i + 2); i++; }
        if (BitConverter.ToUInt32(_source, tagOffset - 4) != starts.Count) return;
        int tail = _source.Length;
        for (int i = starts.Last() + 42; i <= _source.Length - 8; i++)
        {
            if (_source[i] != 255 || _source[i + 1] != 255 || _source[i + 2] != 1 || _source[i + 3] != 0) continue;
            using var ar = MfcArchive.CreateReader(_source, i);
            if (ar.ReadClassTag(out var name, out _, out _) == MfcTagKind.NewClass && name.Length > 0 && name.All(c => char.IsLetterOrDigit(c) || c == '_')) { tail = i; break; }
        }
        for (int b = 0; b < starts.Count; b++)
        {
            int end = b + 1 < starts.Count ? starts[b + 1] - 2 : tail;
            var references = Strings(starts[b] + 42, end).Select(n => n.Value).Where(n => n.Count(c => c == '@') == 1).ToList();
            var owners = references.Select(n => n.Substring(n.IndexOf('@') + 1)).Distinct().ToList();
            string? owner = owners.FirstOrDefault(n => ViewNames.Contains(n, StringComparer.Ordinal));
            if (owner == null && owners.Count == 1) owner = owners[0];
            if (owner == null) continue;
            for (int i = starts[b] + 42; i <= end - 56; i++)
            {
                if (!DrawingCachedPolylineModel.Known(BitConverter.ToUInt32(_source, i))) continue;
                var polyline = new DrawingCachedPolylineModel();
                if (!TryRecord(i, end, polyline, out int length)) continue;
                string document = references.First(n => n.EndsWith("@" + owner, StringComparison.Ordinal));
                document = document.Substring(0, document.IndexOf('@'));
                Fragments.Add(new Fragment { Offset = i, Length = length, Model = polyline, Owner = owner, ReferencedDocument = document });
                i += length - 1;
            }
        }
        // Tessellation is a separate class section, beyond the view-bucket array.
        if (!MfcDocument.Parse(_source).AllElements.Any(e => e.ClassName == "uoTempFaceTessData_c")) return;
        for (int i = tail; i <= _source.Length - 96; i++)
        {
            if (BitConverter.ToUInt32(_source, i) != 4 || BitConverter.ToUInt32(_source, i + 4) != 8 || BitConverter.ToUInt32(_source, i + 8) != 2) continue;
            var table = new DrawingTessellationModel();
            if (!TryRecord(i, _source.Length, table, out int length)) continue;
            Fragments.Add(new Fragment { Offset = i, Length = length, Model = table });
            i += length - 1;
        }
    }
}
