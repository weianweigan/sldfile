using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

// =========================================================================
// 1. 参数驱动层 (Parameter Layer: moLengthParameter_c / moAngleParameter_c)
// =========================================================================

/// <summary>
/// 基础参数驱动抽象类 (对齐 SolidWorks moParameter_c).
/// 存储参数名称与物理驱动数值.
/// </summary>
public abstract class MoParameterModel : IMfcSerializable
{
    public abstract string ClassName { get; }

    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string? DisplayValue { get; set; }
    public string ValueSource { get; set; } = "BinaryParameter";
    public int SourceOffset { get; set; }
    public virtual FeatureDimension ToFeature() => new FeatureDimension(this);

    public virtual void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            string? name = Name;
            ar.WChar(ref name);

            double val = Value;
            ar.F64(ref val);

            string? disp = DisplayValue;
            ar.WChar(ref disp);

            string? src = ValueSource;
            ar.WChar(ref src);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            if (ar.Remaining >= 2)
            {
                string? name = null;
                ar.WChar(ref name);
                if (name != null) Name = name;
            }

            if (ar.Remaining >= 8)
            {
                double val = 0;
                ar.F64(ref val);
                Value = val;
            }

            if (ar.Remaining >= 2)
            {
                string? disp = null;
                ar.WChar(ref disp);
                DisplayValue = disp;
            }

            if (ar.Remaining >= 2)
            {
                string? src = null;
                ar.WChar(ref src);
                if (src != null) ValueSource = src;
            }
        }
    }
}

/// <summary>
/// 长度参数驱动模型 (moLengthParameter_c).
/// 存储线性距离尺寸参数，单位：米.
/// </summary>
public sealed class MoLengthParameterModel : MoParameterModel
{
    public const string MfcClassName = "moLengthParameter_c";
    public override string ClassName => MfcClassName;

    public MoLengthParameterModel() { }

    public MoLengthParameterModel(string name, double lengthMeters)
    {
        Name = name;
        Value = lengthMeters;
    }
}

/// <summary>
/// 角度参数驱动模型 (moAngleParameter_c).
/// 存储角度尺寸参数，单位：弧度.
/// </summary>
public sealed class MoAngleParameterModel : MoParameterModel
{
    public const string MfcClassName = "moAngleParameter_c";
    public override string ClassName => MfcClassName;

    public MoAngleParameterModel() { }

    public MoAngleParameterModel(string name, double angleRadians)
    {
        Name = name;
        Value = angleRadians;
    }
}

// =========================================================================
// 2. 显示几何布局层 (Display Dimension Layer: moDisplayDim_c 族系)
// =========================================================================

/// <summary>
/// 尺寸显示几何布局抽象基类 (对齐 SolidWorks moDisplayDim_c).
/// 存储尺寸在 2D/3D 空间中的文字位置、引线测量点、尺寸线方向与法向向量.
/// </summary>
public abstract class MoDisplayDimModel : IMfcSerializable
{
    public const string BaseClassName = "moDisplayDim_c";
    public virtual string ClassName => BaseClassName;
    public abstract SketchDimensionType DimensionType { get; }

    public int SourceOffset { get; set; }

    // 空间文字位置 (m_ptText)
    public double? TextX { get; set; }
    public double? TextY { get; set; }
    public double? TextZ { get; set; }
    public double? TextAngle { get; set; }

    // 尺寸走向与法向单位向量 (m_vecDir, m_vecNorm)
    public double? DirectionX { get; set; }
    public double? DirectionY { get; set; }
    public double? DirectionZ { get; set; }

    public double? NormalX { get; set; }
    public double? NormalY { get; set; }
    public double? NormalZ { get; set; }

    // 引线测量点 (Witness Points)
    public double? WitnessPoint1X { get; set; }
    public double? WitnessPoint1Y { get; set; }
    public double? WitnessPoint1Z { get; set; }

    public double? WitnessPoint2X { get; set; }
    public double? WitnessPoint2Y { get; set; }
    public double? WitnessPoint2Z { get; set; }

    // 尺寸线端点
    public double? LinePointX { get; set; }
    public double? LinePointY { get; set; }
    public double? LinePointZ { get; set; }

    // 箭头与驱动标志
    public bool? IsInsideArrow { get; set; }
    public bool IsDriven { get; set; }

    public bool HasTextPosition => TextX.HasValue && TextY.HasValue;
    public bool HasDirection => DirectionX.HasValue && DirectionY.HasValue;

    public (double X, double Y, double Z)? GetTextPosition()
    {
        if (TextX.HasValue && TextY.HasValue)
            return (TextX.Value, TextY.Value, TextZ ?? 0.0);
        return null;
    }

