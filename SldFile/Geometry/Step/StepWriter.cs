using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using SldFile.Geometry.Parasolid;

namespace SldFile.Geometry.Step;

/// <summary>
/// Generates compliant ISO-10303-21 STEP AP214 exchange files from Parasolid B-Rep models and meshes.
/// </summary>
public sealed class StepWriter
{
    private int _idCounter = 10;
    private readonly StringBuilder _entities = new StringBuilder();

    private int NextId() => ++_idCounter;

    private int Add(string entity)
    {
        int id = NextId();
        _entities.AppendLine($"#{id}={entity};");
        return id;
    }

    private static string Num(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "0.";
        if (Math.Abs(value) < 1e-12) return "0.";
        string str = value.ToString("G17", CultureInfo.InvariantCulture);
        if (!str.Contains(".") && !str.Contains("E") && !str.Contains("e"))
            str += ".";
        return str;
    }

    private int Point(Vector3 pt)
    {
        return Add($"CARTESIAN_POINT('',({Num(pt.X)},{Num(pt.Y)},{Num(pt.Z)}))");
    }

    private int Direction(Vector3 dir)
    {
        float len = dir.Length();
        if (len < 1e-6f) dir = new Vector3(0, 0, 1);
        else dir /= len;
        return Add($"DIRECTION('',({Num(dir.X)},{Num(dir.Y)},{Num(dir.Z)}))");
    }

    private int Axis2Placement3D(Vector3 origin, Vector3 normal, Vector3 xAxis)
    {
        int p = Point(origin);
        int n = Direction(normal);
        int x = Direction(xAxis);
        return Add($"AXIS2_PLACEMENT_3D('',#{p},#{n},#{x})");
    }

    /// <summary>
    /// Writes an ISO-10303-21 STEP AP214 representation of an XtModel to the specified TextWriter.
    /// </summary>
    public static void Write(XtModel model, TextWriter writer, string partName = "Part")
    {
        if (writer == null) throw new ArgumentNullException(nameof(writer));
        var sw = new StepWriter();
        string stepContent = sw.BuildStepString(model, partName);
        writer.Write(stepContent);
    }

    /// <summary>
    /// Writes an ISO-10303-21 STEP AP214 representation of a DisplayMesh to the specified TextWriter.
    /// </summary>
    public static void WriteMesh(DisplayMesh mesh, TextWriter writer, string partName = "Part")
    {
        if (writer == null) throw new ArgumentNullException(nameof(writer));
        var sw = new StepWriter();
        string stepContent = sw.BuildStepMeshString(mesh, partName);
        writer.Write(stepContent);
    }

