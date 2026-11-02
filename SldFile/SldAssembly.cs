using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Binary;

namespace SldFile;

public class SldAssembly : SwFile
{
    private IReadOnlyList<AssemblyMateInfo>? _mates;

    public SldAssembly(string filePath, bool readOnly = true) : base(filePath, readOnly)
    {
    }

    /// <summary>
    /// Structural mate entries parsed from <c>Contents/Config-0-MatesList</c>.
    /// Serialized class name, display name, reference wrappers, and for the
    /// controlled 2026 layout the mate type, alignment, two entities and
    /// Distance/Angle are decoded. Legacy/deferred layouts remain partially
    /// unresolved; see <see cref="AssemblyMateInfo.Resolution"/>.
    /// </summary>
    public IReadOnlyList<AssemblyMateInfo> Mates =>
        _mates ??= MatesList.Mates.Select(mate => new AssemblyMateInfo(mate)).ToArray();

    /// <summary>
    /// Appends one Coincident mate between two RefPlane entities to a clean
    /// 2026 assembly base (MatesList version 0xAB, zero mates). Selection names
    /// use the form <c>feature@component@assembly</c>, for example
    /// <c>前视基准面@cube-1@assembly_nomate_base</c>.
    /// <para>
    /// The mate record is written into the pending MatesList stream; call
    /// <see cref="SwFile.Save"/> to persist it. The target must be a
    /// single-configuration assembly with no existing mates, and the two
    /// selections must be RefPlane features on two different components.
    /// <see cref="Mates"/> is parsed on open; reopen the document after saving to
    /// read the new mate back.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">The assembly is opened read-only.</exception>
    /// <exception cref="NotSupportedException">
    /// The target is not a clean single-configuration base, the selections are not
    /// two <c>feature@component@assembly</c> RefPlane names on different components,
    /// or the MatesList layout is unknown.
    /// </exception>
    public void AddCoincidentMate(string entity1, string entity2)
        => MateAuthoringWriter.AddMate(this, MateAuthoringTemplates.CoincidentRecord, entity1, entity2, null);

    /// <summary>
    /// Appends one Distance mate between two RefPlane entities to a clean 2026
    /// assembly base. <paramref name="distance"/> is in meters and must be a finite
    /// value in (0, 1000]. The selection name rules and exceptions are the same as
    /// <see cref="AddCoincidentMate"/>.
    /// </summary>
    public void AddDistanceMate(string entity1, string entity2, double distance)
        => MateAuthoringWriter.AddMate(this, MateAuthoringTemplates.DistanceRecord, entity1, entity2, distance);

    /// <summary>
    /// Appends the natively verified dual-mate group to a clean 2026 assembly
    /// base: a Coincident mate between the two front planes followed by a
    /// Distance mate between the two right planes of the same component pair.
    /// The MatesList stream is written with two mate records (version 0xAB); the
    /// assembly tree streams are left untouched, matching the natively verified
    /// MatesList-only layout.
    /// <para>
    /// Only the measured <c>cube-1</c>/<c>cylinder-1</c> component pair and the
    /// localized <c>前视基准面</c> (Coincident) / <c>右视基准面</c> (Distance)
    /// planes are supported. Selection names use
    /// <c>feature@component@assembly</c> and must use the target assembly's
    /// document name. <paramref name="distance"/> is in meters and must be a
    /// finite value in (0, 1000]. Other component pairs, plane layouts, existing
    /// mates and multiple configurations are rejected without changing the
    /// stream. Call <see cref="SwFile.Save"/> to persist and reopen the document
    /// to read both mates back.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">The assembly is opened read-only.</exception>
    /// <exception cref="NotSupportedException">
    /// The target is not a clean single-configuration base or the selections are
    /// not the verified front/right plane pair on the cube-1/cylinder-1
    /// components.
    /// </exception>
    public void AddCoincidentDistanceMates(
        string coincidentEntity1,
        string coincidentEntity2,
        string distanceEntity1,
        string distanceEntity2,
        double distance)
        => MateAuthoringWriter.AddMateGroup(
            this, MateGroupAuthoringTemplates.DualCoincidentDistanceList,
            coincidentEntity1, coincidentEntity2, distanceEntity1, distanceEntity2, distance);

