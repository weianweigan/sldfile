using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Base interface for typed SolidWorks feature definition data.
/// Corresponds to SolidWorks API feature definitions such as IExtrudeFeatureData2, IBaseFlangeFeatureData.
/// </summary>
public interface IFeatureData
{
    /// <summary>MFC class name (e.g. "moExtrusion_c", "moBaseFlange_c").</summary>
    string ClassName { get; }

    /// <summary>Folder/feature type identifier from the FolderHeader.</summary>
    uint TypeId { get; }
}

/// <summary>
/// Extrusion feature definition data (Extrude Boss/Base, Extrude Cut).
/// Corresponds to the SolidWorks API <c>IExtrudeFeatureData2</c> interface.
/// </summary>
public sealed class ExtrudeFeatureDefinitionData : IFeatureData
{
    private readonly HashSet<string> _resolved = new HashSet<string>(StringComparer.Ordinal);

    // Thin extrusion is serialized with its own measured class (moExtruThin_c / TypeId 0x52),
    // not as an option bit on moExtrusion_c. Keep ClassName/TypeId consistent with the
    // serialized class so callers can distinguish a thin body from a solid Boss.
    public string ClassName => IsThinFeature ? "moExtruThin_c" : IsCut ? "moICE_c" : "moExtrusion_c";
    public uint TypeId => IsThinFeature ? 0x52u : IsCut ? 0x56u : 0x4Fu;

    /// <summary>Names of fields marked as structurally resolved by a controlled parser.</summary>
    public IReadOnlyCollection<string> ResolvedProperties => _resolved;

    /// <summary>True only when the named field was explicitly marked resolved. Default is false.</summary>
    public bool IsResolved(string propertyName) =>
        !string.IsNullOrEmpty(propertyName) && _resolved.Contains(propertyName);

    /// <summary>Marks a field as structurally resolved. Parsers call this only after validation.</summary>
    private void MarkResolved(string propertyName)
    {
        if (!string.IsNullOrEmpty(propertyName))
            _resolved.Add(propertyName);
    }

    internal void MarkResolvedInternal(string propertyName) => MarkResolved(propertyName);

    internal void ClearResolvedInternal() => _resolved.Clear();

    // ── 深度与终止条件 (Directions 1 & 2) ──

    /// <summary>方向 1 拉伸深度 (米，SI单位)。对应 IExtrudeFeatureData2::GetDepth(true)</summary>
    public double Depth { get; set; }

    /// <summary>方向 2 拉伸深度 (米，SI单位)。对应 IExtrudeFeatureData2::GetDepth(false)</summary>
    public double DepthDirection2 { get; set; }

    /// <summary>方向 1 终止条件 (0=Blind, 1=ThroughAll, 2=ThroughNext, 3=UpToVertex, 4=UpToSurface, 5=OffsetFromSurface, 6=MidPlane, 7=UpToBody, 9=ThroughAllBoth, 10=UpToSelection, 11=UpToNext)。对应 IExtrudeFeatureData2::GetEndCondition(true)</summary>
    public uint EndCondition { get; set; }

    /// <summary>方向 2 终止条件。对应 IExtrudeFeatureData2::GetEndCondition(false)</summary>
    public uint EndConditionDirection2 { get; set; }

    /// <summary>是否双向拉伸。对应 IExtrudeFeatureData2::BothDirections</summary>
    public bool BothDirections { get; set; }

    /// <summary>是否反向拉伸。对应 IExtrudeFeatureData2::ReverseDirection</summary>
    public bool ReverseDirection { get; set; }

    // ── 拔模选项 (Drafting) ──

    /// <summary>方向 1 拔模角度 (弧度)。对应 IExtrudeFeatureData2::GetDraftAngle(true)</summary>
    public double DraftAngle { get; set; }

    /// <summary>方向 2 拔模角度 (弧度)。对应 IExtrudeFeatureData2::GetDraftAngle(false)</summary>
    public double DraftAngleDirection2 { get; set; }

