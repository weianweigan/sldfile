using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SldFile.Mfc;

public readonly struct DrPoint2D : IEquatable<DrPoint2D>
{
    public double X { get; }
    public double Y { get; }

    public DrPoint2D(double x, double y)
    {
        X = x;
        Y = y;
    }

    public bool Equals(DrPoint2D other) => Math.Abs(X - other.X) < 1e-9 && Math.Abs(Y - other.Y) < 1e-9;
    public override bool Equals(object? obj) => obj is DrPoint2D other && Equals(other);
    public override int GetHashCode() => (X, Y).GetHashCode();
    public static bool operator ==(DrPoint2D left, DrPoint2D right) => left.Equals(right);
    public static bool operator !=(DrPoint2D left, DrPoint2D right) => !left.Equals(right);
    public override string ToString() => $"({X:F4}, {Y:F4})";
}

public readonly struct DrVector3D : IEquatable<DrVector3D>
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public DrVector3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public DrVector3D Normalize()
    {
        double len = Length;
        return len > 1e-12 ? new DrVector3D(X / len, Y / len, Z / len) : this;
    }

    public double Dot(DrVector3D other) => X * other.X + Y * other.Y + Z * other.Z;

    public DrVector3D Cross(DrVector3D other) =>
        new DrVector3D(
            Y * other.Z - Z * other.Y,
            Z * other.X - X * other.Z,
            X * other.Y - Y * other.X
        );

    public bool Equals(DrVector3D other) =>
        Math.Abs(X - other.X) < 1e-9 &&
        Math.Abs(Y - other.Y) < 1e-9 &&
        Math.Abs(Z - other.Z) < 1e-9;

    public override bool Equals(object? obj) => obj is DrVector3D other && Equals(other);
    public override int GetHashCode() => (X, Y, Z).GetHashCode();
    public static bool operator ==(DrVector3D left, DrVector3D right) => left.Equals(right);
    public static bool operator !=(DrVector3D left, DrVector3D right) => !left.Equals(right);
    public static DrVector3D operator *(DrVector3D v, double s) => new(v.X * s, v.Y * s, v.Z * s);
    public static DrVector3D operator +(DrVector3D a, DrVector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static DrVector3D operator -(DrVector3D a, DrVector3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4})";
}

public sealed class DrCachedLineSegment
{
    public DrPoint2D Start { get; set; }
    public DrPoint2D End { get; set; }
    public bool IsHidden { get; set; }
    public string Color { get; set; } = "#000000";

    public DrCachedLineSegment() { }

    public DrCachedLineSegment(DrPoint2D start, DrPoint2D end, bool isHidden = false, string color = "#000000")
    {
        Start = start;
        End = end;
        IsHidden = isHidden;
        Color = color;
    }
}

public sealed class DrCachedPolyline
{
    public List<DrPoint2D> Points { get; } = new();
    public bool IsHidden { get; set; }
    public string Color { get; set; } = "#000000";
    public byte RecordType { get; set; }

    public DrCachedPolyline() { }

    public DrCachedPolyline(IEnumerable<DrPoint2D> points, bool isHidden = false, byte recordType = 0x21)
    {
        Points.AddRange(points);
        IsHidden = isHidden;
        RecordType = recordType;
        Color = isHidden ? "#888888" : "#000000";
    }
}

/// <summary>
/// Drawing sheet MFC model (moDrSheet, swFmDrSheet = 40).
/// Bi-directionally serializes sheet metadata, paper dimensions, and coordinate frames.
/// </summary>
public sealed class DrSheetMfcModel : IMfcSerializable
{
    public const string MfcClassName = "moDrSheet";
    public const uint TypeId = 40; // swFmDrSheet

    public string SheetName { get; set; } = "Sheet1";
    public string PlaneName { get; set; } = "Plane1";
    public string SketchName { get; set; } = "Sketch1";
    public double Width { get; set; } = 0.420;  // Standard A3 default (420mm)
    public double Height { get; set; } = 0.297; // Standard A3 default (297mm)
    public double Scale { get; set; } = 1.0;

    public List<DrViewMfcModel> Views { get; } = new();
    public List<DrNoteMfcModel> Notes { get; } = new();
    public List<DrDimensionMfcModel> Dimensions { get; } = new();
    public List<DrCachedLineSegment> FormatLines { get; } = new();

    public byte[]? RawData { get; set; }

    public DrSheetMfcModel() { }

    public DrSheetMfcModel(string sheetName, double width = 0.420, double height = 0.297)
    {
        SheetName = sheetName;
        Width = width;
        Height = height;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(MfcClassName, ref tag, schema: 1);

            string? name = SheetName;
            ar.WChar(ref name);

            string? plane = PlaneName;
            ar.WChar(ref plane);

            string? skt = SketchName;
            ar.WChar(ref skt);

            double w = Width;
            ar.F64(ref w);

            double h = Height;
            ar.F64(ref h);

            double s = Scale;
            ar.F64(ref s);

            ushort viewCount = (ushort)Views.Count;
            ar.U16(ref viewCount);
            foreach (var view in Views)
            {
                view.Serialize(ar);
            }

            ushort noteCount = (ushort)Notes.Count;
            ar.U16(ref noteCount);
            foreach (var note in Notes)
            {
                note.Serialize(ar);
            }

            ushort dimCount = (ushort)Dimensions.Count;
            ar.U16(ref dimCount);
            foreach (var dim in Dimensions)
            {
                dim.Serialize(ar);
            }
        }
        else
        {
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length == 0) return;

            var reader = new MfcReader(data);
            if (ar.Remaining >= 2)
            {
                ar.ReadClassTag(out _, out _, out _);
            }

            var strings = new List<string>();
            var doubles = new List<double>();

