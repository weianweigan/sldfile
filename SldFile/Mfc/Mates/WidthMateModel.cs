using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Internal MFC model for Width Mate (moMateWidth, TypeId 100 / 0x64).
/// Serializes 4 planar face selections and optional D1 distance/percent parameter.
/// </summary>
internal sealed class WidthMateModel : MfcMateModelBase
{
    public const string MfcClassName = "moMateWidth";
    public const uint FolderTypeId = 100u; // swFmMateWidth

    public override string ClassName => MfcClassName;
    public override AssemblyMateType MateType => AssemblyMateType.Width;

    public AssemblyMateWidthConstraint ConstraintType { get; set; } = AssemblyMateWidthConstraint.Centered;
    public double DistanceFromEnd { get; set; }
    public double PercentDistanceFromEnd { get; set; }
    public bool FlipDimension { get; set; }

    // 4 Planar Face Selections: Width1, Tab1, Width2, Tab2
    public string WidthEntity1Component { get; set; } = string.Empty;
    public string WidthEntity1SurfaceId { get; set; } = "moEndFaceSurfIdRep_c,13,0,-1, \n";

    public string TabEntity1Component { get; set; } = string.Empty;
    public string TabEntity1SurfaceId { get; set; } = "moFromSktEntSurfIdRep_c,18,2, \n";

    public string WidthEntity2Component { get; set; } = string.Empty;
    public string WidthEntity2SurfaceId { get; set; } = "moEndFaceSurfIdRep_c,13,1,-1, \n";

    public string TabEntity2Component { get; set; } = string.Empty;
    public string TabEntity2SurfaceId { get; set; } = "moFromSktEntSurfIdRep_c,18,4, \n";

    public WidthMateModel()
    {
        TypeId = FolderTypeId;
    }

    public override void AddPlaneReference(string selectionName)
    {
        base.AddPlaneReference(selectionName);
        if (string.IsNullOrEmpty(WidthEntity1Component)) WidthEntity1Component = selectionName;
        else if (string.IsNullOrEmpty(TabEntity1Component)) TabEntity1Component = selectionName;
        else if (string.IsNullOrEmpty(WidthEntity2Component)) WidthEntity2Component = selectionName;
        else if (string.IsNullOrEmpty(TabEntity2Component)) TabEntity2Component = selectionName;
    }

    protected override void SerializeEntities(MfcArchive ar)
    {
        SerializeFaceEntity(ar, WidthEntity1SurfaceId, WidthEntity1Component, (s, c) => { WidthEntity1SurfaceId = s; WidthEntity1Component = c; });
        SerializeFaceEntity(ar, TabEntity1SurfaceId, TabEntity1Component, (s, c) => { TabEntity1SurfaceId = s; TabEntity1Component = c; });
        SerializeFaceEntity(ar, WidthEntity2SurfaceId, WidthEntity2Component, (s, c) => { WidthEntity2SurfaceId = s; WidthEntity2Component = c; });
        SerializeFaceEntity(ar, TabEntity2SurfaceId, TabEntity2Component, (s, c) => { TabEntity2SurfaceId = s; TabEntity2Component = c; });

        if (ar.IsLoading)
        {
            _entities.Clear();
            AddPlaneReference(WidthEntity1Component);
            AddPlaneReference(TabEntity1Component);
            AddPlaneReference(WidthEntity2Component);
            AddPlaneReference(TabEntity2Component);
        }
    }

    private void SerializeFaceEntity(MfcArchive ar, string surfId, string component, Action<string, string> update)
    {
        ushort planeRefTag = 0;
        ar.ClassTagOrRef("moPlaneRef_w", ref planeRefTag);

        string? sId = surfId;
        ar.WChar(ref sId);

        string? comp = component;
        ar.WChar(ref comp);

        if (ar.IsLoading)
        {
            update(sId ?? string.Empty, comp ?? string.Empty);
        }
    }

    protected override void SerializeParameters(MfcArchive ar)
    {
        byte paramFlag = (byte)ConstraintType;
        ar.Byte(ref paramFlag);
        if (ar.IsLoading)
        {
            ConstraintType = (AssemblyMateWidthConstraint)paramFlag;
        }

        bool hasParams = ConstraintType == AssemblyMateWidthConstraint.Dimension ||
                         ConstraintType == AssemblyMateWidthConstraint.Percent;

        if (hasParams)
        {
            ushort dispTag = 0;
            ar.ClassTagOrRef("moDisplayWidthDim_c", ref dispTag);

            ushort handleTag = 0;
            ar.ClassTagOrRef("moFeatureDimHandle_c", ref handleTag);

            ushort distDimTag = 0;
            ar.ClassTagOrRef("ParallelPlaneDistanceDim_c", ref distDimTag);

            ushort paramTag = 0;
            ar.ClassTagOrRef("moLengthParameter_c", ref paramTag);

            string? d1Name = "D1";
            ar.WChar(ref d1Name);

            double val = (ConstraintType == AssemblyMateWidthConstraint.Dimension)
                ? DistanceFromEnd
                : PercentDistanceFromEnd;
            ar.F64(ref val);
            if (ar.IsLoading)
            {
                if (ConstraintType == AssemblyMateWidthConstraint.Dimension)
                    DistanceFromEnd = val;
                else
                    PercentDistanceFromEnd = val;
            }

            // FlipDimension sign byte metadata: 0xBF for false, 0x3F for true
            byte signByte = FlipDimension ? (byte)0x3F : (byte)0xBF;
            ar.Byte(ref signByte);
            if (ar.IsLoading)
                FlipDimension = (signByte == 0x3F);

            ushort favTag = 0;
            ar.ClassTagOrRef("moFavoriteHandle_c", ref favTag);
        }
    }

