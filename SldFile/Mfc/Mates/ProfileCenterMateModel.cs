using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Internal MFC model for Profile Center Mate (moMateProfileCenter, TypeId 101 / 0x65).
/// Constrains two profile centroids with optional normal offset distance and rotation lock.
/// </summary>
internal sealed class ProfileCenterMateModel : MfcMateModelBase
{
    public const string MfcClassName = "moMateProfileCenter";
    public const uint FolderTypeId = 101u; // swFmMateProfileCenter

    public override string ClassName => MfcClassName;
    public override AssemblyMateType MateType => AssemblyMateType.ProfileCenter;

    public double OffsetDistance { get; set; }
    public bool FlipDimension { get; set; }
    public bool LockRotation { get; set; }

    public string FirstProfileComponent { get; set; } = string.Empty;
    public string FirstProfileFace { get; set; } = string.Empty;

    public string SecondProfileComponent { get; set; } = string.Empty;
    public string SecondProfileFace { get; set; } = string.Empty;

    public ProfileCenterMateModel()
    {
        TypeId = FolderTypeId;
    }

    public override void AddPlaneReference(string selectionName)
    {
        base.AddPlaneReference(selectionName);
        if (string.IsNullOrEmpty(FirstProfileComponent)) FirstProfileComponent = selectionName;
        else if (string.IsNullOrEmpty(SecondProfileComponent)) SecondProfileComponent = selectionName;
    }

    protected override void SerializeEntities(MfcArchive ar)
    {
        SerializeProfileFace(ar, FirstProfileComponent, FirstProfileFace, (c, f) => { FirstProfileComponent = c; FirstProfileFace = f; });
        SerializeProfileFace(ar, SecondProfileComponent, SecondProfileFace, (c, f) => { SecondProfileComponent = c; SecondProfileFace = f; });

        if (ar.IsLoading)
        {
            _entities.Clear();
            AddPlaneReference(FirstProfileComponent);
            AddPlaneReference(SecondProfileComponent);
        }
    }

    private void SerializeProfileFace(MfcArchive ar, string component, string face, Action<string, string> update)
    {
        ushort planeRefTag = 0;
        ar.ClassTagOrRef("moPlaneRef_w", ref planeRefTag);

        ushort compFaceTag = 0;
        ar.ClassTagOrRef("moCompFace_c", ref compFaceTag);

        string? comp = component;
        ar.WChar(ref comp);

        string? fc = face;
        ar.WChar(ref fc);

        ushort faceRefTag = 0;
        ar.ClassTagOrRef("moFaceRef_c", ref faceRefTag);

        if (ar.IsLoading)
        {
            update(comp ?? string.Empty, fc ?? string.Empty);
        }
    }

    protected override void SerializeParameters(MfcArchive ar)
    {
        bool hasOffset = OffsetDistance != 0.0;
        byte offsetFlag = hasOffset ? (byte)1 : (byte)0;
        ar.Byte(ref offsetFlag);
        if (ar.IsLoading) hasOffset = (offsetFlag != 0);

        if (hasOffset)
        {
            ushort dispTag = 0;
            ar.ClassTagOrRef("moDisplayDistanceDim_c", ref dispTag);

            ushort paramTag = 0;
            ar.ClassTagOrRef("moLengthParameter_c", ref paramTag);

            string? d1Name = "D1";
            ar.WChar(ref d1Name);

            double dist = OffsetDistance;
            ar.F64(ref dist);
            if (ar.IsLoading) OffsetDistance = dist;

            byte signByte = FlipDimension ? (byte)0x3F : (byte)0xBF;
            ar.Byte(ref signByte);
            if (ar.IsLoading) FlipDimension = (signByte == 0x3F);
        }

        // Lock Rotation flag inside favorite handle
        ushort favTag = 0;
        ar.ClassTagOrRef("moFavoriteHandle_c", ref favTag);

        byte lockFlag = LockRotation ? (byte)0x01 : (byte)0x00;
        ar.Byte(ref lockFlag);
        if (ar.IsLoading) LockRotation = (lockFlag != 0x00);
    }

