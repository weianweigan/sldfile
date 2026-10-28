![SldFile — Read SolidWorks files directly from code](docs/assets/readme-banner.png)

# SldFile

**English** | [简体中文](README.zh-CN.md)

**SldFile** is a pure .NET library designed to inspect, read, and edit SolidWorks files (`.sldprt`, `.sldasm`, `.slddrw`) and sketch templates (`.ske`) offline—completely independent of SolidWorks installations or Windows COM interop.

Targeting **.NET Standard 2.0**, SldFile runs seamlessly across Windows, Linux, and macOS runtimes (.NET Core, .NET 5/6/7/8/9+, and .NET Framework 4.6.1+).

---

## Features

- **No SolidWorks Required**: 100% managed C# implementation. Runs on headless servers, containers (Docker), Linux, macOS, and Windows with zero external CAD dependencies.
- **Full Document Format Support**:
  - Parts (`.sldprt`)
  - Assemblies (`.sldasm`)
  - Drawings (`.slddrw`)
  - Sketch Feature Templates (`.ske`)
  - Supports both legacy OLE Compound File (CFB) and modern section-archive containers.
- **Metadata & Properties Editing**:
  - Read and update standard document metadata: Title, Subject, Author, Keywords, Comments, Company, Manager, Category, Last Saved By.
  - Read and edit document-level custom properties and configuration-specific properties.
  - Strongly-typed values: `string`, `int`, `double`, `bool`, and `DateTime`.
- **Non-Destructive Saving**:
  - Saves only the modified metadata and properties.
  - Preserves untouched streams, geometry, and internal data structures byte-by-byte.
- **Assembly & BOM Extraction**:
  - Inspect full component hierarchy trees with configuration details and transform matrices.
  - Automatically aggregate and compute Bill of Materials (BOM) with quantities and part numbers.
  - Read embedded virtual component documents.
- **3D Geometry & Mesh Export**:
  - Extract and export raw Parasolid binary transmit data (`.x_b`) per configuration.
  - Decode 3D display tessellation meshes (vertices, normals, triangle indices, outlines).
  - Export meshes directly to **Wavefront OBJ** (`.obj`) and binary **STL** (`.stl`).
- **2D Drawing Sheets & Views**:
  - Retrieve drawing sheets, drawing view hierarchies, and referenced model paths/configurations.
  - Extract per-sheet PNG preview images.
- **Sketches & Feature Templates**:
  - Extract sketch entities: points, segments (lines, arcs, circles, splines), relations, and dimensions.
  - Inspect, rename dimension/parameter definitions, and save sketch templates (`.ske`).
- **Visuals & Inspection**:
  - Extract high-resolution PNG and standard BMP preview images.
  - Access external file references, user history, revision logs, and FeatureManager feature lists.

---

## Installation

Install via the .NET CLI:

```bash
dotnet add package SldFile
```

Or via the Package Manager Console:

```powershell
Install-Package SldFile
```

Or add the package reference to your `.csproj`:

```xml
<PackageReference Include="SldFile" Version="1.0.0" />
```

---

## How to Use

### 1. Opening and Saving Documents

Documents are opened in **read-only mode by default** to prevent accidental modifications. Pass `readOnly: false` when you plan to edit and save.

```csharp
using SldFile;

// Read-only mode (default)
using var doc = SwFile.Open("bracket.sldprt");

// Edit mode
using var editableDoc = SwFile.Open("bracket.sldprt", readOnly: false);
// ... make modifications ...
editableDoc.Save(); // Persists changes to disk
```

---

### 2. Reading and Editing Document Metadata

Read or update standard metadata fields. Setting a field to `null` clears the value.

```csharp
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

### 3. Custom Properties & Configuration Properties

Manage custom properties at both document level and configuration level. Supported types include `string`, `int`, `double`, `bool`, and `DateTime`.

```csharp
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

### 4. Extracting Preview Images

SolidWorks embeds thumbnail previews in files. SldFile allows extracting them directly without launching CAD software.

```csharp
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

### 5. Assembly Structure & Bill of Materials (BOM)

Inspect component hierarchies, transforms, and automatically generate a Bill of Materials.

```csharp
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

### 6. 3D Geometry: Parasolid & Tessellation Mesh Export

Export native Parasolid B-Rep geometry or display meshes for rendering and 3D printing.

```csharp
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

### 7. 2D Drawing Inspection

Inspect drawing sheets, views, and preview images from `.slddrw` files.

```csharp
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

### 8. Inspecting Sketches

Extract sketch geometry including points, line segments, circles, arcs, and driving dimensions.

```csharp
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

### 9. Working with Sketch Templates (`.ske`)

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

### 10. Document Inspection (References, Features, Revision History)

Inspect references, users, and edit logs without opening CAD.

```csharp
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

