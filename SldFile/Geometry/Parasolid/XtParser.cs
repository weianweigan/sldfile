using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace SldFile.Geometry.Parasolid;

/// <summary>
/// Parser for extracted binary Parasolid streams (X_B), creating structured XtModel representations.
/// </summary>
public static class XtParser
{
    public static XtModel Parse(byte[] data)
    {
        var model = new XtModel();
        if (data == null || data.Length < 16) return model;

        if (XtHeader.TryParse(data, out var header))
        {
            model.Header = header;
        }

        // Lightweight parser: scan records for analytic geometry and B-Rep topologies
        // In Parasolid X_B, coordinates are IEEE 754 64-bit doubles
        var body = new XtBody { Id = 1, BodyType = "solid" };
        var shell = new XtShell { Id = 2, IsClosed = true };
        body.Shells.Add(shell);
        model.Bodies.Add(body);
        model.Topologies[1] = body;
        model.Topologies[2] = shell;

        // Scan for candidate faces and vertex coordinates in binary stream
        int vertexId = 100;

        // If data contains coordinate arrays (e.g. vertices)
        for (int i = 32; i <= data.Length - 24; i += 8)
        {
            double x = BitConverter.ToDouble(data, i);
            double y = BitConverter.ToDouble(data, i + 8);
            double z = BitConverter.ToDouble(data, i + 16);

            if (IsValidCoordinate(x) && IsValidCoordinate(y) && IsValidCoordinate(z) &&
                (Math.Abs(x) > 1e-6 || Math.Abs(y) > 1e-6 || Math.Abs(z) > 1e-6))
            {
                var pt = new XtPoint((float)x, (float)y, (float)z) { Id = vertexId };
                var vertex = new XtVertex { Id = vertexId, PointId = vertexId, Coordinates = pt.Coordinates };
                model.Geometries[vertexId] = pt;
                model.Topologies[vertexId] = vertex;
                vertexId++;
                i += 16; // skip y and z
            }
        }

        return model;
    }

    private static bool IsValidCoordinate(double val)
    {
        return !double.IsNaN(val) && !double.IsInfinity(val) && Math.Abs(val) < 10000.0;
    }
}
