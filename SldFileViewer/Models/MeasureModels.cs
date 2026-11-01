using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace SldFileViewer.Models;

/// <summary>
/// 测量所选几何元素的类型（点、边线、平面、圆/圆弧、圆柱面）
/// </summary>
public enum MeasureElementType
{
    /// <summary>顶点/角点</summary>
    Point,
    /// <summary>直线边/轮廓线</summary>
    Edge,
    /// <summary>平面</summary>
    Face,
    /// <summary>圆孔/圆弧边线（具有圆心、半径、直径、法向）</summary>
    Circle,
    /// <summary>圆柱面（孔内壁或圆柱销外壁，具有轴线、半径、直径）</summary>
    Cylinder
}

/// <summary>
/// 3D 视口中拾取的测量几何元素
/// </summary>
public sealed class MeasureElement
{
    public MeasureElementType Type { get; }

    /// <summary>元素特征中心点/代表点（模型绝对坐标，mm）</summary>
    public Point3D CenterPoint { get; }

    // ── 边线特有属性 ──
    public Point3D EdgeStart { get; }
    public Point3D EdgeEnd { get; }
    public double EdgeLength { get; }
    public Vector3D EdgeDirection { get; }

    // ── 表面特有属性 ──
    public int PartIndex { get; }
    public Vector3D FaceNormal { get; }
    public double FaceArea { get; }
    public List<Point3D> FaceVertices { get; }

    // ── 圆 / 圆柱特有属性 ──
    public double Radius { get; }
    public double Diameter => Radius * 2.0;
    /// <summary>圆所在平面的法向，或圆柱面的中心对称轴线向量</summary>
    public Vector3D AxisDirection { get; }
    /// <summary>圆周闭环多边形的所有三维顶点（用于 3D 视口平滑圆环高亮）</summary>
    public List<Point3D> LoopVertices { get; }

    /// <summary>构造点元素</summary>
    public MeasureElement(Point3D point)
    {
        Type = MeasureElementType.Point;
        CenterPoint = point;
        FaceVertices = new List<Point3D> { point };
        LoopVertices = new List<Point3D>();
    }

    /// <summary>构造直线边元素</summary>
    public MeasureElement(Point3D start, Point3D end)
    {
        Type = MeasureElementType.Edge;
        EdgeStart = start;
        EdgeEnd = end;
        CenterPoint = new Point3D((start.X + end.X) * 0.5, (start.Y + end.Y) * 0.5, (start.Z + end.Z) * 0.5);
        Vector3D dir = end - start;
        EdgeLength = dir.Length;
        if (EdgeLength > 1e-6)
        {
            dir.Normalize();
            EdgeDirection = dir;
        }
        FaceVertices = new List<Point3D> { start, end };
        LoopVertices = new List<Point3D>();
    }

    /// <summary>构造平面元素</summary>
    public MeasureElement(int partIndex, Point3D center, Vector3D normal, double area, List<Point3D> vertices)
    {
        Type = MeasureElementType.Face;
        PartIndex = partIndex;
        CenterPoint = center;
        if (normal.Length > 1e-6) normal.Normalize();
        FaceNormal = normal;
        FaceArea = area;
        FaceVertices = vertices ?? new List<Point3D>();
        LoopVertices = new List<Point3D>();
    }

    /// <summary>构造圆/圆弧边元素</summary>
    public MeasureElement(Point3D center, Vector3D normal, double radius, List<Point3D> loopVertices)
    {
        Type = MeasureElementType.Circle;
        CenterPoint = center;
        if (normal.Length > 1e-6) normal.Normalize();
        AxisDirection = normal;
        FaceNormal = normal;
        Radius = Math.Max(0.001, radius);
        LoopVertices = loopVertices ?? new List<Point3D>();
        FaceVertices = LoopVertices;
    }

    /// <summary>构造圆柱面元素</summary>
    public MeasureElement(int partIndex, Point3D center, Vector3D axis, double radius, double area, List<Point3D> vertices)
    {
        Type = MeasureElementType.Cylinder;
        PartIndex = partIndex;
        CenterPoint = center;
        if (axis.Length > 1e-6) axis.Normalize();
        AxisDirection = axis;
        FaceNormal = axis;
        Radius = Math.Max(0.001, radius);
        FaceArea = area;
        FaceVertices = vertices ?? new List<Point3D>();
        LoopVertices = new List<Point3D>();
    }

