using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Internal MFC model for Hinge Mate (moMateHinge, TypeId 94 / 0x5E).
/// Serializes a coaxial cylindrical pair and a coincident planar pair with optional angle limits.
/// </summary>
internal sealed class HingeMateModel : MfcMateModelBase
{
    public const string MfcClassName = "moMateHinge";
    public const uint FolderTypeId = 94u; // swFmMateHinge

    public override string ClassName => MfcClassName;
    public override AssemblyMateType MateType => AssemblyMateType.Hinge;

    public string CoaxialEntity1 { get; set; } = string.Empty;
    public string CoaxialEntity2 { get; set; } = string.Empty;
    public string CoincidentEntity1 { get; set; } = string.Empty;
    public string CoincidentEntity2 { get; set; } = string.Empty;

    public bool AngleSelection { get; set; }
    public double Angle { get; set; }
    public double MinVal { get; set; }
    public double MaxVal { get; set; }

    public HingeMateModel()
    {
        TypeId = FolderTypeId;
        Alignment = AssemblyMateAlignment.Aligned;
    }

    protected override void SerializeEntities(MfcArchive ar)
    {
        // 1. Coaxial cylindrical selection
        ushort compCylTag = 0;
        ar.ClassTagOrRef("moCompRefCylinder_c", ref compCylTag);

        string? coax1 = CoaxialEntity1;
        ar.WChar(ref coax1);
        if (ar.IsLoading && coax1 != null) CoaxialEntity1 = coax1;

        string? coax2 = CoaxialEntity2;
        ar.WChar(ref coax2);
        if (ar.IsLoading && coax2 != null) CoaxialEntity2 = coax2;

        ushort refCylTag = 0;
        ar.ClassTagOrRef("moRefCylinder_c", ref refCylTag);

        // 2. Coincident planar selection
        ushort planeRefTag = 0;
        ar.ClassTagOrRef("moPlaneRef_w", ref planeRefTag);

        string? coinc1 = CoincidentEntity1;
        ar.WChar(ref coinc1);
        if (ar.IsLoading && coinc1 != null) CoincidentEntity1 = coinc1;

        string? coinc2 = CoincidentEntity2;
        ar.WChar(ref coinc2);
        if (ar.IsLoading && coinc2 != null) CoincidentEntity2 = coinc2;

        // 3. Auxiliary
        ushort analysisTag = 0;
        ar.ClassTagOrRef("moMateAnalysisData_c", ref analysisTag);
    }

    protected override void SerializeParameters(MfcArchive ar)
    {
        bool hasAngle = AngleSelection;
        byte angleFlag = hasAngle ? (byte)1 : (byte)0;
        ar.Byte(ref angleFlag);
        if (ar.IsLoading) AngleSelection = (angleFlag != 0);

        if (AngleSelection)
        {
            ushort dispTag = 0;
            ar.ClassTagOrRef("moDisplayDim_c", ref dispTag);

            ushort paramTag = 0;
            ar.ClassTagOrRef("moAngleParameter_c", ref paramTag);

            string? d1Name = "D1";
            ar.WChar(ref d1Name);

            double a = Angle;
            ar.F64(ref a);
            if (ar.IsLoading) Angle = a;

            double min = MinVal;
            ar.F64(ref min);
            if (ar.IsLoading) MinVal = min;

            double max = MaxVal;
            ar.F64(ref max);
            if (ar.IsLoading) MaxVal = max;
        }
    }

    public override AssemblyMateDefinitionData ToDefinition()
    {
        var def = new HingeMateDefinitionData
        {
            DisplayName = DisplayName,
            Alignment = Alignment,
            CoaxialEntity1 = CoaxialEntity1,
            CoaxialEntity2 = CoaxialEntity2,
            CoincidentEntity1 = CoincidentEntity1,
            CoincidentEntity2 = CoincidentEntity2,
            AngleSelection = AngleSelection,
            Angle = Angle,
            MinVal = MinVal,
            MaxVal = MaxVal
        };
        if (!string.IsNullOrEmpty(CoaxialEntity1)) def.EntitiesToMate.Add(CoaxialEntity1);
        if (!string.IsNullOrEmpty(CoaxialEntity2)) def.EntitiesToMate.Add(CoaxialEntity2);
        if (!string.IsNullOrEmpty(CoincidentEntity1)) def.EntitiesToMate.Add(CoincidentEntity1);
        if (!string.IsNullOrEmpty(CoincidentEntity2)) def.EntitiesToMate.Add(CoincidentEntity2);
        return def;
    }

    public override void FromDefinition(AssemblyMateDefinitionData definition)
    {
        if (definition is not HingeMateDefinitionData def)
            throw new ArgumentException("Definition must be HingeMateDefinitionData.", nameof(definition));

        DisplayName = def.DisplayName;
        Alignment = def.Alignment;
        CoaxialEntity1 = def.CoaxialEntity1;
        CoaxialEntity2 = def.CoaxialEntity2;
        CoincidentEntity1 = def.CoincidentEntity1;
        CoincidentEntity2 = def.CoincidentEntity2;
        AngleSelection = def.AngleSelection;
        Angle = def.Angle;
        MinVal = def.MinVal;
        MaxVal = def.MaxVal;

        if (string.IsNullOrEmpty(CoaxialEntity1) && def.EntitiesToMate.Count > 0)
            CoaxialEntity1 = def.EntitiesToMate[0];
        if (string.IsNullOrEmpty(CoaxialEntity2) && def.EntitiesToMate.Count > 1)
            CoaxialEntity2 = def.EntitiesToMate[1];
        if (string.IsNullOrEmpty(CoincidentEntity1) && def.EntitiesToMate.Count > 2)
            CoincidentEntity1 = def.EntitiesToMate[2];
        if (string.IsNullOrEmpty(CoincidentEntity2) && def.EntitiesToMate.Count > 3)
            CoincidentEntity2 = def.EntitiesToMate[3];
    }

    protected override void PopulateStructureData(MateStructureData data)
    {
        data.Layout = MateStructureLayout.MeasuredMechanical2026;
        data.MateTypeId = 22u; // swMateHINGE
        data.Angle = Angle;

        var entities = new List<MateEntityReferenceStructure>();
        if (!string.IsNullOrEmpty(CoaxialEntity1))
            entities.Add(new MateEntityReferenceStructure { ComponentInstanceName = CoaxialEntity1, Kind = MateReferenceEntityKind.Cylinder });
        if (!string.IsNullOrEmpty(CoaxialEntity2))
            entities.Add(new MateEntityReferenceStructure { ComponentInstanceName = CoaxialEntity2, Kind = MateReferenceEntityKind.Cylinder });
        if (!string.IsNullOrEmpty(CoincidentEntity1))
            entities.Add(new MateEntityReferenceStructure { ComponentInstanceName = CoincidentEntity1, Kind = MateReferenceEntityKind.Plane });
        if (!string.IsNullOrEmpty(CoincidentEntity2))
            entities.Add(new MateEntityReferenceStructure { ComponentInstanceName = CoincidentEntity2, Kind = MateReferenceEntityKind.Plane });
        data.EntityReferences = entities.AsReadOnly();
    }
}