            while (reader.Remaining > 0)
            {
                if (reader.IsWCharPrefix)
                {
                    string? s = reader.ReadWChar();
                    if (!string.IsNullOrWhiteSpace(s)) strings.Add(s!);
                }
                else if (reader.Remaining >= 8)
                {
                    double d = BitConverter.ToDouble(reader.Data, reader.Offset);
                    if (!double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) >= 0.001 && Math.Abs(d) <= 10.0)
                    {
                        doubles.Add(d);
                        reader.Skip(8);
                        continue;
                    }
                    reader.Skip(1);
                }
                else
                {
                    reader.Skip(1);
                }
            }

            if (strings.Count > 0) SheetName = strings[0];
            if (strings.Count > 1) PlaneName = strings[1];
            if (strings.Count > 2) SketchName = strings[2];

            // Assign standard dimensions if detected
            foreach (var d in doubles)
            {
                if (d >= 0.1 && d <= 2.0)
                {
                    if (Width == 0.420 && d != 0.420) Width = d;
                    else if (Height == 0.297 && d != 0.297) Height = d;
                }
            }
        }
    }
}

/// <summary>
/// Drawing sheet template MFC model (moDrTemplate_c, swFmDrTemplate = 41).
/// Reads sheet template format lines and title blocks.
/// </summary>
public sealed class DrTemplateMfcModel : IMfcSerializable
{
    public const string MfcClassName = "moDrTemplate_c";
    public const uint TypeId = 41;

    public string TemplateName { get; set; } = string.Empty;
    public double Width { get; set; } = 0.420;
    public double Height { get; set; } = 0.297;
    public List<DrCachedLineSegment> FormatLines { get; } = new();

    public byte[]? RawData { get; set; }

    public DrTemplateMfcModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(MfcClassName, ref tag, schema: 1);
            string? name = TemplateName;
            ar.WChar(ref name);
            double w = Width;
            ar.F64(ref w);
            double h = Height;
            ar.F64(ref h);
        }
        else
        {
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length == 0) return;

            var reader = new MfcReader(data);
            while (reader.Remaining > 0)
            {
                if (reader.IsWCharPrefix)
                {
                    string? s = reader.ReadWChar();
                    if (!string.IsNullOrWhiteSpace(s) && string.IsNullOrEmpty(TemplateName))
                    {
                        TemplateName = s!;
                    }
                }
                else
                {
                    reader.Skip(1);
                }
            }
        }
    }
}

/// <summary>
/// Drawing view MFC model (moDrView_c, moDrAbsoluteView_c, moDrUnfoldedView_c).
/// Stores drawing view position, orientation, scale, and referenced model.
/// </summary>
public sealed class DrViewMfcModel : IMfcSerializable
{
    public const string GenericViewClassName = "moView_c";
    public const string AbsoluteViewClassName = "moAbsoluteView";
    public const string UnfoldedViewClassName = "moUnfoldedView";

    public string ClassName { get; set; } = AbsoluteViewClassName;
    public string ViewName { get; set; } = string.Empty;
    public string ViewType { get; set; } = "AbsoluteView";
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Scale { get; set; } = 1.0;
    public string ReferencedModel { get; set; } = string.Empty;
    public string ReferencedConfiguration { get; set; } = string.Empty;

    public List<DrCachedLineSegment> Lines { get; } = new();
    public byte[]? RawData { get; set; }

    public DrViewMfcModel() { }

    public DrViewMfcModel(string viewName, double posX, double posY, string viewType = "AbsoluteView")
    {
        ViewName = viewName;
        PositionX = posX;
        PositionY = posY;
        ViewType = viewType;
        ClassName = viewType == "UnfoldedView" ? UnfoldedViewClassName : AbsoluteViewClassName;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(ClassName, ref tag, schema: 1);

            string? name = ViewName;
            ar.WChar(ref name);

            string? type = ViewType;
            ar.WChar(ref type);

            double x = PositionX;
            ar.F64(ref x);

            double y = PositionY;
            ar.F64(ref y);

            double s = Scale;
            ar.F64(ref s);

            string? model = ReferencedModel;
            ar.WChar(ref model);

            string? cfg = ReferencedConfiguration;
            ar.WChar(ref cfg);

            ushort lineCount = (ushort)Lines.Count;
            ar.U16(ref lineCount);
            foreach (var line in Lines)
            {
                double sx = line.Start.X;
                double sy = line.Start.Y;
                double ex = line.End.X;
                double ey = line.End.Y;
                bool hidden = line.IsHidden;
                ar.F64(ref sx);
                ar.F64(ref sy);
                ar.F64(ref ex);
                ar.F64(ref ey);
                ar.Boolean(ref hidden);
            }
        }
        else
        {
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length == 0) return;

            var reader = new MfcReader(data);
            var strings = new List<string>();
            var doubles = new List<double>();

            while (reader.Remaining > 0)
            {
                if (reader.IsWCharPrefix)
                {
                    string? s = reader.ReadWChar();
                    if (!string.IsNullOrWhiteSpace(s)) strings.Add(s!);
                }
                else if (reader.Remaining >= 8)
                {
                    double d = BitConverter.ToDouble(reader.Data, reader.Offset);
                    if (!double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) >= 0.0001 && Math.Abs(d) <= 10.0)
                    {
                        doubles.Add(d);
                        reader.Skip(8);
                        continue;
                    }
                    reader.Skip(1);
                }
                else
                {
                    reader.Skip(1);
                }
            }

            foreach (var s in strings)
            {
                if (s.StartsWith("*") || s.Contains("View") || s.Contains("视") || s.Contains("轴测"))
                {
                    if (string.IsNullOrEmpty(ViewName)) ViewName = s;
                }
                else if (s.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase) || s.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase))
                {
                    ReferencedModel = s;
                }
                else if (ReferencedConfiguration == string.Empty && !s.Contains(" ") && s.Length > 1 && s.Length < 30)
                {
                    ReferencedConfiguration = s;
                }
            }

            if (doubles.Count >= 2)
            {
                PositionX = doubles[0];
                PositionY = doubles[1];
            }
            if (doubles.Count >= 3 && doubles[2] > 0.01 && doubles[2] <= 100.0)
            {
                Scale = doubles[2];
            }
        }
    }
}

