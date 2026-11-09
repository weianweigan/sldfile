using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Internal MFC model for standard geometric and limit mates (Coincident, Concentric, Distance, Parallel, Tangent, Perpendicular, Angle).
/// </summary>
internal sealed class StandardMateModel : MfcMateModelBase
{
    private readonly string _className;
    private readonly AssemblyMateType _mateType;

    public override string ClassName => _className;
    public override AssemblyMateType MateType => _mateType;

    public double? Distance { get; set; }
    public double? Angle { get; set; }

    private readonly List<string> _planeReferences = new();
    public IReadOnlyList<string> PlaneReferences => _planeReferences;

    public StandardMateModel(string className, AssemblyMateType mateType, uint typeId = 0)
    {
        _className = className ?? throw new ArgumentNullException(nameof(className));
        _mateType = mateType;
        TypeId = typeId > 0 ? typeId : DefaultTypeIdFor(className);
        Alignment = AssemblyMateAlignment.Aligned;
    }

    private static uint DefaultTypeIdFor(string className) => className switch
    {
        "moMateCoincident" or "moMateCoincidentM" or "moMateCoincident_cM" => 53u,
        "moMateConcentric" => 54u,
        "moMateDistanceDim_c" or "moMateLimitDistanceDim_c" => 55u,
        "moMateParallel" => 56u,
        "moMateTangent" => 57u,
        "moMatePerpendicular" => 89u,
        "moMatePlanarAngleDim_c" or "moMateLimitPlanarAngleDim_c" => 93u,
        "moMateCoordinate" or "moMateCoordinateM" or "moMateCoordinate_cM" => 20u,
        _ => 53u
    };

    public override void AddPlaneReference(string selectionName)
    {
        base.AddPlaneReference(selectionName);
        if (!string.IsNullOrEmpty(selectionName) && !_planeReferences.Contains(selectionName))
            _planeReferences.Add(selectionName);
    }

    protected override void SerializeEntities(MfcArchive ar)
    {
        ushort count = (ushort)_planeReferences.Count;
        ar.U16(ref count);

        if (ar.IsStoring)
        {
            foreach (var sel in _planeReferences)
            {
                ushort planeRefTag = 0;
                ar.ClassTagOrRef("moPlaneRef_w", ref planeRefTag);

                ushort refPlnDataTag = 0;
                ar.ClassTagOrRef("moAbsoluteRefPlnData_c", ref refPlnDataTag);

                string comp = sel.Contains("@") ? sel.Substring(sel.IndexOf('@') + 1) : sel;
                string? compStr = comp;
                ar.WChar(ref compStr);

                string? selStr = sel;
                ar.WChar(ref selStr);
            }
        }
        else
        {
            _planeReferences.Clear();
            _entities.Clear();
            for (int i = 0; i < count; i++)
            {
                ushort planeRefTag = 0;
                ar.ClassTagOrRef("moPlaneRef_w", ref planeRefTag);

                ushort refPlnDataTag = 0;
                ar.ClassTagOrRef("moAbsoluteRefPlnData_c", ref refPlnDataTag);

                string? comp = null;
                ar.WChar(ref comp);

                string? sel = null;
                ar.WChar(ref sel);
                string entityName = !string.IsNullOrEmpty(sel) ? sel! : (comp ?? string.Empty);
                if (!string.IsNullOrEmpty(entityName))
                {
                    AddPlaneReference(entityName);
                }
            }
        }
    }