    /// <summary>
    /// Appends the verified dual-mate group using the measured 0.01 m Distance
    /// value. See the overload with an explicit distance for the supported
    /// component/plane combination and exceptions.
    /// </summary>
    public void AddCoincidentDistanceMates(
        string coincidentEntity1,
        string coincidentEntity2,
        string distanceEntity1,
        string distanceEntity2)
        => AddCoincidentDistanceMates(
            coincidentEntity1, coincidentEntity2, distanceEntity1, distanceEntity2, 0.01);

    /// <summary>
    /// Appends one Gear mate between two cylindrical-face components to the
    /// matching clean base (the base must retain the Gear specimen's ModelHeader
    /// template with display name "齿轮配合1"). Component names use
    /// <c>component@assembly</c>; the gear ratio is numerator/denominator.
    /// </summary>
    public void AddGearMate(string component1, string component2, double numerator, double denominator)
    {
        ValidatePositiveParameter(numerator, nameof(numerator));
        ValidatePositiveParameter(denominator, nameof(denominator));
        MateAuthoringWriter.AddAdvancedMate(
            this, MateAuthoringTemplates.GearRecord, MateAuthoringTemplates.GearStreamVersion,
            "齿轮配合1", MateAuthoringWriter.MateAuthoringEntityLayout.ComponentInstances,
            component1, component2, numerator, "moLengthParameter_c", denominator);
    }

    /// <summary>
    /// Appends one Screw mate between two cylindrical-face components to the
    /// matching clean base (ModelHeader display name "螺旋1"). The revolution value
    /// is patched into the measured D1 parameter.
    /// </summary>
    public void AddScrewMate(string component1, string component2, double revolutionsPerUnit)
    {
        ValidatePositiveParameter(revolutionsPerUnit, nameof(revolutionsPerUnit));
        MateAuthoringWriter.AddAdvancedMate(
            this, MateAuthoringTemplates.ScrewRecord, MateAuthoringTemplates.ScrewStreamVersion,
            "螺旋1", MateAuthoringWriter.MateAuthoringEntityLayout.ComponentInstances,
            component1, component2, revolutionsPerUnit, "moScalerParameter_c", null);
    }

    /// <summary>
    /// Appends one UniversalJoint mate between two cylindrical-face components to
    /// the matching clean base (ModelHeader display name "万向节1"). The specimen's
    /// measured values are reused; no numeric parameter is written.
    /// </summary>
    public void AddUniversalJointMate(string component1, string component2)
        => MateAuthoringWriter.AddAdvancedMate(
            this, MateAuthoringTemplates.UniversalJointRecord, MateAuthoringTemplates.UniversalJointStreamVersion,
            "万向节1", MateAuthoringWriter.MateAuthoringEntityLayout.None,
            component1, component2, null, null, null);

    /// <summary>
    /// Appends one LimitDistance mate between two RefPlane entities to the matching
    /// clean base (ModelHeader display name "LimitDistance1"). The distance is in
    /// meters, patched into the measured D1 parameter.
    /// </summary>
    public void AddLimitDistanceMate(string entity1, string entity2, double distance)
    {
        if (double.IsNaN(distance) || double.IsInfinity(distance) || distance <= 0 || distance > 1000)
            throw new ArgumentOutOfRangeException(nameof(distance), "Distance must be a finite value in (0, 1000] meters.");
        MateAuthoringWriter.AddAdvancedMate(
            this, MateAuthoringTemplates.LimitDistanceRecord, MateAuthoringTemplates.LimitDistanceStreamVersion,
            "LimitDistance1", MateAuthoringWriter.MateAuthoringEntityLayout.PlaneSelectionPairs,
            entity1, entity2, distance, "moLengthParameter_c", null);
    }

    /// <summary>
    /// Appends one LimitAngle mate between two RefPlane entities to the matching
    /// clean base (ModelHeader display name "LimitAngle1"). The angle is in radians
    /// and must be in (0, 2π), patched into the measured D1 parameter.
    /// </summary>
    public void AddLimitAngleMate(string entity1, string entity2, double angleRadians)
    {
        if (double.IsNaN(angleRadians) || double.IsInfinity(angleRadians) ||
            angleRadians <= 0 || angleRadians >= 2 * Math.PI)
            throw new ArgumentOutOfRangeException(nameof(angleRadians), "Angle must be a finite value in (0, 2π) radians.");
        MateAuthoringWriter.AddAdvancedMate(
            this, MateAuthoringTemplates.LimitAngleRecord, MateAuthoringTemplates.LimitAngleStreamVersion,
            "LimitAngle1", MateAuthoringWriter.MateAuthoringEntityLayout.PlaneSelectionPairs,
            entity1, entity2, angleRadians, "moAngleParameter_c", null);
    }