/// <summary>
/// Drawing note / annotation MFC model (moDisplayNote_c, moCompNote_c).
/// Reads text, font, leader line geometry, and balloon properties.
/// </summary>
public sealed class DrNoteMfcModel : IMfcSerializable
{
    public const string DisplayNoteClassName = "moDisplayNote_c";
    public const string CompNoteClassName = "moCompNote_c";

    public string Text { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsBalloon { get; set; }
    public string BalloonShape { get; set; } = "Circle";
    public List<DrPoint2D> LeaderPoints { get; } = new();

    public byte[]? RawData { get; set; }

    public DrNoteMfcModel() { }

    public DrNoteMfcModel(string text, double posX, double posY, bool isBalloon = false)
    {
        Text = text;
        PositionX = posX;
        PositionY = posY;
        IsBalloon = isBalloon;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(DisplayNoteClassName, ref tag, schema: 1);

            string? txt = Text;
            ar.WChar(ref txt);

            string? item = ItemName;
            ar.WChar(ref item);

            double x = PositionX;
            ar.F64(ref x);

            double y = PositionY;
            ar.F64(ref y);

            double w = Width;
            ar.F64(ref w);

            double h = Height;
            ar.F64(ref h);

            bool balloon = IsBalloon;
            ar.Boolean(ref balloon);

            string? shape = BalloonShape;
            ar.WChar(ref shape);
        }
        else
        {
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length == 0) return;

            var reader = new MfcReader(data);
            var strings = new List<string>();
            var doubles = new List<double>();

            while (reader.Remaining > 0)
            {
                if (reader.IsWCharPrefix)
                {
                    string? s = reader.ReadWChar();
                    if (!string.IsNullOrWhiteSpace(s)) strings.Add(s!);
                }
                else if (reader.Remaining >= 8)
                {
                    double d = BitConverter.ToDouble(reader.Data, reader.Offset);
                    if (!double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) >= 0.0001 && Math.Abs(d) <= 10.0)
                    {
                        doubles.Add(d);
                        reader.Skip(8);
                        continue;
                    }
                    reader.Skip(1);
                }
                else
                {
                    reader.Skip(1);
                }
            }

            foreach (var s in strings)
            {
                if (s.StartsWith("DetailItem") || s.StartsWith("NoteItem"))
                {
                    ItemName = s;
                }
                else if (string.IsNullOrEmpty(Text))
                {
                    Text = s;
                    if (int.TryParse(s, out _) && s.Length <= 3)
                    {
                        IsBalloon = true;
                    }
                }
            }

            if (doubles.Count >= 2)
            {
                PositionX = doubles[0];
                PositionY = doubles[1];
            }
            if (doubles.Count >= 4)
            {
                Width = doubles[2];
                Height = doubles[3];
            }
        }
    }
}

/// <summary>
/// Drawing dimension MFC model (moDimBackupDef_c, moDisplayDistanceDim_c).
/// Decodes dimension value, text override, witness and leader lines.
/// </summary>
public sealed class DrDimensionMfcModel : IMfcSerializable
{
    public const string DimBackupDefClassName = "moDimBackupDef_c";
    public const string DisplayDistanceDimClassName = "moDisplayDistanceDim_c";

    public string Text { get; set; } = string.Empty;
    public double? Value { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public List<DrCachedLineSegment> LeaderLines { get; } = new();

    public byte[]? RawData { get; set; }

    public DrDimensionMfcModel() { }

    public DrDimensionMfcModel(string text, double? value, double posX, double posY)
    {
        Text = text;
        Value = value;
        PositionX = posX;
        PositionY = posY;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(DimBackupDefClassName, ref tag, schema: 1);

            string? txt = Text;
            ar.WChar(ref txt);

            bool hasVal = Value.HasValue;
            ar.Boolean(ref hasVal);
            if (hasVal)
            {
                double v = Value!.Value;
                ar.F64(ref v);
            }

            double x = PositionX;
            ar.F64(ref x);

            double y = PositionY;
            ar.F64(ref y);

            ushort lineCount = (ushort)LeaderLines.Count;
            ar.U16(ref lineCount);
            foreach (var line in LeaderLines)
            {
                double sx = line.Start.X;
                double sy = line.Start.Y;
                double ex = line.End.X;
                double ey = line.End.Y;
                bool hidden = line.IsHidden;
                ar.F64(ref sx);
                ar.F64(ref sy);
                ar.F64(ref ex);
                ar.F64(ref ey);
                ar.Boolean(ref hidden);
            }
        }
        else
        {
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length == 0) return;

            var reader = new MfcReader(data);
            var strings = new List<string>();
            var doubles = new List<double>();

            while (reader.Remaining > 0)
            {
                if (reader.IsWCharPrefix)
                {
                    string? s = reader.ReadWChar();
                    if (!string.IsNullOrWhiteSpace(s)) strings.Add(s!);
                }
                else if (reader.Remaining >= 8)
                {
                    double d = BitConverter.ToDouble(reader.Data, reader.Offset);
                    if (!double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) >= 0.0001 && Math.Abs(d) <= 1000.0)
                    {
                        doubles.Add(d);
                        reader.Skip(8);
                        continue;
                    }
                    reader.Skip(1);
                }
                else
                {
                    reader.Skip(1);
                }
            }

            foreach (var s in strings)
            {
                if (s.Contains("\"") || s.Contains("mm") || char.IsDigit(s[0]))
                {
                    Text = s;
                    break;
                }
            }

            if (doubles.Count > 0)
            {
                Value = doubles[0];
            }
            if (doubles.Count >= 3)
            {
                PositionX = doubles[1];
                PositionY = doubles[2];
            }
        }
    }
}

/// <summary>
/// Drawing display bucket MFC model (uiViewBucket_c in Contents/VBLists).
/// Symmetrically reads and writes vector display lists and cached tessellation polylines.
/// </summary>
public sealed class DrViewBucketMfcModel : IMfcSerializable
{
    public const string MfcClassName = "uiViewBucket_c";