    public override AssemblyMateDefinitionData ToDefinition()
    {
        var def = new ProfileCenterMateDefinitionData
        {
            DisplayName = DisplayName,
            Alignment = Alignment,
            OffsetDistance = OffsetDistance,
            FlipDimension = FlipDimension,
            LockRotation = LockRotation,
            FirstProfileEntity = FirstProfileFace,
            SecondProfileEntity = SecondProfileFace
        };
        if (!string.IsNullOrEmpty(FirstProfileComponent))
            def.EntitiesToMate.Add(FirstProfileComponent);
        if (!string.IsNullOrEmpty(SecondProfileComponent))
            def.EntitiesToMate.Add(SecondProfileComponent);
        return def;
    }

    public override void FromDefinition(AssemblyMateDefinitionData definition)
    {
        if (definition is not ProfileCenterMateDefinitionData def)
            throw new ArgumentException("Definition must be ProfileCenterMateDefinitionData.", nameof(definition));

        DisplayName = def.DisplayName;
        Alignment = def.Alignment;
        OffsetDistance = def.OffsetDistance;
        FlipDimension = def.FlipDimension;
        LockRotation = def.LockRotation;
        FirstProfileFace = def.FirstProfileEntity;
        SecondProfileFace = def.SecondProfileEntity;

        if (def.EntitiesToMate.Count >= 2)
        {
            FirstProfileComponent = def.EntitiesToMate[0];
            SecondProfileComponent = def.EntitiesToMate[1];
        }

        _entities.Clear();
        foreach (var ent in def.EntitiesToMate)
            AddPlaneReference(ent);
    }

    protected override void PopulateStructureData(MateStructureData data)
    {
        data.Layout = MateStructureLayout.PlaneReference2026;
        data.MateTypeId = 23u; // swMatePROFILECENTER
        data.Distance = OffsetDistance;

        var entities = new List<MateEntityReferenceStructure>
        {
            new MateEntityReferenceStructure
            {
                Kind = MateReferenceEntityKind.Plane,
                ComponentInstanceName = FirstProfileComponent,
                SelectionName = FirstProfileFace
            },
            new MateEntityReferenceStructure
            {
                Kind = MateReferenceEntityKind.Plane,
                ComponentInstanceName = SecondProfileComponent,
                SelectionName = SecondProfileFace
            }
        };
        data.EntityReferences = entities.AsReadOnly();
    }

    protected override void PopulateFromDecodedStructure(MateStructureData structure)
    {
        base.PopulateFromDecodedStructure(structure);
        if (structure.Distance.HasValue)
            OffsetDistance = structure.Distance.Value;
        if (structure.EntityReferences.Count >= 2)
        {
            FirstProfileComponent = structure.EntityReferences[0].ComponentInstanceName;
            FirstProfileFace = structure.EntityReferences[0].SelectionName;
            SecondProfileComponent = structure.EntityReferences[1].ComponentInstanceName;
            SecondProfileFace = structure.EntityReferences[1].SelectionName;
            _entities.Clear();
            AddPlaneReference(FirstProfileComponent);
            AddPlaneReference(SecondProfileComponent);
        }
    }

    protected override void PopulateReferences(List<MateReferenceEntry> refs)
    {
        if (refs.Count == 0)
        {
            refs.Add(new MateReferenceEntry
            {
                WrapperClassName = "moPlaneRef_w",
                EntityClassNames = new[] { "moCompFace_c", "moFaceRef_c" }
            });
            refs.Add(new MateReferenceEntry
            {
                WrapperClassName = "moPlaneRef_w",
                EntityClassNames = new[] { "moCompFace_c", "moFaceRef_c" }
            });
        }
    }
}