    /// <summary>方向 1 是否向外拔模。对应 IExtrudeFeatureData2::GetDraftOutward(true)</summary>
    public bool DraftOutward { get; set; }

    /// <summary>方向 2 是否向外拔模。对应 IExtrudeFeatureData2::GetDraftOutward(false)</summary>
    public bool DraftOutwardDirection2 { get; set; }

    // ── 起始条件 (From Condition) ──

    /// <summary>起始条件类型 (0=SketchPlane, 1=Surface/Face/Plane, 2=Vertex, 3=Offset)。对应 IExtrudeFeatureData2::FromType</summary>
    public uint FromType { get; set; }

    /// <summary>起始偏移距离 (米)。对应 IExtrudeFeatureData2::FromOffsetDistance</summary>
    public double FromOffsetDistance { get; set; }

    /// <summary>起始偏移是否反向。对应 IExtrudeFeatureData2::FromOffsetReverse</summary>
    public bool FromOffsetReverse { get; set; }

    // ── 实体操作与合并选项 (Body & Merge) ──

    /// <summary>是否合并结果 (多实体零件中合并实体)。对应 IExtrudeFeatureData2::Merge</summary>
    public bool Merge { get; set; } = true;

    /// <summary>多实体特征范围选项。对应 IExtrudeFeatureData2::FeatureScope</summary>
    public bool FeatureScope { get; set; }

    /// <summary>是否自动选择受影响实体。对应 IExtrudeFeatureData2::AutoSelect</summary>
    public bool AutoSelect { get; set; } = true;

    /// <summary>装配体特征范围选项。对应 IExtrudeFeatureData2::AssemblyFeatureScope</summary>
    public bool AssemblyFeatureScope { get; set; }

    /// <summary>装配体中是否自动选择组件。对应 IExtrudeFeatureData2::AutoSelectComponents</summary>
    public bool AutoSelectComponents { get; set; }

    /// <summary>是否将装配体特征传播到零件。对应 IExtrudeFeatureData2::PropagateFeatureToParts</summary>
    public bool PropagateFeatureToParts { get; set; }

    // ── 切除特有属性 (Cut Extrude) ──

    /// <summary>是否为切除特征。若为 true，对应 IExtrudeFeatureData2 属于切除操作</summary>
    public bool IsCut { get; set; }

    /// <summary>反侧切除。对应 IExtrudeFeatureData2::FlipSideToCut</summary>
    public bool FlipSideToCut { get; set; }

    /// <summary>正交切除 (钣金)。对应 IExtrudeFeatureData2::NormalCut</summary>
    public bool NormalCut { get; set; }

    /// <summary>优化几何体。对应 IExtrudeFeatureData2::OptimizeGeometry</summary>
    public bool OptimizeGeometry { get; set; }

    // ── 薄壁特征选项 (Thin Feature) ──

    /// <summary>是否为薄壁特征。对应 IExtrudeFeatureData2::IsThinFeature()</summary>
    public bool IsThinFeature { get; set; }

    /// <summary>薄壁类型 (0=OneDirection, 1=OppDirection, 2=MidPlane, 3=TwoDirection)，对齐 swThinWallType_e。对应 IExtrudeFeatureData2::ThinWallType</summary>
    public uint ThinWallType { get; set; }

    /// <summary>方向 1 壁厚 (米)。对应 IExtrudeFeatureData2::GetWallThickness(true)</summary>
    public double WallThickness { get; set; }

    /// <summary>方向 2 壁厚 (米)。对应 IExtrudeFeatureData2::GetWallThickness(false)</summary>
    public double WallThicknessDirection2 { get; set; }

    /// <summary>加盖端点。对应 IExtrudeFeatureData2::CapEnds</summary>
    public bool CapEnds { get; set; }

    /// <summary>端盖厚度 (米)。对应 IExtrudeFeatureData2::CapThickness</summary>
    public double CapThickness { get; set; }

