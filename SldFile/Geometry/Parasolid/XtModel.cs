using System;
using System.Collections.Generic;
using System.Numerics;

namespace SldFile.Geometry.Parasolid;

// ── Geometry primitives ──────────────────────────────────────────────────────

public abstract class XtGeometry
{
    public int Id { get; set; }
}

public sealed class XtPoint : XtGeometry
{
    public Vector3 Coordinates { get; set; }
    public XtPoint() { }
    public XtPoint(Vector3 coord) { Coordinates = coord; }
    public XtPoint(float x, float y, float z) { Coordinates = new Vector3(x, y, z); }
}

public sealed class XtLine : XtGeometry
{
    public Vector3 Origin { get; set; }
    public Vector3 Direction { get; set; }
}

public sealed class XtCircle : XtGeometry
{
    public Vector3 Center { get; set; }
    public Vector3 Normal { get; set; }
    public Vector3 XAxis { get; set; }
    public double Radius { get; set; }
}

public sealed class XtPlane : XtGeometry
{
    public Vector3 Origin { get; set; }
    public Vector3 Normal { get; set; }
    public Vector3 XAxis { get; set; }
}

public sealed class XtCylinder : XtGeometry
{
    public Vector3 Origin { get; set; }
    public Vector3 Axis { get; set; }
    public Vector3 XAxis { get; set; }
    public double Radius { get; set; }
}

public sealed class XtCone : XtGeometry
{
    public Vector3 Origin { get; set; }
    public Vector3 Axis { get; set; }
    public Vector3 XAxis { get; set; }
    public double HalfAngle { get; set; }
}

public sealed class XtSphere : XtGeometry
{
    public Vector3 Center { get; set; }
    public double Radius { get; set; }
}

public sealed class XtTorus : XtGeometry
{
    public Vector3 Origin { get; set; }
    public Vector3 Axis { get; set; }
    public double MajorRadius { get; set; }
    public double MinorRadius { get; set; }
}

// ── Topology primitives ──────────────────────────────────────────────────────

public abstract class XtTopology
{
    public int Id { get; set; }
}

public sealed class XtVertex : XtTopology
{
    public int PointId { get; set; }
    public Vector3 Coordinates { get; set; }
}

public sealed class XtEdge : XtTopology
{
    public int CurveId { get; set; }
    public int StartVertexId { get; set; }
    public int EndVertexId { get; set; }
    public XtGeometry? Curve { get; set; }
    public XtVertex? StartVertex { get; set; }
    public XtVertex? EndVertex { get; set; }
}

public sealed class XtFin : XtTopology
{
    public int EdgeId { get; set; }
    public bool Sense { get; set; } = true; // true = forward, false = reversed
    public XtEdge? Edge { get; set; }
}

public sealed class XtLoop : XtTopology
{
    public bool IsOuter { get; set; } = true;
    public IList<XtFin> Fins { get; } = new List<XtFin>();
}

public sealed class XtFace : XtTopology
{
    public int SurfaceId { get; set; }
    public bool Sense { get; set; } = true;
    public XtGeometry? Surface { get; set; }
    public IList<XtLoop> Loops { get; } = new List<XtLoop>();
}

public sealed class XtShell : XtTopology
{
    public bool IsClosed { get; set; } = true;
    public IList<XtFace> Faces { get; } = new List<XtFace>();
}

public sealed class XtBody : XtTopology
{
    public string Name { get; set; } = string.Empty;
    public string BodyType { get; set; } = "solid"; // solid, sheet, wire
    public IList<XtShell> Shells { get; } = new List<XtShell>();
}

/// <summary>
/// Owned Parasolid B-Rep model containing topology and analytic/parametric geometry.
/// </summary>
public sealed class XtModel
{
    public XtHeader? Header { get; set; }
    public IList<XtBody> Bodies { get; } = new List<XtBody>();
    public IDictionary<int, XtGeometry> Geometries { get; } = new Dictionary<int, XtGeometry>();
    public IDictionary<int, XtTopology> Topologies { get; } = new Dictionary<int, XtTopology>();
}
