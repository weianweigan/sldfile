using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFile;

public partial class SldAssembly : SwFile
{
    private IReadOnlyList<AssemblyMateInfo>? _mates;

    public SldAssembly(string filePath, bool readOnly = true) : base(filePath, readOnly)
    {
    }

    /// <summary>Components belonging to the active assembly configuration.</summary>
    public IReadOnlyList<AssemblyComponentNode> Components => GetAssemblyTree();



    /// <summary>
    /// Structural mate entries parsed from <c>Contents/Config-0-MatesList</c>.
    /// Serialized class name, display name, reference wrappers, and for the
    /// controlled 2026 layout the mate type, alignment, two entities and
    /// Distance/Angle are decoded. Legacy/deferred layouts remain partially
    /// unresolved; see <see cref="AssemblyMateInfo.Resolution"/>.
    /// </summary>
    public IReadOnlyList<AssemblyMateInfo> Mates =>
        _mates ??= MatesList.Mates.Select(mate => new AssemblyMateInfo(mate.ToMateEntry())).ToArray();

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
    {
        if (IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        ValidatePlaneSelections(entity1, entity2);

        var mate = new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident)
        {
            DisplayName = "Coincident" + (MatesList.Mates.Count + 1),
            Alignment = AssemblyMateAlignment.Aligned
        };
        mate.AddPlaneReference(entity1);
        mate.AddPlaneReference(entity2);
        AddMate(mate);
    }

    /// <summary>
    /// Appends one Distance mate between two RefPlane entities to a clean 2026
    /// assembly base. <paramref name="distance"/> is in meters and must be a finite
    /// value in (0, 1000]. The selection name rules and exceptions are the same as
    /// <see cref="AddCoincidentMate"/>.
    /// </summary>
    public void AddDistanceMate(string entity1, string entity2, double distance)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");
        if (double.IsNaN(distance) || double.IsInfinity(distance) || distance <= 0 || distance > 1000)
            throw new ArgumentOutOfRangeException(nameof(distance), "Distance must be a finite value in (0, 1000] meters.");
        ValidatePlaneSelections(entity1, entity2);