    public override AssemblyMateDefinitionData ToDefinition()
    {
        var def = new WidthMateDefinitionData
        {
            DisplayName = DisplayName,
            Alignment = Alignment,
            ConstraintType = ConstraintType,
            DistanceFromEnd = DistanceFromEnd,
            PercentDistanceFromEnd = PercentDistanceFromEnd,
            FlipDimension = FlipDimension
        };
        def.WidthEntities.Add(WidthEntity1Component);
        def.WidthEntities.Add(WidthEntity2Component);
        def.TabEntities.Add(TabEntity1Component);
        def.TabEntities.Add(TabEntity2Component);
        def.EntitiesToMate.AddRange(def.WidthEntities);
        def.EntitiesToMate.AddRange(def.TabEntities);
        return def;
    }

    public override void FromDefinition(AssemblyMateDefinitionData definition)
    {
        if (definition is not WidthMateDefinitionData def)
            throw new ArgumentException("Definition must be WidthMateDefinitionData.", nameof(definition));

        DisplayName = def.DisplayName;
        Alignment = def.Alignment;
        ConstraintType = def.ConstraintType;
        DistanceFromEnd = def.DistanceFromEnd;
        PercentDistanceFromEnd = def.PercentDistanceFromEnd;
        FlipDimension = def.FlipDimension;

        if (def.WidthEntities.Count >= 2)
        {
            WidthEntity1Component = def.WidthEntities[0];
            WidthEntity2Component = def.WidthEntities[1];
        }
        else if (def.EntitiesToMate.Count >= 2)
        {
            WidthEntity1Component = def.EntitiesToMate[0];
            WidthEntity2Component = def.EntitiesToMate[1];
        }

        if (def.TabEntities.Count >= 2)
        {
            TabEntity1Component = def.TabEntities[0];
            TabEntity2Component = def.TabEntities[1];
        }
        else if (def.EntitiesToMate.Count >= 4)
        {
            TabEntity1Component = def.EntitiesToMate[2];
            TabEntity2Component = def.EntitiesToMate[3];
        }

        _entities.Clear();
        foreach (var ent in def.EntitiesToMate)
            AddPlaneReference(ent);
    }

    protected override void PopulateStructureData(MateStructureData data)
    {
        data.Layout = MateStructureLayout.Width2026;
        data.MateTypeId = 11u;
        data.WidthConstraintTypeId = ConstraintType switch
        {
            AssemblyMateWidthConstraint.Centered => 0u,
            AssemblyMateWidthConstraint.Free => 1u,
            AssemblyMateWidthConstraint.Dimension => 2u,
            AssemblyMateWidthConstraint.Percent => 3u,
            _ => (uint?)null
        };

        if (ConstraintType == AssemblyMateWidthConstraint.Dimension)
        {
            data.WidthDistanceFromEnd = DistanceFromEnd;
            data.WidthFlipDimension = FlipDimension;
            data.Distance = DistanceFromEnd;
        }
        else if (ConstraintType == AssemblyMateWidthConstraint.Percent)
        {
            data.WidthDistanceFromEnd = PercentDistanceFromEnd;
            data.WidthFlipDimension = FlipDimension;
        }

        var entities = new List<MateEntityReferenceStructure>
        {
            new MateEntityReferenceStructure { ComponentInstanceName = WidthEntity1Component, SelectionName = WidthEntity1SurfaceId, Kind = MateReferenceEntityKind.Plane },
            new MateEntityReferenceStructure { ComponentInstanceName = WidthEntity2Component, SelectionName = WidthEntity2SurfaceId, Kind = MateReferenceEntityKind.Plane },
            new MateEntityReferenceStructure { ComponentInstanceName = TabEntity1Component, SelectionName = TabEntity1SurfaceId, Kind = MateReferenceEntityKind.Plane },
            new MateEntityReferenceStructure { ComponentInstanceName = TabEntity2Component, SelectionName = TabEntity2SurfaceId, Kind = MateReferenceEntityKind.Plane },
        };
        data.EntityReferences = entities.AsReadOnly();
    }

    protected override void PopulateFromDecodedStructure(MateStructureData structure)
    {
        base.PopulateFromDecodedStructure(structure);

        if (structure.WidthConstraintTypeId.HasValue)
        {
            ConstraintType = structure.WidthConstraintTypeId.Value switch
            {
                0u => AssemblyMateWidthConstraint.Centered,
                1u => AssemblyMateWidthConstraint.Free,
                2u => AssemblyMateWidthConstraint.Dimension,
                3u => AssemblyMateWidthConstraint.Percent,
                _ => AssemblyMateWidthConstraint.Centered
            };
        }
        else if (structure.WidthDistanceFromEnd.HasValue)
        {
            ConstraintType = AssemblyMateWidthConstraint.Dimension;
        }

        if (structure.WidthDistanceFromEnd.HasValue)
        {
            if (ConstraintType == AssemblyMateWidthConstraint.Percent)
                PercentDistanceFromEnd = structure.WidthDistanceFromEnd.Value;
            else
                DistanceFromEnd = structure.WidthDistanceFromEnd.Value;
        }

        if (structure.WidthFlipDimension.HasValue)
        {
            FlipDimension = structure.WidthFlipDimension.Value;
        }

        if (structure.EntityReferences.Count >= 4)
        {
            WidthEntity1Component = structure.EntityReferences[0].ComponentInstanceName;
            WidthEntity2Component = structure.EntityReferences[1].ComponentInstanceName;
            TabEntity1Component = structure.EntityReferences[2].ComponentInstanceName;
            TabEntity2Component = structure.EntityReferences[3].ComponentInstanceName;
        }
    }
}