    public DrVector3D Direction { get; set; } = new(0, 0, 1);
    public DrVector3D UpVector { get; set; } = new(0, 1, 0);
    public double Scale { get; set; } = 1.0;
    public uint Flags { get; set; }
    public string ReferencedViewName { get; set; } = string.Empty;

    public DrVector3D HorizontalBasis { get; private set; } = new(1, 0, 0);
    public DrVector3D VerticalBasis { get; private set; } = new(0, 1, 0);

    public List<DrCachedLineSegment> Lines { get; } = new();
    public List<DrCachedPolyline> Polylines { get; } = new();

    public IReadOnlyList<DrCachedLineSegment> VisibleLines => Lines.Where(l => !l.IsHidden).ToList();
    public IReadOnlyList<DrCachedLineSegment> HiddenLines => Lines.Where(l => l.IsHidden).ToList();

    public byte[]? RawData { get; set; }

    public DrViewBucketMfcModel()
    {
        ComputeBasis();
    }

    public void ComputeBasis()
    {
        var dir = Direction.Normalize();
        var up = UpVector.Normalize();
        if (Math.Abs(dir.Dot(up)) > 0.99)
        {
            up = Math.Abs(dir.Y) < 0.9 ? new DrVector3D(0, 1, 0) : new DrVector3D(1, 0, 0);
        }
        HorizontalBasis = up.Cross(dir).Normalize();
        VerticalBasis = dir.Cross(HorizontalBasis).Normalize();
    }

    public DrPoint2D Project(DrVector3D pt)
    {
        double u = pt.Dot(HorizontalBasis) * Scale;
        double v = pt.Dot(VerticalBasis) * Scale;
        return new DrPoint2D(u, v);
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(MfcClassName, ref tag, schema: 1);

            double dx = Direction.X;
            double dy = Direction.Y;
            double dz = Direction.Z;
            ar.F64(ref dx);
            ar.F64(ref dy);
            ar.F64(ref dz);

            double s = Scale;
            ar.F64(ref s);

            uint f = Flags;
            ar.U32(ref f);

            string? viewName = ReferencedViewName;
            ar.WChar(ref viewName);

            ushort count = (ushort)Lines.Count;
            ar.U16(ref count);
            foreach (var line in Lines)
            {
                double sx = line.Start.X;
                double sy = line.Start.Y;
                double ex = line.End.X;
                double ey = line.End.Y;
                bool hidden = line.IsHidden;
                ar.F64(ref sx);
                ar.F64(ref sy);
                ar.F64(ref ex);
                ar.F64(ref ey);
                ar.Boolean(ref hidden);
            }
        }
        else
        {
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length == 0) return;

            ParseBucketWireFormat(data);
        }
    }

    public void ParseBucketWireFormat(byte[] data)
    {
        Lines.Clear();
        Polylines.Clear();

        if (data.Length >= 24)
        {
            double dx = BitConverter.ToDouble(data, 0);
            double dy = BitConverter.ToDouble(data, 8);
            double dz = BitConverter.ToDouble(data, 16);
            if (!double.IsNaN(dx) && !double.IsInfinity(dx) && (Math.Abs(dx) > 1e-6 || Math.Abs(dy) > 1e-6 || Math.Abs(dz) > 1e-6))
            {
                Direction = new DrVector3D(dx, dy, dz).Normalize();
            }
        }

        if (data.Length >= 40)
        {
            double scale = BitConverter.ToDouble(data, 32);
            if (!double.IsNaN(scale) && !double.IsInfinity(scale) && scale > 1e-4 && scale < 1000.0)
            {
                Scale = scale;
            }
        }

        ComputeBasis();

        int viewMarker = -1;
        for (int i = 0; i + 3 < data.Length && i < 200; i++)
        {
            if (data[i] == 0xFF && data[i + 1] == 0xFE && data[i + 2] == 0xFF)
            {
                viewMarker = i + 3;
                break;
            }
        }
        if (viewMarker >= 0 && viewMarker + 2 < data.Length)
        {
            int strLen = data[viewMarker] | (data[viewMarker + 1] << 8);
            if (strLen > 0 && strLen < 128 && viewMarker + 2 + strLen * 2 <= data.Length)
            {
                ReferencedViewName = System.Text.Encoding.Unicode.GetString(data, viewMarker + 2, strLen * 2);
            }
        }

        int scanOffset = 48;
        while (scanOffset + 8 <= data.Length)
        {
            byte recordType = data[scanOffset];
            bool isVisible = (recordType == 0x21 || recordType == 0x23);
            bool isHidden = (recordType == 0x31 || recordType == 0x33);

            if (isVisible || isHidden)
            {
                int ptCount = BitConverter.ToInt32(data, scanOffset + 4);
                if (ptCount >= 2 && ptCount <= 10000 && scanOffset + 8 + ptCount * 24 <= data.Length)
                {
                    var polyPoints = new List<DrPoint2D>(ptCount);
                    int ptOffset = scanOffset + 8;
                    for (int p = 0; p < ptCount; p++)
                    {
                        double px = BitConverter.ToDouble(data, ptOffset);
                        double py = BitConverter.ToDouble(data, ptOffset + 8);
                        double pz = BitConverter.ToDouble(data, ptOffset + 16);
                        polyPoints.Add(Project(new DrVector3D(px, py, pz)));
                        ptOffset += 24;
                    }

                    var poly = new DrCachedPolyline(polyPoints, isHidden, recordType);
                    Polylines.Add(poly);

                    for (int i = 0; i + 1 < polyPoints.Count; i++)
                    {
                        Lines.Add(new DrCachedLineSegment(polyPoints[i], polyPoints[i + 1], isHidden, poly.Color));
                    }

                    scanOffset = ptOffset;
                    continue;
                }
            }

            scanOffset += 4;
        }

        if (Lines.Count == 0)
        {
            var reader = new MfcReader(data);
            var points = new List<DrPoint2D>();
            while (reader.Remaining >= 16)
            {
                double x = BitConverter.ToDouble(reader.Data, reader.Offset);
                double y = BitConverter.ToDouble(reader.Data, reader.Offset + 8);
                if (!double.IsNaN(x) && !double.IsInfinity(x) &&
                    !double.IsNaN(y) && !double.IsInfinity(y) &&
                    Math.Abs(x) >= 0.001 && Math.Abs(x) <= 5.0 &&
                    Math.Abs(y) >= 0.001 && Math.Abs(y) <= 5.0)
                {
                    points.Add(new DrPoint2D(x, y));
                    reader.Skip(16);
                }
                else
                {
                    reader.Skip(4);
                }
            }

            for (int i = 0; i + 1 < points.Count; i += 2)
            {
                Lines.Add(new DrCachedLineSegment(points[i], points[i + 1]));
            }
        }
    }

    public static IReadOnlyList<DrViewBucketMfcModel> ParseAllBuckets(byte[] streamData)
    {
        var buckets = new List<DrViewBucketMfcModel>();
        if (streamData == null || streamData.Length == 0) return buckets;

        byte[] payload = streamData;
        if (streamData.Length >= 8 &&
            streamData[0] == '_' && streamData[1] == '_' && streamData[2] == 'Z' && streamData[3] == 'L' && streamData[4] == 'B')
        {
            var zlb = new SldFile.Binary.ZlbData();
            SldFile.Binary.ZlbStreamHandler.Decompress(streamData, zlb);
            if (zlb.RawData != null && zlb.RawData.Length > 0)
            {
                payload = zlb.RawData;
            }
        }

        var doc = MfcDocument.Parse(payload);
        foreach (var el in doc.AllElements)
        {
            if (el.ClassName == MfcClassName)
            {
                var bucket = new DrViewBucketMfcModel();
                int available = Math.Min(payload.Length - el.DataStart, Math.Max(el.DataLength, 128));
                byte[] bucketSlice = new byte[available];
                Array.Copy(payload, el.DataStart, bucketSlice, 0, available);
                bucket.ParseBucketWireFormat(bucketSlice);
                buckets.Add(bucket);
            }
        }

        if (buckets.Count == 0 && payload.Length > 0)
        {
            var bucket = new DrViewBucketMfcModel();
            bucket.ParseBucketWireFormat(payload);
            if (bucket.Lines.Count > 0 || bucket.Polylines.Count > 0)
            {
                buckets.Add(bucket);
            }
        }

        return buckets;
    }
}

