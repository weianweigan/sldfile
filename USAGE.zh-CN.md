# SldFile 详细使用指南

[English](USAGE.md) | **简体中文** | [返回 README](README.zh-CN.md)

本文档提供 **SldFile** 类库的详细代码示例与功能用法。

---

## 目录

- [1. 读取与编辑文档元数据](#1-读取与编辑文档元数据)
- [2. 自定义属性与配置属性](#2-自定义属性与配置属性)
- [3. 提取预览图](#3-提取预览图)
- [4. 装配体组件树与物料清单 (BOM)](#4-装配体组件树与物料清单-bom)
- [5. 三维几何导出：Parasolid 与显示网格](#5-三维几何导出parasolid-与显示网格)
- [6. 工程图图纸与视图读取](#6-工程图图纸与视图读取)
- [7. 草图几何读取](#7-草图几何读取)
- [8. 草图特征模板 (.ske)](#8-草图特征模板-ske)
- [9. 文档信息探查（外部引用、特征树、修订日志）](#9-文档信息探查外部引用特征树修订日志)

---

### 1. 读取与编辑文档元数据

读取或修改核心摘要信息。将属性赋值为 `null` 可清空对应内容。

```csharp
using SldFile;

using var doc = SwFile.Open("part.sldprt", readOnly: false);

// 读取元数据
Console.WriteLine($"标题: {doc.Metadata.Title}");
Console.WriteLine($"作者: {doc.Metadata.Author}");
Console.WriteLine($"创建时间: {doc.Metadata.CreatedAt}");
Console.WriteLine($"最后保存: {doc.Metadata.LastSavedAt}");

// 编辑元数据
doc.Metadata.Title = "固定支架";
doc.Metadata.Author = "研发部";
doc.Metadata.Subject = "机器人机械臂组件";
doc.Metadata.Keywords = "支架; 机械臂; 铝合金";
doc.Metadata.Comments = "B版本图纸，已通过结构评审";
doc.Metadata.Company = "示例智能装备有限公司";
doc.Metadata.Manager = "项目负责人";
doc.Metadata.Category = "机械结构件";

doc.Save();
```

---

### 2. 自定义属性与配置属性

支持在文档级别和特定配置级别读取、新增、修改或删除自定义属性，支持的数据类型包括 `string`、`int`、`double`、`bool` 和 `DateTime`。

```csharp
using SldFile;

using var doc = SwFile.Open("assembly.sldasm", readOnly: false);

// 1. 文档级自定义属性
doc.CustomProperties.Set("零件编号", "BRK-2026-001");
doc.CustomProperties.Set("单价", 24.95);
doc.CustomProperties.Set("已审核", true);
doc.CustomProperties.Set("生效日期", DateTime.UtcNow);

// 读取属性
if (doc.CustomProperties.TryGetValue("零件编号", out var partNo))
{
    Console.WriteLine($"零件编号: {partNo}");
}

// 删除属性
doc.CustomProperties.Remove("旧属性名称");

// 2. 配置特有属性
foreach (var config in doc.Configurations)
{
    Console.WriteLine($"配置名: {config.Name} (索引: {config.Index})");
    config.CustomProperties.Set("材料", "铝合金 6061-T6");
    config.CustomProperties.Set("重量_Kg", 0.45);
}

doc.Save();
```

---

### 3. 提取预览图

SolidWorks 会在文件内嵌入预览图像，SldFile 支持无需启动 CAD 直接提取高清 PNG 或标准 BMP。

```csharp
using SldFile;

using var doc = SwFile.Open("model.sldprt");

// 提取高清 PNG 预览图
byte[]? pngBytes = doc.GetPreviewPng();
if (pngBytes != null)
{
    File.WriteAllBytes("preview.png", pngBytes);
}

// 提取标准 BMP 预览图
byte[]? bmpBytes = doc.GetPreviewBitmap();
if (bmpBytes != null)
{
    File.WriteAllBytes("preview.bmp", bmpBytes);
}
```

---

### 4. 装配体组件树与物料清单 (BOM)

读取装配体的层级架构、空间位姿矩阵，并自动生成物料清单统计。

```csharp
using SldFile;

using var assembly = SwFile.Open("robot_arm.sldasm");

// 1. 遍历装配体树
var components = assembly.GetAssemblyTree();
foreach (var node in components)
{
    Console.WriteLine($"{node.Name} (配置: {node.ConfigurationName})");
    Console.WriteLine($"  文件路径: {node.FilePath}");
    Console.WriteLine($"  是否虚拟件: {node.IsVirtual}");
    Console.WriteLine($"  变换矩阵: {node.Transform}");
}

// 2. 自动汇总物料清单 (BOM)
var bom = assembly.GetBom();
foreach (var item in bom)
{
    Console.WriteLine($"项次 #{item.ItemNumber}: {item.PartNumber} - 数量: {item.Quantity} ({item.ConfigurationName})");
}

// 3. 提取内置虚拟零部件文件字节
byte[]? virtualPartBytes = assembly.GetVirtualComponentBytes("VirtualPart1");
```

---

### 5. 三维几何导出：Parasolid 与显示网格

导出 Parasolid 边界表示（B-Rep）几何数据，或导出轻量化三维网格用于可视化或 3D 打印。

```csharp
using SldFile;

using var part = SwFile.Open("housing.sldprt");

// 1. 导出 Parasolid 二进制格式 (.x_b)
part.ExportParasolid("housing.x_b", configIndex: 0);

// 或直接获取内存字节数组
byte[]? parasolidBytes = part.ExtractParasolid(configIndex: 0);

// 2. 解码三维显示网格
DisplayMesh? mesh = part.GetDisplayMesh();
if (mesh != null)
{
    Console.WriteLine($"顶点数: {mesh.VertexCount}, 三角面数: {mesh.TriangleCount}");

    // 导出为 Wavefront OBJ 格式
    using (var writer = new StreamWriter("housing.obj"))
    {
        mesh.WriteObj(writer);
    }

    // 导出为二进制 STL 格式 (例如用于 3D 打印)
    using (var stream = File.Create("housing.stl"))
    {
        mesh.WriteStl(stream);
    }
}
```

---

### 6. 工程图图纸与视图读取

从 `.slddrw` 工程图文件中获取图纸信息、视图信息及对应模型引用。

```csharp
using SldFile;

using var drawing = SwFile.Open("drawing.slddrw");

// 获取所有图纸
var sheets = drawing.GetDrawingSheets();
foreach (var sheet in sheets)
{
    Console.WriteLine($"图纸名称: {sheet.Name} (索引: {sheet.SheetIndex})");

    // 保存图纸预览图
    if (sheet.PreviewPng != null)
    {
        File.WriteAllBytes($"{sheet.Name}.png", sheet.PreviewPng);
    }

    // 遍历图纸上的所有视图
    foreach (var view in sheet.Views)
    {
        Console.WriteLine($"  视图: {view.Name}");
        Console.WriteLine($"    引用模型: {view.ReferencedModelPath}");
        Console.WriteLine($"    引用配置: {view.ReferencedConfiguration}");
    }
}
```

---

### 7. 草图几何读取

读取草图中的点坐标、图元（线段、圆弧、圆、曲线）及尺寸参数。

```csharp
using SldFile;

using var doc = SwFile.Open("bracket.sldprt");

// 读取当前配置下的所有草图
foreach (var sketch in doc.Sketches)
{
    Console.WriteLine($"草图: {sketch.Name}");

    // 点坐标（米）
    foreach (var pt in sketch.Points)
    {
        Console.WriteLine($"  点 {pt.Index}: ({pt.X:F4}, {pt.Y:F4}, {pt.Z:F4})");
    }

    // 图元段
    foreach (var seg in sketch.Segments)
    {
        Console.WriteLine($"  图元: {seg.Kind} 起点[{seg.StartPointIndex}] -> 终点[{seg.EndPointIndex}]");
    }

    // 尺寸
    foreach (var dim in sketch.Dimensions)
    {
        Console.WriteLine($"  尺寸: {dim.Name} = {dim.Value} (显示文本: {dim.DisplayText})");
    }
}
```

---

### 8. 草图特征模板（`.ske`）

解析异型孔向导及各类自定义成形特征的草图模板，支持重命名尺寸和参数。

```csharp
using SldFile;

// 打开草图模板
var template = SketchTemplate.Open("CounterboreHole.ske");
Console.WriteLine($"特征数量: {template.FeatureCount}");

// 查看尺寸和参数名称
foreach (var dim in template.DimensionNames)
    Console.WriteLine($"尺寸名称: {dim}");

foreach (var param in template.ParameterNames)
    Console.WriteLine($"参数名称: {param}");

// 重命名尺寸或参数
template.TryRenameDimension("D1", "HoleDiameter");
template.TryRenameParameter("Drill Angle", "CustomDrillAngle");

// 保存模板
template.Save("CustomCounterboreHole.ske");
```

---

### 9. 文档信息探查（外部引用、特征树、修订日志）

无需启动 CAD 即可快速了解文件的外部依赖和演变历史。

```csharp
using SldFile;

using var doc = SwFile.Open("assembly.sldasm");

// 外部引用文件
Console.WriteLine("外部引用:");
foreach (var reference in doc.References)
{
    Console.WriteLine($"  {reference.ShortName} -> {reference.FilePath}");
}

// FeatureManager 特征树名称与类型
Console.WriteLine("\n特征列表:");
foreach (var feature in doc.Features)
{
    Console.WriteLine($"  [{feature.Kind}] {feature.Name}");
}

// 历史修订日志
Console.WriteLine("\n修订记录:");
foreach (var rev in doc.Revisions)
{
    Console.WriteLine($"  [{rev.Timestamp}] {rev.UserName}: {rev.Action} {rev.FeatureName}");
}

// 参与编辑的用户
Console.WriteLine("\n用户: " + string.Join(", ", doc.Users));
```
