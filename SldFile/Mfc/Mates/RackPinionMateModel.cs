using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Internal MFC model for Rack & Pinion Mate (moMateRackPinionDim_c, TypeId 95 / 0x5F).
/// Serializes rack linear reference, pinion circular reference, D1 dimension, and reverse flag.
/// </summary>
internal sealed class RackPinionMateModel : MfcMateModelBase
{
    public const string MfcClassName = "moMateRackPinionDim_c";
    public const uint FolderTypeId = 95u; // swFmMateRackPinionDim

    public override string ClassName => MfcClassName;
    public override AssemblyMateType MateType => AssemblyMateType.RackPinion;

    public AssemblyMateRackPinionDiameterType DiameterType { get; set; } = AssemblyMateRackPinionDiameterType.PinionPitchDiameter;
    public double DiameterValue { get; set; }
    public bool Reverse { get; set; }

    public string RackComponent { get; set; } = string.Empty;
    public string PinionComponent { get; set; } = string.Empty;

    public RackPinionMateModel()
    {
        TypeId = FolderTypeId;
        Alignment = AssemblyMateAlignment.Aligned;
    }

    protected override void SerializeEntities(MfcArchive ar)
    {
        // 1. Rack Entity (linear edge)
        ushort compLineTag = 0;
        ar.ClassTagOrRef("moCompRefLine_c", ref compLineTag);

        string? rack = RackComponent;
        ar.WChar(ref rack);
        if (ar.IsLoading && rack != null) RackComponent = rack;

        ushort refLineTag = 0;
        ar.ClassTagOrRef("moRefLine_c", ref refLineTag);

        ushort refLineDataTag = 0;
        ar.ClassTagOrRef("moRefLineData_c", ref refLineDataTag);

        // 2. Pinion Entity (circular edge/face)
        ushort cylRefTag = 0;
        ar.ClassTagOrRef("moCylinderRef_w", ref cylRefTag);

        ushort compCylTag = 0;
        ar.ClassTagOrRef("moCompRefCylinder_c", ref compCylTag);

        string? pinion = PinionComponent;
        ar.WChar(ref pinion);
        if (ar.IsLoading && pinion != null) PinionComponent = pinion;

        ushort refCylTag = 0;
        ar.ClassTagOrRef("moRefCylinder_c", ref refCylTag);
    }

    protected override void SerializeParameters(MfcArchive ar)
    {
        ushort dispGearTag = 0;
        ar.ClassTagOrRef("moDisplayGearDim_c", ref dispGearTag);

        ushort scalerDimTag = 0;
        ar.ClassTagOrRef("moScalerLengthDim_c", ref scalerDimTag);

        ushort paramTag = 0;
        ar.ClassTagOrRef("moLengthParameter_c", ref paramTag);

        string? d1Name = "D1";
        ar.WChar(ref d1Name);

        double d1 = (DiameterType == AssemblyMateRackPinionDiameterType.PinionPitchDiameter)
            ? DiameterValue
            : (DiameterValue / Math.PI);
        ar.F64(ref d1);

        ushort favTag = 0;
        ar.ClassTagOrRef("moFavoriteHandle_c", ref favTag);

        // Reverse flag in SW wire format: 0x00 for true, 0x01 for false
        byte revByte = Reverse ? (byte)0x00 : (byte)0x01;
        ar.Byte(ref revByte);
        if (ar.IsLoading) Reverse = (revByte == 0x00);

        uint dt = (DiameterType == AssemblyMateRackPinionDiameterType.PinionPitchDiameter) ? 0u : 1u;
        ar.U32(ref dt);
        if (ar.IsLoading)
        {
            DiameterType = (dt == 0)
                ? AssemblyMateRackPinionDiameterType.PinionPitchDiameter
                : AssemblyMateRackPinionDiameterType.RackTravelPerRevolution;
            DiameterValue = (DiameterType == AssemblyMateRackPinionDiameterType.PinionPitchDiameter)
                ? d1
                : d1 * Math.PI;
        }
    }

    public void SetEntities(string rackComponent, string pinionComponent)
    {
        RackComponent = rackComponent ?? string.Empty;
        PinionComponent = pinionComponent ?? string.Empty;
    }

    public override AssemblyMateDefinitionData ToDefinition()
    {
        var def = new RackPinionMateDefinitionData
        {
            DisplayName = DisplayName,
            Alignment = Alignment,
            DiameterType = DiameterType,
            DiameterValue = DiameterValue,
            Reverse = Reverse,
            RackEntity = RackComponent,
            PinionEntity = PinionComponent
        };
        return def;
    }

    public override void FromDefinition(AssemblyMateDefinitionData definition)
    {
        if (definition is not RackPinionMateDefinitionData def)
            throw new ArgumentException("Definition must be RackPinionMateDefinitionData.", nameof(definition));

        DisplayName = def.DisplayName;
        Alignment = def.Alignment;
        DiameterType = def.DiameterType;
        DiameterValue = def.DiameterValue;
        Reverse = def.Reverse;
        RackComponent = def.RackEntity;
        PinionComponent = def.PinionEntity;
    }

    protected override void PopulateStructureData(MateStructureData data)
    {
        data.Layout = MateStructureLayout.RackPinion2026;
        data.MateTypeId = 13u; // swMateRACKPINION
        data.RackPinionDiameterTypeId = (DiameterType == AssemblyMateRackPinionDiameterType.PinionPitchDiameter) ? 0u : 1u;
        data.RackPinionDiameterValue = DiameterValue;
        data.RackPinionReverse = Reverse;

        var entities = new List<MateEntityReferenceStructure>
        {
            new MateEntityReferenceStructure { ComponentInstanceName = RackComponent, Kind = MateReferenceEntityKind.Edge },
            new MateEntityReferenceStructure { ComponentInstanceName = PinionComponent, Kind = MateReferenceEntityKind.Circle }
        };
        data.EntityReferences = entities.AsReadOnly();
    }

    protected override void PopulateFromDecodedStructure(MateStructureData structure)
    {
        base.PopulateFromDecodedStructure(structure);
        if (structure.RackPinionDiameterTypeId.HasValue)
        {
            DiameterType = structure.RackPinionDiameterTypeId.Value switch
            {
                0u => AssemblyMateRackPinionDiameterType.PinionPitchDiameter,
                1u => AssemblyMateRackPinionDiameterType.RackTravelPerRevolution,
                _ => AssemblyMateRackPinionDiameterType.PinionPitchDiameter
            };
        }
        if (structure.RackPinionDiameterValue.HasValue)
            DiameterValue = structure.RackPinionDiameterValue.Value;
        if (structure.RackPinionReverse.HasValue)
            Reverse = structure.RackPinionReverse.Value;

        if (structure.EntityReferences.Count > 0)
            RackComponent = structure.EntityReferences[0].ComponentInstanceName;
        if (structure.EntityReferences.Count > 1)
            PinionComponent = structure.EntityReferences[1].ComponentInstanceName;
    }
}