    // ── 钣金联动 ──

    /// <summary>钣金联动厚度。对应 IExtrudeFeatureData2::LinkToThickness</summary>
    public bool LinkToThickness { get; set; }

    // ── 拔模与平移高级选项 ──

    /// <summary>方向 1 是否拔模。对应 IExtrudeFeatureData2::GetDraftWhileExtruding(true)</summary>
    public bool DraftWhileExtruding { get; set; }

    /// <summary>方向 2 是否拔模。对应 IExtrudeFeatureData2::GetDraftWhileExtruding(false)</summary>
    public bool DraftWhileExtrudingDirection2 { get; set; }

    /// <summary>方向 1 表面平移。对应 IExtrudeFeatureData2::GetTranslateSurface(true)</summary>
    public bool TranslateSurface { get; set; }

    /// <summary>方向 2 表面平移。对应 IExtrudeFeatureData2::GetTranslateSurface(false)</summary>
    public bool TranslateSurfaceDirection2 { get; set; }

    /// <summary>方向 1 反向等距。对应 IExtrudeFeatureData2::GetReverseOffset(true)</summary>
    public bool ReverseOffset { get; set; }

    /// <summary>方向 2 反向等距。对应 IExtrudeFeatureData2::GetReverseOffset(false)</summary>
    public bool ReverseOffsetDirection2 { get; set; }

    /// <summary>自动圆角角点。对应 IExtrudeFeatureData2::AutoFilletCorners</summary>
    public bool AutoFilletCorners { get; set; }

    /// <summary>自动圆角半径 (米)。对应 IExtrudeFeatureData2::AutoFilletRadius</summary>
    public double AutoFilletRadius { get; set; }

    /// <summary>是否为基体拉伸。对应 IExtrudeFeatureData2::IsBaseExtrude()</summary>
    public bool IsBaseExtrude { get; set; }

    // ── 拓扑与 Selection 选择项 (Topology & Selection) ──

    /// <summary>候选父草图名称，由前序草图推断；持久父草图句柄尚未解析。</summary>
    public string? SketchName { get; set; }

    /// <summary>已验证布局中的草图选择模式。显式模式不代表所选轮廓/区域句柄已解析。</summary>
    public SketchSelectionMode SketchSelectionMode { get; set; }

    /// <summary>候选前序草图的可用轮廓结构，并非特征的已选轮廓。</summary>
    public IReadOnlyList<SketchContourReference> AvailableSketchContours { get; set; } = Array.Empty<SketchContourReference>();

    /// <summary>候选前序草图的可用区域结构，并非特征的已选区域。</summary>
    public IReadOnlyList<SketchRegionReference> AvailableSketchRegions { get; set; } = Array.Empty<SketchRegionReference>();

    /// <summary>已解析的所选封闭轮廓。读取时选择句柄尚未解析则为空，应结合 SketchSelectionMode 判断策略。</summary>
    public IReadOnlyList<SketchContourReference> SelectedContours { get; set; } = Array.Empty<SketchContourReference>();

    /// <summary>已解析的所选区域。读取时选择句柄尚未解析则为空，不代表原生特征没有显式选区。</summary>
    public IReadOnlyList<SketchRegionReference> SelectedRegions { get; set; } = Array.Empty<SketchRegionReference>();

    /// <summary>拉伸方向向量或参考实体 (如边线、基准面)。对应 IExtrudeFeatureData2::DirectionReference</summary>
    public DirectionReference? DirectionReference { get; set; }

    /// <summary>终止目标实体引用 (Up to Surface/Vertex/Body)。对应 IExtrudeFeatureData2::GetEndConditionEntity()</summary>
    public EndConditionEntityReference? EndConditionEntity { get; set; }

    /// <summary>多实体特征作用范围。对应 IExtrudeFeatureData2::FeatureScopeBodies</summary>
    public BodyScopeData BodyScope { get; set; } = new BodyScopeData();