    public override string ToString()
    {
        return Type switch
        {
            MeasureElementType.Circle => $"圆孔 (⌀: {Diameter:F2} mm, 半径: {Radius:F2} mm)",
            MeasureElementType.Cylinder => $"圆柱面 (⌀: {Diameter:F2} mm, 半径: {Radius:F2} mm)",
            MeasureElementType.Point => $"点 ({CenterPoint.X:F2}, {CenterPoint.Y:F2}, {CenterPoint.Z:F2})",
            MeasureElementType.Edge => $"边线 (长: {EdgeLength:F2} mm)",
            MeasureElementType.Face => $"平面 (面积: {FaceArea:F2} mm²)",
            _ => "未知元素"
        };
    }
}

/// <summary>
/// 3D 尺寸测量综合计算结果（支持点-点、圆-圆、圆-面、点-面、线-线、面-面、单圆、单线、单面等）
/// </summary>
public sealed class MeasureResult
{
    public MeasureElement Element1 { get; }
    public MeasureElement? Element2 { get; }

    /// <summary>测量的空间核心数值（距离 / 直径 / 长度 / 面积）</summary>
    public double Value { get; }

    /// <summary>空间三维直线距离（向后兼容属性）</summary>
    public double Distance => Value;

    /// <summary>起点（向后兼容属性）</summary>
    public Point3D Point1 => Element1.CenterPoint;

    /// <summary>终点（向后兼容属性）</summary>
    public Point3D Point2 => Element2?.CenterPoint ?? Element1.CenterPoint;

    /// <summary>X 轴投影距离分量（单位：mm）</summary>
    public double DeltaX { get; }

    /// <summary>Y 轴投影距离分量（单位：mm）</summary>
    public double DeltaY { get; }

    /// <summary>Z 轴投影距离分量（单位：mm）</summary>
    public double DeltaZ { get; }

    /// <summary>两圆/圆柱中心距（若适用）</summary>
    public double? CenterDistance { get; }

    /// <summary>两圆/圆柱最小边缘净距（若适用）</summary>
    public double? MinDistance { get; }

    /// <summary>两圆/圆柱最大边缘跨距（若适用）</summary>
    public double? MaxDistance { get; }

    /// <summary>圆直径（单选圆或圆柱时）</summary>
    public double? Diameter { get; }

    /// <summary>圆半径（单选圆或圆柱时）</summary>
    public double? Radius { get; }

    /// <summary>3D 尺寸标尺连线起点（若有尺寸引线）</summary>
    public Point3D? DimensionLineStart { get; }

    /// <summary>3D 尺寸标尺连线终点（若有尺寸引线）</summary>
    public Point3D? DimensionLineEnd { get; }

    /// <summary>视口 2D 气泡标签的 3D 锚点位置</summary>
    public Point3D CalloutAnchorPoint { get; }

    /// <summary>就地显示的尺寸主标题（例如："100.00 mm", "⌀ 12.00 mm", "中心距: 45.00 mm"）</summary>
    public string MainText { get; }

    /// <summary>就地显示的辅助详情信息（例如："最小间距: 33.00 mm | 最大间距: 57.00 mm"）</summary>
    public string SubText { get; }

    /// <summary>
    /// 纯点对点测距构造函数（向后兼容）
    /// </summary>
    public MeasureResult(Point3D p1, Point3D p2) : this(new MeasureElement(p1), new MeasureElement(p2))
    {
    }

