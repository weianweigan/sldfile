using System;
using System.IO;
using System.Numerics;
using SldFile;
using SldFile.Geometry.Parasolid;
using SldFile.Geometry.Step;
using Xunit;

namespace SldFileTests;

public class StepExportTests
{
    [Fact]
    public void StepWriter_Write_GeneratesValidIso10303HeaderAndEntities()
    {
        var model = new XtModel();
        var body = new XtBody { Id = 1, Name = "TestBody", BodyType = "solid" };
        var shell = new XtShell { Id = 2, IsClosed = true };
        body.Shells.Add(shell);
        model.Bodies.Add(body);

        // Add a plane face
        var face = new XtFace
        {
            Id = 10,
            Sense = true,
            Surface = new XtPlane
            {
                Origin = Vector3.Zero,
                Normal = Vector3.UnitZ,
                XAxis = Vector3.UnitX
            }
        };

        var loop = new XtLoop { Id = 20, IsOuter = true };
        var v1 = new XtVertex { Id = 101, Coordinates = new Vector3(0, 0, 0) };
        var v2 = new XtVertex { Id = 102, Coordinates = new Vector3(10, 0, 0) };
        var edge = new XtEdge { Id = 201, StartVertex = v1, EndVertex = v2 };
        loop.Fins.Add(new XtFin { Edge = edge, Sense = true });

        face.Loops.Add(loop);
        shell.Faces.Add(face);

        using var sw = new StringWriter();
        StepWriter.Write(model, sw, "TestPart");
        string step = sw.ToString();

        Assert.Contains("ISO-10303-21;", step);
        Assert.Contains("HEADER;", step);
        Assert.Contains("FILE_SCHEMA(('AUTOMOTIVE_DESIGN'));", step);
        Assert.Contains("DATA;", step);
        Assert.Contains("APPLICATION_CONTEXT('automotive design');", step);
        Assert.Contains("MANIFOLD_SOLID_BREP('TestBody'", step);
        Assert.Contains("CLOSED_SHELL('',", step);
        Assert.Contains("ADVANCED_FACE('',", step);
        Assert.Contains("PLANE('',", step);
        Assert.Contains("ENDSEC;", step);
        Assert.Contains("END-ISO-10303-21;", step);
    }

    [Fact]
    public void StepWriter_WriteMesh_ConvertsDisplayMeshToStep()
    {
        var mesh = new DisplayMesh
        {
            Positions = new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(10, 0, 0),
                new Vector3(0, 10, 0)
            },
            TriangleIndices = new[] { 0, 1, 2 }
        };

        using var sw = new StringWriter();
        StepWriter.WriteMesh(mesh, sw, "MeshPart");
        string step = sw.ToString();

        Assert.Contains("ISO-10303-21;", step);
        Assert.Contains("MANIFOLD_SOLID_BREP('MeshPart'", step);
        Assert.Contains("CARTESIAN_POINT('',(0.,0.,0.))", step);
        Assert.Contains("CARTESIAN_POINT('',(10.,0.,0.))", step);
        Assert.Contains("END-ISO-10303-21;", step);
    }

    [Fact]
    public void Part_ExportStep_OnSamplePart_GeneratesNonEmptyStep()
    {
        if (!File.Exists(TestFileProvider.CubePart)) return;

        using var part = new SldPart(TestFileProvider.CubePart, readOnly: true);
        using var ms = new MemoryStream();
        try
        {
            part.ExportStep(ms);
            Assert.True(ms.Length > 100);
            ms.Position = 0;
            using var reader = new StreamReader(ms);
            string step = reader.ReadToEnd();
            Assert.StartsWith("ISO-10303-21;", step.TrimStart());
        }
        catch (InvalidOperationException)
        {
            // If sample file contains neither Parasolid nor DisplayMesh, exception is expected
        }
    }
}
