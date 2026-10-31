using SldFile;

namespace SldFileViewer.Models;

/// <summary>
/// 供 UI 绑定的 SolidWorks 配置项包装模型
/// </summary>
public sealed class ConfigurationItem
{
    /// <summary>底层的 SolidWorks 配置对象</summary>
    public Configuration Configuration { get; }

    /// <summary>配置索引</summary>
    public int Index => Configuration.Index;

    /// <summary>配置显示名称（若未命名则使用默认名称）</summary>
    public string Name => !string.IsNullOrWhiteSpace(Configuration.Name)
        ? Configuration.Name!
        : $"配置 {Configuration.Index}";

    /// <summary>该配置独有的自定义属性数量</summary>
    public int PropertyCount => Configuration.CustomProperties.Count;

    public ConfigurationItem(Configuration configuration)
    {
        Configuration = configuration ?? throw new System.ArgumentNullException(nameof(configuration));
    }

    public override string ToString() => Name;
}