    /// <summary>
    /// 单个几何元素的测量（如单选圆显示直径/半径，单选边线显示长度，单选平面显示面积）
    /// </summary>
    public MeasureResult(MeasureElement element)
    {
        Element1 = element;
        Element2 = null;

        if (element.Type == MeasureElementType.Circle || element.Type == MeasureElementType.Cylinder)
        {
            Value = element.Diameter;
            Diameter = element.Diameter;
            Radius = element.Radius;
            DeltaX = 0; DeltaY = 0; DeltaZ = 0;
            DimensionLineStart = element.CenterPoint;
            DimensionLineEnd = element.CenterPoint;
            CalloutAnchorPoint = element.CenterPoint;
            MainText = $"⌀ {element.Diameter:F2} mm";
            SubText = $"半径 R: {element.Radius:F2} mm | 圆心: ({element.CenterPoint.X:F2}, {element.CenterPoint.Y:F2}, {element.CenterPoint.Z:F2})";
        }
        else if (element.Type == MeasureElementType.Edge)
        {
            Value = element.EdgeLength;
            DeltaX = Math.Abs(element.EdgeEnd.X - element.EdgeStart.X);
            DeltaY = Math.Abs(element.EdgeEnd.Y - element.EdgeStart.Y);
            DeltaZ = Math.Abs(element.EdgeEnd.Z - element.EdgeStart.Z);
            DimensionLineStart = element.EdgeStart;
            DimensionLineEnd = element.EdgeEnd;
            CalloutAnchorPoint = element.CenterPoint;
            MainText = $"{element.EdgeLength:F2} mm (边长)";
            SubText = $"ΔX: {DeltaX:F2}  ΔY: {DeltaY:F2}  ΔZ: {DeltaZ:F2} mm";
        }
        else if (element.Type == MeasureElementType.Face)
        {
            Value = element.FaceArea;
            DeltaX = 0; DeltaY = 0; DeltaZ = 0;
            CalloutAnchorPoint = element.CenterPoint;
            MainText = $"{element.FaceArea:F2} mm² (面积)";
            SubText = $"法向: ({element.FaceNormal.X:F2}, {element.FaceNormal.Y:F2}, {element.FaceNormal.Z:F2})";
        }
        else
        {
            Value = 0;
            DeltaX = 0; DeltaY = 0; DeltaZ = 0;
            CalloutAnchorPoint = element.CenterPoint;
            MainText = "已选择点";
            SubText = $"X: {element.CenterPoint.X:F2}, Y: {element.CenterPoint.Y:F2}, Z: {element.CenterPoint.Z:F2} mm";
        }
    }

