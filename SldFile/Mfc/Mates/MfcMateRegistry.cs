using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Central factory registry mapping MFC class names to strongly-typed mate models.
/// </summary>
internal static class MfcMateRegistry
{
    private static readonly Dictionary<string, Func<IMfcMateModel>> _factories =
        new(StringComparer.Ordinal);

    static MfcMateRegistry()
    {
        // ── P0 Advanced Mates ─────────────────────────────────────────────
        Register("moMateWidth", () => new WidthMateModel());
        Register("moMatePath_c", () => new PathMateModel());
        Register("moMateProfileCenter", () => new ProfileCenterMateModel());

        // ── P0 Mechanical Mates ───────────────────────────────────────────
        Register("moMateGearDim_c", () => new GearMateModel());
        Register("moMateRackPinionDim_c", () => new RackPinionMateModel());
        Register("moMateCamTangent", () => new CamFollowerMateModel(isTangent: true));
        Register("moMateCamCoincident", () => new CamFollowerMateModel(isTangent: false));
        Register("moMateHinge", () => new HingeMateModel());
        Register("moMateScrew_c", () => new ScrewMateModel());
        Register("moMateUniversalJoint_c", () => new UniversalJointMateModel());

        // ── Standard & Limit Mates ────────────────────────────────────────
        Register("moMateCoincident", () => new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident, 53));
        Register("moMateConcentric", () => new StandardMateModel("moMateConcentric", AssemblyMateType.Concentric, 54));
        Register("moMateDistanceDim_c", () => new StandardMateModel("moMateDistanceDim_c", AssemblyMateType.Distance, 55));
        Register("moMateParallel", () => new StandardMateModel("moMateParallel", AssemblyMateType.Parallel, 56));
        Register("moMateTangent", () => new StandardMateModel("moMateTangent", AssemblyMateType.Tangent, 57));
        Register("moMatePerpendicular", () => new StandardMateModel("moMatePerpendicular", AssemblyMateType.Perpendicular, 89));
        Register("moMateLock", () => new StandardMateModel("moMateLock", AssemblyMateType.Unknown, 90));
        Register("moMateSlot", () => new StandardMateModel("moMateSlot", AssemblyMateType.Unknown, 92));
        Register("moMatePlanarAngleDim_c", () => new StandardMateModel("moMatePlanarAngleDim_c", AssemblyMateType.PlanarAngle, 93));
        Register("moMateLimitDistanceDim_c", () => new StandardMateModel("moMateLimitDistanceDim_c", AssemblyMateType.LimitDistance, 55));
        Register("moMateLimitPlanarAngleDim_c", () => new StandardMateModel("moMateLimitPlanarAngleDim_c", AssemblyMateType.LimitAngle, 93));
        Register("moMateInPlace", () => new StandardMateModel("moMateInPlace", AssemblyMateType.InPlace, 53));
        Register("moMateSymmetric", () => new StandardMateModel("moMateSymmetric", AssemblyMateType.Unknown, 99));
        Register("moMateLinearCoupler_c", () => new StandardMateModel("moMateLinearCoupler_c", AssemblyMateType.Unknown, 102));

        // ── SW2026 Coordinate Mate ────────────────────────────────────────
        Register("moMateCoordinate", () => new StandardMateModel("moMateCoordinate", AssemblyMateType.Coordinate, 20));

        // ── Legacy SW2012 Mates ───────────────────────────────────────────
        Register("moMateCoordinateM", () => new StandardMateModel("moMateCoordinateM", AssemblyMateType.Coordinate, 20));
        Register("moMateCoincidentM", () => new StandardMateModel("moMateCoincidentM", AssemblyMateType.Coincident, 53));
        Register("moMateCoordinate_cM", () => new StandardMateModel("moMateCoordinate_cM", AssemblyMateType.Coordinate, 20));
        Register("moMateCoincident_cM", () => new StandardMateModel("moMateCoincident_cM", AssemblyMateType.Coincident, 53));
    }

    private static readonly HashSet<string> _excludedAuxiliaryClasses = new(StringComparer.Ordinal)
    {
        "moMateEntityHelpData_c",
        "moMateAnalysisData_c",
    };

    public static void Register(string className, Func<IMfcMateModel> factory)
    {
        if (string.IsNullOrEmpty(className)) throw new ArgumentNullException(nameof(className));
        _factories[className] = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public static bool IsMateClass(string? className)
    {
        if (string.IsNullOrEmpty(className)) return false;
        if (className!.StartsWith("moMaterial", StringComparison.Ordinal)) return false;
        if (_excludedAuxiliaryClasses.Contains(className)) return false;
        return _factories.ContainsKey(className) ||
               className.StartsWith("moMate", StringComparison.Ordinal);
    }

    public static IMfcMateModel Create(string className)
    {
        if (_factories.TryGetValue(className, out var factory))
            return factory();

        // Fallback for unmodeled or custom mate types: preserves raw structure losslessly
        return new GenericMateModel(className);
    }

    public static IMfcMateModel CreateForDefinition(AssemblyMateDefinitionData definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        IMfcMateModel model = definition switch
        {
            WidthMateDefinitionData => new WidthMateModel(),
            PathMateDefinitionData => new PathMateModel(),
            GearMateDefinitionData => new GearMateModel(),
            RackPinionMateDefinitionData => new RackPinionMateModel(),
            CamFollowerMateDefinitionData cam => new CamFollowerMateModel(cam.IsTangent),
            ProfileCenterMateDefinitionData => new ProfileCenterMateModel(),
            HingeMateDefinitionData => new HingeMateModel(),
            ScrewMateDefinitionData => new ScrewMateModel(),
            UniversalJointMateDefinitionData => new UniversalJointMateModel(),
            StandardMateDefinitionData std => new StandardMateModel(
                std.MateType switch
                {
                    AssemblyMateType.Distance => "moMateDistanceDim_c",
                    AssemblyMateType.Concentric => "moMateConcentric",
                    AssemblyMateType.Perpendicular => "moMatePerpendicular",
                    AssemblyMateType.Parallel => "moMateParallel",
                    AssemblyMateType.Tangent => "moMateTangent",
                    AssemblyMateType.PlanarAngle or AssemblyMateType.Angle => "moMatePlanarAngleDim_c",
                    AssemblyMateType.LimitDistance => "moMateLimitDistanceDim_c",
                    AssemblyMateType.LimitAngle => "moMateLimitPlanarAngleDim_c",
                    _ => "moMateCoincident"
                },
                std.MateType),
            _ => throw new NotSupportedException($"Unsupported mate definition type: {definition.GetType().Name}")
        };
        model.FromDefinition(definition);
        return model;
    }
}
