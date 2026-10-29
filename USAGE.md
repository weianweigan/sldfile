# SldFile Detailed Usage Guide

**English** | [简体中文](USAGE.zh-CN.md) | [Back to README](README.md)

This document provides in-depth code examples and usage guides for the **SldFile** library.

---

## Table of Contents

- [1. Reading and Editing Document Metadata](#1-reading-and-editing-document-metadata)
- [2. Custom Properties and Configuration Properties](#2-custom-properties-and-configuration-properties)
- [3. Extracting Preview Images](#3-extracting-preview-images)
- [4. Assembly Hierarchy and Bill of Materials (BOM)](#4-assembly-hierarchy-and-bill-of-materials-bom)
- [5. 3D Geometry: Parasolid and Mesh Export](#5-3d-geometry-parasolid-and-mesh-export)
- [6. 2D Drawing Inspection](#6-2d-drawing-inspection)
- [7. Inspecting Sketches](#7-inspecting-sketches)
- [8. Working with Sketch Templates (.ske)](#8-working-with-sketch-templates-ske)
- [9. Document Inspection (References, Features, Revision History)](#9-document-inspection-references-features-revision-history)

---

### 1. Reading and Editing Document Metadata

Read or update standard metadata fields. Setting a field to `null` clears the value.

```csharp
using SldFile;

using var doc = SwFile.Open("part.sldprt", readOnly: false);

// Read metadata
Console.WriteLine($"Title: {doc.Metadata.Title}");
Console.WriteLine($"Author: {doc.Metadata.Author}");
Console.WriteLine($"Created: {doc.Metadata.CreatedAt}");
Console.WriteLine($"Last Saved: {doc.Metadata.LastSavedAt}");

// Edit metadata
doc.Metadata.Title = "Mounting Bracket";
doc.Metadata.Author = "Engineering Team";
doc.Metadata.Subject = "Robotics Subsystem";
doc.Metadata.Keywords = "bracket; robotics; aluminum";
doc.Metadata.Comments = "Revision B approved for manufacturing";
doc.Metadata.Company = "Acme Robotics Corp";
doc.Metadata.Manager = "Jane Doe";
doc.Metadata.Category = "Mechanical Parts";

doc.Save();
```

---

### 2. Custom Properties and Configuration Properties

Manage custom properties at both the document level and configuration level. Supported types include `string`, `int`, `double`, `bool`, and `DateTime`.

```csharp
using SldFile;

using var doc = SwFile.Open("assembly.sldasm", readOnly: false);

// Document-level custom properties
doc.CustomProperties.Set("PartNumber", "BRK-2026-001");
doc.CustomProperties.Set("Cost", 24.95);
doc.CustomProperties.Set("Approved", true);
doc.CustomProperties.Set("ReleaseDate", DateTime.UtcNow);

// Read a property
if (doc.CustomProperties.TryGetValue("PartNumber", out var partNo))
{
    Console.WriteLine($"Part No: {partNo}");
}

// Remove a property
doc.CustomProperties.Remove("ObsoleteProperty");

// Configuration-specific properties
foreach (var config in doc.Configurations)
{
    Console.WriteLine($"Config: {config.Name} (Index: {config.Index})");
    config.CustomProperties.Set("Material", "Aluminum 6061-T6");
    config.CustomProperties.Set("Weight_Kg", 0.45);
}

doc.Save();
```

---

### 3. Extracting Preview Images

SolidWorks embeds thumbnail previews in files. SldFile allows extracting them directly without launching CAD software.

```csharp
using SldFile;

using var doc = SwFile.Open("model.sldprt");

// Extract high-resolution PNG preview
byte[]? pngBytes = doc.GetPreviewPng();
if (pngBytes != null)
{
    File.WriteAllBytes("preview.png", pngBytes);
}

// Extract standard BMP preview
byte[]? bmpBytes = doc.GetPreviewBitmap();
if (bmpBytes != null)
{
    File.WriteAllBytes("preview.bmp", bmpBytes);
}
```

---

### 4. Assembly Hierarchy and Bill of Materials (BOM)

Inspect component hierarchies, transforms, and automatically generate an aggregated Bill of Materials.

```csharp
using SldFile;

using var assembly = SwFile.Open("robot_arm.sldasm");

// 1. Inspect Assembly Component Tree
var components = assembly.GetAssemblyTree();
foreach (var node in components)
{
    Console.WriteLine($"{node.Name} (Config: {node.ConfigurationName})");
    Console.WriteLine($"  File: {node.FilePath}");
    Console.WriteLine($"  Virtual Component: {node.IsVirtual}");
    Console.WriteLine($"  Transform Matrix: {node.Transform}");
}

// 2. Generate Aggregated Bill of Materials (BOM)
var bom = assembly.GetBom();
foreach (var item in bom)
{
    Console.WriteLine($"Item #{item.ItemNumber}: {item.PartNumber} - Qty: {item.Quantity} ({item.ConfigurationName})");
}

// 3. Extract Embedded Virtual Component Data
byte[]? virtualPartBytes = assembly.GetVirtualComponentBytes("VirtualPart1");
```

---

### 5. 3D Geometry: Parasolid and Mesh Export

Export native Parasolid B-Rep geometry or display meshes for rendering and 3D printing.

```csharp
using SldFile;

using var part = SwFile.Open("housing.sldprt");

// 1. Export Parasolid Binary Transmit Data (.x_b)
part.ExportParasolid("housing.x_b", configIndex: 0);

// Or get bytes in memory
byte[]? parasolidBytes = part.ExtractParasolid(configIndex: 0);

// 2. Decode 3D Display Mesh
DisplayMesh? mesh = part.GetDisplayMesh();
if (mesh != null)
{
    Console.WriteLine($"Vertices: {mesh.VertexCount}, Triangles: {mesh.TriangleCount}");

    // Export to Wavefront OBJ format
    using (var writer = new StreamWriter("housing.obj"))
    {
        mesh.WriteObj(writer);
    }

    // Export to binary STL format (e.g. for 3D printing)
    using (var stream = File.Create("housing.stl"))
    {
        mesh.WriteStl(stream);
    }
}
```

---

### 6. 2D Drawing Inspection

Inspect drawing sheets, views, and preview images from `.slddrw` files.

```csharp
using SldFile;

using var drawing = SwFile.Open("drawing.slddrw");

// Get drawing sheets
var sheets = drawing.GetDrawingSheets();
foreach (var sheet in sheets)
{
    Console.WriteLine($"Sheet: {sheet.Name} (Index: {sheet.SheetIndex})");

    // Sheet preview image
    if (sheet.PreviewPng != null)
    {
        File.WriteAllBytes($"{sheet.Name}.png", sheet.PreviewPng);
    }

    // Inspect drawing views
    foreach (var view in sheet.Views)
    {
        Console.WriteLine($"  View: {view.Name}");
        Console.WriteLine($"    Referenced Model: {view.ReferencedModelPath}");
        Console.WriteLine($"    Referenced Config: {view.ReferencedConfiguration}");
    }
}
```

---

### 7. Inspecting Sketches

Extract sketch geometry including points, line segments, circles, arcs, and driving dimensions.

```csharp
using SldFile;

using var doc = SwFile.Open("bracket.sldprt");

// Read sketches from active configuration
foreach (var sketch in doc.Sketches)
{
    Console.WriteLine($"Sketch: {sketch.Name}");

    // Points (coordinates in meters)
    foreach (var pt in sketch.Points)
    {
        Console.WriteLine($"  Point {pt.Index}: ({pt.X:F4}, {pt.Y:F4}, {pt.Z:F4})");
    }

    // Segments (lines, arcs, circles, splines)
    foreach (var seg in sketch.Segments)
    {
        Console.WriteLine($"  Segment: {seg.Kind} from Pt[{seg.StartPointIndex}] to Pt[{seg.EndPointIndex}]");
    }

    // Dimensions
    foreach (var dim in sketch.Dimensions)
    {
        Console.WriteLine($"  Dimension: {dim.Name} = {dim.Value} (Display: {dim.DisplayText})");
    }
}
```

---

### 8. Working with Sketch Templates (`.ske`)

Inspect and modify SolidWorks sketch-based feature templates (e.g., Hole Wizard, custom hole profiles).

```csharp
using SldFile;

// Open a sketch template
var template = SketchTemplate.Open("CounterboreHole.ske");
Console.WriteLine($"Features: {template.FeatureCount}");

// Inspect dimension & parameter names
foreach (var dim in template.DimensionNames)
    Console.WriteLine($"Dimension: {dim}");

foreach (var param in template.ParameterNames)
    Console.WriteLine($"Parameter: {param}");

// Rename dimensions or parameters
template.TryRenameDimension("D1", "HoleDiameter");
template.TryRenameParameter("Drill Angle", "CustomDrillAngle");

// Save template
template.Save("CustomCounterboreHole.ske");
```

---

### 9. Document Inspection (References, Features, Revision History)

Inspect external file references, FeatureManager tree features, contributing users, and revision logs.

```csharp
using SldFile;

using var doc = SwFile.Open("assembly.sldasm");

// External file references
Console.WriteLine("External References:");
foreach (var reference in doc.References)
{
    Console.WriteLine($"  {reference.ShortName} -> {reference.FilePath}");
}

// FeatureManager feature names and kinds
Console.WriteLine("\nFeatures:");
foreach (var feature in doc.Features)
{
    Console.WriteLine($"  [{feature.Kind}] {feature.Name}");
}

// Revision history / change logs
Console.WriteLine("\nRevisions:");
foreach (var rev in doc.Revisions)
{
    Console.WriteLine($"  [{rev.Timestamp}] {rev.UserName}: {rev.Action} {rev.FeatureName}");
}

// Contributing users
Console.WriteLine("\nUsers: " + string.Join(", ", doc.Users));
```
