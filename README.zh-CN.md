![SldFile — 离线读取与编辑 SolidWorks 文件](docs/assets/readme-banner.png)

# SldFile

[English](README.md) | **简体中文**

**SldFile** 是一个轻量级、高性能的纯 .NET 库，用于在脱离 SolidWorks 软件和 Windows COM 接口的环境下，离线读取与编辑 SolidWorks 文件（`.sldprt`、`.sldasm`、`.slddrw`）以及草图模板（`.ske`）。

类库目标框架为 **.NET Standard 2.0**，完美支持跨平台运行于 Windows、Linux 和 macOS（支持 .NET Core、.NET 5/6/7/8/9+ 及 .NET Framework 4.6.1+）。

---

## 项目特性

- **完全独立，无任何 CAD 依赖**：纯 C# 托管实现，无需安装 SolidWorks，无需 Windows COM Interop。可无缝运行在无头服务器、Docker 容器以及 Linux / macOS / Windows 环境中。
- **全文档格式覆盖**：
  - 零件（`.sldprt`）
  - 装配体（`.sldasm`）
  - 工程图（`.slddrw`）
  - 草图特征模板（`.ske`）
  - 完整兼容传统 OLE 复合文档（CFB）及高版本 section-archive 容器格式。
- **元数据与属性编辑**：
  - 读取与修改标准文档元数据：标题、主题、作者、关键词、备注、最后保存者、公司、经理、分类。
  - 读取与编辑文档级自定义属性及各配置特有属性。
  - 支持强类型值：`string`、`int`、`double`、`bool` 和 `DateTime`。
- **无损保存（Non-Destructive Saving）**：
  - 增量保存仅更新被修改的元数据和属性流。
  - 未修改的几何体、特征流和二进制数据保持逐字节完全一致。
- **装配体树与 BOM 生成**：
  - 解析完整装配体组件树、装配变换矩阵及配置映射。
  - 自动汇总并计算装配体物料清单（BOM），包含零件号与数量统计。
  - 支持提取嵌入式虚拟零部件数据。
- **三维几何与网格导出**：
  - 提取并导出指定配置的原生 Parasolid 二进制传输数据（`.x_b`）。
  - 解码轻量化 3D 显示网格（包含顶点、法线、三角面索引及轮廓边）。
  - 支持将网格直接导出为 **Wavefront OBJ**（`.obj`）和二进制 **STL**（`.stl`）。
- **工程图图纸与视图**：
  - 提取工程图图纸列表、图纸视图层级及引用的三维模型路径与配置。
  - 提取各图纸的独立 PNG 预览图。
- **草图几何与特征模板**：
  - 提取草图点表、图元（直线、圆/圆弧、样条曲线）、几何关系及驱动尺寸。
  - 解析、重命名尺寸/参数名称，并保存修改后的 `.ske` 草图模板。
- **可视化与信息探查**：
  - 提取高分辨率 PNG 预览图和标准 BMP 缩略图。
  - 读取外部引用文件列表、修改者列表、版本修订日志及 FeatureManager 特征树名称。

---

## 安装

通过 .NET CLI 安装：

```bash
dotnet add package SldFile
```

通过 Package Manager 控制台安装：

```powershell
Install-Package SldFile
```

或在项目 `.csproj` 文件中添加引用：

```xml
<PackageReference Include="SldFile" Version="1.0.0" />
```

---

## 如何使用

### 1. 打开与保存文档

为避免意外更改原始模型，文档**默认以只读模式打开**。若需编辑并保存，请显式传入 `readOnly: false`。

```csharp
using SldFile;

// 默认只读模式打开
using var doc = SwFile.Open("bracket.sldprt");

// 可编辑模式打开
using var editableDoc = SwFile.Open("bracket.sldprt", readOnly: false);
// ... 进行修改 ...
editableDoc.Save(); // 保存更改到磁盘
```

---

### 2. 读取与编辑文档元数据

读取或修改核心摘要信息。将属性赋值为 `null` 可清空对应内容。

```csharp
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

### 3. 自定义属性与配置属性

支持在文档级别和特定配置级别读取、新增、修改或删除自定义属性，支持的数据类型包括 `string`、`int`、`double`、`bool` 和 `DateTime`。

```csharp
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

### 4. 提取预览图

SolidWorks 会在文件内嵌入预览图像，SldFile 支持无需启动 CAD 直接提取高清 PNG 或标准 BMP。

```csharp
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

### 5. 装配体组件树与物料清单（BOM）

读取装配体的层级架构、空间位姿矩阵，并自动生成物料清单统计。

```csharp
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

### 6. 三维几何导出：Parasolid 与显示网格

导出 Parasolid 边界表示（B-Rep）几何数据，或导出轻量化三维网格用于可视化或 3D 打印。

```csharp
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

### 7. 工程图图纸与视图读取

从 `.slddrw` 工程图文件中获取图纸信息、视图信息及对应模型引用。

```csharp
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

### 8. 草图几何读取

读取草图中的点坐标、图元（线段、圆弧、圆、曲线）及尺寸参数。

```csharp
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

### 9. 草图特征模板（`.ske`）

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

### 10. 文档信息探查（外部引用、特征树、修订日志）

无需启动 CAD 即可快速了解文件的外部依赖和演变历史。

```csharp
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