    protected override void SerializeParameters(MfcArchive ar)
    {
        bool hasDistance = Distance.HasValue;
        byte distFlag = hasDistance ? (byte)1 : (byte)0;
        ar.Byte(ref distFlag);
        if (ar.IsLoading) hasDistance = (distFlag != 0);

        if (hasDistance)
        {
            ushort dispTag = 0;
            ar.ClassTagOrRef("moDisplayDistanceDim_c", ref dispTag);

            ushort paramTag = 0;
            ar.ClassTagOrRef("moLengthParameter_c", ref paramTag);

            string? d1Name = "D1";
            ar.WChar(ref d1Name);

            double d = Distance ?? 0.0;
            ar.F64(ref d);
            if (ar.IsLoading) Distance = d;
        }

        bool hasAngle = Angle.HasValue;
        byte angleFlag = hasAngle ? (byte)1 : (byte)0;
        ar.Byte(ref angleFlag);
        if (ar.IsLoading) hasAngle = (angleFlag != 0);

        if (hasAngle)
        {
            ushort dispTag = 0;
            ar.ClassTagOrRef("moDisplayDim_c", ref dispTag);

            ushort paramTag = 0;
            ar.ClassTagOrRef("moAngleParameter_c", ref paramTag);

            string? d1Name = "D1";
            ar.WChar(ref d1Name);

            double a = Angle ?? 0.0;
            ar.F64(ref a);
            if (ar.IsLoading) Angle = a;
        }
    }

    public override AssemblyMateDefinitionData ToDefinition()
    {
        var def = new StandardMateDefinitionData(MateType)
        {
            DisplayName = DisplayName,
            Alignment = Alignment,
            Distance = Distance,
            Angle = Angle
        };
        def.EntitiesToMate.AddRange(_planeReferences);
        return def;
    }

    public override void FromDefinition(AssemblyMateDefinitionData definition)
    {
        DisplayName = definition.DisplayName;
        Alignment = definition.Alignment;
        if (definition is StandardMateDefinitionData std)
        {
            Distance = std.Distance;
            Angle = std.Angle;
        }
        _planeReferences.Clear();
        _entities.Clear();
        foreach (var ent in definition.EntitiesToMate)
            AddPlaneReference(ent);
    }

    protected override void PopulateStructureData(MateStructureData data)
    {
        data.Layout = MateStructureLayout.PlaneReference2026;
        data.MateTypeId = _mateType switch
        {
            AssemblyMateType.Coincident => 0u,
            AssemblyMateType.Concentric => 1u,
            AssemblyMateType.Perpendicular => 2u,
            AssemblyMateType.Parallel => 3u,
            AssemblyMateType.Tangent => 4u,
            AssemblyMateType.Distance or AssemblyMateType.LimitDistance => 5u,
            AssemblyMateType.Angle or AssemblyMateType.LimitAngle => 6u,
            AssemblyMateType.Coordinate => 20u,
            _ => data.MateTypeId
        };
        data.Distance = Distance;
        data.Angle = Angle;

        var entities = new List<MateEntityReferenceStructure>();
        foreach (var sel in _planeReferences)
        {
            string feature = string.Empty;
            string comp = sel;
            if (sel.Contains("@"))
            {
                int firstAt = sel.IndexOf('@');
                feature = sel.Substring(0, firstAt);
                comp = sel.Substring(firstAt + 1);
            }

            entities.Add(new MateEntityReferenceStructure
            {
                Kind = MateReferenceEntityKind.Plane,
                ComponentInstanceName = comp,
                FeatureName = feature,
                SelectionName = sel
            });
        }
        data.EntityReferences = entities.AsReadOnly();
    }

    protected override void PopulateReferences(List<MateReferenceEntry> refs)
    {
        if (refs.Count == 0)
        {
            for (int i = 0; i < _planeReferences.Count; i++)
            {
                refs.Add(new MateReferenceEntry
                {
                    WrapperClassName = "moPlaneRef_w",
                    EntityClassNames = new[] { "moAbsoluteRefPlnData_c" }
                });
            }
        }
    }

    protected override void PopulateFromDecodedStructure(MateStructureData structure)
    {
        base.PopulateFromDecodedStructure(structure);
        if (structure.Distance.HasValue)
            Distance = structure.Distance.Value;
        if (structure.Angle.HasValue)
            Angle = structure.Angle.Value;

        if (structure.EntityReferences.Count > 0)
        {
            _planeReferences.Clear();
            foreach (var ent in structure.EntityReferences)
            {
                if (!string.IsNullOrEmpty(ent.SelectionName))
                    _planeReferences.Add(ent.SelectionName);
                else if (!string.IsNullOrEmpty(ent.ComponentInstanceName))
                    _planeReferences.Add(ent.ComponentInstanceName);
            }
        }
    }
}