    // ── IExtrudeFeatureData2 对齐便捷方法 (Convenience API Methods) ──

    /// <summary>获取拉伸深度 (米)。对应 IExtrudeFeatureData2::GetDepth</summary>
    public double GetDepth(bool forward) => forward ? Depth : DepthDirection2;

    /// <summary>设置拉伸深度 (米)。对应 IExtrudeFeatureData2::SetDepth</summary>
    public void SetDepth(bool forward, double depth) { if (forward) Depth = depth; else DepthDirection2 = depth; }

    /// <summary>获取终止条件。对应 IExtrudeFeatureData2::GetEndCondition</summary>
    public uint GetEndCondition(bool forward) => forward ? EndCondition : EndConditionDirection2;

    /// <summary>设置终止条件。对应 IExtrudeFeatureData2::SetEndCondition</summary>
    public void SetEndCondition(bool forward, uint condition) { if (forward) EndCondition = condition; else EndConditionDirection2 = condition; }

    /// <summary>获取拔模角度。对应 IExtrudeFeatureData2::GetDraftAngle</summary>
    public double GetDraftAngle(bool forward) => forward ? DraftAngle : DraftAngleDirection2;

    /// <summary>设置拔模角度。对应 IExtrudeFeatureData2::SetDraftAngle</summary>
    public void SetDraftAngle(bool forward, double angle) { if (forward) DraftAngle = angle; else DraftAngleDirection2 = angle; }

    /// <summary>获取是否向外拔模。对应 IExtrudeFeatureData2::GetDraftOutward</summary>
    public bool GetDraftOutward(bool forward) => forward ? DraftOutward : DraftOutwardDirection2;

    /// <summary>设置是否向外拔模。对应 IExtrudeFeatureData2::SetDraftOutward</summary>
    public void SetDraftOutward(bool forward, bool outward) { if (forward) DraftOutward = outward; else DraftOutwardDirection2 = outward; }

    /// <summary>获取拉伸时是否拔模。对应 IExtrudeFeatureData2::GetDraftWhileExtruding</summary>
    public bool GetDraftWhileExtruding(bool forward) => forward ? DraftWhileExtruding : DraftWhileExtrudingDirection2;

    /// <summary>设置拉伸时是否拔模。对应 IExtrudeFeatureData2::SetDraftWhileExtruding</summary>
    public void SetDraftWhileExtruding(bool forward, bool draft) { if (forward) DraftWhileExtruding = draft; else DraftWhileExtrudingDirection2 = draft; }

    /// <summary>获取壁厚 (米)。对应 IExtrudeFeatureData2::GetWallThickness</summary>
    public double GetWallThickness(bool forward) => forward ? WallThickness : WallThicknessDirection2;

    /// <summary>设置壁厚 (米)。对应 IExtrudeFeatureData2::SetWallThickness</summary>
    public void SetWallThickness(bool forward, double thickness) { if (forward) WallThickness = thickness; else WallThicknessDirection2 = thickness; }

    /// <summary>获取是否反向等距。对应 IExtrudeFeatureData2::GetReverseOffset</summary>
    public bool GetReverseOffset(bool forward) => forward ? ReverseOffset : ReverseOffsetDirection2;

    /// <summary>设置是否反向等距。对应 IExtrudeFeatureData2::SetReverseOffset</summary>
    public void SetReverseOffset(bool forward, bool reverse) { if (forward) ReverseOffset = reverse; else ReverseOffsetDirection2 = reverse; }

    /// <summary>获取是否平移表面。对应 IExtrudeFeatureData2::GetTranslateSurface</summary>
    public bool GetTranslateSurface(bool forward) => forward ? TranslateSurface : TranslateSurfaceDirection2;

    /// <summary>设置是否平移表面。对应 IExtrudeFeatureData2::SetTranslateSurface</summary>
    public void SetTranslateSurface(bool forward, bool translate) { if (forward) TranslateSurface = translate; else TranslateSurfaceDirection2 = translate; }

