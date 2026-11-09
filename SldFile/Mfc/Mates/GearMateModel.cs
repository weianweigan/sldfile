using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Internal MFC model for Gear Mate (moMateGearDim_c, TypeId 96 / 0x60).
/// Serializes two cylindrical references, D1 (numerator), D2 (denominator), and reverse flag.
/// </summary>
internal sealed class GearMateModel : MfcMateModelBase
{
    public const string MfcClassName = "moMateGearDim_c";
    public const uint FolderTypeId = 96u; // swFmMateGearDim

    public override string ClassName => MfcClassName;
    public override AssemblyMateType MateType => AssemblyMateType.Gear;

    public double GearRatioNumerator { get; set; } = 1.0;
    public double GearRatioDenominator { get; set; } = 1.0;
    public bool Reverse { get; set; }

    public string Pinion1Component { get; set; } = string.Empty;
    public string Pinion2Component { get; set; } = string.Empty;

    public GearMateModel()
    {
        TypeId = FolderTypeId;
        Alignment = AssemblyMateAlignment.Closest;
    }

    protected override void SerializeEntities(MfcArchive ar)
    {
        SerializeCylinderEntity(ar, Pinion1Component, c => Pinion1Component = c);
        SerializeCylinderEntity(ar, Pinion2Component, c => Pinion2Component = c);
    }

    private void SerializeCylinderEntity(MfcArchive ar, string component, Action<string> update)
    {
        ushort cylRefTag = 0;
        ar.ClassTagOrRef("moCylinderRef_w", ref cylRefTag);

        ushort compCylTag = 0;
        ar.ClassTagOrRef("moCompRefCylinder_c", ref compCylTag);

        string? comp = component;
        ar.WChar(ref comp);

        ushort refCylTag = 0;
        ar.ClassTagOrRef("moRefCylinder_c", ref refCylTag);

        if (ar.IsLoading && comp != null)
        {
            update(comp);
        }
    }

    protected override void SerializeParameters(MfcArchive ar)
    {
        ushort dispGearTag = 0;
        ar.ClassTagOrRef("moDisplayGearDim_c", ref dispGearTag);

        ushort featHandleTag = 0;
        ar.ClassTagOrRef("moFeatureDimHandle_c", ref featHandleTag);

        ushort scalerDimTag = 0;
        ar.ClassTagOrRef("moScalerLengthDim_c", ref scalerDimTag);

        ushort paramTag = 0;
        ar.ClassTagOrRef("moLengthParameter_c", ref paramTag);

        string? d1Name = "D1";
        ar.WChar(ref d1Name);

        double num = GearRatioNumerator;
        ar.F64(ref num);
        if (ar.IsLoading) GearRatioNumerator = num;

        ushort favTag = 0;
        ar.ClassTagOrRef("moFavoriteHandle_c", ref favTag);

        string? d2Name = "D2";
        ar.WChar(ref d2Name);

        double den = GearRatioDenominator;
        ar.F64(ref den);
        if (ar.IsLoading) GearRatioDenominator = den;

        byte rev = Reverse ? (byte)0x01 : (byte)0x00;
        ar.Byte(ref rev);
        if (ar.IsLoading) Reverse = (rev != 0x00);
    }

    public override AssemblyMateDefinitionData ToDefinition()
    {
        var def = new GearMateDefinitionData
        {
            DisplayName = DisplayName,
            Alignment = Alignment,
            GearRatioNumerator = GearRatioNumerator,
            GearRatioDenominator = GearRatioDenominator,
            Reverse = Reverse
        };
        if (!string.IsNullOrEmpty(Pinion1Component)) def.Pinion1Entity = Pinion1Component;
        if (!string.IsNullOrEmpty(Pinion2Component)) def.Pinion2Entity = Pinion2Component;
        return def;
    }

    public override void FromDefinition(AssemblyMateDefinitionData definition)
    {
        if (definition is not GearMateDefinitionData def)
            throw new ArgumentException("Definition must be GearMateDefinitionData.", nameof(definition));

        DisplayName = def.DisplayName;
        Alignment = def.Alignment;
        GearRatioNumerator = def.GearRatioNumerator;
        GearRatioDenominator = def.GearRatioDenominator;
        Reverse = def.Reverse;
        Pinion1Component = def.Pinion1Entity;
        Pinion2Component = def.Pinion2Entity;
    }

    protected override void PopulateStructureData(MateStructureData data)
    {
        data.Layout = MateStructureLayout.MeasuredMechanical2026;
        data.MateTypeId = 10u; // swMateGEAR
        data.GearRatioNumerator = GearRatioNumerator;
        data.GearRatioDenominator = GearRatioDenominator;

        var entities = new List<MateEntityReferenceStructure>
        {
            new MateEntityReferenceStructure { ComponentInstanceName = Pinion1Component, Kind = MateReferenceEntityKind.Cylinder },
            new MateEntityReferenceStructure { ComponentInstanceName = Pinion2Component, Kind = MateReferenceEntityKind.Cylinder }
        };
        data.EntityReferences = entities.AsReadOnly();
    }

    protected override void PopulateFromDecodedStructure(MateStructureData structure)
    {
        base.PopulateFromDecodedStructure(structure);
        if (structure.GearRatioNumerator.HasValue)
            GearRatioNumerator = structure.GearRatioNumerator.Value;
        if (structure.GearRatioDenominator.HasValue)
            GearRatioDenominator = structure.GearRatioDenominator.Value;

        if (structure.EntityReferences.Count > 0)
            Pinion1Component = structure.EntityReferences[0].ComponentInstanceName;
        if (structure.EntityReferences.Count > 1)
            Pinion2Component = structure.EntityReferences[1].ComponentInstanceName;
    }
}
