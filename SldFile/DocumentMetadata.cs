using System;
using System.Collections;
using System.Collections.Generic;
using SldFile.Mfc;

namespace SldFile;

/// <summary>Editable document properties. Call SwFile.Save to persist changes.</summary>
public sealed class DocumentMetadata
{
    public string? Title { get; set; }
    public string? Subject { get; set; }
    public string? Author { get; set; }
    public string? Keywords { get; set; }
    public string? Comments { get; set; }
    public string? LastSavedBy { get; set; }
    public string? Company { get; set; }
    public string? Manager { get; set; }
    public string? Category { get; set; }
    public DateTimeOffset? CreatedAt { get; internal set; }
    public DateTimeOffset? LastSavedAt { get; internal set; }
    public DocumentUnits? Units { get; internal set; }
    public ModelBoundingBox? BoundingBox { get; internal set; }
}

public enum UnitSystem
{
    Cgs = 1,
    Mks = 2,
    Ips = 3,
    Mmgs = 4,
    Custom = 5
}

public enum LengthUnit
{
    Millimeter = 0,
    Centimeter = 1,
    Meter = 2,
    Inch = 3,
    Foot = 4,
    FeetInches = 5,
    Angstrom = 6,
    Nanometer = 7,
    Micron = 8,
    Mil = 9,
    Microinch = 10
}

public enum AngleUnit
{
    Degrees = 0,
    DegreesMinutes = 1,
    DegreesMinutesSeconds = 2,
    Radians = 3
}

public enum MassUnit
{
    Milligram = 0,
    Gram = 1,
    Kilogram = 2,
    Pound = 3
}

public sealed class DocumentUnits
{
    public UnitSystem? System { get; internal set; }
    public LengthUnit? Length { get; internal set; }
    public LengthUnit? DualLength { get; internal set; }
    public AngleUnit? Angle { get; internal set; }
    public MassUnit? Mass { get; internal set; }
}

public sealed class ModelBoundingBox
{
    public double MinX { get; }
    public double MinY { get; }
    public double MinZ { get; }
    public double MaxX { get; }
    public double MaxY { get; }
    public double MaxZ { get; }

    public ModelBoundingBox(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        MinX = minX;
        MinY = minY;
        MinZ = minZ;
        MaxX = maxX;
        MaxY = maxY;
        MaxZ = maxZ;
    }

    public override string ToString() => $"[{MinX:F4}, {MinY:F4}, {MinZ:F4}] to [{MaxX:F4}, {MaxY:F4}, {MaxZ:F4}]";
}

/// <summary>Named values. Supported writable values: string, int, double, bool and DateTime.</summary>
public sealed class PropertyCollection : IReadOnlyDictionary<string, object>
{
    private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);
    internal readonly HashSet<string> Changes = new HashSet<string>(StringComparer.Ordinal);

    public object this[string name] => _values[name];
    public IEnumerable<string> Keys => _values.Keys;
    public IEnumerable<object> Values => _values.Values;
    public int Count => _values.Count;
    public bool ContainsKey(string name) => _values.ContainsKey(name);
    public bool TryGetValue(string name, out object value) => _values.TryGetValue(name, out value!);
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Set(string name, object value)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A property name is required.", nameof(name));
        if (!(value is string || value is int || value is bool || value is DateTime || value is double))
            throw new ArgumentException("Use a string, int, double, bool or DateTime value.", nameof(value));
        if (value is double number && (double.IsNaN(number) || double.IsInfinity(number)))
            throw new ArgumentException("A finite number is required.", nameof(value));
        if (_values.TryGetValue(name, out var previous) && Equals(previous, value)) return;
        _values[name] = value;
        Changes.Add(name);
    }

    public bool Remove(string name)
    {
        if (!_values.Remove(name)) return false;
        Changes.Add(name);
        return true;
    }

    internal void Load(string name, object value) => _values[name] = value;
}

/// <summary>Properties of an existing configuration, identified by its stored index.</summary>
public sealed class Configuration
{
    public int Index { get; }
    /// <summary>The recorded display name, or null when unavailable or conflicting.</summary>
    public string? Name { get; }
    public PropertyCollection CustomProperties { get; } = new PropertyCollection();
    internal string SourcePath { get; }
    public MassProperties? MassProperties { get; internal set; }
    public IReadOnlyList<CutListItem> CutListItems { get; internal set; } = Array.Empty<CutListItem>();
    internal Configuration(int index, string sourcePath, string? name = null) { Index = index; SourcePath = sourcePath; Name = name; }
}