/// <summary>
/// Root Drawing Document MFC Model (moDrawing_c).
/// Manages all sheets, templates, views, notes, and dimensions extracted from Contents/Definition.
/// </summary>
public sealed class DrawingDocumentMfcModel : IMfcSerializable
{
    public const string MfcClassName = "moDrawing_c";

    public List<DrSheetMfcModel> Sheets { get; } = new();
    public List<DrTemplateMfcModel> Templates { get; } = new();
    public List<DrViewMfcModel> Views { get; } = new();
    public List<DrNoteMfcModel> Notes { get; } = new();
    public List<DrDimensionMfcModel> Dimensions { get; } = new();
    public List<DrBalloonMfcModel> Balloons { get; } = new();
    public List<DrGtolMfcModel> Gtols { get; } = new();
    public List<DrDatumMfcModel> Datums { get; } = new();
    public List<DrBomTableMfcModel> BomTables { get; } = new();

    public byte[]? RawData { get; set; }

    public DrawingDocumentMfcModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(MfcClassName, ref tag, schema: 1);

            ushort sheetCount = (ushort)Sheets.Count;
            ar.U16(ref sheetCount);
            foreach (var s in Sheets) s.Serialize(ar);

            ushort tmplCount = (ushort)Templates.Count;
            ar.U16(ref tmplCount);
            foreach (var t in Templates) t.Serialize(ar);

            ushort viewCount = (ushort)Views.Count;
            ar.U16(ref viewCount);
            foreach (var v in Views) v.Serialize(ar);

            ushort noteCount = (ushort)Notes.Count;
            ar.U16(ref noteCount);
            foreach (var n in Notes) n.Serialize(ar);

            ushort dimCount = (ushort)Dimensions.Count;
            ar.U16(ref dimCount);
            foreach (var d in Dimensions) d.Serialize(ar);

            ushort balloonCount = (ushort)Balloons.Count;
            ar.U16(ref balloonCount);
            foreach (var b in Balloons) b.Serialize(ar);

            ushort gtolCount = (ushort)Gtols.Count;
            ar.U16(ref gtolCount);
            foreach (var g in Gtols) g.Serialize(ar);

            ushort datumCount = (ushort)Datums.Count;
            ar.U16(ref datumCount);
            foreach (var dt in Datums) dt.Serialize(ar);