    /// <summary>是否为凸台特征。对应 IExtrudeFeatureData2::IsBossFeature()</summary>
    public bool IsBossFeature() => !IsCut;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>
/// Records which <see cref="SheetMetalBaseFlangeDefinitionData"/> properties were actually
/// decoded from the ResolvedFeatures stream. A false flag means the corresponding CLR value
/// is only a model default or an authoring input; it is not evidence of the value stored in
/// the file. This distinction is required because several native defaults (for example
/// K = 0.5 and relief ratio = 0.5) coincide with common real values.
/// </summary>
public sealed class SheetMetalBaseFlangeParameterResolution
{
    public bool Thickness { get; internal set; }
    public bool BendRadius { get; internal set; }
    public bool Depth { get; internal set; }
    public bool BothDirections { get; internal set; }
    public bool DepthDirection2 { get; internal set; }
    public bool ReverseDirection { get; internal set; }
    public bool ReverseThickness { get; internal set; }
    public bool UseDefaultBendRadius { get; internal set; }
    public bool KFactor { get; internal set; }
    public bool BendAllowanceType { get; internal set; }
    public bool AutoReliefType { get; internal set; }
    public bool ReliefRatio { get; internal set; }
    public bool ReliefWidth { get; internal set; }
    public bool ReliefDepth { get; internal set; }
    public bool UseDefaultBendRelief { get; internal set; }
    public bool UseGaugeTable { get; internal set; }
    public bool BendAllowance { get; internal set; }
    public bool BendDeduction { get; internal set; }
    public bool GaugeTablePath { get; internal set; }
    public bool ThicknessTableName { get; internal set; }
    public bool BendTableFile { get; internal set; }
}

/// <summary>
/// Sheet metal base flange feature definition data.
/// Corresponds to the SolidWorks API <c>IBaseFlangeFeatureData</c> interface.
/// </summary>
public sealed class SheetMetalBaseFlangeDefinitionData : IFeatureData
{
    public string ClassName => "moSMBaseFlange_c";
    public uint TypeId => 0x5E;

    /// <summary>
    /// Per-property evidence flags. Only decoded stream values are marked true.
    /// </summary>
    public SheetMetalBaseFlangeParameterResolution ParameterResolution { get; } = new();

    // ── 核心钣金参数 ──

    /// <summary>钣金板厚 (米)。对应 IBaseFlangeFeatureData::Thickness。仅当 <see cref="SheetMetalBaseFlangeParameterResolution.Thickness"/> 为 true 时为文件值。</summary>
    public double Thickness { get; set; }

    /// <summary>折弯内圆角半径 (米)。对应 IBaseFlangeFeatureData::BendRadius。仅当 <see cref="SheetMetalBaseFlangeParameterResolution.BendRadius"/> 为 true 时为文件值。</summary>
    public double BendRadius { get; set; }

    /// <summary>
    /// 是否使用默认折弯内圆角半径。对应 IBaseFlangeFeatureData 的 OverrideRadius 语义。
    /// 尚未从流中解码：sm_l_override_radius 与 sm_l_use_global_radius 的原生
    /// OverrideDefaultSheetMetalParameters/OverrideRadius 均回读 false，只有半径值可读。
    /// </summary>
    public bool UseDefaultBendRadius { get; set; } = true;

    /// <summary>板厚方向反向。对应 IBaseFlangeFeatureData::ReverseThickness。仅当 <see cref="SheetMetalBaseFlangeParameterResolution.ReverseThickness"/> 为 true 时为文件值。</summary>
    public bool ReverseThickness { get; set; }

    // ── 拉伸深度与方向 ──

    /// <summary>基体法兰拉伸深度 (米)。对应 IBaseFlangeFeatureData::D1EndConditionDistance。仅当 <see cref="SheetMetalBaseFlangeParameterResolution.Depth"/> 为 true 时为文件值。</summary>
    public double Depth { get; set; }