public enum DocumentKind { Part, Assembly, Drawing }

/// <summary>A recorded edit. History is read-only and may be incomplete in unsupported files.</summary>
public sealed class Revision
{
    public string Action { get; }
    public string? FeatureName { get; }
    public string? UserName { get; }
    public DateTimeOffset? OccurredAt { get; }
    internal Revision(string action, string? featureName, string? userName, DateTimeOffset? occurredAt)
    { Action = action; FeatureName = featureName; UserName = userName; OccurredAt = occurredAt; }
}

public sealed class DocumentReference
{
    public string FilePath { get; }
    public string Name { get; }
    internal DocumentReference(string filePath, string name) { FilePath = filePath; Name = name; }
}

public enum FeatureKind
{
    Other,
    ReferencePlane,
    Sketch,
    Extrusion,
    SurfaceExtrude,
    SurfaceRevolve,
    PlanarSurface,
    SurfaceKnit,
    SurfaceExtend,
    SurfaceOffset,
    FillSurface,
    SurfaceSweep,
    SurfaceLoft,
    Dimension,
    Folder,
    Cut,
    Revolve,
    Sweep,
    Loft,
    LoftedBend,
    Jog,
    Fold,
    UnFold,
    StructuralMember,
    EndCap,
    WeldBead,
    Fillet,
    Chamfer,
    HoleWizard,
    Pattern,
    Mirror,
    Rib,
    Shell,
    Draft,
    DeleteBody,
    CombineBodies,
    SplitBody,
    Gusset,
    SurfaceTrim,
    Thread,
    Hem,
    FlatPattern
}

public sealed class FeatureDimension
{
    public string Name { get; }
    public string Value { get; }
    public double? NumericValue { get; }
    public double? SystemValue => NumericValue;
    public SketchDimensionType DimensionType { get; }
    public SldFile.Mfc.MoParameterModel? Parameter { get; }
    public SldFile.Mfc.MoDisplayDimModel? DisplayDimension { get; }
    public SldFile.Mfc.DimensionModel? Model { get; }

    public FeatureDimension(string name, string value)
    {
        Name = name ?? string.Empty;
        Value = value ?? string.Empty;
        DimensionType = SketchDimensionType.Distance;
    }

    public FeatureDimension(
        string name,
        double numericValue,
        SketchDimensionType dimensionType = SketchDimensionType.Distance,
        string? formattedValue = null,
        SldFile.Mfc.MoParameterModel? parameter = null,
        SldFile.Mfc.MoDisplayDimModel? displayDimension = null,
        SldFile.Mfc.DimensionModel? model = null)
    {
        Name = name ?? string.Empty;
        NumericValue = numericValue;
        DimensionType = dimensionType;
        Value = formattedValue ?? (dimensionType == SketchDimensionType.Angle
            ? $"{(numericValue * 180.0 / Math.PI):F2}°"
            : $"{(numericValue * 1000.0):F2}mm");
        Parameter = parameter ?? (dimensionType == SketchDimensionType.Angle
            ? (SldFile.Mfc.MoParameterModel)new SldFile.Mfc.MoAngleParameterModel(Name, numericValue)
            : new SldFile.Mfc.MoLengthParameterModel(Name, numericValue));
        DisplayDimension = displayDimension ?? SldFile.Mfc.MoDisplayDimModel.CreateForType(dimensionType);
        Model = model ?? new SldFile.Mfc.DimensionModel(Parameter, DisplayDimension);
    }