            ushort bomCount = (ushort)BomTables.Count;
            ar.U16(ref bomCount);
            foreach (var bt in BomTables) bt.Serialize(ar);
        }
        else
        {
            ushort tag = 0;
            if (ar.Remaining >= 2)
            {
                ar.ReadClassTag(out _, out tag, out _);
            }

            if (ar.Remaining >= 2)
            {
                ushort sheetCount = 0; ar.U16(ref sheetCount);
                Sheets.Clear();
                for (int i = 0; i < sheetCount; i++)
                {
                    var s = new DrSheetMfcModel(); s.Serialize(ar); Sheets.Add(s);
                }
            }

            if (ar.Remaining >= 2)
            {
                ushort tmplCount = 0; ar.U16(ref tmplCount);
                Templates.Clear();
                for (int i = 0; i < tmplCount; i++)
                {
                    var t = new DrTemplateMfcModel(); t.Serialize(ar); Templates.Add(t);
                }
            }

            if (ar.Remaining >= 2)
            {
                ushort viewCount = 0; ar.U16(ref viewCount);
                Views.Clear();
                for (int i = 0; i < viewCount; i++)
                {
                    var v = new DrViewMfcModel(); v.Serialize(ar); Views.Add(v);
                }
            }

            if (ar.Remaining >= 2)
            {
                ushort noteCount = 0; ar.U16(ref noteCount);
                Notes.Clear();
                for (int i = 0; i < noteCount; i++)
                {
                    var n = new DrNoteMfcModel(); n.Serialize(ar); Notes.Add(n);
                }
            }

            if (ar.Remaining >= 2)
            {
                ushort dimCount = 0; ar.U16(ref dimCount);
                Dimensions.Clear();
                for (int i = 0; i < dimCount; i++)
                {
                    var d = new DrDimensionMfcModel(); d.Serialize(ar); Dimensions.Add(d);
                }
            }

            if (ar.Remaining >= 2)
            {
                ushort bCount = 0; ar.U16(ref bCount);
                Balloons.Clear();
                for (int i = 0; i < bCount; i++)
                {
                    var b = new DrBalloonMfcModel(); b.Serialize(ar); Balloons.Add(b);
                }
            }

            if (ar.Remaining >= 2)
            {
                ushort gCount = 0; ar.U16(ref gCount);
                Gtols.Clear();
                for (int i = 0; i < gCount; i++)
                {
                    var g = new DrGtolMfcModel(); g.Serialize(ar); Gtols.Add(g);
                }
            }

            if (ar.Remaining >= 2)
            {
                ushort dCount = 0; ar.U16(ref dCount);
                Datums.Clear();
                for (int i = 0; i < dCount; i++)
                {
                    var dt = new DrDatumMfcModel(); dt.Serialize(ar); Datums.Add(dt);
                }
            }

            if (ar.Remaining >= 2)
            {
                ushort bomCount = 0; ar.U16(ref bomCount);
                BomTables.Clear();
                for (int i = 0; i < bomCount; i++)
                {
                    var bt = new DrBomTableMfcModel(); bt.Serialize(ar); BomTables.Add(bt);
                }
            }
        }
    }

    public static DrawingDocumentMfcModel FromBytes(byte[] data)
    {
        var model = new DrawingDocumentMfcModel();
        if (data != null && data.Length > 0)
        {
            try
            {
                var doc = MfcDocument.Parse(data);
                model.PopulateFromDocument(doc);
                if (model.Sheets.Count > 0 || model.Views.Count > 0 || model.Notes.Count > 0)
                {
                    return model;
                }
            }
            catch { }

            using var ar = MfcArchive.CreateReader(data);
            model.Serialize(ar);
        }
        return model;
    }

    internal void PopulateFromDocument(MfcDocument doc)
    {
        Sheets.Clear();
        Templates.Clear();
        Views.Clear();
        Notes.Clear();
        Dimensions.Clear();
        Balloons.Clear();
        Gtols.Clear();
        Datums.Clear();
        BomTables.Clear();

        foreach (var el in doc.AllElements)
        {
            if (el.ClassName == DrSheetMfcModel.MfcClassName)
            {
                var sheet = new DrSheetMfcModel();
                MfcArchive.DeserializeInto(el.RawData, sheet);
                Sheets.Add(sheet);
            }
            else if (el.ClassName == DrTemplateMfcModel.MfcClassName)
            {
                var tmpl = new DrTemplateMfcModel();
                MfcArchive.DeserializeInto(el.RawData, tmpl);
                Templates.Add(tmpl);
            }
            else if (el.ClassName is DrViewMfcModel.AbsoluteViewClassName or DrViewMfcModel.UnfoldedViewClassName or DrViewMfcModel.GenericViewClassName)
            {
                var view = new DrViewMfcModel { ClassName = el.ClassName };
                MfcArchive.DeserializeInto(el.RawData, view);
                if (el.ClassName == DrViewMfcModel.AbsoluteViewClassName) view.ViewType = "AbsoluteView";
                else if (el.ClassName == DrViewMfcModel.UnfoldedViewClassName) view.ViewType = "UnfoldedView";
                else view.ViewType = "StandardView";

                if (!string.IsNullOrEmpty(view.ViewName) && !Views.Any(v => v.ViewName == view.ViewName))
                {
                    Views.Add(view);
                }
            }
            else if (el.ClassName is DrNoteMfcModel.DisplayNoteClassName or DrNoteMfcModel.CompNoteClassName)
            {
                var note = new DrNoteMfcModel();
                MfcArchive.DeserializeInto(el.RawData, note);
                if (!string.IsNullOrEmpty(note.Text))
                {
                    if (!Notes.Any(n => n.Text == note.Text && Math.Abs(n.PositionX - note.PositionX) < 1e-4 && Math.Abs(n.PositionY - note.PositionY) < 1e-4))
                    {
                        Notes.Add(note);
                    }
                }
            }
            else if (el.ClassName is DrDimensionMfcModel.DimBackupDefClassName or DrDimensionMfcModel.DisplayDistanceDimClassName)
            {
                var dim = new DrDimensionMfcModel();
                MfcArchive.DeserializeInto(el.RawData, dim);
                if (!string.IsNullOrEmpty(dim.Text) || (dim.Value.HasValue && dim.Value.Value > 0.0001 && dim.Value.Value < 100.0))
                {
                    if (string.IsNullOrEmpty(dim.Text) && dim.Value.HasValue)
                    {
                        dim.Text = dim.Value.Value.ToString("F2", CultureInfo.InvariantCulture);
                    }
                    Dimensions.Add(dim);
                }
            }
            else if (el.ClassName == DrBalloonMfcModel.MfcClassName)
            {
                var balloon = new DrBalloonMfcModel();
                MfcArchive.DeserializeInto(el.RawData, balloon);
                if (!string.IsNullOrEmpty(balloon.ItemNumber))
                {
                    Balloons.Add(balloon);
                }
            }
            else if (el.ClassName == DrGtolMfcModel.MfcClassName)
            {
                var gtol = new DrGtolMfcModel();
                MfcArchive.DeserializeInto(el.RawData, gtol);
                if (!string.IsNullOrEmpty(gtol.Symbol))
                {
                    Gtols.Add(gtol);
                }
            }
            else if (el.ClassName == DrDatumMfcModel.MfcClassName)
            {
                var datum = new DrDatumMfcModel();
                MfcArchive.DeserializeInto(el.RawData, datum);
                if (!string.IsNullOrEmpty(datum.DatumLabel))
                {
                    Datums.Add(datum);
                }
            }
            else if (el.ClassName is DrBomTableMfcModel.MfcClassName or DrBomTableMfcModel.TableTemplateClassName)
            {
                var bom = new DrBomTableMfcModel();
                MfcArchive.DeserializeInto(el.RawData, bom);
                if (!string.IsNullOrEmpty(bom.TableName) || bom.Columns.Count > 0)
                {
                    BomTables.Add(bom);
                }
            }
        }
    }
}