    /// <summary>方向 2 深度 (米)。对应 IBaseFlangeFeatureData::D2EndConditionDistance。仅当 <see cref="SheetMetalBaseFlangeParameterResolution.DepthDirection2"/> 为 true 时为文件值。</summary>
    public double DepthDirection2 { get; set; }

    /// <summary>是否双向拉伸。存在已解析的方向 2 距离时为 true。对应方向 2 终止状态。</summary>
    public bool BothDirections { get; set; }

    /// <summary>拉伸方向反向。对应 IBaseFlangeFeatureData::ReverseDirection。仅当 <see cref="SheetMetalBaseFlangeParameterResolution.ReverseDirection"/> 为 true 时为文件值。</summary>
    public bool ReverseDirection { get; set; }

    // ── 折弯余量与计算 ──

    /// <summary>K 因子。对应 IBaseFlangeFeatureData::KFactor，读取自全局 moSheetMetal_c 的 D2 标量。仅当 <see cref="SheetMetalBaseFlangeParameterResolution.KFactor"/> 为 true 时为文件值。</summary>
    public double KFactor { get; set; } = 0.5;

    /// <summary>
    /// 折弯余量类型/算法。对应 IBaseFlangeFeatureData 的自定义折弯余量 (ICustomBendAllowance)。
    /// 读取自全局 moSheetMetal_c 的 D2 名称记录前 0x2A2 字节处的类型字节：
    /// 1 -> 2 KFactor，2 -> 3 Direct，3 -> 4 Deduction（2026-10-31 原生重试样本；
    /// 折弯表类型仍无可持久化样本，保持未解析）。
    /// </summary>
    public uint BendAllowanceType { get; set; }

    /// <summary>
    /// 自定义折弯余量值 (米)。对应 ICustomBendAllowance::BendAllowance。
    /// 仅当 <see cref="SheetMetalBaseFlangeParameterResolution.BendAllowance"/> 为 true 时为文件值；
    /// 读取自全局 moSheetMetal_c 的 D3 标量（Direct 类型样本）。
    /// </summary>
    public double? BendAllowance { get; set; }

    /// <summary>
    /// 折弯扣除值 (米)。对应 ICustomBendAllowance::BendDeduction。
    /// 仅当 <see cref="SheetMetalBaseFlangeParameterResolution.BendDeduction"/> 为 true 时为文件值；
    /// 读取自全局 moSheetMetal_c 的 D3 标量（Deduction 类型样本，原生回读 0.001）。
    /// </summary>
    public double? BendDeduction { get; set; }

    // ── 释放槽 (Auto Relief) ──

    /// <summary>自动释放槽类型 (1=Rectangular, 2=Tear, 3=Obround, 4=None, 5=TearBend)。对应 IBaseFlangeFeatureData::ReliefType。仅当 <see cref="SheetMetalBaseFlangeParameterResolution.AutoReliefType"/> 为 true 时为文件值。</summary>
    public uint AutoReliefType { get; set; }

    /// <summary>释放槽比例。对应 IBaseFlangeFeatureData::ReliefRatio，读取自模板命名 D6。仅当 <see cref="SheetMetalBaseFlangeParameterResolution.ReliefRatio"/> 为 true 时为文件值。</summary>
    public double ReliefRatio { get; set; } = 0.5;

    /// <summary>
    /// 释放槽宽度 (米)。对应 IBaseFlangeFeatureData::ReliefWidth。尚未从流中解码：
    /// D8 标量随 ratio×T 变化，但 sm_l_relief_w15_d25 的显式宽度/深度在保存后
    /// 回退为 0.0005/0.0005，D8/D9 宽度-深度角色仍无法消歧。
    /// </summary>
    public double ReliefWidth { get; set; }