    /// <summary>
    /// 双几何元素组合测量（两圆测距、圆与面、点-点、点-线、点-面、线-线、面-面等）
    /// </summary>
    public MeasureResult(MeasureElement elem1, MeasureElement elem2)
    {
        Element1 = elem1;
        Element2 = elem2;

        bool isCircle1 = elem1.Type == MeasureElementType.Circle || elem1.Type == MeasureElementType.Cylinder;
        bool isCircle2 = elem2.Type == MeasureElementType.Circle || elem2.Type == MeasureElementType.Cylinder;

        // 1. 两圆 / 两圆柱间距测量（SolidWorks 经典双圆测量：中心距、内侧最小间距、外侧最大间距）
        if (isCircle1 && isCircle2)
        {
            Point3D c1 = elem1.CenterPoint;
            Point3D c2 = elem2.CenterPoint;
            DeltaX = Math.Abs(c2.X - c1.X);
            DeltaY = Math.Abs(c2.Y - c1.Y);
            DeltaZ = Math.Abs(c2.Z - c1.Z);
            double centerDist = Math.Sqrt(DeltaX * DeltaX + DeltaY * DeltaY + DeltaZ * DeltaZ);
            CenterDistance = centerDist;
            Value = centerDist;

            double r1 = elem1.Radius;
            double r2 = elem2.Radius;
            MinDistance = Math.Max(0, centerDist - r1 - r2);
            MaxDistance = centerDist + r1 + r2;

            DimensionLineStart = c1;
            DimensionLineEnd = c2;
            CalloutAnchorPoint = new Point3D((c1.X + c2.X) * 0.5, (c1.Y + c2.Y) * 0.5, (c1.Z + c2.Z) * 0.5);
            MainText = $"中心距: {centerDist:F2} mm";
            SubText = $"最小间距: {MinDistance.Value:F2} mm | 最大间距: {MaxDistance.Value:F2} mm";
        }
        // 2. 圆与点测距
        else if ((isCircle1 && elem2.Type == MeasureElementType.Point) ||
                 (isCircle2 && elem1.Type == MeasureElementType.Point))
        {
            var circleElem = isCircle1 ? elem1 : elem2;
            var ptElem = isCircle1 ? elem2 : elem1;
            Point3D c = circleElem.CenterPoint;
            Point3D p = ptElem.CenterPoint;
            DeltaX = Math.Abs(p.X - c.X);
            DeltaY = Math.Abs(p.Y - c.Y);
            DeltaZ = Math.Abs(p.Z - c.Z);
            double dist = Math.Sqrt(DeltaX * DeltaX + DeltaY * DeltaY + DeltaZ * DeltaZ);
            CenterDistance = dist;
            Value = dist;
            MinDistance = Math.Max(0, dist - circleElem.Radius);
            MaxDistance = dist + circleElem.Radius;
            DimensionLineStart = c;
            DimensionLineEnd = p;
            CalloutAnchorPoint = new Point3D((c.X + p.X) * 0.5, (c.Y + p.Y) * 0.5, (c.Z + p.Z) * 0.5);
            MainText = $"中心距: {dist:F2} mm";
            SubText = $"到圆边缘最小间距: {MinDistance.Value:F2} mm (最大跨距: {MaxDistance.Value:F2} mm)";
        }
        // 3. 圆与平面测距
        else if ((isCircle1 && elem2.Type == MeasureElementType.Face) ||
                 (isCircle2 && elem1.Type == MeasureElementType.Face))
        {
            var circleElem = isCircle1 ? elem1 : elem2;
            var faceElem = isCircle1 ? elem2 : elem1;
            Point3D c = circleElem.CenterPoint;
            Point3D facePt = faceElem.CenterPoint;
            Vector3D n = faceElem.FaceNormal;
            double dist = Vector3D.DotProduct(c - facePt, n);
            Point3D proj = c - n * dist;
            DeltaX = Math.Abs(c.X - proj.X);
            DeltaY = Math.Abs(c.Y - proj.Y);
            DeltaZ = Math.Abs(c.Z - proj.Z);
            Value = Math.Abs(dist);
            CenterDistance = Value;
            DimensionLineStart = c;
            DimensionLineEnd = proj;
            CalloutAnchorPoint = new Point3D((c.X + proj.X) * 0.5, (c.Y + proj.Y) * 0.5, (c.Z + proj.Z) * 0.5);
            MainText = $"{Value:F2} mm (圆心到平面间距)";
            SubText = $"ΔX: {DeltaX:F2}  ΔY: {DeltaY:F2}  ΔZ: {DeltaZ:F2} mm";
        }
        // 4. 点与点测距
        else if (elem1.Type == MeasureElementType.Point && elem2.Type == MeasureElementType.Point)
        {
            Point3D p1 = elem1.CenterPoint;
            Point3D p2 = elem2.CenterPoint;
            DeltaX = Math.Abs(p2.X - p1.X);
            DeltaY = Math.Abs(p2.Y - p1.Y);
            DeltaZ = Math.Abs(p2.Z - p1.Z);
            Value = Math.Sqrt(DeltaX * DeltaX + DeltaY * DeltaY + DeltaZ * DeltaZ);
            DimensionLineStart = p1;
            DimensionLineEnd = p2;
            CalloutAnchorPoint = new Point3D((p1.X + p2.X) * 0.5, (p1.Y + p2.Y) * 0.5, (p1.Z + p2.Z) * 0.5);
            MainText = $"{Value:F2} mm";
            SubText = $"ΔX: {DeltaX:F2}  ΔY: {DeltaY:F2}  ΔZ: {DeltaZ:F2} mm";
        }
        // 5. 点与线测距
        else if ((elem1.Type == MeasureElementType.Point && elem2.Type == MeasureElementType.Edge) ||
                 (elem1.Type == MeasureElementType.Edge && elem2.Type == MeasureElementType.Point))
        {
            var ptElem = elem1.Type == MeasureElementType.Point ? elem1 : elem2;
            var edgeElem = elem1.Type == MeasureElementType.Edge ? elem1 : elem2;
            Point3D pt = ptElem.CenterPoint;
            Point3D a = edgeElem.EdgeStart;
            Point3D b = edgeElem.EdgeEnd;

            Vector3D ab = b - a;
            double abLenSq = ab.LengthSquared;
            double t = abLenSq > 1e-8 ? Vector3D.DotProduct(pt - a, ab) / abLenSq : 0;
            t = Math.Clamp(t, 0.0, 1.0);
            Point3D proj = a + ab * t;

            DeltaX = Math.Abs(pt.X - proj.X);
            DeltaY = Math.Abs(pt.Y - proj.Y);
            DeltaZ = Math.Abs(pt.Z - proj.Z);
            Value = (pt - proj).Length;
            DimensionLineStart = pt;
            DimensionLineEnd = proj;
            CalloutAnchorPoint = new Point3D((pt.X + proj.X) * 0.5, (pt.Y + proj.Y) * 0.5, (pt.Z + proj.Z) * 0.5);
            MainText = $"{Value:F2} mm (点到线)";
            SubText = $"ΔX: {DeltaX:F2}  ΔY: {DeltaY:F2}  ΔZ: {DeltaZ:F2} mm";
        }
        // 6. 点与面测距
        else if ((elem1.Type == MeasureElementType.Point && elem2.Type == MeasureElementType.Face) ||
                 (elem1.Type == MeasureElementType.Face && elem2.Type == MeasureElementType.Point))
        {
            var ptElem = elem1.Type == MeasureElementType.Point ? elem1 : elem2;
            var faceElem = elem1.Type == MeasureElementType.Face ? elem1 : elem2;
            Point3D pt = ptElem.CenterPoint;
            Point3D facePt = faceElem.CenterPoint;
            Vector3D normal = faceElem.FaceNormal;

            double dist = Vector3D.DotProduct(pt - facePt, normal);
            Point3D proj = pt - normal * dist;

            DeltaX = Math.Abs(pt.X - proj.X);
            DeltaY = Math.Abs(pt.Y - proj.Y);
            DeltaZ = Math.Abs(pt.Z - proj.Z);
            Value = Math.Abs(dist);
            DimensionLineStart = pt;
            DimensionLineEnd = proj;
            CalloutAnchorPoint = new Point3D((pt.X + proj.X) * 0.5, (pt.Y + proj.Y) * 0.5, (pt.Z + proj.Z) * 0.5);
            MainText = $"{Value:F2} mm (点到面法向距离)";
            SubText = $"ΔX: {DeltaX:F2}  ΔY: {DeltaY:F2}  ΔZ: {DeltaZ:F2} mm";
        }
        // 7. 面与面测距
        else if (elem1.Type == MeasureElementType.Face && elem2.Type == MeasureElementType.Face)
        {
            Vector3D n1 = elem1.FaceNormal;
            Vector3D n2 = elem2.FaceNormal;
            double dot = Math.Abs(Vector3D.DotProduct(n1, n2));

            // 平行平面
            if (dot >= 0.98)
            {
                // 计算两平行面法向间距（厚度/跨度）
                double dist = Math.Abs(Vector3D.DotProduct(elem2.CenterPoint - elem1.CenterPoint, n1));
                Point3D p1 = elem1.CenterPoint;
                Point3D p2 = p1 + n1 * (Vector3D.DotProduct(elem2.CenterPoint - p1, n1));

                DeltaX = Math.Abs(p2.X - p1.X);
                DeltaY = Math.Abs(p2.Y - p1.Y);
                DeltaZ = Math.Abs(p2.Z - p1.Z);
                Value = dist;
                DimensionLineStart = p1;
                DimensionLineEnd = p2;
                CalloutAnchorPoint = new Point3D((p1.X + p2.X) * 0.5, (p1.Y + p2.Y) * 0.5, (p1.Z + p2.Z) * 0.5);
                MainText = $"{Value:F2} mm (平行面间距)";
                SubText = $"ΔX: {DeltaX:F2}  ΔY: {DeltaY:F2}  ΔZ: {DeltaZ:F2} mm";
            }
            else
            {
                // 非平行平面，计算夹角
                double angleDeg = Math.Acos(Math.Clamp(dot, 0.0, 1.0)) * (180.0 / Math.PI);
                Value = (elem2.CenterPoint - elem1.CenterPoint).Length;
                DeltaX = Math.Abs(elem2.CenterPoint.X - elem1.CenterPoint.X);
                DeltaY = Math.Abs(elem2.CenterPoint.Y - elem1.CenterPoint.Y);
                DeltaZ = Math.Abs(elem2.CenterPoint.Z - elem1.CenterPoint.Z);
                DimensionLineStart = elem1.CenterPoint;
                DimensionLineEnd = elem2.CenterPoint;
                CalloutAnchorPoint = new Point3D((elem1.CenterPoint.X + elem2.CenterPoint.X) * 0.5, (elem1.CenterPoint.Y + elem2.CenterPoint.Y) * 0.5, (elem1.CenterPoint.Z + elem2.CenterPoint.Z) * 0.5);
                MainText = $"夹角: {angleDeg:F1}° (间距: {Value:F2} mm)";
                SubText = $"面1面积: {elem1.FaceArea:F1} mm² | 面2面积: {elem2.FaceArea:F1} mm²";
            }
        }
        // 8. 线与线测距
        else if (elem1.Type == MeasureElementType.Edge && elem2.Type == MeasureElementType.Edge)
        {
            Vector3D d1 = elem1.EdgeDirection;
            Vector3D d2 = elem2.EdgeDirection;
            double dot = Math.Abs(Vector3D.DotProduct(d1, d2));

            // 平行线
            if (dot >= 0.98)
            {
                Point3D p1 = elem1.CenterPoint;
                Point3D a = elem2.EdgeStart;
                Point3D b = elem2.EdgeEnd;
                Vector3D ab = b - a;
                double t = ab.LengthSquared > 1e-8 ? Vector3D.DotProduct(p1 - a, ab) / ab.LengthSquared : 0;
                Point3D proj = a + ab * t;

                Value = (p1 - proj).Length;
                DeltaX = Math.Abs(p1.X - proj.X);
                DeltaY = Math.Abs(p1.Y - proj.Y);
                DeltaZ = Math.Abs(p1.Z - proj.Z);
                DimensionLineStart = p1;
                DimensionLineEnd = proj;
                CalloutAnchorPoint = new Point3D((p1.X + proj.X) * 0.5, (p1.Y + proj.Y) * 0.5, (p1.Z + proj.Z) * 0.5);
                MainText = $"{Value:F2} mm (平行线间距)";
                SubText = $"ΔX: {DeltaX:F2}  ΔY: {DeltaY:F2}  ΔZ: {DeltaZ:F2} mm";
            }
            else
            {
                double angleDeg = Math.Acos(Math.Clamp(dot, 0.0, 1.0)) * (180.0 / Math.PI);
                Value = (elem2.CenterPoint - elem1.CenterPoint).Length;
                DeltaX = Math.Abs(elem2.CenterPoint.X - elem1.CenterPoint.X);
                DeltaY = Math.Abs(elem2.CenterPoint.Y - elem1.CenterPoint.Y);
                DeltaZ = Math.Abs(elem2.CenterPoint.Z - elem1.CenterPoint.Z);
                DimensionLineStart = elem1.CenterPoint;
                DimensionLineEnd = elem2.CenterPoint;
                CalloutAnchorPoint = new Point3D((elem1.CenterPoint.X + elem2.CenterPoint.X) * 0.5, (elem1.CenterPoint.Y + elem2.CenterPoint.Y) * 0.5, (elem1.CenterPoint.Z + elem2.CenterPoint.Z) * 0.5);
                MainText = $"夹角: {angleDeg:F1}° (中心距: {Value:F2} mm)";
                SubText = $"线1长: {elem1.EdgeLength:F2} mm | 线2长: {elem2.EdgeLength:F2} mm";
            }
        }
        // 9. 线与面测距
        else
        {
            var edgeElem = elem1.Type == MeasureElementType.Edge ? elem1 : elem2;
            var faceElem = elem1.Type == MeasureElementType.Face ? elem1 : elem2;
            Point3D pt = edgeElem.CenterPoint;
            Point3D facePt = faceElem.CenterPoint;
            Vector3D normal = faceElem.FaceNormal;

            double dist = Vector3D.DotProduct(pt - facePt, normal);
            Point3D proj = pt - normal * dist;

            DeltaX = Math.Abs(pt.X - proj.X);
            DeltaY = Math.Abs(pt.Y - proj.Y);
            DeltaZ = Math.Abs(pt.Z - proj.Z);
            Value = Math.Abs(dist);
            DimensionLineStart = pt;
            DimensionLineEnd = proj;
            CalloutAnchorPoint = new Point3D((pt.X + proj.X) * 0.5, (pt.Y + proj.Y) * 0.5, (pt.Z + proj.Z) * 0.5);
            MainText = $"{Value:F2} mm (线到面法向距离)";
            SubText = $"ΔX: {DeltaX:F2}  ΔY: {DeltaY:F2}  ΔZ: {DeltaZ:F2} mm";
        }
    }
}
