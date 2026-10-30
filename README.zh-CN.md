![SldFile — 离线读取与编辑 SolidWorks 文件](Assets/banner.png)

# SldFile

[English](README.md) | **简体中文**

> [!WARNING]
> **开发中警告**：SldFile 目前处于活跃开发阶段，对不同 SolidWorks 版本及复杂文档特性的兼容性测试尚不充分。如果您在测试或使用中遇到无法兼容或解析异常的文件，非常欢迎提交 [Issue](https://github.com/weianweigan/sldfile/issues) 并附带示例模型或错误日志，共同帮助完善兼容性！

**SldFile** 是一个轻量级、高性能的纯 .NET 库，用于在脱离 SolidWorks 软件和 Windows COM 接口的环境下，离线读取与编辑 SolidWorks 文件（`.sldprt`、`.sldasm`、`.slddrw`）以及草图模板（`.ske`）。

类库目标框架为 **.NET Standard 2.0**，完美支持跨平台运行于 Windows、Linux 和 macOS（支持 .NET Core、.NET 5/6/7/8/9+ 及 .NET Framework 4.6.1+）。

![SldFile Viewer](Assets/SldFileViewer.png)

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
  - 支持将网格直接导出为 **glTF 2.0**（`.glb`）、**Wavefront OBJ**（`.obj`）和二进制 **STL**（`.stl`）。
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

### 打开与保存文档

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

### 详细使用指南

关于各项功能的完整代码示例，请参阅 **[详细使用指南](USAGE.zh-CN.md)**：

- **[1. 读取与编辑文档元数据](USAGE.zh-CN.md#1-读取与编辑文档元数据)**：标题、作者、主题、关键词、备注、公司等核心摘要信息。
- **[2. 自定义属性与配置属性](USAGE.zh-CN.md#2-自定义属性与配置属性)**：文档级与配置级强类型自定义属性（`string`、`int`、`double`、`bool`、`DateTime`）的增删改查。
- **[3. 提取预览图](USAGE.zh-CN.md#3-提取预览图)**：提取高清 PNG 与标准 BMP 缩略图。
- **[4. 装配体组件树与物料清单 (BOM)](USAGE.zh-CN.md#4-装配体组件树与物料清单-bom)**：组件层级树、位姿矩阵、虚拟件及自动汇总 BOM。
- **[5. 三维几何导出：Parasolid 与显示网格](USAGE.zh-CN.md#5-三维几何导出parasolid-与显示网格)**：导出 Parasolid（`.x_b`）及将 3D 显示网格导出为 GLB、OBJ 与 STL。
- **[6. 工程图图纸与视图读取](USAGE.zh-CN.md#6-工程图图纸与视图读取)**：图纸列表、视图层级、关联模型与图纸 PNG 预览。
- **[7. 草图几何读取](USAGE.zh-CN.md#7-草图几何读取)**：草图点坐标、图元线段/弧线/圆/样条曲线及尺寸。
- **[8. 草图特征模板 (.ske)](USAGE.zh-CN.md#8-草图特征模板-ske)**：异型孔向导等 `.ske` 模板的尺寸与参数名称修改。
- **[9. 文档信息探查](USAGE.zh-CN.md#9-文档信息探查外部引用特征树修订日志)**：外部引用、FeatureManager 特征树名称、用户与历史修订日志。
