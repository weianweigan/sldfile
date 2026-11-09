using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Internal MFC model for Screw Mate (moMateScrew_c, TypeId 97 / 0x61).
/// Serializes two cylindrical references, D1 revolution value, and reverse flag.
/// </summary>
internal sealed class ScrewMateModel : MfcMateModelBase
{
    public const string MfcClassName = "moMateScrew_c";
    public const uint FolderTypeId = 97u; // swFmMateScrew

    public override string ClassName => MfcClassName;
    public override AssemblyMateType MateType => AssemblyMateType.Screw;

    public int RevolutionType { get; set; } // 0 = pitch, 1 = revs/unit
    public double RevolutionVal { get; set; } = 0.005;
    public bool Reverse { get; set; }

    public string ScrewEntity1 { get; set; } = string.Empty;
    public string ScrewEntity2 { get; set; } = string.Empty;

    public ScrewMateModel()
    {
        TypeId = FolderTypeId;
        Alignment = AssemblyMateAlignment.Aligned;
    }

    protected override void SerializeEntities(MfcArchive ar)
    {
        SerializeCylinderEntity(ar, ScrewEntity1, s => ScrewEntity1 = s);
        SerializeCylinderEntity(ar, ScrewEntity2, s => ScrewEntity2 = s);
    }

    private void SerializeCylinderEntity(MfcArchive ar, string component, Action<string> update)
    {
        ushort cylRefTag = 0;
        ar.ClassTagOrRef("moCylinderRef_w", ref cylRefTag);

        ushort compCylTag = 0;
        ar.ClassTagOrRef("moCompRefCylinder_c", ref compCylTag);

        string? comp = component;
        ar.WChar(ref comp);

        if (ar.IsLoading && comp != null)
        {
            update(comp);
        }
    }

    protected override void SerializeParameters(MfcArchive ar)
    {
        ushort dispDimTag = 0;
        ar.ClassTagOrRef("moDisplayDim_c", ref dispDimTag);

        ushort featHandleTag = 0;
        ar.ClassTagOrRef("moFeatureDimHandle_c", ref featHandleTag);

        ushort doubleDimTag = 0;
        ar.ClassTagOrRef("moDoubleDim_c", ref doubleDimTag);

        ushort scalerParamTag = 0;
        ar.ClassTagOrRef("moScalerParameter_c", ref scalerParamTag);

        string? d1Name = "D1";
        ar.WChar(ref d1Name);

        double revVal = RevolutionVal;
        ar.F64(ref revVal);
        if (ar.IsLoading) RevolutionVal = revVal;

        ushort favTag = 0;
        ar.ClassTagOrRef("moFavoriteHandle_c", ref favTag);

        byte rev = Reverse ? (byte)0x01 : (byte)0x00;
        ar.Byte(ref rev);
        if (ar.IsLoading) Reverse = (rev != 0x00);
    }

    public override AssemblyMateDefinitionData ToDefinition()
    {
        var def = new ScrewMateDefinitionData
        {
            DisplayName = DisplayName,
            Alignment = Alignment,
            RevolutionType = RevolutionType,
            RevolutionVal = RevolutionVal,
            Reverse = Reverse,
            ScrewEntity1 = ScrewEntity1,
            ScrewEntity2 = ScrewEntity2
        };
        if (!string.IsNullOrEmpty(ScrewEntity1)) def.EntitiesToMate.Add(ScrewEntity1);
        if (!string.IsNullOrEmpty(ScrewEntity2)) def.EntitiesToMate.Add(ScrewEntity2);
        return def;
    }

    public override void FromDefinition(AssemblyMateDefinitionData definition)
    {
        if (definition is not ScrewMateDefinitionData def)
            throw new ArgumentException("Definition must be ScrewMateDefinitionData.", nameof(definition));

        DisplayName = def.DisplayName;
        Alignment = def.Alignment;
        RevolutionType = def.RevolutionType;
        RevolutionVal = def.RevolutionVal;
        Reverse = def.Reverse;
        ScrewEntity1 = def.ScrewEntity1;
        ScrewEntity2 = def.ScrewEntity2;

        if (string.IsNullOrEmpty(ScrewEntity1) && def.EntitiesToMate.Count > 0)
            ScrewEntity1 = def.EntitiesToMate[0];
        if (string.IsNullOrEmpty(ScrewEntity2) && def.EntitiesToMate.Count > 1)
            ScrewEntity2 = def.EntitiesToMate[1];
    }

    protected override void PopulateStructureData(MateStructureData data)
    {
        data.Layout = MateStructureLayout.MeasuredMechanical2026;
        data.MateTypeId = 17u; // swMateSCREW
        data.ScrewRevolutionsPerUnit = RevolutionVal;

        var entities = new List<MateEntityReferenceStructure>
        {
            new MateEntityReferenceStructure { ComponentInstanceName = ScrewEntity1, Kind = MateReferenceEntityKind.Cylinder },
            new MateEntityReferenceStructure { ComponentInstanceName = ScrewEntity2, Kind = MateReferenceEntityKind.Cylinder }
        };
        data.EntityReferences = entities.AsReadOnly();
    }

    protected override void PopulateFromDecodedStructure(MateStructureData structure)
    {
        base.PopulateFromDecodedStructure(structure);
        if (structure.ScrewRevolutionsPerUnit.HasValue)
            RevolutionVal = structure.ScrewRevolutionsPerUnit.Value;
        if (structure.EntityReferences.Count > 0)
            ScrewEntity1 = structure.EntityReferences[0].ComponentInstanceName;
        if (structure.EntityReferences.Count > 1)
            ScrewEntity2 = structure.EntityReferences[1].ComponentInstanceName;
    }
}
