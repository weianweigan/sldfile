using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Internal MFC model for Cam Follower Mate (moMateCamTangent / moMateCamCoincident, TypeId 91 / 0x5B).
/// Serializes a follower cylinder/point entity, a sequence of tangent cam profile faces, and alignment.
/// </summary>
internal sealed class CamFollowerMateModel : MfcMateModelBase
{
    public const string TangentClassName = "moMateCamTangent";
    public const string CoincidentClassName = "moMateCamCoincident";
    public const uint FolderTypeId = 91u; // swFmMateCamTangent

    public bool IsTangent { get; set; } = true;

    public override string ClassName => IsTangent ? TangentClassName : CoincidentClassName;
    public override AssemblyMateType MateType => AssemblyMateType.Cam;

    public string FollowerEntity { get; set; } = string.Empty;
    public List<string> CamProfileFaces { get; } = new();

    public CamFollowerMateModel(bool isTangent = true)
    {
        IsTangent = isTangent;
        TypeId = FolderTypeId;
    }

    protected override void SerializeEntities(MfcArchive ar)
    {
        // 1. Follower Entity
        if (IsTangent)
        {
            ushort cylRefTag = 0;
            ar.ClassTagOrRef("moCylinderRef_w", ref cylRefTag);

            ushort compCylTag = 0;
            ar.ClassTagOrRef("moCompRefCylinder_c", ref compCylTag);

            string? follower = FollowerEntity;
            ar.WChar(ref follower);
            if (ar.IsLoading && follower != null) FollowerEntity = follower;

            ushort refCylTag = 0;
            ar.ClassTagOrRef("moRefCylinder_c", ref refCylTag);
        }
        else
        {
            ushort ptRefTag = 0;
            ar.ClassTagOrRef("moPointRef_w", ref ptRefTag);

            ushort compPtTag = 0;
            ar.ClassTagOrRef("moCompRefPoint_c", ref compPtTag);

            string? follower = FollowerEntity;
            ar.WChar(ref follower);
            if (ar.IsLoading && follower != null) FollowerEntity = follower;

            ushort refPtTag = 0;
            ar.ClassTagOrRef("moRefPoint_c", ref refPtTag);

            ushort refPtDataTag = 0;
            ar.ClassTagOrRef("moRefPointData_c", ref refPtDataTag);
        }

        // 2. Cam Profile Tangent Faces
        ushort count = (ushort)CamProfileFaces.Count;
        ar.U16(ref count);

        if (ar.IsStoring)
        {
            foreach (var face in CamProfileFaces)
            {
                ushort compFaceTag = 0;
                ar.ClassTagOrRef("moCompFace_c", ref compFaceTag);

                string? f = face;
                ar.WChar(ref f);

                ushort faceRefTag = 0;
                ar.ClassTagOrRef("moFaceRef_c", ref faceRefTag);
            }
        }
        else
        {
            CamProfileFaces.Clear();
            for (int i = 0; i < count; i++)
            {
                ushort compFaceTag = 0;
                ar.ClassTagOrRef("moCompFace_c", ref compFaceTag);

                string? f = null;
                ar.WChar(ref f);
                if (f != null) CamProfileFaces.Add(f);

                ushort faceRefTag = 0;
                ar.ClassTagOrRef("moFaceRef_c", ref faceRefTag);
            }
        }

        // 3. Auxiliary
        ushort helpTag = 0;
        ar.ClassTagOrRef("moMateEntityHelpData_c", ref helpTag);
    }

    protected override void SerializeParameters(MfcArchive ar)
    {
        // Cam mates carry no numeric D1 parameter
    }

    public override AssemblyMateDefinitionData ToDefinition()
    {
        var def = new CamFollowerMateDefinitionData
        {
            DisplayName = DisplayName,
            Alignment = Alignment,
            IsTangent = IsTangent,
            FollowerEntity = FollowerEntity
        };
        def.CamProfileFaces.AddRange(CamProfileFaces);
        if (!string.IsNullOrEmpty(FollowerEntity))
            def.EntitiesToMate.Add(FollowerEntity);
        def.EntitiesToMate.AddRange(CamProfileFaces);
        return def;
    }

    public override void FromDefinition(AssemblyMateDefinitionData definition)
    {
        if (definition is not CamFollowerMateDefinitionData def)
            throw new ArgumentException("Definition must be CamFollowerMateDefinitionData.", nameof(definition));

        DisplayName = def.DisplayName;
        Alignment = def.Alignment;
        IsTangent = def.IsTangent;
        FollowerEntity = def.FollowerEntity;
        CamProfileFaces.Clear();
        CamProfileFaces.AddRange(def.CamProfileFaces);
    }

    protected override void PopulateStructureData(MateStructureData data)
    {
        data.Layout = MateStructureLayout.MeasuredMechanical2026;
        data.MateTypeId = 9u; // swMateCAMFOLLOWER

        var entities = new List<MateEntityReferenceStructure>
        {
            new MateEntityReferenceStructure
            {
                Kind = IsTangent ? MateReferenceEntityKind.Cylinder : MateReferenceEntityKind.Face,
                ComponentInstanceName = FollowerEntity
            }
        };
        foreach (var face in CamProfileFaces)
        {
            entities.Add(new MateEntityReferenceStructure
            {
                Kind = MateReferenceEntityKind.Plane,
                SelectionName = face
            });
        }
        data.EntityReferences = entities.AsReadOnly();
    }

    protected override void PopulateFromDecodedStructure(MateStructureData structure)
    {
        base.PopulateFromDecodedStructure(structure);
        if (structure.EntityReferences.Count > 0)
        {
            FollowerEntity = structure.EntityReferences[0].ComponentInstanceName;
        }
        CamProfileFaces.Clear();
        for (int i = 1; i < structure.EntityReferences.Count; i++)
        {
            CamProfileFaces.Add(structure.EntityReferences[i].SelectionName);
        }
    }
}
