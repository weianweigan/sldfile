using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFileViewer.ViewModels;

/// <summary>FeatureData 解析状态分类，用于详情面板的徽章配色。</summary>
public enum FeatureDataStatus
{
    /// <summary>当前选择没有关联的 <see cref="Feature"/>，不显示参数模型区。</summary>
    None,
    /// <summary>文件夹特征数据已由受控解析器结构化解码。</summary>
    Resolved,
    /// <summary>该类的解析器已注册，但当前样本布局未通过受控校验（保持 null）。</summary>
    Unparsed,
    /// <summary>该类尚未注册 FeatureData 解析器。</summary>
    Unsupported
}

/// <summary>FeatureData 参数表中的一行（对应官方 API 的一个属性）。</summary>
public sealed class FeatureDataPropertyItem
{
    public string Group { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsResolved { get; set; }
    public bool IsUnresolved { get; set; }
}

/// <summary>选择项/持久拓扑引用表中的一行。</summary>
public sealed class FeatureDataReferenceItem
{
    public string Category { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

/// <summary>参数分组标题与对应参数行。</summary>
public sealed class FeatureDataSection
{
    public string Title { get; set; } = string.Empty;
    public ObservableCollection<FeatureDataPropertyItem> Items { get; } = new();
}

/// <summary>
/// 特征数据（FeatureData）只读视图模型：把 <see cref="Feature.Definition"/> 暴露的
/// 强类型离线模型（对应 SolidWorks 的 <c>IFeature.GetDefinition()</c>）反射为可浏览的
/// 参数表与选择项表，并区分“已解析 / 未标记 / 未解析 / 未支持”。
/// 不改变任何解析语义：只展示模型上已有的公开属性与解析证据标记。
/// </summary>
public sealed class FeatureDataViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private static readonly string[] GroupOrder =
    {
        "深度与终止条件",
        "起始条件",
        "方向与拔模",
        "实体操作与合并",
        "切除选项",
        "薄壁特征",
        "自动圆角",
        "核心钣金参数",
        "折弯余量",
        "释放槽与规格表",
        "半径与圆角",
        "厚度",
        "几何与尺寸",
        "方向与终止条件",
        "实体与合并",
        "选择与拓扑",
        "其他参数"
    };

    private static readonly Dictionary<string, string> ReferenceCategoryNames = new(StringComparer.Ordinal)
    {
        ["SelectedContours"] = "所选轮廓",
        ["SelectedRegions"] = "所选区域",
        ["AvailableSketchContours"] = "候选轮廓",
        ["AvailableSketchRegions"] = "候选区域",
        ["DirectionReference"] = "方向参考",
        ["EndConditionEntity"] = "终止目标",
        ["BodyScope"] = "多实体范围",
        ["Edges"] = "边选择",
        ["Faces"] = "面选择",
        ["FaceReferences"] = "面选择",
        ["Loops"] = "环选择",
        ["Features"] = "特征选择",
        ["HoldLines"] = "保持线",
        ["HelpPoint"] = "帮助点",
        ["ProfileReferences"] = "轮廓参考",
        ["PathReferences"] = "路径参考",
        ["GuideReferences"] = "引导线参考",
        ["CenterlineReferences"] = "中心线参考",
        ["MirrorPlaneReference"] = "镜像面参考",
        ["Direction1Reference"] = "方向 1 参考",
        ["Direction2Reference"] = "方向 2 参考"
    };

    private static readonly HashSet<string> SkippedPropertyNames = new(StringComparer.Ordinal)
    {
        nameof(IFeatureData.ClassName),
        nameof(IFeatureData.TypeId),
        "ResolvedProperties",
        "ParameterResolution",
        "RawData",
        "ItemRadii",
        "FolderTypeId"
    };

    private string _className = string.Empty;
    private uint _typeId;

    /// <summary>选中特征定义的 MFC 序列化类名（如 moExtrusion_c）；未解析时取自文件夹类名。</summary>
    public string ClassName
    {
        get => _className;
        private set => SetProperty(ref _className, value);
    }

    /// <summary>选中特征定义的类型标识（FolderHeader TypeId）。</summary>
    public uint TypeId
    {
        get => _typeId;
        private set => SetProperty(ref _typeId, value);
    }

    public string TypeIdText => TypeId == 0 ? "-" : $"0x{TypeId:X2} ({TypeId})";

    /// <summary>强类型模型 CLR 类型名（未解析时为空）。</summary>
    public string ModelType { get; private set; } = string.Empty;

    public FeatureDataStatus Status { get; private set; } = FeatureDataStatus.None;

    public string StatusText => Status switch
    {
        FeatureDataStatus.Resolved => "已解析 (Resolved)",
        FeatureDataStatus.Unparsed => "未解析 (Registered · Layout not verified)",
        FeatureDataStatus.Unsupported => "未支持 (No registered parser)",
        _ => "无关联特征"
    };

    /// <summary>状态说明：解释为什么没有（或只有部分）参数值。</summary>
    public string StatusDescription { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public int ResolvedCount { get; private set; }

    public int PropertyCount { get; private set; }

    public bool IsVisible => Status != FeatureDataStatus.None;

    public bool IsResolved => Status == FeatureDataStatus.Resolved;

    public bool IsUnparsed => Status == FeatureDataStatus.Unparsed;

    public bool IsUnsupported => Status == FeatureDataStatus.Unsupported;

    public ObservableCollection<FeatureDataSection> Sections { get; } = new();

    public ObservableCollection<FeatureDataReferenceItem> References { get; } = new();

    public bool HasSections => Sections.Count > 0;

    public bool HasReferences => References.Count > 0;

    public void Clear()
    {
        ClassName = string.Empty;
        TypeId = 0;
        ModelType = string.Empty;
        Status = FeatureDataStatus.None;
        StatusDescription = string.Empty;
        Summary = string.Empty;
        ResolvedCount = 0;
        PropertyCount = 0;
        Sections.Clear();
        References.Clear();
        RaiseStateChanged();
    }

    /// <summary>用选中特征刷新视图；<paramref name="feature"/> 为 null 时清空。</summary>
    public void Load(Feature? feature)
    {
        Clear();
        if (feature == null) return;

        feature.Attributes.TryGetValue("ClassName", out var folderClass);
        folderClass ??= feature.TypeName;

        var def = feature.Model;
        if (def == null)
        {
            ClassName = folderClass ?? string.Empty;
            TypeId = feature.Id.HasValue && feature.Id.Value >= 0 ? (uint)feature.Id.Value : 0u;
            bool registered = FeatureDataRegistry.IsRegistered(ClassName) ||
                              FeatureDataRegistry.IsFeatureRegistered(ClassName);
            Status = registered ? FeatureDataStatus.Unparsed : FeatureDataStatus.Unsupported;
            StatusDescription = registered
                ? "该类 FeatureData 解析器已注册，但当前样本的字节布局未通过受控校验；为避免猜测，解析结果保持为空。"
                : "该类尚未注册 FeatureData 解析器；当前仅能显示文件夹头层面的信息（类名、类型、存储偏移）。";
            Summary = "无参数（FeatureData 为 null）";
            RaiseStateChanged();
            return;
        }

        string className = def is IMfcFeatureModel mfc ? mfc.ClassName : (def is IFeatureData ifd ? ifd.ClassName : folderClass ?? string.Empty);
        ClassName = className;
        uint defTypeId = def is IMfcFeatureModel mfc2 ? mfc2.TypeId : (def is IFeatureData ifd2 ? ifd2.TypeId : 0u);
        TypeId = defTypeId != 0 ? defTypeId : ParseTypeId(feature.Attributes);
        ModelType = def.GetType().Name;
        Status = FeatureDataStatus.Resolved;
        StatusDescription = "参数取自 ResolvedFeatures 流的结构化解码结果；未标记的属性可能只是模型默认值，不代表文件中存储的值。";

        var resolvedSet = TryGetResolvedProperties(def);
        var sheetMetalResolution = def as SheetMetalBaseFlangeModel;

        var rows = new List<FeatureDataPropertyItem>();
        foreach (var prop in EnumerateProperties(def.GetType()))
        {
            if (SkippedPropertyNames.Contains(prop.Name)) continue;
            if (prop.GetIndexParameters().Length > 0) continue;
            if (typeof(IFeatureData).IsAssignableFrom(prop.PropertyType)) continue;
            if (IsReferenceLike(prop.PropertyType, out _)) continue;

            object? value;
            try { value = prop.GetValue(def); }
            catch { continue; }

            // Sheet-metal models track evidence per parameter through ParameterResolution
            // instead of the ResolvedProperties set; both are evidence, so a non-null
            // (!resolved) value there is still only a model default.
            bool tracked = resolvedSet != null || sheetMetalResolution != null;
            bool resolved = resolvedSet != null && resolvedSet.Contains(prop.Name);
            if (sheetMetalResolution != null)
                resolved = IsSheetMetalParameterResolved(sheetMetalResolution, prop.Name);

            rows.Add(new FeatureDataPropertyItem
            {
                Name = prop.Name,
                Value = FormatValue(prop.Name, value),
                Group = Categorize(className, prop.Name),
                IsResolved = resolved,
                IsUnresolved = value == null,
                Status = value == null
                    ? "未解析"
                    : resolved
                        ? "已解析"
                        : tracked
                            ? "未标记"
                            : "已读取"
            });
        }

        PropertyCount = rows.Count;
        ResolvedCount = rows.Count(r => r.IsResolved);

        foreach (var group in rows.GroupBy(r => r.Group)
                                   .OrderBy(g => GroupIndex(g.Key))
                                   .ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            var section = new FeatureDataSection { Title = $"{group.Key} ({group.Count()})" };
            foreach (var row in group.OrderBy(r => r.Name, StringComparer.Ordinal))
                section.Items.Add(row);
            Sections.Add(section);
        }

        foreach (var prop in EnumerateProperties(def.GetType()))
        {
            if (SkippedPropertyNames.Contains(prop.Name)) continue;
            if (prop.GetIndexParameters().Length > 0) continue;
            if (!IsReferenceLike(prop.PropertyType, out _)) continue;

            object? value;
            try { value = prop.GetValue(def); }
            catch { continue; }
            CollectReferences(prop.Name, value);
        }

        Summary = $"{ResolvedCount}/{PropertyCount} 个参数已标记解析" +
                  (References.Count > 0 ? $" · {References.Count} 条选择引用" : string.Empty);
        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(IsResolved));
        OnPropertyChanged(nameof(IsUnparsed));
        OnPropertyChanged(nameof(IsUnsupported));
        OnPropertyChanged(nameof(TypeIdText));
        OnPropertyChanged(nameof(ModelType));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusDescription));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ResolvedCount));
        OnPropertyChanged(nameof(PropertyCount));
        OnPropertyChanged(nameof(HasSections));
        OnPropertyChanged(nameof(HasReferences));
    }

    private static uint ParseTypeId(IReadOnlyDictionary<string, string> attributes)
    {
        if (attributes.TryGetValue("TypeId", out var text) &&
            uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            return id;
        return 0;
    }

    private static IEnumerable<PropertyInfo> EnumerateProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetGetMethod() != null)
            .OrderBy(p => p.MetadataToken);

    /// <summary>
    /// 已解析属性集合。仅由带解析证据跟踪的模型提供
    /// （<see cref="MfcFeatureModelBase.ResolvedProperties"/> 或同等结构的 ResolvedProperties 属性）。
    /// </summary>
    private static HashSet<string>? TryGetResolvedProperties(object def)
    {
        var prop = def.GetType().GetProperty("ResolvedProperties", BindingFlags.Public | BindingFlags.Instance);
        if (prop?.GetValue(def) is IEnumerable<string> names)
            return new HashSet<string>(names, StringComparer.Ordinal);
        return null;
    }

    private static bool IsSheetMetalParameterResolved(SheetMetalBaseFlangeModel def, string name)
    {
        var r = def.ParameterResolution;
        return name switch
        {
            nameof(SheetMetalBaseFlangeModel.Thickness) => r.Thickness,
            nameof(SheetMetalBaseFlangeModel.BendRadius) => r.BendRadius,
            nameof(SheetMetalBaseFlangeModel.Depth) => r.Depth,
            nameof(SheetMetalBaseFlangeModel.DepthDirection2) => r.DepthDirection2,
            nameof(SheetMetalBaseFlangeModel.BothDirections) => r.BothDirections,
            nameof(SheetMetalBaseFlangeModel.ReverseDirection) => r.ReverseDirection,
            nameof(SheetMetalBaseFlangeModel.ReverseThickness) => r.ReverseThickness,
            nameof(SheetMetalBaseFlangeModel.UseDefaultBendRadius) => r.UseDefaultBendRadius,
            nameof(SheetMetalBaseFlangeModel.KFactor) => r.KFactor,
            nameof(SheetMetalBaseFlangeModel.BendAllowanceType) => r.BendAllowanceType,
            nameof(SheetMetalBaseFlangeModel.AutoReliefType) => r.AutoReliefType,
            nameof(SheetMetalBaseFlangeModel.ReliefRatio) => r.ReliefRatio,
            nameof(SheetMetalBaseFlangeModel.ReliefWidth) => r.ReliefWidth,
            nameof(SheetMetalBaseFlangeModel.ReliefDepth) => r.ReliefDepth,
            nameof(SheetMetalBaseFlangeModel.UseDefaultBendRelief) => r.UseDefaultBendRelief,
            nameof(SheetMetalBaseFlangeModel.UseGaugeTable) => r.UseGaugeTable,
            nameof(SheetMetalBaseFlangeModel.BendAllowance) => r.BendAllowance,
            nameof(SheetMetalBaseFlangeModel.BendDeduction) => r.BendDeduction,
            nameof(SheetMetalBaseFlangeModel.GaugeTablePath) => r.GaugeTablePath,
            nameof(SheetMetalBaseFlangeModel.ThicknessTableName) => r.ThicknessTableName,
            nameof(SheetMetalBaseFlangeModel.BendTableFile) => r.BendTableFile,
            _ => false
        };
    }

    private static bool IsReferenceLike(Type type, out string kind)
    {
        if (typeof(ISelectionReference).IsAssignableFrom(type))
        {
            kind = "single";
            return true;
        }
        if (typeof(FeatureReferenceData).IsAssignableFrom(type) || type == typeof(BodyScopeData))
        {
            kind = "single";
            return true;
        }
        if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
        {
            var element = type.IsArray ? type.GetElementType() : ElementTypeOf(type);
            if (element != null && (typeof(ISelectionReference).IsAssignableFrom(element) ||
                                    typeof(FeatureReferenceData).IsAssignableFrom(element)))
            {
                kind = "list";
                return true;
            }
        }
        kind = string.Empty;
        return false;
    }

    private static Type? ElementTypeOf(Type type)
    {
        if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            if (args.Length == 1) return args[0];
        }
        return type.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    private static string FormatValue(string name, object? value)
    {
        switch (value)
        {
            case null:
                return "—";
            case bool b:
                return b ? "true" : "false";
            case string s:
                return string.IsNullOrEmpty(s) ? "\"\"" : s;
            case Enum e:
                return $"{e} ({(int)(object)e})";
            case double d:
                return FormatDouble(name, d);
            case float f:
                return FormatDouble(name, f);
            case uint u:
                return u.ToString(CultureInfo.InvariantCulture);
            case int i:
                return i.ToString(CultureInfo.InvariantCulture);
            case byte[] bytes:
                return $"byte[{bytes.Length}]";
            default:
                var type = value.GetType();
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                var text = value.ToString();
                return text == type.FullName ? type.Name : text ?? string.Empty;
        }
    }

    private static string FormatDouble(string name, double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsInfinity(value)) return value > 0 ? "+∞" : "-∞";
        string raw = value.ToString("0.##########", CultureInfo.InvariantCulture);
        if (name.Contains("Angle", StringComparison.Ordinal))
        {
            double degrees = value * 180.0 / Math.PI;
            return $"{degrees:0.####}° ({raw} rad)";
        }
        bool looksLikeLength = name.IndexOf("Depth", StringComparison.Ordinal) >= 0 ||
                               name.IndexOf("Thickness", StringComparison.Ordinal) >= 0 ||
                               name.IndexOf("Radius", StringComparison.Ordinal) >= 0 ||
                               name.IndexOf("Distance", StringComparison.Ordinal) >= 0 ||
                               name.IndexOf("Offset", StringComparison.Ordinal) >= 0 ||
                               name.IndexOf("Width", StringComparison.Ordinal) >= 0 ||
                               name.IndexOf("Height", StringComparison.Ordinal) >= 0 ||
                               name.IndexOf("Length", StringComparison.Ordinal) >= 0 ||
                               name.IndexOf("Gap", StringComparison.Ordinal) >= 0;
        if (looksLikeLength && Math.Abs(value) > 0)
            return $"{raw} m ({value * 1000.0:0.####} mm)";
        return raw;
    }

    private static string Categorize(string className, string name)
    {
        if (name.Contains("Sketch", StringComparison.Ordinal) ||
            name.Contains("Contour", StringComparison.Ordinal) ||
            name.Contains("Region", StringComparison.Ordinal))
            return "选择与拓扑";

        if (className.StartsWith("moExtrusion", StringComparison.Ordinal) ||
            className == "moICE_c" ||
            className.StartsWith("moExtruThin", StringComparison.Ordinal))
            return CategorizeExtrude(name);

        if (className.StartsWith("moSMBaseFlange", StringComparison.Ordinal))
            return CategorizeSheetMetal(name);

        if (name.Contains("Radius", StringComparison.Ordinal) || name.Contains("Fillet", StringComparison.Ordinal))
            return "半径与圆角";
        if (name.Contains("Thickness", StringComparison.Ordinal) || name.Contains("Wall", StringComparison.Ordinal))
            return "厚度";
        if (name.Contains("Depth", StringComparison.Ordinal) || name.Contains("Distance", StringComparison.Ordinal) ||
            name.Contains("Offset", StringComparison.Ordinal) || name.Contains("Length", StringComparison.Ordinal) ||
            name.Contains("Width", StringComparison.Ordinal) || name.Contains("Height", StringComparison.Ordinal) ||
            name.Contains("Angle", StringComparison.Ordinal) || name.Contains("Diameter", StringComparison.Ordinal))
            return "几何与尺寸";
        if (name.Contains("EndCondition", StringComparison.Ordinal) || name.StartsWith("Reverse", StringComparison.Ordinal) ||
            name.StartsWith("Flip", StringComparison.Ordinal) || name.Contains("Direction", StringComparison.Ordinal))
            return "方向与终止条件";
        if (name.Contains("Merge", StringComparison.Ordinal) || name.Contains("Scope", StringComparison.Ordinal) ||
            name.Contains("Body", StringComparison.Ordinal) || name.StartsWith("Auto", StringComparison.Ordinal) ||
            name.Contains("Pattern", StringComparison.Ordinal))
            return "实体与合并";
        return "其他参数";
    }

    private static string CategorizeExtrude(string name)
    {
        switch (name)
        {
            case "Depth":
            case "DepthDirection2":
            case "EndCondition":
            case "EndConditionDirection2":
            case "BothDirections":
            case "ReverseDirection":
                return "深度与终止条件";
        }
        if (name.StartsWith("From", StringComparison.Ordinal)) return "起始条件";
        if (name.StartsWith("Draft", StringComparison.Ordinal) ||
            name.Contains("TranslateSurface", StringComparison.Ordinal) ||
            name.Contains("ReverseOffset", StringComparison.Ordinal))
            return "方向与拔模";
        if (name == "IsCut" || name == "FlipSideToCut" || name == "NormalCut" || name == "OptimizeGeometry")
            return "切除选项";
        if (name == "IsThinFeature" || name == "ThinWallType" || name.Contains("WallThickness", StringComparison.Ordinal) ||
            name == "CapEnds" || name == "CapThickness" || name == "LinkToThickness")
            return "薄壁特征";
        if (name.Contains("AutoFillet", StringComparison.Ordinal)) return "自动圆角";
        return "实体操作与合并";
    }

    private static string CategorizeSheetMetal(string name)
    {
        if (name.StartsWith("BendAllowance", StringComparison.Ordinal) || name.Contains("KFactor", StringComparison.Ordinal) ||
            name.Contains("BendTable", StringComparison.Ordinal) || name == "BendRadius")
            return "折弯余量";
        if (name.Contains("Relief", StringComparison.Ordinal) || name.Contains("GaugeTable", StringComparison.Ordinal) ||
            name.Contains("ThicknessTable", StringComparison.Ordinal))
            return "释放槽与规格表";
        if (name == "Depth" || name == "DepthDirection2" || name == "BothDirections" ||
            name == "ReverseDirection" || name == "ReverseThickness")
            return "深度与终止条件";
        return "核心钣金参数";
    }

    private static int GroupIndex(string group)
    {
        int index = Array.IndexOf(GroupOrder, group);
        return index < 0 ? GroupOrder.Length : index;
    }

    private void CollectReferences(string propertyName, object? value)
    {
        string category = ReferenceCategoryNames.TryGetValue(propertyName, out var friendly)
            ? friendly
            : propertyName;

        switch (value)
        {
            case null:
                return;
            case ISelectionReference reference:
                AddReference(category, reference);
                return;
            case FeatureReferenceData featureReference:
                AddFeatureReference(category, featureReference);
                return;
            case BodyScopeData scope:
                References.Add(new FeatureDataReferenceItem
                {
                    Category = category,
                    EntityType = "BodyScope",
                    EntityId = scope.BodyIds.Count > 0 ? string.Join(", ", scope.BodyIds) : "-",
                    Details = $"AutoSelect={scope.AutoSelect}, Flags=0x{scope.Flags:X4}, Bodies=[{string.Join(", ", scope.BodyIds)}]"
                });
                return;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                if (item is ISelectionReference reference)
                    AddReference(category, reference);
                else if (item is FeatureReferenceData featureReference)
                    AddFeatureReference(category, featureReference);
            }
        }
    }

    private void AddReference(string category, ISelectionReference reference)
    {
        string entityType = reference switch
        {
            DirectionReference direction => direction.EntityType.ToString(),
            EntityReference entity => entity.EntityType.ToString(),
            EndConditionEntityReference end => end.EntityType.ToString(),
            _ => reference.ReferenceType
        };

        string details = reference switch
        {
            SketchContourReference contour =>
                $"Chain={contour.ChainId}, 实体=[{string.Join(", ", contour.EntityIndices)}], Closed={contour.IsClosed}" +
                (contour.IsCandidateOnly ? ", 仅候选" : string.Empty),
            SketchRegionReference region =>
                $"区域索引={region.RegionIndex}, 环数={region.LoopCount}, 边界链=[{string.Join(", ", region.EdgeIds)}]",
            DirectionReference direction =>
                $"名称='{direction.ReferenceName}'" +
                (direction.Vector != null ? $", 向量=[{string.Join(", ", direction.Vector.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)))}]" : string.Empty),
            EndConditionEntityReference end =>
                $"Surface='{end.SurfaceName}', FaceCount={end.FaceCount}",
            EntityReference entity => $"名称='{entity.Name}'",
            _ => reference.ToString() ?? string.Empty
        };

        References.Add(new FeatureDataReferenceItem
        {
            Category = category,
            EntityType = entityType,
            EntityId = reference.EntityId.ToString(CultureInfo.InvariantCulture),
            Details = details
        });
    }

    private void AddFeatureReference(string category, FeatureReferenceData reference)
    {
        var details = new List<string>();
        if (reference.PersistentId.HasValue) details.Add($"PersistentId={reference.PersistentId.Value}");
        if (reference.ClassWord.HasValue) details.Add($"ClassWord=0x{reference.ClassWord.Value:X}");
        if (!string.IsNullOrEmpty(reference.ArchiveGuid)) details.Add($"GUID={reference.ArchiveGuid}");
        if (!string.IsNullOrEmpty(reference.ReferenceName)) details.Add($"名称='{reference.ReferenceName}'");
        if (reference.RecordCount.HasValue) details.Add($"Records={reference.RecordCount.Value}");

        References.Add(new FeatureDataReferenceItem
        {
            Category = category,
            EntityType = reference.EntityType?.ToString() ?? "-",
            EntityId = reference.EntityId?.ToString(CultureInfo.InvariantCulture) ?? "-",
            Details = details.Count > 0 ? string.Join(", ", details) : "（引用记录未解码）"
        });
    }
}