/// <summary>
/// Direct strongly-typed MFC model for drawing balloon annotation (<c>moBalloon_c</c>).
/// Implements IMfcSerializable for symmetric bi-directional reading and writing.
/// </summary>
public sealed class DrBalloonMfcModel : IMfcSerializable
{
    public const string MfcClassName = "moBalloon_c";

    public string ItemNumber { get; set; } = "1";
    public string BalloonShape { get; set; } = "Circle";
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Size { get; set; } = 0.01;
    public string? CustomText { get; set; }
    public uint AttachedEntityId { get; set; }
    public List<DrPoint2D> LeaderPoints { get; } = new();

    public byte[]? RawData { get; set; }

    public DrBalloonMfcModel() { }

    public DrBalloonMfcModel(string itemNumber, double posX, double posY, string shape = "Circle")
    {
        ItemNumber = itemNumber;
        PositionX = posX;
        PositionY = posY;
        BalloonShape = shape;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(MfcClassName, ref tag, schema: 1);

            string? item = ItemNumber; ar.WChar(ref item);
            string? shape = BalloonShape; ar.WChar(ref shape);
            double x = PositionX; ar.F64(ref x);
            double y = PositionY; ar.F64(ref y);
            double sz = Size; ar.F64(ref sz);
            string? custom = CustomText ?? string.Empty; ar.WChar(ref custom);
            uint entId = AttachedEntityId; ar.U32(ref entId);

            ushort lCount = (ushort)LeaderPoints.Count;
            ar.U16(ref lCount);
            foreach (var pt in LeaderPoints)
            {
                double px = pt.X; ar.F64(ref px);
                double py = pt.Y; ar.F64(ref py);
            }
        }
        else
        {
            ushort refTag = 0;
            ar.ReadClassTag(out _, out refTag, out _);

            string? item = null; ar.WChar(ref item); ItemNumber = item ?? "1";
            string? shape = null; ar.WChar(ref shape); BalloonShape = shape ?? "Circle";
            double x = 0; ar.F64(ref x); PositionX = x;
            double y = 0; ar.F64(ref y); PositionY = y;
            double sz = 0; ar.F64(ref sz); Size = sz;
            string? custom = null; ar.WChar(ref custom); CustomText = string.IsNullOrEmpty(custom) ? null : custom;
            uint entId = 0; ar.U32(ref entId); AttachedEntityId = entId;

            ushort lCount = 0; ar.U16(ref lCount);
            LeaderPoints.Clear();
            for (int i = 0; i < lCount; i++)
            {
                double px = 0; ar.F64(ref px);
                double py = 0; ar.F64(ref py);
                LeaderPoints.Add(new DrPoint2D(px, py));
            }
        }
    }
}

/// <summary>
/// Direct strongly-typed MFC model for geometric tolerance frame annotation (<c>moGtol_c</c>).
/// Implements IMfcSerializable for symmetric bi-directional reading and writing.
/// </summary>
public sealed class DrGtolMfcModel : IMfcSerializable
{
    public const string MfcClassName = "moGtol_c";

    public string Symbol { get; set; } = "Position";
    public double ToleranceValue { get; set; } = 0.05;
    public string MaterialCondition { get; set; } = "MMC";
    public string PrimaryDatum { get; set; } = "A";
    public string SecondaryDatum { get; set; } = "B";
    public string TertiaryDatum { get; set; } = "C";
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public int FrameCount { get; set; } = 1;

    public byte[]? RawData { get; set; }

    public DrGtolMfcModel() { }

    public DrGtolMfcModel(string symbol, double tolerance, string datumA, double posX = 0, double posY = 0)
    {
        Symbol = symbol;
        ToleranceValue = tolerance;
        PrimaryDatum = datumA;
        PositionX = posX;
        PositionY = posY;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(MfcClassName, ref tag, schema: 1);

            string? sym = Symbol; ar.WChar(ref sym);
            double tol = ToleranceValue; ar.F64(ref tol);
            string? mc = MaterialCondition; ar.WChar(ref mc);
            string? d1 = PrimaryDatum; ar.WChar(ref d1);
            string? d2 = SecondaryDatum; ar.WChar(ref d2);
            string? d3 = TertiaryDatum; ar.WChar(ref d3);
            double x = PositionX; ar.F64(ref x);
            double y = PositionY; ar.F64(ref y);
            int frames = FrameCount; ar.I32(ref frames);
        }
        else
        {
            ushort refTag = 0;
            ar.ReadClassTag(out _, out refTag, out _);

            string? sym = null; ar.WChar(ref sym); Symbol = sym ?? "Position";
            double tol = 0; ar.F64(ref tol); ToleranceValue = tol;
            string? mc = null; ar.WChar(ref mc); MaterialCondition = mc ?? "MMC";
            string? d1 = null; ar.WChar(ref d1); PrimaryDatum = d1 ?? "A";
            string? d2 = null; ar.WChar(ref d2); SecondaryDatum = d2 ?? string.Empty;
            string? d3 = null; ar.WChar(ref d3); TertiaryDatum = d3 ?? string.Empty;
            double x = 0; ar.F64(ref x); PositionX = x;
            double y = 0; ar.F64(ref y); PositionY = y;
            int frames = 1; ar.I32(ref frames); FrameCount = frames;
        }
    }
}