    /// <summary>
    /// 释放槽深度 (米)。对应 IBaseFlangeFeatureData::ReliefDepth。尚未从流中解码：
    /// 与 ReliefWidth 同理，显式值未落盘，D8/D9 角色未消歧。
    /// </summary>
    public double ReliefDepth { get; set; }

    /// <summary>
    /// 是否使用默认释放槽。对应 IBaseFlangeFeatureData::UseDefaultBendRelief。
    /// 读取自全局 moSheetMetal_c 元素数据 +0x1B 的 0x40 位（2026-10-31 原生重试：
    /// 全局 AutoRelief 覆盖样本为 0x20 -> false，基线/其它样本为 0x40/0x60 -> true）。
    /// </summary>
    public bool UseDefaultBendRelief { get; set; } = true;

    /// <summary>
    /// 钣金折弯表文件路径。对应 ICustomBendAllowance::BendTableFile。
    /// 教程布局证据（Paper Airplane、drive sled）：全局 moSheetMetal_c 窗口内唯一包含
    /// “Bend Tables” 的 WChar 路径（base bend table.xls）；Complex 样本无该路径且
    /// 原生回读为 null。仅当 <see cref="SheetMetalBaseFlangeParameterResolution.BendTableFile"/> 为 true 时为文件值。
    /// </summary>
    public string? BendTableFile { get; set; }

    /// <summary>
    /// 是否使用钣金规格表。对应 IBaseFlangeFeatureData::UseGaugeTable。
    /// 读取自特征窗口内是否出现 <c>moSMEnvTableFeat_c</c>（2026-10-31 原生重试规格表样本存在，基线不存在）。
    /// </summary>
    public bool UseGaugeTable { get; set; }

    /// <summary>
    /// 规格表文件路径。对应 IBaseFlangeFeatureData::GaugeTablePath。
    /// 仅当 <see cref="SheetMetalBaseFlangeParameterResolution.GaugeTablePath"/> 为 true 时为文件值；
    /// 读取自规格表样本中唯一包含 <c>Sheet Metal Gauge Tables</c> 的 WChar 路径（API 回读为空，但流中保留完整路径）。
    /// </summary>
    public string? GaugeTablePath { get; set; }

    /// <summary>
    /// 规格表名称。对应 IBaseFlangeFeatureData::ThicknessTableName。
    /// 读取自 <c>moSMEnvTableFeat_c</c> 元素数据中的 WChar 名称（原始存储名，API 显示名可能追加行号）。
    /// </summary>
    public string? ThicknessTableName { get; set; }

    // ── 拓扑与 Selection 选择项 (Topology & Selection) ──

    /// <summary>父草图名称 (如 "草图1" 或 "Sketch1")</summary>
    public string? SketchName { get; set; }

    /// <summary>
    /// 父草图的结构化闭合轮廓 (来自 moSketchChain_c)。当前样本均为整草图基体法兰，
    /// 尚无显式“所选轮廓”样本，因此该集合是可用轮廓，不代表特征显式选择了全部条目。
    /// 对应 IBaseFlangeFeatureData::Contours。
    /// </summary>
    public IReadOnlyList<SketchContourReference> SelectedContours { get; set; } = Array.Empty<SketchContourReference>();

    /// <summary>
    /// 父草图的结构化区域 (来自 moSketchRegion_c)。当前样本均为整草图基体法兰，
    /// 尚无显式“所选区域”样本。对应 IBaseFlangeFeatureData::Contours (Region)。
    /// </summary>
    public IReadOnlyList<SketchRegionReference> SelectedRegions { get; set; } = Array.Empty<SketchRegionReference>();

    /// <summary>成形方向向量或参考实体。当前样本未使用显式方向参考，保持 null。</summary>
    public DirectionReference? DirectionReference { get; set; }