        var mate = new StandardMateModel("moMateDistanceDim_c", AssemblyMateType.Distance)
        {
            DisplayName = "Distance" + (MatesList.Mates.Count + 1),
            Alignment = AssemblyMateAlignment.Aligned,
            Distance = distance
        };
        mate.AddPlaneReference(entity1);
        mate.AddPlaneReference(entity2);
        AddMate(mate);
    }

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
        ValidateComponentSelections(component1, component2, "齿轮配合1");

        MatesList.StreamVersion = 0x00BF;
        var mate = new GearMateModel
        {
            DisplayName = "齿轮配合1",
            Pinion1Component = component1,
            Pinion2Component = component2,
            GearRatioNumerator = numerator,
            GearRatioDenominator = denominator
        };
        mate.AddPlaneReference(component1);
        mate.AddPlaneReference(component2);
        AddMate(mate);
    }

    /// <summary>
    /// Appends one Screw mate between two cylindrical-face components to the
    /// matching clean base (ModelHeader display name "螺旋1"). The revolution value
    /// is patched into the measured D1 parameter.
    /// </summary>
    public void AddScrewMate(string component1, string component2, double revolutionsPerUnit)
    {
        ValidatePositiveParameter(revolutionsPerUnit, nameof(revolutionsPerUnit));
        ValidateComponentSelections(component1, component2, "螺旋1");

        MatesList.StreamVersion = 0x00BF;
        var mate = new ScrewMateModel
        {
            DisplayName = "螺旋1",
            ScrewEntity1 = component1,
            ScrewEntity2 = component2,
            RevolutionVal = revolutionsPerUnit
        };
        mate.AddPlaneReference(component1);
        mate.AddPlaneReference(component2);
        AddMate(mate);
    }

    /// <summary>
    /// Appends one UniversalJoint mate between two cylindrical-face components to
    /// the matching clean base (ModelHeader display name "万向节1"). The specimen's
    /// measured values are reused; no numeric parameter is written.
    /// </summary>
    public void AddUniversalJointMate(string component1, string component2)
    {
        ValidateComponentSelections(component1, component2, "万向节1");

        MatesList.StreamVersion = 0x00BF;
        var mate = new UniversalJointMateModel
        {
            DisplayName = "万向节1",
            Shaft1Entity = component1,
            Shaft2Entity = component2
        };
        AddMate(mate);
    }

    /// <summary>
    /// Appends one LimitDistance mate between two RefPlane entities to the matching
    /// clean base (ModelHeader display name "LimitDistance1"). The distance is in
    /// meters, patched into the measured D1 parameter.
    /// </summary>
    public void AddLimitDistanceMate(string entity1, string entity2, double distance)
    {
        if (double.IsNaN(distance) || double.IsInfinity(distance) || distance <= 0 || distance > 1000)
            throw new ArgumentOutOfRangeException(nameof(distance), "Distance must be a finite value in (0, 1000] meters.");
        ValidatePlaneSelections(entity1, entity2);

        var mate = new StandardMateModel("moMateLimitDistanceDim_c", AssemblyMateType.LimitDistance)
        {
            DisplayName = "LimitDistance1",
            TypeId = 5u,
            Alignment = AssemblyMateAlignment.Aligned,
            Distance = distance
        };
        mate.AddPlaneReference(entity1);
        mate.AddPlaneReference(entity2);
        AddMate(mate);
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
        ValidatePlaneSelections(entity1, entity2);

        var mate = new StandardMateModel("moMateLimitPlanarAngleDim_c", AssemblyMateType.LimitAngle)
        {
            DisplayName = "LimitAngle1",
            TypeId = 6u,
            Alignment = AssemblyMateAlignment.Aligned,
            Angle = angleRadians
        };
        mate.AddPlaneReference(entity1);
        mate.AddPlaneReference(entity2);
        AddMate(mate);
    }



    /// <summary>
    /// Builds a structured hierarchical and flat Bill of Materials using the specified options and path resolver.
    /// </summary>
    public SldFile.Assembly.BomResult BuildBom(SldFile.Assembly.BomOptions? options = null) => SldFile.Assembly.BomTransformer.Transform(this, options);

    /// <summary>
    /// Adds a strongly-typed mate to the assembly using structured MFC serialization.
    /// </summary>
    internal void AddMate(IMfcMateModel mate)
    {
        if (mate == null) throw new ArgumentNullException(nameof(mate));
        if (IsReadOnly)
            throw new InvalidOperationException("The assembly is opened in read-only mode.");

        MatesList.Mates.Add(mate);
        _mates = null;

        string streamPath = FindMatesListStreamPath();
        byte[] serialized = SldFile.Mfc.MfcArchive.Serialize(MatesList);
        SetPendingStream(streamPath, serialized);
    }

    /// <summary>
    /// Adds a strongly-typed mate to the assembly using structured MFC serialization.
    /// </summary>
    public void AddMate(AssemblyMateDefinitionData definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        var mate = SldFile.Mfc.MfcMateRegistry.CreateForDefinition(definition);
        AddMate(mate);
    }

    internal string FindMatesListStreamPath()
    {
        return RawStreams.Keys.FirstOrDefault(p =>
            p.EndsWith("-MatesList", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("Contents/Config-0-MatesList", StringComparison.OrdinalIgnoreCase))
            ?? "Contents/Config-0-MatesList";
    }

    /// <summary>
    /// Adds a Width mate constraining a tab between two width faces.
    /// </summary>
    public void AddWidthMate(
        string widthFace1, string widthFace2,
        string tabFace1, string tabFace2,
        AssemblyMateWidthConstraint constraintType = AssemblyMateWidthConstraint.Centered,
        double distance = 0.0,
        bool flipDimension = false)
    {
        var def = new WidthMateDefinitionData
        {
            DisplayName = "Width" + (MatesList.Mates.Count + 1),
            ConstraintType = constraintType,
            DistanceFromEnd = distance,
            FlipDimension = flipDimension
        };
        def.EntitiesToMate.Add(widthFace1);
        def.EntitiesToMate.Add(widthFace2);
        def.EntitiesToMate.Add(tabFace1);
        def.EntitiesToMate.Add(tabFace2);
        AddMate(def);
    }

    /// <summary>
    /// Adds a ProfileCenter mate between two planar faces.
    /// </summary>
    public void AddProfileCenterMate(
        string entity1, string entity2,
        double offsetDistance = 0.0,
        bool flipDimension = false,
        bool lockRotation = false)
    {
        var def = new ProfileCenterMateDefinitionData
        {
            DisplayName = "ProfileCenter" + (MatesList.Mates.Count + 1),
            OffsetDistance = offsetDistance,
            FlipDimension = flipDimension,
            LockRotation = lockRotation
        };
        def.EntitiesToMate.Add(entity1);
        def.EntitiesToMate.Add(entity2);
        AddMate(def);
    }

    /// <summary>
    /// Adds a CamFollower mate between a follower entity and cam profile faces.
    /// </summary>
    public void AddCamFollowerMate(
        string followerEntity,
        IEnumerable<string> camFaces,
        bool isTangent = true)
    {
        var def = new CamFollowerMateDefinitionData
        {
            DisplayName = "CamFollower" + (MatesList.Mates.Count + 1),
            IsTangent = isTangent,
            FollowerEntity = followerEntity
        };
        if (camFaces != null)
        {
            foreach (var face in camFaces)
                def.CamProfileFaces.Add(face);
        }
        AddMate(def);
    }

    private static void ValidatePositiveParameter(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            throw new ArgumentOutOfRangeException(parameterName, "The parameter must be a finite positive value.");
    }

    private void ValidateCleanBase(ushort? expectedVersion = null)
    {
        string streamPath = FindMatesListStreamPath();
        byte[]? stream = GetStreamBytes(streamPath);
        if (stream == null || stream.Length != 6)
            throw new NotSupportedException("Only a clean assembly without mates is supported.");
        ushort ver = BitConverter.ToUInt16(stream, 0);
        if (expectedVersion.HasValue)
        {
            if (ver != expectedVersion.Value)
                throw new NotSupportedException($"Unsupported MatesList stream version 0x{ver:X4}.");
        }
        else if (ver != 0x00AB && ver != 0x00BF)
        {
            throw new NotSupportedException($"Unsupported MatesList stream version 0x{ver:X4}.");
        }
        if (BitConverter.ToUInt16(stream, 2) != 0)
            throw new NotSupportedException("Unsupported MatesList header layout.");
        if (BitConverter.ToUInt16(stream, 4) != 0)
            throw new NotSupportedException("The assembly already contains mate records.");

        MatesList.StreamVersion = ver;
    }

    private void ValidatePlaneSelections(string entity1, string entity2)
    {
        ValidateCleanBase();
        var p1 = ParseEntity(entity1, nameof(entity1));
        var p2 = ParseEntity(entity2, nameof(entity2));
        if (string.Equals(p1.Component, p2.Component, StringComparison.Ordinal))
            throw new NotSupportedException("The two RefPlane entities must belong to different components.");
        if (!string.Equals(p1.Assembly, p2.Assembly, StringComparison.Ordinal))
            throw new NotSupportedException("The two RefPlane entities must belong to the same assembly instance.");
        if (string.Equals(p1.Feature, p2.Feature, StringComparison.Ordinal) &&
            string.Equals(p1.Component, p2.Component, StringComparison.Ordinal))
            throw new NotSupportedException("The two entity selection names must differ.");

        var headerStrings = ReadModelHeaderStrings();
        if (!headerStrings.Contains(p1.Component) || !headerStrings.Contains(p2.Component))
            throw new NotSupportedException("A selected component instance is not present in the target assembly tree.");
        if (!headerStrings.Contains(p1.Assembly))
            throw new NotSupportedException("The selection names do not match the target assembly instance name.");
    }

    private HashSet<string> ReadModelHeaderStrings()
    {
        var key = RawStreams.Keys.FirstOrDefault(k =>
            k.EndsWith("Config-0-ModelHeader", StringComparison.OrdinalIgnoreCase));
        if (key == null)
            throw new NotSupportedException("The target assembly has no Config-0-ModelHeader stream.");
        byte[] data = GetStreamBytes(key) ?? Array.Empty<byte>();
        return new HashSet<string>(
            MfcPrimitives.ScanWCharFields(data, 0, data.Length).Select(static f => f.Value),
            StringComparer.Ordinal);
    }

    private static (string Feature, string Component, string Assembly) ParseEntity(string entity, string paramName)
    {
        if (string.IsNullOrWhiteSpace(entity))
            throw new ArgumentException("A RefPlane selection name is required.", paramName);

        var parts = entity.Split('@');
        if (parts.Length != 3)
            throw new NotSupportedException($"Only RefPlane selection names of the form feature@component@assembly are supported (was '{entity}').");
        if (string.IsNullOrWhiteSpace(parts[0]) ||
            string.IsNullOrWhiteSpace(parts[1]) ||
            string.IsNullOrWhiteSpace(parts[2]))
            throw new NotSupportedException($"The selection '{entity}' contains empty component/feature parts.");

        return (parts[0], parts[1], parts[2]);
    }

    private void ValidateComponentSelections(string component1, string component2, string? modelHeaderMarker)
    {
        ValidateCleanBase();
        var headerStrings = ReadModelHeaderStrings();
        if (!string.IsNullOrEmpty(modelHeaderMarker) && !headerStrings.Contains(modelHeaderMarker!))
            throw new NotSupportedException($"The base does not carry the matching ModelHeader template (missing '{modelHeaderMarker}').");

        var p1 = ParseComponentInstance(component1, nameof(component1));
        var p2 = ParseComponentInstance(component2, nameof(component2));
        if (string.Equals(p1.Component, p2.Component, StringComparison.Ordinal))
            throw new NotSupportedException("The two entities must belong to different components.");
        if (!string.Equals(p1.Assembly, p2.Assembly, StringComparison.Ordinal))
            throw new NotSupportedException("The two entities must belong to the same assembly instance.");
        if (!headerStrings.Contains(p1.Component) || !headerStrings.Contains(p2.Component))
            throw new NotSupportedException("A selected component instance is not present in the target assembly tree.");
        if (!headerStrings.Contains(p1.Assembly))
            throw new NotSupportedException("The selection names do not match the target assembly instance name.");
    }

    private static (string Component, string Assembly) ParseComponentInstance(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A component instance name is required.", paramName);
        var parts = value.Split('@');
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
            throw new NotSupportedException($"Only component instance names of the form component@assembly are supported (was '{value}').");
        return (parts[0], parts[1]);
    }
}
