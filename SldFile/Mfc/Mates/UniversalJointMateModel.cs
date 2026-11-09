using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Internal MFC model for Universal Joint Mate (moMateUniversalJoint_c, TypeId 98 / 0x62).
/// Serializes two cylindrical axis selections and optional joint pivot point.
/// </summary>
internal sealed class UniversalJointMateModel : MfcMateModelBase
{
    public const string MfcClassName = "moMateUniversalJoint_c";
    public const uint FolderTypeId = 98u; // swFmMateUniversalJoint

    public override string ClassName => MfcClassName;
    public override AssemblyMateType MateType => AssemblyMateType.UniversalJoint;

    public string Shaft1Entity { get; set; } = string.Empty;
    public string Shaft2Entity { get; set; } = string.Empty;
    public bool DefineJointPoint { get; set; }
    public string JointPointEntity { get; set; } = string.Empty;

    public UniversalJointMateModel()
    {
        TypeId = FolderTypeId;
        Alignment = AssemblyMateAlignment.Closest;
    }

    protected override void SerializeEntities(MfcArchive ar)
    {
        SerializeShaftEntity(ar, Shaft1Entity, s => Shaft1Entity = s);
        SerializeShaftEntity(ar, Shaft2Entity, s => Shaft2Entity = s);
    }

    private void SerializeShaftEntity(MfcArchive ar, string component, Action<string> update)
    {
        ushort cylRefTag = 0;
        ar.ClassTagOrRef("moCylinderRef_w", ref cylRefTag);

        ushort compFaceTag = 0;
        ar.ClassTagOrRef("moCompFace_c", ref compFaceTag);

        string? comp = component;
        ar.WChar(ref comp);

        ushort faceRefTag = 0;
        ar.ClassTagOrRef("moFaceRef_c", ref faceRefTag);

        if (ar.IsLoading && comp != null)
        {
            update(comp);
        }
    }

    protected override void SerializeParameters(MfcArchive ar)
    {
        // Universal Joint specimen carries no D1 numeric parameter
    }

    public override AssemblyMateDefinitionData ToDefinition()
    {
        var def = new UniversalJointMateDefinitionData
        {
            DisplayName = DisplayName,
            Alignment = Alignment,
            Shaft1Entity = Shaft1Entity,
            Shaft2Entity = Shaft2Entity,
            DefineJointPoint = DefineJointPoint,
            JointPointEntity = JointPointEntity
        };
        if (!string.IsNullOrEmpty(Shaft1Entity)) def.EntitiesToMate.Add(Shaft1Entity);
        if (!string.IsNullOrEmpty(Shaft2Entity)) def.EntitiesToMate.Add(Shaft2Entity);
        if (!string.IsNullOrEmpty(JointPointEntity)) def.EntitiesToMate.Add(JointPointEntity);
        return def;
    }

    public override void FromDefinition(AssemblyMateDefinitionData definition)
    {
        if (definition is not UniversalJointMateDefinitionData def)
            throw new ArgumentException("Definition must be UniversalJointMateDefinitionData.", nameof(definition));

        DisplayName = def.DisplayName;
        Alignment = def.Alignment;
        Shaft1Entity = def.Shaft1Entity;
        Shaft2Entity = def.Shaft2Entity;
        DefineJointPoint = def.DefineJointPoint;
        JointPointEntity = def.JointPointEntity;

        if (string.IsNullOrEmpty(Shaft1Entity) && def.EntitiesToMate.Count > 0)
            Shaft1Entity = def.EntitiesToMate[0];
        if (string.IsNullOrEmpty(Shaft2Entity) && def.EntitiesToMate.Count > 1)
            Shaft2Entity = def.EntitiesToMate[1];
    }

    protected override void PopulateStructureData(MateStructureData data)
    {
        data.Layout = MateStructureLayout.MeasuredMechanical2026;
        data.MateTypeId = 19u; // swMateUNIVERSALJOINT
        data.EntityReferences = Array.Empty<MateEntityReferenceStructure>();
    }

    protected override void PopulateFromDecodedStructure(MateStructureData structure)
    {
        base.PopulateFromDecodedStructure(structure);
        if (structure.EntityReferences.Count > 0)
            Shaft1Entity = structure.EntityReferences[0].ComponentInstanceName;
        if (structure.EntityReferences.Count > 1)
            Shaft2Entity = structure.EntityReferences[1].ComponentInstanceName;
    }
}