    /// <summary>多实体特征作用范围</summary>
    public BodyScopeData BodyScope { get; set; } = new BodyScopeData();

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>
/// Measured revolution data (SolidWorks 2026): single-direction solid (0x70), both-directions solid
/// (Type=2, 0x70), thin one-direction (moRevolutionThin_c / 0x50 / 0x70) and cut full revolution
/// (moRevCut_c / 0x55 / 0x71). Authoring supports the controlled solid slice plus the measured thin
/// 90/360, both-directions and cut-360 template blocks.
/// </summary>
public sealed class RevolveFeatureDefinitionData : IFeatureData
{
    private readonly HashSet<string> _resolved = new HashSet<string>(StringComparer.Ordinal);

    public string ClassName => IsThinFeature ? "moRevolutionThin_c" : IsCut ? "moRevCut_c" : "moRevolution_c";
    public uint TypeId => FolderTypeId ?? (IsThinFeature ? 0x50u : IsCut ? 0x55u : 0x4Eu);

    /// <summary>Names of fields marked as structurally resolved by a controlled parser.</summary>
    public IReadOnlyCollection<string> ResolvedProperties => _resolved;

    /// <summary>True only when the named field was explicitly marked resolved. Default is false.</summary>
    public bool IsResolved(string propertyName) =>
        !string.IsNullOrEmpty(propertyName) && _resolved.Contains(propertyName);

    private void MarkResolved(string propertyName)
    {
        if (!string.IsNullOrEmpty(propertyName))
            _resolved.Add(propertyName);
    }

    internal void MarkResolvedInternal(string propertyName) => MarkResolved(propertyName);

    internal void ClearResolvedInternal() => _resolved.Clear();

    /// <summary>Direction 1 revolution angle in radians. Authoring range: (0, 2*pi].</summary>
    public double Angle { get; set; }

    /// <summary>Direction 2 angle; only 0 has controlled evidence.</summary>
    public double AngleDirection2 { get; set; }

    /// <summary>Native IRevolveFeatureData2.Type; measured 0 (single direction) and 2 (both directions).</summary>
    public int Type { get; set; }

    /// <summary>Native axis type when decoded (1 = model edge for the Task 1.1 handoff sample).</summary>
    public int? AxisType { get; set; }

    /// <summary>Measured axis reference when decoded; null when the stream layout is unresolved.</summary>
    public FeatureReferenceData? AxisReference { get; set; }

    /// <summary>FolderHeader TypeId when it differs from the class-implied value.</summary>
    public uint? FolderTypeId { get; set; }

    /// <summary>Both directions; measured π/2 + π/4 sample with Type=2.</summary>
    public bool BothDirections { get; set; }

    /// <summary>Reverses the direction-1 sweep. Partial revolutions are evidenced; full reverse is not.</summary>
    public bool ReverseDirection { get; set; }

    /// <summary>Cut revolution; not yet supported by authoring.</summary>
    public bool IsCut { get; set; }

    /// <summary>Thin revolution; measured as moRevolutionThin_c / TypeId 0x50.</summary>
    public bool IsThinFeature { get; set; }

    /// <summary>Thin wall type (swThinWallType_e); the measured thin samples use 0 (OneDirection).</summary>
    public uint ThinWallType { get; set; }

    /// <summary>Thin wall thickness 1 (metres); measured D3=0.001 in both thin samples.</summary>
    public double Thickness1 { get; set; }

    /// <summary>Thin wall thickness 2 (metres); measured D4=0 for the one-direction thin samples.</summary>
    public double Thickness2 { get; set; }

    /// <summary>Merge result; the controlled solid samples use true.</summary>
    public bool Merge { get; set; } = true;

    /// <summary>Candidate parent sketch name used by the authoring entry point.</summary>
    public string? SketchName { get; set; }

    public RevolveFeatureDefinitionData()
    {
    }

    internal RevolveFeatureDefinitionData(double angle, bool reverseDirection)
    {
        Angle = angle;
        ReverseDirection = reverseDirection;
    }

    public double GetRevolutionAngle(bool forward) => forward ? Angle : AngleDirection2;
}