    public FeatureDimension(SldFile.Mfc.MoParameterModel parameter, SldFile.Mfc.MoDisplayDimModel? displayDim = null)
    {
        if (parameter == null) throw new ArgumentNullException(nameof(parameter));
        Parameter = parameter;
        DisplayDimension = displayDim;
        Name = parameter.Name;
        NumericValue = parameter.Value;
        bool isAngle = parameter is SldFile.Mfc.MoAngleParameterModel;
        DimensionType = displayDim?.DimensionType ?? (isAngle ? SketchDimensionType.Angle : SketchDimensionType.Distance);
        bool hasCustomDisplay = !string.IsNullOrEmpty(parameter.DisplayValue)
            && !double.TryParse(parameter.DisplayValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
        Value = hasCustomDisplay
            ? parameter.DisplayValue!
            : (isAngle
                ? $"{(parameter.Value * 180.0 / Math.PI):F2}°"
                : $"{(parameter.Value * 1000.0):F2}mm");
        Model = new SldFile.Mfc.DimensionModel(parameter, displayDim);
    }

    public FeatureDimension(SldFile.Mfc.DimensionModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        Model = model;
        Parameter = model.Parameter;
        DisplayDimension = model.DisplayDimension;
        Name = model.Name;
        NumericValue = model.SystemValue;
        DimensionType = model.DimensionType != SketchDimensionType.Unknown ? model.DimensionType : SketchDimensionType.Distance;
        bool hasCustomDisplay = !string.IsNullOrEmpty(model.DisplayValue)
            && !double.TryParse(model.DisplayValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
        Value = hasCustomDisplay
            ? model.DisplayValue!
            : (NumericValue.HasValue
                ? (DimensionType == SketchDimensionType.Angle
                    ? $"{(NumericValue.Value * 180.0 / Math.PI):F2}°"
                    : $"{(NumericValue.Value * 1000.0):F2}mm")
                : model.Name);
    }

    public SldFile.Mfc.DimensionModel ToDimensionModel() => Model ?? new SldFile.Mfc.DimensionModel
    {
        Name = Name,
        SystemValue = NumericValue,
        DimensionType = DimensionType,
        Parameter = Parameter,
        DisplayDimension = DisplayDimension
    };

    public override string ToString() => string.IsNullOrEmpty(Value) ? Name : $"{Name} = {Value}";
}

/// <summary>A recognized feature. The list does not imply a reconstructed parent/child hierarchy.</summary>
public sealed class Feature
{
    public string Name { get; }
    public FeatureKind Kind { get; }
    public string TypeName { get; }
    public int? Id { get; }

    /// <summary>Whether this feature is suppressed in the active configuration.</summary>
    public bool IsSuppressed { get; }

    /// <summary>Whether this feature is visible in the viewport.</summary>
    public bool IsVisible { get; }

    /// <summary>
    /// Typed MFC feature model (e.g. ExtrudeFeatureModel, RevolveFeatureModel, FilletFeatureModel, etc.).
    /// </summary>
    public IFeatureData? Model { get; }

    /// <summary>
    /// Alias for <see cref="Model"/> representing the typed feature data definition.
    /// </summary>
    public IFeatureData? FeatureData => Model;

    public IReadOnlyDictionary<string, string> Attributes { get; }
    public IReadOnlyList<FeatureDimension> Dimensions { get; }
    public IReadOnlyList<Feature> Children { get; }

    internal Feature(string name, FeatureKind kind)
        : this(name, kind, null, null, null, null, null, false, true, null) { }

    internal Feature(
        string name,
        FeatureKind kind,
        string? typeName = null,
        int? id = null,
        IReadOnlyDictionary<string, string>? attributes = null,
        IReadOnlyList<FeatureDimension>? dimensions = null,
        IReadOnlyList<Feature>? children = null,
        bool isSuppressed = false,
        bool isVisible = true,
        IFeatureData? model = null)
    {
        Name = name ?? string.Empty;
        Kind = kind;
        TypeName = typeName ?? kind.ToString();
        Id = id;
        Attributes = attributes ?? new Dictionary<string, string>();
        Dimensions = dimensions ?? Array.Empty<FeatureDimension>();
        Children = children ?? Array.Empty<Feature>();
        IsSuppressed = isSuppressed;
        IsVisible = isVisible;
        Model = model;
    }

    /// <summary>
    /// Creates a domain <see cref="Feature"/> from an internal MFC <see cref="SldFile.Mfc.FeatureFolder"/>.
    /// </summary>
    internal static Feature FromFolder(SldFile.Mfc.FeatureFolder folder) => SldFile.SwFile.MapFolder(folder);

    /// <summary>
    /// Converts this domain <see cref="Feature"/> to an internal MFC <see cref="SldFile.Mfc.FeatureFolder"/>.
    /// </summary>
    internal SldFile.Mfc.FeatureFolder ToFolder()
    {
        var folder = new SldFile.Mfc.FeatureFolder
        {
            ClassName = TypeName,
            DisplayName = Name,
            FeatureData = Model
        };
        if (Id.HasValue)
        {
            folder.Header.TypeId = (uint)Id.Value;
        }
        if (Children.Count > 0)
        {
            var childFolders = new List<SldFile.Mfc.FeatureFolder>(Children.Count);
            foreach (var child in Children)
            {
                childFolders.Add(child.ToFolder());
            }
            folder.Children = childFolders.AsReadOnly();
        }
        return folder;
    }
}
