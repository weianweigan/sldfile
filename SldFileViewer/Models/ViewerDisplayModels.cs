namespace SldFileViewer.Models;

/// <summary>
/// SolidWorks 经典 7+1 标准视角类型定义
/// </summary>
public enum StandardViewType
{
    /// <summary>等轴测视图 (Isometric, 快捷键 Ctrl+7)</summary>
    Isometric,
    /// <summary>主视 / 前视图 (Front, 快捷键 Ctrl+1)</summary>
    Front,
    /// <summary>后视图 (Back, 快捷键 Ctrl+2)</summary>
    Back,
    /// <summary>左视图 (Left, 快捷键 Ctrl+3)</summary>
    Left,
    /// <summary>右视图 (Right, 快捷键 Ctrl+4)</summary>
    Right,
    /// <summary>俯视 / 上视图 (Top, 快捷键 Ctrl+5)</summary>
    Top,
    /// <summary>仰视 / 下视图 (Bottom, 快捷键 Ctrl+6)</summary>
    Bottom,
    /// <summary>正视于所选面 (Normal To, 快捷键 Ctrl+8)</summary>
    NormalTo
}

/// <summary>
/// 3D CAD 模型显示样式定义
/// </summary>
public enum ModelDisplayStyle
{
    /// <summary>带边线上色 (Shaded with Edges) - 默认 CAD 金属漆面与深色工程边缘</summary>
    ShadedWithEdges,
    /// <summary>仅上色 (Shaded) - 平滑实体表面，无轮廓黑线</summary>
    Shaded,
    /// <summary>线框模式 (Wireframe) - 纯轮廓线框模式，实体表面透明</summary>
    Wireframe,
    /// <summary>消除隐藏线白模 (Technical Illustration) - 哑光纯白工程纸感，高对比黑线</summary>
    TechnicalWhite
}
