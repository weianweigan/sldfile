namespace SldFileViewer.Models;

/// <summary>
/// 3D 剖视图剖切轴向定义
/// </summary>
public enum SectionAxis
{
    /// <summary>垂直于 X 轴的剖切平面 (YZ 平面)</summary>
    X,
    /// <summary>垂直于 Y 轴的剖切平面 (XZ 平面)</summary>
    Y,
    /// <summary>垂直于 Z 轴的剖切平面 (XY 平面)</summary>
    Z
}