    public (double X, double Y, double Z)? GetDirectionVector()
    {
        if (DirectionX.HasValue && DirectionY.HasValue)
            return (DirectionX.Value, DirectionY.Value, DirectionZ ?? 0.0);
        return null;
    }

    public (double X, double Y, double Z)? GetNormalVector()
    {
        if (NormalX.HasValue && NormalY.HasValue)
            return (NormalX.Value, NormalY.Value, NormalZ ?? 0.0);
        if (DirectionX.HasValue && DirectionY.HasValue)
            return (-DirectionY.Value, DirectionX.Value, 0.0);
        return null;
    }

    public ((double X, double Y, double Z)? P1, (double X, double Y, double Z)? P2) GetWitnessPoints()
    {
        (double X, double Y, double Z)? p1 = WitnessPoint1X.HasValue && WitnessPoint1Y.HasValue
            ? (WitnessPoint1X.Value, WitnessPoint1Y.Value, WitnessPoint1Z ?? 0.0) : null;
        (double X, double Y, double Z)? p2 = WitnessPoint2X.HasValue && WitnessPoint2Y.HasValue
            ? (WitnessPoint2X.Value, WitnessPoint2Y.Value, WitnessPoint2Z ?? 0.0) : null;
        return (p1, p2);
    }

    public virtual void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            int dt = (int)DimensionType;
            ar.I32(ref dt);

            bool hasText = HasTextPosition;
            ar.Boolean(ref hasText);
            if (hasText)
            {
                double tx = TextX!.Value, ty = TextY!.Value, tz = TextZ ?? 0.0;
                ar.F64(ref tx); ar.F64(ref ty); ar.F64(ref tz);
            }

            bool hasDir = HasDirection;
            ar.Boolean(ref hasDir);
            if (hasDir)
            {
                double dx = DirectionX!.Value, dy = DirectionY!.Value, dz = DirectionZ ?? 0.0;
                ar.F64(ref dx); ar.F64(ref dy); ar.F64(ref dz);
            }

            bool hasNorm = NormalX.HasValue && NormalY.HasValue;
            ar.Boolean(ref hasNorm);
            if (hasNorm)
            {
                double nx = NormalX!.Value, ny = NormalY!.Value, nz = NormalZ ?? 0.0;
                ar.F64(ref nx); ar.F64(ref ny); ar.F64(ref nz);
            }

            bool hasWitness = WitnessPoint1X.HasValue && WitnessPoint1Y.HasValue;
            ar.Boolean(ref hasWitness);
            if (hasWitness)
            {
                double w1x = WitnessPoint1X!.Value, w1y = WitnessPoint1Y!.Value, w1z = WitnessPoint1Z ?? 0.0;
                double w2x = WitnessPoint2X ?? 0.0, w2y = WitnessPoint2Y ?? 0.0, w2z = WitnessPoint2Z ?? 0.0;
                ar.F64(ref w1x); ar.F64(ref w1y); ar.F64(ref w1z);
                ar.F64(ref w2x); ar.F64(ref w2y); ar.F64(ref w2z);
            }

            bool driven = IsDriven;
            ar.Boolean(ref driven);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            if (ar.Remaining >= 4)
            {
                int dt = 0;
                ar.I32(ref dt);
            }