/// <summary>
/// Direct strongly-typed MFC model for datum feature symbol annotation (<c>moDatum_c</c>).
/// Implements IMfcSerializable for symmetric bi-directional reading and writing.
/// </summary>
public sealed class DrDatumMfcModel : IMfcSerializable
{
    public const string MfcClassName = "moDatum_c";

    public string DatumLabel { get; set; } = "A";
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public bool IsFilledTriangle { get; set; } = true;
    public List<DrPoint2D> LeaderPoints { get; } = new();

    public byte[]? RawData { get; set; }

    public DrDatumMfcModel() { }

    public DrDatumMfcModel(string label, double posX, double posY, bool isFilled = true)
    {
        DatumLabel = label;
        PositionX = posX;
        PositionY = posY;
        IsFilledTriangle = isFilled;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(MfcClassName, ref tag, schema: 1);

            string? lbl = DatumLabel; ar.WChar(ref lbl);
            double x = PositionX; ar.F64(ref x);
            double y = PositionY; ar.F64(ref y);
            bool filled = IsFilledTriangle; ar.Boolean(ref filled);

            ushort lCount = (ushort)LeaderPoints.Count;
            ar.U16(ref lCount);
            foreach (var pt in LeaderPoints)
            {
                double px = pt.X; ar.F64(ref px);
                double py = pt.Y; ar.F64(ref py);
            }
        }
        else
        {
            ushort refTag = 0;
            ar.ReadClassTag(out _, out refTag, out _);

            string? lbl = null; ar.WChar(ref lbl); DatumLabel = lbl ?? "A";
            double x = 0; ar.F64(ref x); PositionX = x;
            double y = 0; ar.F64(ref y); PositionY = y;
            bool filled = true; ar.Boolean(ref filled); IsFilledTriangle = filled;

            ushort lCount = 0; ar.U16(ref lCount);
            LeaderPoints.Clear();
            for (int i = 0; i < lCount; i++)
            {
                double px = 0; ar.F64(ref px);
                double py = 0; ar.F64(ref py);
                LeaderPoints.Add(new DrPoint2D(px, py));
            }
        }
    }
}

/// <summary>
/// Column definition for BOM table.
/// </summary>
public sealed class DrBomColumnModel
{
    public string Title { get; set; } = string.Empty;
    public string PropertyBinding { get; set; } = string.Empty;
    public double Width { get; set; } = 0.025;
}

/// <summary>
/// Row item for BOM table.
/// </summary>
public sealed class DrBomRowModel
{
    public string ItemNumber { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// Direct strongly-typed MFC model for drawing BOM/table annotation (<c>moBomTemplate_c</c>).
/// Implements IMfcSerializable for symmetric bi-directional reading and writing.
/// </summary>
public sealed class DrBomTableMfcModel : IMfcSerializable
{
    public const string MfcClassName = "moBomTemplate_c";
    public const string TableTemplateClassName = "moTableTemplate_c";

    public string TableName { get; set; } = "BOM Table1";
    public double AnchorX { get; set; }
    public double AnchorY { get; set; }
    public List<DrBomColumnModel> Columns { get; } = new();
    public List<DrBomRowModel> Rows { get; } = new();

    public byte[]? RawData { get; set; }

    public DrBomTableMfcModel() { }

    public DrBomTableMfcModel(string name, double x = 0, double y = 0)
    {
        TableName = name;
        AnchorX = x;
        AnchorY = y;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(MfcClassName, ref tag, schema: 1);

            string? name = TableName; ar.WChar(ref name);
            double ax = AnchorX; ar.F64(ref ax);
            double ay = AnchorY; ar.F64(ref ay);

            ushort colCount = (ushort)Columns.Count;
            ar.U16(ref colCount);
            foreach (var col in Columns)
            {
                string? title = col.Title; ar.WChar(ref title);
                string? prop = col.PropertyBinding; ar.WChar(ref prop);
                double w = col.Width; ar.F64(ref w);
            }

            ushort rowCount = (ushort)Rows.Count;
            ar.U16(ref rowCount);
            foreach (var row in Rows)
            {
                string? item = row.ItemNumber; ar.WChar(ref item);
                string? part = row.PartNumber; ar.WChar(ref part);
                string? desc = row.Description; ar.WChar(ref desc);
                int qty = row.Quantity; ar.I32(ref qty);
            }
        }
        else
        {
            ushort refTag = 0;
            ar.ReadClassTag(out _, out refTag, out _);

            string? name = null; ar.WChar(ref name); TableName = name ?? "BOM Table1";
            double ax = 0; ar.F64(ref ax); AnchorX = ax;
            double ay = 0; ar.F64(ref ay); AnchorY = ay;

            ushort colCount = 0; ar.U16(ref colCount);
            Columns.Clear();
            for (int i = 0; i < colCount; i++)
            {
                string? title = null; ar.WChar(ref title);
                string? prop = null; ar.WChar(ref prop);
                double w = 0; ar.F64(ref w);
                Columns.Add(new DrBomColumnModel { Title = title ?? string.Empty, PropertyBinding = prop ?? string.Empty, Width = w });
            }

            ushort rowCount = 0; ar.U16(ref rowCount);
            Rows.Clear();
            for (int i = 0; i < rowCount; i++)
            {
                string? item = null; ar.WChar(ref item);
                string? part = null; ar.WChar(ref part);
                string? desc = null; ar.WChar(ref desc);
                int qty = 1; ar.I32(ref qty);
                Rows.Add(new DrBomRowModel { ItemNumber = item ?? string.Empty, PartNumber = part ?? string.Empty, Description = desc ?? string.Empty, Quantity = qty });
            }
        }
    }
}