    /// <summary>
    /// Applies the measured Width mate template (ConstraintType = Dimension) with
    /// DistanceFromEnd 0.01 m or 0.02 m and the requested FlipDimension. The base
    /// must contain exactly one measured dimensioned <c>moMateWidth</c> mate
    /// (display dimension + D1 length parameter + four deferred planar-face
    /// references). The measured four-mate MatesList template for the requested
    /// flip state is written and its D1 distance patched; other distances are
    /// rejected. The width feature is known to carry a SOLIDWORKS warning
    /// (code 1) after switching from Centered to Dimension; this is the measured
    /// native state.
    /// </summary>
    public void SetWidthMateDimension(double distance, bool flipDimension)
        => MateAuthoringWriter.SetWidthMateDimension(this, distance, flipDimension);

    /// <summary>
    /// Adds a RackPinion mate to the measured clean three-mate rack-and-pinion
    /// base by writing the natively verified four-mate MatesList template and
    /// patching the RackPinion diameter option/value and reverse flag. Only the
    /// measured <c>rack-1@rack and pinion</c> / <c>spurgear-1@rack and pinion</c>
    /// pair is supported; the base must be the clean Coincident+Distance+Parallel
    /// assembly with no existing RackPinion mate.
    /// </summary>
    public void AddRackPinionMate(
        string rackComponent,
        string pinionComponent,
        AssemblyMateRackPinionDiameterType diameterType,
        double diameterValue,
        bool reverse)
    {
        if (diameterType != AssemblyMateRackPinionDiameterType.PinionPitchDiameter &&
            diameterType != AssemblyMateRackPinionDiameterType.RackTravelPerRevolution)
            throw new ArgumentOutOfRangeException(nameof(diameterType), "A known RackPinion diameter option is required.");
        MateAuthoringWriter.AddRackPinionMate(
            this, rackComponent, pinionComponent,
            diameterType == AssemblyMateRackPinionDiameterType.PinionPitchDiameter ? 0 : 1,
            diameterValue, reverse);
    }

    /// <summary>
    /// Writes the fixed measured motion_path_tutorial Path template: four free
    /// Path mates (moMatePath_c) plus the tutorial Hinge/Concentric/Coincident/
    /// Parallel mates. The base must be the measured clean tutorial assembly with
    /// the four PathMate records removed (MatesList version 0x00E6, u16@4=12);
    /// no Path parameters or entities are rewritten.
    /// </summary>
    public void AddTutorialPathMates()
        => MateAuthoringWriter.AddTutorialPathMates(this);

    /// <summary>
    /// Writes the fixed measured chain_link1 Hinge template (four Hinge mates).
    /// The base must be the measured clean chain_link1 assembly with its Hinge
    /// mates removed (MatesList version 0x00E7, zero entries, 6 bytes).
    /// </summary>
    public void AddTutorialHingeMates()
        => MateAuthoringWriter.AddTutorialHingeMates(this);

    /// <summary>
    /// Writes the fixed measured cam-follower Cam template (CamTangent and
    /// CamCoincident plus the tutorial's other mates). The base must be the
    /// measured clean cam-follower assembly with the CamMate features removed.
    /// </summary>
    public void AddTutorialCamMates()
        => MateAuthoringWriter.AddTutorialCamMates(this);

    /// <summary>
    /// Writes the fixed measured key pad PlanarAngle template (two measured
    /// PlanarAngle mates plus the tutorial's other mates). The base must be the
    /// measured clean key pad assembly with Angle1/Angle2 removed.
    /// </summary>
    public void AddTutorialPlanarAngleMates()
        => MateAuthoringWriter.AddTutorialPlanarAngleMates(this);

    private static void ValidatePositiveParameter(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            throw new ArgumentOutOfRangeException(parameterName, "The parameter must be a finite positive value.");
    }
}