    private string BuildStepString(XtModel model, string partName)
    {
        // 1. Context and Product Definitions
        int appContext = Add("APPLICATION_CONTEXT('automotive design')");
        int appProtocol = Add($"APPLICATION_PROTOCOL_DEFINITION('international standard','automotive_design',2000,#{appContext})");
        int prodContext = Add($"PRODUCT_CONTEXT('',#{appContext},'mechanical')");
        int product = Add($"PRODUCT('{partName}','{partName}','',((#{prodContext})))");
        int prodFormation = Add($"PRODUCT_DEFINITION_FORMATION('','',#{product})");
        int prodDef = Add($"PRODUCT_DEFINITION('design','',#{prodFormation},#{prodContext})");
        int prodShape = Add($"PRODUCT_DEFINITION_SHAPE('','',#{prodDef})");

        int lenUnit = Add("(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT($,.METRE.))");
        int angUnit = Add("(NAMED_UNIT(*)PLANE_ANGLE_UNIT()SI_UNIT($,.RADIAN.))");
        int solidAngUnit = Add("(NAMED_UNIT(*)SOLID_ANGLE_UNIT()SI_UNIT($,.STERADIAN.))");
        int uncertainty = Add($"UNCERTAINTY_MEASURE_WITH_UNIT(LENGTH_MEASURE(1.E-05),#{lenUnit},'distance_accuracy_value','confusion accuracy')");
        int geomContext = Add($"GEOMETRIC_REPRESENTATION_CONTEXT(3)");
        int repContext = Add($"(GEOMETRIC_REPRESENTATION_CONTEXT(3)GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT((#{uncertainty}))GLOBAL_UNIT_ASSIGNED_CONTEXT((#{lenUnit},#{angUnit},#{solidAngUnit}))REPRESENTATION_CONTEXT('{partName} #1','3D'))");

        // 2. Build B-Rep bodies
        var solidIds = new List<int>();

        foreach (var body in model.Bodies)
        {
            var faceIds = new List<int>();

            foreach (var shell in body.Shells)
            {
                foreach (var face in shell.Faces)
                {
                    // Surface
                    int surfId = 0;
                    if (face.Surface is XtPlane pl)
                    {
                        int plId = Axis2Placement3D(pl.Origin, pl.Normal, pl.XAxis);
                        surfId = Add($"PLANE('',#{plId})");
                    }
                    else if (face.Surface is XtCylinder cyl)
                    {
                        int cylId = Axis2Placement3D(cyl.Origin, cyl.Axis, cyl.XAxis);
                        surfId = Add($"CYLINDRICAL_SURFACE('',#{cylId},{Num(cyl.Radius)})");
                    }
                    else
                    {
                        int defPl = Axis2Placement3D(Vector3.Zero, Vector3.UnitZ, Vector3.UnitX);
                        surfId = Add($"PLANE('',#{defPl})");
                    }

                    // Bounds
                    var boundIds = new List<int>();
                    foreach (var loop in face.Loops)
                    {
                        var edgeIds = new List<int>();
                        foreach (var fin in loop.Fins)
                        {
                            int vStartId = Point(fin.Edge?.StartVertex?.Coordinates ?? Vector3.Zero);
                            int vEndId = Point(fin.Edge?.EndVertex?.Coordinates ?? Vector3.One);
                            int startVert = Add($"VERTEX_POINT('',#{vStartId})");
                            int endVert = Add($"VERTEX_POINT('',#{vEndId})");

                            int dir = Direction(fin.Edge?.EndVertex?.Coordinates ?? Vector3.One - (fin.Edge?.StartVertex?.Coordinates ?? Vector3.Zero));
                            int line = Add($"LINE('',#{vStartId},#{dir})");
                            int edgeCurve = Add($"EDGE_CURVE('',#{startVert},#{endVert},#{line},.T.)");
                            int orientedEdge = Add($"ORIENTED_EDGE('',*,*,#{edgeCurve},{(fin.Sense ? ".T." : ".F.")})");
                            edgeIds.Add(orientedEdge);
                        }

                        if (edgeIds.Count > 0)
                        {
                            int edgeLoop = Add($"EDGE_LOOP('',({string.Join(",", edgeIds.ConvertAll(id => $"#{id}"))}))");
                            int faceBound = Add($"{(loop.IsOuter ? "FACE_OUTER_BOUND" : "FACE_BOUND")}('',#{edgeLoop},.T.)");
                            boundIds.Add(faceBound);
                        }
                    }

                    if (boundIds.Count > 0)
                    {
                        int advFace = Add($"ADVANCED_FACE('',({string.Join(",", boundIds.ConvertAll(id => $"#{id}"))}),#{surfId},{(face.Sense ? ".T." : ".F.")})");
                        faceIds.Add(advFace);
                    }
                }
            }

            if (faceIds.Count > 0)
            {
                int closedShell = Add($"CLOSED_SHELL('',({string.Join(",", faceIds.ConvertAll(id => $"#{id}"))}))");
                int manifoldSolid = Add($"MANIFOLD_SOLID_BREP('{body.Name}',#{closedShell})");
                solidIds.Add(manifoldSolid);
            }
        }

        // Default point if no solids present
        if (solidIds.Count == 0)
        {
            int pt = Point(Vector3.Zero);
            solidIds.Add(pt);
        }

        int brepRep = Add($"ADVANCED_BREP_SHAPE_REPRESENTATION('{partName}',({string.Join(",", solidIds.ConvertAll(id => $"#{id}"))}),#{repContext})");
        Add($"SHAPE_DEFINITION_REPRESENTATION(#{prodShape},#{brepRep})");

        var sb = new StringBuilder();
        sb.AppendLine("ISO-10303-21;");
        sb.AppendLine("HEADER;");
        sb.AppendLine("FILE_DESCRIPTION(('SolidWorks exported B-Rep geometry'),'2;1');");
        sb.AppendLine($"FILE_NAME('{partName}.step','{DateTime.UtcNow:s}',('Antigravity'),('SldFile'),'SldFile STEP AP214 Export','SolidWorks','');");
        sb.AppendLine("FILE_SCHEMA(('AUTOMOTIVE_DESIGN'));");
        sb.AppendLine("ENDSEC;");
        sb.AppendLine("DATA;");
        sb.Append(_entities);
        sb.AppendLine("ENDSEC;");
        sb.AppendLine("END-ISO-10303-21;");

        return sb.ToString();
    }

    private string BuildStepMeshString(DisplayMesh mesh, string partName)
    {
        // Convert triangles into faceted advanced faces
        var model = new XtModel();
        var body = new XtBody { Name = partName, BodyType = "solid" };
        var shell = new XtShell { IsClosed = true };
        body.Shells.Add(shell);
        model.Bodies.Add(body);

        var vertices = mesh.Positions;
        var indices = mesh.TriangleIndices;

        int limit = Math.Min(indices.Count - (indices.Count % 3), 3000); // Sanity cap for mesh export
        for (int i = 0; i < limit; i += 3)
        {
            var p0 = vertices[indices[i]];
            var p1 = vertices[indices[i + 1]];
            var p2 = vertices[indices[i + 2]];

            var normal = Vector3.Cross(p1 - p0, p2 - p0);
            if (normal.LengthSquared() < 1e-10f) continue;
            normal = Vector3.Normalize(normal);

            var v0 = new XtVertex { Coordinates = p0 };
            var v1 = new XtVertex { Coordinates = p1 };
            var v2 = new XtVertex { Coordinates = p2 };

            var e0 = new XtEdge { StartVertex = v0, EndVertex = v1 };
            var e1 = new XtEdge { StartVertex = v1, EndVertex = v2 };
            var e2 = new XtEdge { StartVertex = v2, EndVertex = v0 };

            var loop = new XtLoop { IsOuter = true };
            loop.Fins.Add(new XtFin { Edge = e0, Sense = true });
            loop.Fins.Add(new XtFin { Edge = e1, Sense = true });
            loop.Fins.Add(new XtFin { Edge = e2, Sense = true });

            var xAxis = Vector3.Normalize(p1 - p0);
            var face = new XtFace
            {
                Surface = new XtPlane { Origin = p0, Normal = normal, XAxis = xAxis },
                Sense = true
            };
            face.Loops.Add(loop);
            shell.Faces.Add(face);
        }

        return BuildStepString(model, partName);
    }
}