            if (ar.Remaining >= 1)
            {
                bool hasText = false;
                ar.Boolean(ref hasText);
                if (hasText && ar.Remaining >= 24)
                {
                    double tx = 0, ty = 0, tz = 0;
                    ar.F64(ref tx); ar.F64(ref ty); ar.F64(ref tz);
                    TextX = tx; TextY = ty; TextZ = tz;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasDir = false;
                ar.Boolean(ref hasDir);
                if (hasDir && ar.Remaining >= 24)
                {
                    double dx = 0, dy = 0, dz = 0;
                    ar.F64(ref dx); ar.F64(ref dy); ar.F64(ref dz);
                    DirectionX = dx; DirectionY = dy; DirectionZ = dz;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasNorm = false;
                ar.Boolean(ref hasNorm);
                if (hasNorm && ar.Remaining >= 24)
                {
                    double nx = 0, ny = 0, nz = 0;
                    ar.F64(ref nx); ar.F64(ref ny); ar.F64(ref nz);
                    NormalX = nx; NormalY = ny; NormalZ = nz;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasWitness = false;
                ar.Boolean(ref hasWitness);
                if (hasWitness && ar.Remaining >= 48)
                {
                    double w1x = 0, w1y = 0, w1z = 0;
                    double w2x = 0, w2y = 0, w2z = 0;
                    ar.F64(ref w1x); ar.F64(ref w1y); ar.F64(ref w1z);
                    ar.F64(ref w2x); ar.F64(ref w2y); ar.F64(ref w2z);
                    WitnessPoint1X = w1x; WitnessPoint1Y = w1y; WitnessPoint1Z = w1z;
                    WitnessPoint2X = w2x; WitnessPoint2Y = w2y; WitnessPoint2Z = w2z;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool driven = false;
                ar.Boolean(ref driven);
                IsDriven = driven;
            }
        }
    }

    /// <summary>
    /// 根据 MFC 运行时类名构造具体的显示尺寸对象.
    /// </summary>
    public static MoDisplayDimModel CreateFromClassName(string className)
    {
        return className switch
        {
            MoDisplayDistanceDimModel.MfcClassName => new MoDisplayDistanceDimModel(),
            MoDisplayRadialDimModel.MfcClassName or MoDisplayRadialDimModel.TrueRadialClassName => new MoDisplayRadialDimModel(),
            MoDisplayDiameterDimModel.MfcClassName => new MoDisplayDiameterDimModel(),
            MoDisplayAngularDimModel.MfcClassName => new MoDisplayAngularDimModel(),
            MoDisplayArcLengthDimModel.MfcClassName => new MoDisplayArcLengthDimModel(),
            MoDisplayOrdDimModel.MfcClassName or MoDisplayOrdDimModel.AngOrdClassName => new MoDisplayOrdDimModel(),
            MoDisplayChamferDimModel.MfcClassName => new MoDisplayChamferDimModel(),
            _ => new MoDisplayDistanceDimModel()
        };
    }

    /// <summary>
    /// 根据语义尺寸类型构造对应的显示尺寸对象.
    /// </summary>
    public static MoDisplayDimModel CreateForType(SketchDimensionType type)
    {
        return type switch
        {
            SketchDimensionType.Linear => new MoDisplayDistanceDimModel(),
            SketchDimensionType.Radius => new MoDisplayRadialDimModel(),
            SketchDimensionType.Diameter => new MoDisplayDiameterDimModel(),
            SketchDimensionType.Angle => new MoDisplayAngularDimModel(),
            SketchDimensionType.ArcLength => new MoDisplayArcLengthDimModel(),
            SketchDimensionType.Ordinate => new MoDisplayOrdDimModel(),
            SketchDimensionType.Chamfer => new MoDisplayChamferDimModel(),
            _ => new MoDisplayDistanceDimModel()
        };
    }
}

/// <summary>
/// 线性距离显示尺寸 (moDisplayDistanceDim_c).
/// </summary>
public sealed class MoDisplayDistanceDimModel : MoDisplayDimModel
{
    public const string MfcClassName = "moDisplayDistanceDim_c";
    public override string ClassName => MfcClassName;
    public override SketchDimensionType DimensionType => SketchDimensionType.Linear;
}

/// <summary>
/// 半径显示尺寸 (moDisplayRadialDim_c / moDisplayTrueRadialDim_c).
/// </summary>
public sealed class MoDisplayRadialDimModel : MoDisplayDimModel
{
    public const string MfcClassName = "moDisplayRadialDim_c";
    public const string TrueRadialClassName = "moDisplayTrueRadialDim_c";
    public override string ClassName => MfcClassName;
    public override SketchDimensionType DimensionType => SketchDimensionType.Radius;

    public double? CenterX { get; set; }
    public double? CenterY { get; set; }
    public double? CenterZ { get; set; }
    public double? Radius { get; set; }
}

/// <summary>
/// 直径显示尺寸 (moDisplayDiameterDim_c).
/// </summary>
public sealed class MoDisplayDiameterDimModel : MoDisplayDimModel
{
    public const string MfcClassName = "moDisplayDiameterDim_c";
    public override string ClassName => MfcClassName;
    public override SketchDimensionType DimensionType => SketchDimensionType.Diameter;

    public double? CenterX { get; set; }
    public double? CenterY { get; set; }
    public double? CenterZ { get; set; }
    public double? Diameter { get; set; }
    public double? Radius => Diameter.HasValue ? Diameter.Value / 2.0 : null;
}

/// <summary>
/// 角度显示尺寸 (moDisplayAngularDim_c).
/// </summary>
public sealed class MoDisplayAngularDimModel : MoDisplayDimModel
{
    public const string MfcClassName = "moDisplayAngularDim_c";
    public override string ClassName => MfcClassName;
    public override SketchDimensionType DimensionType => SketchDimensionType.Angle;

    public double? VertexX { get; set; }
    public double? VertexY { get; set; }
    public double? VertexZ { get; set; }
    public double? Ray1X { get; set; }
    public double? Ray1Y { get; set; }
    public double? Ray1Z { get; set; }
    public double? Ray2X { get; set; }
    public double? Ray2Y { get; set; }
    public double? Ray2Z { get; set; }
    public double? ArcAngle { get; set; }
}

/// <summary>
/// 弧长显示尺寸 (moDisplayArcLengthDim_c).
/// </summary>
public sealed class MoDisplayArcLengthDimModel : MoDisplayDimModel
{
    public const string MfcClassName = "moDisplayArcLengthDim_c";
    public override string ClassName => MfcClassName;
    public override SketchDimensionType DimensionType => SketchDimensionType.ArcLength;

    public double? CenterX { get; set; }
    public double? CenterY { get; set; }
    public double? CenterZ { get; set; }
    public double? Radius { get; set; }
    public double? ArcLength { get; set; }
}

/// <summary>
/// 坐标标注显示尺寸 (moDisplayOrdDim_c / moDisplayAngOrdDim_c).
/// </summary>
public sealed class MoDisplayOrdDimModel : MoDisplayDimModel
{
    public const string MfcClassName = "moDisplayOrdDim_c";
    public const string AngOrdClassName = "moDisplayAngOrdDim_c";
    public override string ClassName => MfcClassName;
    public override SketchDimensionType DimensionType => SketchDimensionType.Ordinate;

    public double? BasePointX { get; set; }
    public double? BasePointY { get; set; }
    public double? BasePointZ { get; set; }
    public double? OrdinateOffset { get; set; }
}

/// <summary>
/// 倒角显示尺寸 (moDisplayChamferDim_c).
/// </summary>
public sealed class MoDisplayChamferDimModel : MoDisplayDimModel
{
    public const string MfcClassName = "moDisplayChamferDim_c";
    public override string ClassName => MfcClassName;
    public override SketchDimensionType DimensionType => SketchDimensionType.Chamfer;

    public double? Distance1 { get; set; }
    public double? Distance2 { get; set; }
    public double? Angle { get; set; }
}

// =========================================================================
// 3. 统一尺寸对象模型 (Unified Dimension Object Model)
// =========================================================================

/// <summary>
/// SolidWorks 统一尺寸对象模型 (Unified Dimension Object Model).
/// 适用于所有草图尺寸与三维特征尺寸 (Extrude, Fillet, Revolve, Chamfer, SheetMetal, HoleWizard 等).
/// 聚合了底层参数驱动层 (moLengthParameter_c / moAngleParameter_c) 与显示几何布局层 (moDisplayDim_c 族系).
/// </summary>
public class DimensionModel : IMfcSerializable
{
    public const string DefaultMfcClassName = "moLengthParameter_c";

    private string _name = string.Empty;
    private double? _systemValue;
    private string? _displayValue;
    private string _valueSource = "BinaryParameter";
    private SketchDimensionType _dimensionType = SketchDimensionType.Unknown;

    private double? _textX, _textY, _textZ, _textAngle;
    private double? _dirX, _dirY, _dirZ;
    private double? _normX, _normY, _normZ;
    private double? _w1x, _w1y, _w1z, _w2x, _w2y, _w2z;
    private double? _lpX, _lpY, _lpZ;
    private double? _cx, _cy, _cz, _radius;
    private bool? _isInsideArrow;
    private bool _isDriven;
    private string? _displayDimClassName;
    private int _displayDimOffset;

    /// <summary>
    /// 底层参数驱动层对象 (moLengthParameter_c / moAngleParameter_c).
    /// </summary>
    public MoParameterModel? Parameter { get; set; }

    /// <summary>
    /// 底层显示几何布局层对象 (moDisplayDim_c 族系派生类).
    /// </summary>
    public MoDisplayDimModel? DisplayDimension { get; set; }

    public virtual FeatureDimension ToFeature() => new FeatureDimension(this);

    public string Name
    {
        get => Parameter?.Name ?? _name;
        set
        {
            _name = value ?? string.Empty;
            if (Parameter != null) Parameter.Name = _name;
        }
    }

    public SketchDimensionType DimensionType
    {
        get => DisplayDimension?.DimensionType ?? _dimensionType;
        set
        {
            _dimensionType = value;
            if (DisplayDimension == null || (value != SketchDimensionType.Unknown && DisplayDimension.DimensionType != value))
            {
                var old = DisplayDimension;
                DisplayDimension = MoDisplayDimModel.CreateForType(value);
                if (old != null) CopyDisplayDimState(old, DisplayDimension);
            }
        }
    }

    public double? SystemValue
    {
        get => Parameter != null ? Parameter.Value : _systemValue;
        set
        {
            _systemValue = value;
            if (value.HasValue)
            {
                EnsureParameter();
                Parameter!.Value = value.Value;
            }
        }
    }

    public string? DisplayValue
    {
        get => Parameter?.DisplayValue ?? _displayValue;
        set
        {
            _displayValue = value;
            if (Parameter != null) Parameter.DisplayValue = value;
        }
    }

    public string ValueSource
    {
        get => Parameter?.ValueSource ?? _valueSource;
        set
        {
            _valueSource = value ?? "BinaryParameter";
            if (Parameter != null) Parameter.ValueSource = _valueSource;
        }
    }

    public int SourceOffset { get; set; }

    // Position and Placement Coordinates (aligned with moDisplayDim_c hierarchy)
    public double? TextX
    {
        get => DisplayDimension?.TextX ?? _textX;
        set { _textX = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.TextX = value; } }
    }
    public double? TextY
    {
        get => DisplayDimension?.TextY ?? _textY;
        set { _textY = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.TextY = value; } }
    }
    public double? TextZ
    {
        get => DisplayDimension?.TextZ ?? _textZ;
        set { _textZ = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.TextZ = value; } }
    }
    public double? TextAngle
    {
        get => DisplayDimension?.TextAngle ?? _textAngle;
        set { _textAngle = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.TextAngle = value; } }
    }

    public double? DirectionX
    {
        get => DisplayDimension?.DirectionX ?? _dirX;
        set { _dirX = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.DirectionX = value; } }
    }
    public double? DirectionY
    {
        get => DisplayDimension?.DirectionY ?? _dirY;
        set { _dirY = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.DirectionY = value; } }
    }
    public double? DirectionZ
    {
        get => DisplayDimension?.DirectionZ ?? _dirZ;
        set { _dirZ = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.DirectionZ = value; } }
    }

    public double? NormalX
    {
        get => DisplayDimension?.NormalX ?? _normX;
        set { _normX = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.NormalX = value; } }
    }
    public double? NormalY
    {
        get => DisplayDimension?.NormalY ?? _normY;
        set { _normY = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.NormalY = value; } }
    }
    public double? NormalZ
    {
        get => DisplayDimension?.NormalZ ?? _normZ;
        set { _normZ = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.NormalZ = value; } }
    }

    public double? WitnessPoint1X
    {
        get => DisplayDimension?.WitnessPoint1X ?? _w1x;
        set { _w1x = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.WitnessPoint1X = value; } }
    }
    public double? WitnessPoint1Y
    {
        get => DisplayDimension?.WitnessPoint1Y ?? _w1y;
        set { _w1y = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.WitnessPoint1Y = value; } }
    }
    public double? WitnessPoint1Z
    {
        get => DisplayDimension?.WitnessPoint1Z ?? _w1z;
        set { _w1z = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.WitnessPoint1Z = value; } }
    }

    public double? WitnessPoint2X
    {
        get => DisplayDimension?.WitnessPoint2X ?? _w2x;
        set { _w2x = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.WitnessPoint2X = value; } }
    }
    public double? WitnessPoint2Y
    {
        get => DisplayDimension?.WitnessPoint2Y ?? _w2y;
        set { _w2y = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.WitnessPoint2Y = value; } }
    }
    public double? WitnessPoint2Z
    {
        get => DisplayDimension?.WitnessPoint2Z ?? _w2z;
        set { _w2z = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.WitnessPoint2Z = value; } }
    }

    public double? LinePointX
    {
        get => DisplayDimension?.LinePointX ?? _lpX;
        set { _lpX = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.LinePointX = value; } }
    }
    public double? LinePointY
    {
        get => DisplayDimension?.LinePointY ?? _lpY;
        set { _lpY = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.LinePointY = value; } }
    }
    public double? LinePointZ
    {
        get => DisplayDimension?.LinePointZ ?? _lpZ;
        set { _lpZ = value; if (value.HasValue) { EnsureDisplayDimension(); DisplayDimension!.LinePointZ = value; } }
    }

    public double? CenterX
    {
        get => DisplayDimension switch
        {
            MoDisplayRadialDimModel r => r.CenterX ?? _cx,
            MoDisplayDiameterDimModel d => d.CenterX ?? _cx,
            MoDisplayArcLengthDimModel a => a.CenterX ?? _cx,
            _ => _cx
        };
        set
        {
            _cx = value;
            if (DisplayDimension is MoDisplayRadialDimModel r) r.CenterX = value;
            else if (DisplayDimension is MoDisplayDiameterDimModel d) d.CenterX = value;
            else if (DisplayDimension is MoDisplayArcLengthDimModel a) a.CenterX = value;
        }
    }

    public double? CenterY
    {
        get => DisplayDimension switch
        {
            MoDisplayRadialDimModel r => r.CenterY ?? _cy,
            MoDisplayDiameterDimModel d => d.CenterY ?? _cy,
            MoDisplayArcLengthDimModel a => a.CenterY ?? _cy,
            _ => _cy
        };
        set
        {
            _cy = value;
            if (DisplayDimension is MoDisplayRadialDimModel r) r.CenterY = value;
            else if (DisplayDimension is MoDisplayDiameterDimModel d) d.CenterY = value;
            else if (DisplayDimension is MoDisplayArcLengthDimModel a) a.CenterY = value;
        }
    }

    public double? CenterZ
    {
        get => DisplayDimension switch
        {
            MoDisplayRadialDimModel r => r.CenterZ ?? _cz,
            MoDisplayDiameterDimModel d => d.CenterZ ?? _cz,
            MoDisplayArcLengthDimModel a => a.CenterZ ?? _cz,
            _ => _cz
        };
        set
        {
            _cz = value;
            if (DisplayDimension is MoDisplayRadialDimModel r) r.CenterZ = value;
            else if (DisplayDimension is MoDisplayDiameterDimModel d) d.CenterZ = value;
            else if (DisplayDimension is MoDisplayArcLengthDimModel a) a.CenterZ = value;
        }
    }

    public double? Radius
    {
        get => DisplayDimension switch
        {
            MoDisplayRadialDimModel r => r.Radius ?? _radius,
            MoDisplayDiameterDimModel d => d.Radius ?? _radius,
            MoDisplayArcLengthDimModel a => a.Radius ?? _radius,
            _ => _radius
        };
        set
        {
            _radius = value;
            if (DisplayDimension is MoDisplayRadialDimModel r) r.Radius = value;
            else if (DisplayDimension is MoDisplayDiameterDimModel d) d.Diameter = value.HasValue ? value.Value * 2.0 : null;
            else if (DisplayDimension is MoDisplayArcLengthDimModel a) a.Radius = value;
        }
    }

    public bool? IsInsideArrow
    {
        get => DisplayDimension?.IsInsideArrow ?? _isInsideArrow;
        set { _isInsideArrow = value; if (DisplayDimension != null) DisplayDimension.IsInsideArrow = value; }
    }

    public bool IsDriven
    {
        get => DisplayDimension != null ? DisplayDimension.IsDriven : _isDriven;
        set { _isDriven = value; if (DisplayDimension != null) DisplayDimension.IsDriven = value; }
    }

    public string? DisplayDimClassName
    {
        get => DisplayDimension?.ClassName ?? _displayDimClassName;
        set
        {
            _displayDimClassName = value;
            if (!string.IsNullOrEmpty(value) && (DisplayDimension == null || DisplayDimension.ClassName != value))
            {
                var old = DisplayDimension;
                DisplayDimension = MoDisplayDimModel.CreateFromClassName(value!);
                if (old != null) CopyDisplayDimState(old, DisplayDimension);
            }
        }
    }

    public int DisplayDimOffset
    {
        get => DisplayDimension != null ? DisplayDimension.SourceOffset : _displayDimOffset;
        set { _displayDimOffset = value; if (DisplayDimension != null) DisplayDimension.SourceOffset = value; }
    }

    public bool HasTextPosition => TextX.HasValue && TextY.HasValue;
    public bool HasDirection => DirectionX.HasValue && DirectionY.HasValue;

    public (double X, double Y, double Z)? GetTextPosition()
    {
        if (DisplayDimension != null && DisplayDimension.HasTextPosition)
            return DisplayDimension.GetTextPosition();
        if (TextX.HasValue && TextY.HasValue)
            return (TextX.Value, TextY.Value, TextZ ?? 0.0);
        return null;
    }

    public (double X, double Y, double Z)? GetDirectionVector()
    {
        if (DisplayDimension != null && DisplayDimension.HasDirection)
            return DisplayDimension.GetDirectionVector();
        if (DirectionX.HasValue && DirectionY.HasValue)
            return (DirectionX.Value, DirectionY.Value, DirectionZ ?? 0.0);
        return null;
    }

    public (double X, double Y, double Z)? GetNormalVector()
    {
        if (DisplayDimension != null)
            return DisplayDimension.GetNormalVector();
        if (NormalX.HasValue && NormalY.HasValue)
            return (NormalX.Value, NormalY.Value, NormalZ ?? 0.0);
        if (DirectionX.HasValue && DirectionY.HasValue)
            return (-DirectionY.Value, DirectionX.Value, 0.0);
        return null;
    }

    public ((double X, double Y, double Z)? P1, (double X, double Y, double Z)? P2) GetWitnessPoints()
    {
        if (DisplayDimension != null)
            return DisplayDimension.GetWitnessPoints();
        (double X, double Y, double Z)? p1 = WitnessPoint1X.HasValue && WitnessPoint1Y.HasValue
            ? (WitnessPoint1X.Value, WitnessPoint1Y.Value, WitnessPoint1Z ?? 0.0) : null;
        (double X, double Y, double Z)? p2 = WitnessPoint2X.HasValue && WitnessPoint2Y.HasValue
            ? (WitnessPoint2X.Value, WitnessPoint2Y.Value, WitnessPoint2Z ?? 0.0) : null;
        return (p1, p2);
    }

    public void EnsureParameter(bool isAngle = false)
    {
        if (Parameter == null)
        {
            if (isAngle || DimensionType == SketchDimensionType.Angle)
                Parameter = new MoAngleParameterModel(Name, SystemValue ?? 0.0);
            else
                Parameter = new MoLengthParameterModel(Name, SystemValue ?? 0.0);
            Parameter.DisplayValue = DisplayValue;
            Parameter.ValueSource = ValueSource;
            Parameter.SourceOffset = SourceOffset;
        }
    }

    public void EnsureDisplayDimension()
    {
        if (DisplayDimension == null)
        {
            DisplayDimension = MoDisplayDimModel.CreateForType(DimensionType != SketchDimensionType.Unknown ? DimensionType : SketchDimensionType.Linear);
            DisplayDimension.TextX = _textX;
            DisplayDimension.TextY = _textY;
            DisplayDimension.TextZ = _textZ;
            DisplayDimension.TextAngle = _textAngle;
            DisplayDimension.DirectionX = _dirX;
            DisplayDimension.DirectionY = _dirY;
            DisplayDimension.DirectionZ = _dirZ;
            DisplayDimension.NormalX = _normX;
            DisplayDimension.NormalY = _normY;
            DisplayDimension.NormalZ = _normZ;
            DisplayDimension.WitnessPoint1X = _w1x;
            DisplayDimension.WitnessPoint1Y = _w1y;
            DisplayDimension.WitnessPoint1Z = _w1z;
            DisplayDimension.WitnessPoint2X = _w2x;
            DisplayDimension.WitnessPoint2Y = _w2y;
            DisplayDimension.WitnessPoint2Z = _w2z;
            DisplayDimension.LinePointX = _lpX;
            DisplayDimension.LinePointY = _lpY;
            DisplayDimension.LinePointZ = _lpZ;
            DisplayDimension.IsInsideArrow = _isInsideArrow;
            DisplayDimension.IsDriven = _isDriven;
            DisplayDimension.SourceOffset = _displayDimOffset;
        }
    }

    protected static void CopyDisplayDimState(MoDisplayDimModel src, MoDisplayDimModel dst)
    {
        dst.TextX = src.TextX;
        dst.TextY = src.TextY;
        dst.TextZ = src.TextZ;
        dst.TextAngle = src.TextAngle;
        dst.DirectionX = src.DirectionX;
        dst.DirectionY = src.DirectionY;
        dst.DirectionZ = src.DirectionZ;
        dst.NormalX = src.NormalX;
        dst.NormalY = src.NormalY;
        dst.NormalZ = src.NormalZ;
        dst.WitnessPoint1X = src.WitnessPoint1X;
        dst.WitnessPoint1Y = src.WitnessPoint1Y;
        dst.WitnessPoint1Z = src.WitnessPoint1Z;
        dst.WitnessPoint2X = src.WitnessPoint2X;
        dst.WitnessPoint2Y = src.WitnessPoint2Y;
        dst.WitnessPoint2Z = src.WitnessPoint2Z;
        dst.LinePointX = src.LinePointX;
        dst.LinePointY = src.LinePointY;
        dst.LinePointZ = src.LinePointZ;
        dst.IsInsideArrow = src.IsInsideArrow;
        dst.IsDriven = src.IsDriven;
        dst.SourceOffset = src.SourceOffset;
    }

    public DimensionModel() { }

    public DimensionModel(MoParameterModel parameter, MoDisplayDimModel? displayDim = null)
    {
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        DisplayDimension = displayDim;
        Name = parameter.Name;
        SystemValue = parameter.Value;
        DisplayValue = parameter.DisplayValue;
        ValueSource = parameter.ValueSource;
        SourceOffset = parameter.SourceOffset;
        if (displayDim != null)
        {
            DimensionType = displayDim.DimensionType;
            DisplayDimClassName = displayDim.ClassName;
            DisplayDimOffset = displayDim.SourceOffset;
        }
    }

    public DimensionModel(DimensionModel other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        Name = other.Name;
        DimensionType = other.DimensionType;
        SystemValue = other.SystemValue;
        DisplayValue = other.DisplayValue;
        ValueSource = other.ValueSource;
        SourceOffset = other.SourceOffset;
        TextX = other.TextX;
        TextY = other.TextY;
        TextZ = other.TextZ;
        TextAngle = other.TextAngle;
        DirectionX = other.DirectionX;
        DirectionY = other.DirectionY;
        DirectionZ = other.DirectionZ;
        NormalX = other.NormalX;
        NormalY = other.NormalY;
        NormalZ = other.NormalZ;
        WitnessPoint1X = other.WitnessPoint1X;
        WitnessPoint1Y = other.WitnessPoint1Y;
        WitnessPoint1Z = other.WitnessPoint1Z;
        WitnessPoint2X = other.WitnessPoint2X;
        WitnessPoint2Y = other.WitnessPoint2Y;
        WitnessPoint2Z = other.WitnessPoint2Z;
        LinePointX = other.LinePointX;
        LinePointY = other.LinePointY;
        LinePointZ = other.LinePointZ;
        CenterX = other.CenterX;
        CenterY = other.CenterY;
        CenterZ = other.CenterZ;
        Radius = other.Radius;
        IsInsideArrow = other.IsInsideArrow;
        IsDriven = other.IsDriven;
        DisplayDimClassName = other.DisplayDimClassName;
        DisplayDimOffset = other.DisplayDimOffset;

        if (other.Parameter != null)
        {
            Parameter = other.Parameter is MoAngleParameterModel
                ? new MoAngleParameterModel(other.Parameter.Name, other.Parameter.Value)
                : new MoLengthParameterModel(other.Parameter.Name, other.Parameter.Value);
            Parameter.DisplayValue = other.Parameter.DisplayValue;
            Parameter.ValueSource = other.Parameter.ValueSource;
            Parameter.SourceOffset = other.Parameter.SourceOffset;
        }

        if (other.DisplayDimension != null)
        {
            DisplayDimension = MoDisplayDimModel.CreateFromClassName(other.DisplayDimension.ClassName);
            CopyDisplayDimState(other.DisplayDimension, DisplayDimension);
        }
    }

    public virtual void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(Parameter?.ClassName ?? DefaultMfcClassName, ref refTag);

            string? name = Name;
            ar.WChar(ref name);

            double val = SystemValue ?? 0.0;
            ar.F64(ref val);

            string? disp = DisplayValue;
            ar.WChar(ref disp);

            string? src = ValueSource;
            ar.WChar(ref src);

            int dt = (int)DimensionType;
            ar.I32(ref dt);

            bool hasDisplayDim = DisplayDimension != null;
            ar.Boolean(ref hasDisplayDim);
            if (hasDisplayDim)
            {
                DisplayDimension!.Serialize(ar);
            }
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(DefaultMfcClassName, ref refTag);

            if (ar.Remaining >= 2)
            {
                string? name = null;
                ar.WChar(ref name);
                if (name != null) Name = name;
            }

            if (ar.Remaining >= 8)
            {
                double val = 0;
                ar.F64(ref val);
                SystemValue = val;
            }

            if (ar.Remaining >= 2)
            {
                string? disp = null;
                ar.WChar(ref disp);
                DisplayValue = disp;
            }

            if (ar.Remaining >= 2)
            {
                string? src = null;
                ar.WChar(ref src);
                if (src != null) ValueSource = src;
            }

            if (ar.Remaining >= 4)
            {
                int dt = 0;
                ar.I32(ref dt);
                DimensionType = (SketchDimensionType)dt;
            }

            if (ar.Remaining >= 1)
            {
                bool hasDisplayDim = false;
                ar.Boolean(ref hasDisplayDim);
                if (hasDisplayDim)
                {
                    DisplayDimension = MoDisplayDimModel.CreateForType(DimensionType);
                    DisplayDimension.Serialize(ar);
                }
            }
        }
    }
}
