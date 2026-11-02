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
    public string ClassName => IsCut ? "moICE_c" : "moExtrusion_c";
    public uint TypeId => IsCut ? 0x56u : 0x4Fu;

    // ── 深度与终止条件 (Directions 1 & 2) ──

    /// <summary>方向 1 拉伸深度 (米，SI单位)。对应 IExtrudeFeatureData2::GetDepth(true)</summary>
    public double Depth { get; set; }

    /// <summary>方向 2 拉伸深度 (米，SI单位)。对应 IExtrudeFeatureData2::GetDepth(false)</summary>
    public double DepthDirection2 { get; set; }

    /// <summary>方向 1 终止条件 (0=Blind, 1=ThroughAll, 2=ThroughAllBoth, 3=UpToNext, 4=UpToVertex, 5=UpToSurface, 6=MidPlane 等)。对应 IExtrudeFeatureData2::GetEndCondition(true)</summary>
    public uint EndCondition { get; set; }

    /// <summary>方向 2 终止条件。对应 IExtrudeFeatureData2::GetEndCondition(false)</summary>
    public uint EndConditionDirection2 { get; set; }

    /// <summary>是否双向拉伸。对应 IExtrudeFeatureData2::BothDirections</summary>
    public bool BothDirections { get; set; }

    /// <summary>是否反向拉伸。对应 IExtrudeFeatureData2::ReverseDirection</summary>
    public bool ReverseDirection { get; set; }

    // ── 拔模选项 (Drafting) ──

    /// <summary>方向 1 拔模角度 (度或弧度)。对应 IExtrudeFeatureData2::GetDraftAngle(true)</summary>
    public double DraftAngle { get; set; }

    /// <summary>方向 2 拔模角度。对应 IExtrudeFeatureData2::GetDraftAngle(false)</summary>
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

    /// <summary>薄壁类型 (0=OneDirection, 1=MidPlane, 2=TwoDirection)。对应 IExtrudeFeatureData2::ThinWallType</summary>
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

    /// <summary>父草图名称 (如 "草图1" 或 "Sketch1")</summary>
    public string? SketchName { get; set; }

    /// <summary>所选封闭轮廓集合。对应 IExtrudeFeatureData2::Contours</summary>
    public IReadOnlyList<SketchContourReference> SelectedContours { get; set; } = Array.Empty<SketchContourReference>();

    /// <summary>所选封闭区域集合。对应 IExtrudeFeatureData2::Contours (Region)</summary>
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
/// Sheet metal base flange feature definition data.
/// Corresponds to the SolidWorks API <c>IBaseFlangeFeatureData</c> interface.
/// </summary>
public sealed class SheetMetalBaseFlangeDefinitionData : IFeatureData
{
    public string ClassName => "moSMBaseFlange_c";
    public uint TypeId => 0x5E;

    // ── 核心钣金参数 ──

    /// <summary>钣金板厚 (米)。对应 IBaseFlangeFeatureData::Thickness</summary>
    public double Thickness { get; set; }

    /// <summary>折弯内圆角半径 (米)。对应 IBaseFlangeFeatureData::BendRadius</summary>
    public double BendRadius { get; set; }

    /// <summary>是否使用默认折弯内圆角半径。对应 IBaseFlangeFeatureData::UseDefaultBendRadius</summary>
    public bool UseDefaultBendRadius { get; set; } = true;

    /// <summary>板厚方向反向。对应 IBaseFlangeFeatureData::ReverseThickness</summary>
    public bool ReverseThickness { get; set; }

    // ── 拉伸深度与方向 ──

    /// <summary>基体法兰拉伸深度 (米)。对应 IBaseFlangeFeatureData::Depth / GetDepth(true)</summary>
    public double Depth { get; set; }

    /// <summary>方向 2 深度 (米)。对应 IBaseFlangeFeatureData::GetDepth(false)</summary>
    public double DepthDirection2 { get; set; }

    /// <summary>是否双向拉伸。对应 IBaseFlangeFeatureData::BothDirections</summary>
    public bool BothDirections { get; set; }

    /// <summary>拉伸方向反向。对应 IBaseFlangeFeatureData::ReverseDirection</summary>
    public bool ReverseDirection { get; set; }

    // ── 折弯余量与计算 ──

    /// <summary>K 因子 (默认 0.5)。对应 IBaseFlangeFeatureData::KFactor</summary>
    public double KFactor { get; set; } = 0.5;

    /// <summary>折弯余量类型 (0=KFactor, 1=BendAllowance, 2=BendDeduction, 3=BendCalculation)。对应 IBaseFlangeFeatureData::BendAllowanceType</summary>
    public uint BendAllowanceType { get; set; }

    // ── 释放槽 (Auto Relief) ──

    /// <summary>自动释放槽类型 (0=Rectangular, 1=Tear, 2=Obround)。对应 IBaseFlangeFeatureData::AutoReliefType</summary>
    public uint AutoReliefType { get; set; }

    /// <summary>释放槽比例。对应 IBaseFlangeFeatureData::ReliefRatio</summary>
    public double ReliefRatio { get; set; } = 0.5;

    /// <summary>释放槽宽度 (米)。对应 IBaseFlangeFeatureData::ReliefWidth</summary>
    public double ReliefWidth { get; set; }

    /// <summary>释放槽深度 (米)。对应 IBaseFlangeFeatureData::ReliefDepth</summary>
    public double ReliefDepth { get; set; }

    /// <summary>是否使用默认释放槽。对应 IBaseFlangeFeatureData::UseDefaultBendRelief</summary>
    public bool UseDefaultBendRelief { get; set; } = true;

    /// <summary>是否使用钣金规格表。对应 IBaseFlangeFeatureData::UseGaugeTable</summary>
    public bool UseGaugeTable { get; set; }

    // ── 拓扑与 Selection 选择项 (Topology & Selection) ──

    /// <summary>父草图名称 (如 "草图1" 或 "Sketch1")</summary>
    public string? SketchName { get; set; }

    /// <summary>所选封闭轮廓集合。对应 IBaseFlangeFeatureData::Contours</summary>
    public IReadOnlyList<SketchContourReference> SelectedContours { get; set; } = Array.Empty<SketchContourReference>();

    /// <summary>所选封闭区域集合。对应 IBaseFlangeFeatureData::Contours (Region)</summary>
    public IReadOnlyList<SketchRegionReference> SelectedRegions { get; set; } = Array.Empty<SketchRegionReference>();

    /// <summary>成形方向向量或参考实体</summary>
    public DirectionReference? DirectionReference { get; set; }

    /// <summary>多实体特征作用范围</summary>
    public BodyScopeData BodyScope { get; set; } = new BodyScopeData();

    internal byte[] RawData = Array.Empty<byte>();
}
