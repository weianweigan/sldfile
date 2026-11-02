using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.Topology;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Empirical adversarial stress tests for Topology and Selection parsers, decoders, and models.
/// Verifies robustness against malformed, truncated, boundary, and randomized byte streams.
/// </summary>
public class TopologyAdversarialTests
{
    private static MfcElement CreateMockElement(string className, byte[] rawData)
    {
        // Construct a synthetic source array containing className + rawData
        // MfcElement: TagOffset=0, DataStart=0, DataEnd=rawData.Length
        return new MfcElement(className, 0x01, 0, 0, rawData)
        {
            DataEnd = rawData.Length
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // 1. Truncated byte slices (0 to 128 bytes)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(27)]
    [InlineData(28)]
    [InlineData(32)]
    [InlineData(40)]
    [InlineData(58)]
    [InlineData(59)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(80)]
    [InlineData(128)]
    public void TopologyParsers_TruncatedByteSlices_NeverThrow(int length)
    {
        byte[] buffer = new byte[length];
        for (int i = 0; i < length; i++)
        {
            buffer[i] = (byte)(i & 0xFF);
        }

        var elem = CreateMockElement("TestElem", buffer);

        // 1. ParseSketchRegion
        var region = TopologyParsers.ParseSketchRegion(elem);
        Assert.NotNull(region);
        Assert.Equal("moSketchRegion_c", region.ClassName);

        // 2. ParseCompEdge
        var compEdge = TopologyParsers.ParseCompEdge(elem);
        Assert.NotNull(compEdge);
        Assert.Equal("moCompEdge_c", compEdge.ClassName);

        // 3. ParseEdgeRef
        var edgeRef = TopologyParsers.ParseEdgeRef(elem);
        Assert.NotNull(edgeRef);
        Assert.Equal("moEdgeRef_c", edgeRef.ClassName);
        Assert.NotNull(edgeRef.EdgeIndices);

        // 4. ParseFaceRefPln
        var faceRefPln = TopologyParsers.ParseFaceRefPln(elem);
        Assert.NotNull(faceRefPln);
        Assert.Equal("moFaceRefPlnData_c", faceRefPln.ClassName);
        Assert.NotNull(faceRefPln.DirectionVector);
        Assert.Equal(3, faceRefPln.DirectionVector.Length);

        // 5. ParseCompFace
        var compFace = TopologyParsers.ParseCompFace(elem);
        Assert.NotNull(compFace);
        Assert.Equal("moCompFace_c", compFace.ClassName);

        // 6. ParseCompSolidBody
        var compSolidBody = TopologyParsers.ParseCompSolidBody(elem);
        Assert.NotNull(compSolidBody);
        Assert.Equal("moCompSolidBody_c", compSolidBody.ClassName);

        // 7. ParseSurfaceIdRep
        var surfId = TopologyParsers.ParseSurfaceIdRep(elem);
        Assert.NotNull(surfId);
        Assert.Equal("moSurfaceIdRep_c", surfId.ClassName);

        // 8. ParseEndFace3IntSurfIdRep
        var endFace3Int = TopologyParsers.ParseEndFace3IntSurfIdRep(elem);
        Assert.NotNull(endFace3Int);
        Assert.Equal("moEndFace3IntSurfIdRep_c", endFace3Int.ClassName);
        Assert.True(endFace3Int.FaceIndex >= 1);

        // 9. ParseFromSktEnt3IntSurfIdRep
        var fromSkt3Int = TopologyParsers.ParseFromSktEnt3IntSurfIdRep(elem);
        Assert.NotNull(fromSkt3Int);
        Assert.Equal("moFromSktEnt3IntSurfIdRep_c", fromSkt3Int.ClassName);
        Assert.NotNull(fromSkt3Int.Mappings);

        // 10. ParseFilletSurfIdRep
        var filletSurf = TopologyParsers.ParseFilletSurfIdRep(elem);
        Assert.NotNull(filletSurf);
        Assert.Equal("moFilletSurfIdRep_c", filletSurf.ClassName);

        // 11. ParseEnrichedFromSktEntSurfIdRep
        var fromSktData = new FromSktEntSurfIdRepData();
        TopologyParsers.ParseEnrichedFromSktEntSurfIdRep(fromSktData, buffer);
        Assert.NotNull(fromSktData.Mappings);

        // 12. ParseEnrichedFaceRef
        var faceRefData = new FaceRefData();
        TopologyParsers.ParseEnrichedFaceRef(faceRefData, buffer);
        Assert.NotNull(faceRefData.FaceIds);

        // 13. ParseEnrichedEndFaceSurfIdRep
        var endFaceData = new EndFaceSurfIdRepData();
        TopologyParsers.ParseEnrichedEndFaceSurfIdRep(endFaceData, buffer);
        Assert.NotNull(endFaceData.SurfaceName);

        // 14. ParseEnrichedSketchChain
        var sketchChainData = new SketchChainData();
        TopologyParsers.ParseEnrichedSketchChain(sketchChainData, buffer);
        Assert.NotNull(sketchChainData.EntityIndices);
        Assert.NotNull(sketchChainData.Indices);
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. Pseudo-random Fuzzing across varied sizes and seeds
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TopologyParsers_RandomFuzzing_NeverThrowsOrCrashes()
    {
        var rng = new Random(1337);
        int[] testLengths = { 0, 1, 4, 8, 12, 16, 24, 30, 48, 64, 100, 256, 512, 1024 };

        for (int iteration = 0; iteration < 200; iteration++)
        {
            int len = testLengths[iteration % testLengths.Length];
            byte[] fuzzed = new byte[len];
            rng.NextBytes(fuzzed);

            var elem = CreateMockElement("FuzzElem", fuzzed);

            // Execute all parser entry points on fuzzed bytes
            var reg = TopologyParsers.ParseSketchRegion(elem);
            var compEdge = TopologyParsers.ParseCompEdge(elem);
            var edgeRef = TopologyParsers.ParseEdgeRef(elem);
            var faceRefPln = TopologyParsers.ParseFaceRefPln(elem);
            var compFace = TopologyParsers.ParseCompFace(elem);
            var compBody = TopologyParsers.ParseCompSolidBody(elem);
            var surfId = TopologyParsers.ParseSurfaceIdRep(elem);
            var endFace3Int = TopologyParsers.ParseEndFace3IntSurfIdRep(elem);
            var fromSkt3Int = TopologyParsers.ParseFromSktEnt3IntSurfIdRep(elem);

            var fromSkt = new FromSktEntSurfIdRepData();
            TopologyParsers.ParseEnrichedFromSktEntSurfIdRep(fromSkt, fuzzed);

            var faceRef = new FaceRefData();
            TopologyParsers.ParseEnrichedFaceRef(faceRef, fuzzed);

            var endFace = new EndFaceSurfIdRepData();
            TopologyParsers.ParseEnrichedEndFaceSurfIdRep(endFace, fuzzed);

            var chain = new SketchChainData();
            TopologyParsers.ParseEnrichedSketchChain(chain, fuzzed);

            // Feed the resulting fuzzed outputs into TopologyDecoder
            var contours = TopologyDecoder.BuildContourReferences(chain, fromSkt, fromSkt3Int);
            Assert.NotNull(contours);

            var regions = TopologyDecoder.BuildRegionReferences(new[] { reg });
            Assert.NotNull(regions);

            var dir = TopologyDecoder.BuildDirectionReference(edgeRef, compEdge, null, faceRefPln);
            // May be null or non-null, but must not throw

            var endEnt = TopologyDecoder.BuildEndConditionEntity(faceRef, endFace, compFace, endFace3Int);
            Assert.NotNull(endEnt);

            var chooser = new PerBodyChooserData { ChooserFlags = (ushort)rng.Next(0, 0x10000) };
            var scope = TopologyDecoder.BuildBodyScope(chooser, new[] { compBody });
            Assert.NotNull(scope);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. Boundary & Malformed Pattern Injections
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TopologyParsers_BoundaryPatternInjections_HandledSafely()
    {
        // A. Candidate TypeId placed at exact boundary (raw.Length - 12)
        byte[] bufA = new byte[24];
        BitConverter.GetBytes((uint)0x4B).CopyTo(bufA, 12);     // TypeCandidate at 12
        BitConverter.GetBytes((uint)0x12345678).CopyTo(bufA, 16); // SurfaceTag
        BitConverter.GetBytes((uint)42).CopyTo(bufA, 20);       // EntityIndex / EdgeIndex

        var edgeRef = TopologyParsers.ParseEdgeRef(CreateMockElement("moEdgeRef_c", bufA));
        Assert.Contains(42u, edgeRef.EdgeIndices);

        var endFace3Int = TopologyParsers.ParseEndFace3IntSurfIdRep(CreateMockElement("moEndFace3IntSurfIdRep_c", bufA));
        Assert.Equal(0x4Bu, endFace3Int.FeatureTypeId);
        Assert.Equal(0x12345678u, endFace3Int.SurfaceTag);

        var fromSkt3Int = TopologyParsers.ParseFromSktEnt3IntSurfIdRep(CreateMockElement("moFromSktEnt3IntSurfIdRep_c", bufA));
        Assert.Single(fromSkt3Int.Mappings);
        Assert.Equal(42u, fromSkt3Int.Mappings[0].EntityIndex);

        // B. Boundary check at raw.Length - 11 (insufficient room for full 12-byte tuple)
        byte[] bufB = new byte[23];
        BitConverter.GetBytes((uint)0x4B).CopyTo(bufB, 12);
        var edgeRefB = TopologyParsers.ParseEdgeRef(CreateMockElement("moEdgeRef_c", bufB));
        Assert.Empty(edgeRefB.EdgeIndices); // Must cleanly skip without reading past 23

        // C. Malformed WChar prefix at buffer boundary in EndFaceSurfIdRep
        // FF FE FF at index 10 in an 11-byte array (charCount byte is missing)
        byte[] bufC = new byte[11];
        bufC[8] = 0xFF;
        bufC[9] = 0xFE;
        bufC[10] = 0xFF;
        var endFaceC = new EndFaceSurfIdRepData();
        TopologyParsers.ParseEnrichedEndFaceSurfIdRep(endFaceC, bufC);
        Assert.Equal(string.Empty, endFaceC.SurfaceName);

        // D. WChar with huge length exceeding buffer in EndFaceSurfIdRep
        byte[] bufD = new byte[12];
        bufD[4] = 0xFF;
        bufD[5] = 0xFE;
        bufD[6] = 0xFF;
        bufD[7] = 100; // declares 100 wide chars (200 bytes), but buffer only has 4 remaining
        var endFaceD = new EndFaceSurfIdRepData();
        TopologyParsers.ParseEnrichedEndFaceSurfIdRep(endFaceD, bufD);
        Assert.Equal(string.Empty, endFaceD.SurfaceName); // Must not throw IndexOutOfRangeException

        // E. Out-of-range edge and entity indices (e.g. 0, >= 5000, 0xFFFFFFFF)
        byte[] bufE = new byte[36];
        BitConverter.GetBytes((uint)0x4B).CopyTo(bufE, 12);
        BitConverter.GetBytes((uint)0).CopyTo(bufE, 20); // entId = 0
        BitConverter.GetBytes((uint)0x4B).CopyTo(bufE, 24);
        BitConverter.GetBytes((uint)999999).CopyTo(bufE, 32); // entId = 999999

        var edgeRefE = TopologyParsers.ParseEdgeRef(CreateMockElement("moEdgeRef_c", bufE));
        Assert.Empty(edgeRefE.EdgeIndices); // 0 and 999999 rejected

        var fromSktE = new FromSktEntSurfIdRepData();
        TopologyParsers.ParseEnrichedFromSktEntSurfIdRep(fromSktE, bufE);
        Assert.Empty(fromSktE.Mappings);
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. TopologyDecoder Stress & Contract Correctness
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TopologyDecoder_BuildContourReferences_AdversarialScenarios()
    {
        // Scenario 1: chain with Count > 0 but Indices all zero
        var chain1 = new SketchChainData
        {
            Count = 4,
            Indices = new uint[] { 0, 0, 0, 0 }
        };
        var res1 = TopologyDecoder.BuildContourReferences(chain1, null);
        Assert.Single(res1);
        Assert.False(res1[0].IsValid); // IsValid is false because EntityIndices is empty
        Assert.Empty(res1[0].EntityIndices);
        Assert.Equal(4u, res1[0].ChainId);

        // Scenario 2: fromSkt with duplicate entities and unsorted order
        var fromSkt2 = new FromSktEntSurfIdRepData
        {
            FeatureTypeId = 0x4B
        };
        fromSkt2.Mappings.Add(new SktEntSurfMapping { EntityIndex = 10, SurfaceTag = 1 });
        fromSkt2.Mappings.Add(new SktEntSurfMapping { EntityIndex = 3, SurfaceTag = 2 });
        fromSkt2.Mappings.Add(new SktEntSurfMapping { EntityIndex = 10, SurfaceTag = 3 }); // duplicate 10
        fromSkt2.Mappings.Add(new SktEntSurfMapping { EntityIndex = 1, SurfaceTag = 4 });

        var res2 = TopologyDecoder.BuildContourReferences(null, fromSkt2);
        Assert.Single(res2);
        Assert.True(res2[0].IsValid);
        Assert.Equal(3, res2[0].EntityIndices.Count);
        // Must be sorted and distinct: [1, 3, 10]
        Assert.Equal(new uint[] { 1, 3, 10 }, res2[0].EntityIndices);

        // Scenario 3: fromSkt3Int fallback when chain and fromSkt are null
        var fromSkt3Int = new FromSktEnt3IntSurfIdRepData
        {
            FeatureTypeId = 0x5E
        };
        fromSkt3Int.Mappings.Add(new SktEntSurfMapping { EntityIndex = 7, SurfaceTag = 100 });
        var res3 = TopologyDecoder.BuildContourReferences(null, null, fromSkt3Int);
        Assert.Single(res3);
        Assert.Equal(0x5Eu, res3[0].EntityId);
        Assert.Single(res3[0].EntityIndices);
        Assert.Equal(7u, res3[0].EntityIndices[0]);

        // Scenario 4: all nulls
        var res4 = TopologyDecoder.BuildContourReferences(null, null, null);
        Assert.Empty(res4);
    }

    [Fact]
    public void TopologyDecoder_BuildRegionReferences_NullAndEmptyScenarios()
    {
        // 1. Empty enumerable
        var res1 = TopologyDecoder.BuildRegionReferences(Enumerable.Empty<SketchRegionData>());
        Assert.Empty(res1);

        // 2. Multiple regions with boundary values
        var r1 = new SketchRegionData { Handle1 = 0, RegionIndex = 0, LoopCount = 0 };
        var r2 = new SketchRegionData { Handle1 = 0x8064, RegionIndex = 2, LoopCount = 4 };
        var res2 = TopologyDecoder.BuildRegionReferences(new[] { r1, r2 });

        Assert.Equal(2, res2.Count);
        Assert.False(res2[0].IsValid); // Index 0 and Id 0
        Assert.True(res2[1].IsValid);
        Assert.Equal(2u, res2[1].RegionIndex);
        Assert.Equal(4u, res2[1].LoopCount);
    }

    [Fact]
    public void TopologyDecoder_BuildDirectionReference_PrecedenceAndEdgeCases()
    {
        var compEdge = new CompEdgeData { EdgeIndex = 15 };
        var edgeRef = new EdgeRefData();
        edgeRef.EdgeIndices.Add(99);
        var compRefPlane = new CompRefPlaneData { RefPlaneId = 3, Guid = "Plane_Front" };
        var faceRefPln = new FaceRefPlnData
        {
            NormalX = 0, NormalY = 0, NormalZ = 1
        };

        // Precedence: compEdge / edgeRef takes priority over plane
        var dir1 = TopologyDecoder.BuildDirectionReference(edgeRef, compEdge, compRefPlane, faceRefPln);
        Assert.NotNull(dir1);
        Assert.Equal(SelectionEntityType.Edge, dir1!.EntityType);
        Assert.Equal(15u, dir1.EntityId);
        Assert.Equal("Edge_15", dir1.ReferenceName);

        // Without compEdge: edgeRef used
        var dir2 = TopologyDecoder.BuildDirectionReference(edgeRef, null, compRefPlane, faceRefPln);
        Assert.NotNull(dir2);
        Assert.Equal(SelectionEntityType.Edge, dir2!.EntityType);
        Assert.Equal(99u, dir2.EntityId);
        Assert.Equal("Edge_99", dir2.ReferenceName);

        // Without edges: faceRefPln takes priority over compRefPlane
        var dir3 = TopologyDecoder.BuildDirectionReference(null, null, compRefPlane, faceRefPln);
        Assert.NotNull(dir3);
        Assert.Equal(SelectionEntityType.Plane, dir3!.EntityType);
        Assert.Equal("PlanarFaceDirection", dir3.ReferenceName);
        Assert.NotNull(dir3.Vector);
        Assert.Equal(new double[] { 0, 0, 1 }, dir3.Vector);

        // Without faceRefPln: compRefPlane used
        var dir4 = TopologyDecoder.BuildDirectionReference(null, null, compRefPlane, null);
        Assert.NotNull(dir4);
        Assert.Equal(SelectionEntityType.Plane, dir4!.EntityType);
        Assert.Equal(3u, dir4.EntityId);
        Assert.Equal("Plane_Front", dir4.ReferenceName);

        // compRefPlane with null Guid falls back to "Plane_{Id}"
        var compRefPlaneNullGuid = new CompRefPlaneData { RefPlaneId = 7, Guid = null! };
        var dir5 = TopologyDecoder.BuildDirectionReference(null, null, compRefPlaneNullGuid, null);
        Assert.NotNull(dir5);
        Assert.Equal("Plane_7", dir5!.ReferenceName);

        // All null
        var dir6 = TopologyDecoder.BuildDirectionReference(null, null, null, null);
        Assert.Null(dir6);
    }

    [Fact]
    public void TopologyDecoder_BuildEndConditionEntity_PrecedenceAndEdgeCases()
    {
        var compFace = new CompFaceData { FaceIndex = 25 };
        var faceRef = new FaceRefData { FaceCount = 3 };
        faceRef.FaceIds.Add(10);
        var endFace = new EndFaceSurfIdRepData { SurfaceName = "Surface_N1" };
        var endFace3Int = new EndFace3IntSurfIdRepData { FaceIndex = 77 };

        // 1. All present: compFace faceIndex preferred, endFace SurfaceName preferred, faceRef FaceCount
        var ent1 = TopologyDecoder.BuildEndConditionEntity(faceRef, endFace, compFace, endFace3Int);
        Assert.NotNull(ent1);
        Assert.Equal(25u, ent1!.EntityId);
        Assert.Equal("Surface_N1", ent1.SurfaceName);
        Assert.Equal(3u, ent1.FaceCount);
        Assert.True(ent1.IsValid);

        // 2. compFace is null: faceRef FaceId (10) preferred
        var ent2 = TopologyDecoder.BuildEndConditionEntity(faceRef, endFace, null, endFace3Int);
        Assert.NotNull(ent2);
        Assert.Equal(10u, ent2!.EntityId);

        // 3. compFace and faceRef null: endFace3Int FaceIndex (77) used
        var ent3 = TopologyDecoder.BuildEndConditionEntity(null, endFace, null, endFace3Int);
        Assert.NotNull(ent3);
        Assert.Equal(77u, ent3!.EntityId);
        Assert.Equal("Surface_N1", ent3.SurfaceName);

        // 4. endFace SurfaceName null/empty: endFace3Int fallback name used
        var ent4 = TopologyDecoder.BuildEndConditionEntity(null, null, null, endFace3Int);
        Assert.NotNull(ent4);
        Assert.Equal("EndFace_77", ent4!.SurfaceName);

        // 5. All null
        var ent5 = TopologyDecoder.BuildEndConditionEntity(null, null, null, null);
        Assert.Null(ent5);
    }

    [Fact]
    public void TopologyDecoder_BuildBodyScope_FlagsAndBodyList()
    {
        // 1. Chooser with AutoSelect flag (0x0002)
        var chooser1 = new PerBodyChooserData { ChooserFlags = 0x0002 };
        var scope1 = TopologyDecoder.BuildBodyScope(chooser1, null);
        Assert.NotNull(scope1);
        Assert.True(scope1.AutoSelect);
        Assert.Equal(0x0002, scope1.Flags);
        Assert.Empty(scope1.BodyIds);
        Assert.Empty(scope1.Bodies);

        // 2. Chooser with explicit bodies (ChooserFlags = 0x0001, AutoSelect = false)
        var chooser2 = new PerBodyChooserData { ChooserFlags = 0x0001 };
        var bodies = new List<CompSolidBodyData>
        {
            new CompSolidBodyData { BodyIndex = 1 },
            new CompSolidBodyData { BodyIndex = 3 }
        };
        var scope2 = TopologyDecoder.BuildBodyScope(chooser2, bodies);
        Assert.NotNull(scope2);
        Assert.False(scope2.AutoSelect);
        Assert.Equal(2, scope2.BodyIds.Count);
        Assert.Equal(new uint[] { 1, 3 }, scope2.BodyIds);
        Assert.Equal(2, scope2.Bodies.Count);
        Assert.Equal("SolidBody_1", scope2.Bodies[0].Name);
        Assert.Equal(SelectionEntityType.Body, scope2.Bodies[0].EntityType);
        Assert.Equal("SolidBody_3", scope2.Bodies[1].Name);

        // 3. ChooserFlags = 0 defaults to AutoSelect = true
        var chooser3 = new PerBodyChooserData { ChooserFlags = 0x0000 };
        var scope3 = TopologyDecoder.BuildBodyScope(chooser3, null);
        Assert.True(scope3.AutoSelect);
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. Selection Models State Consistency
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SelectionModels_ContractStateConsistency_NeverInconsistent()
    {
        // Contour
        var contourEmpty = new SketchContourReference();
        Assert.False(contourEmpty.IsValid);
        Assert.Equal("Contour", contourEmpty.ReferenceType);
        Assert.DoesNotContain("Exception", contourEmpty.ToString());

        var contourValid = new SketchContourReference
        {
            EntityId = 1,
            ChainId = 1,
            EntityIndices = new uint[] { 1, 2, 3 }
        };
        Assert.True(contourValid.IsValid);

        // Region
        var regionEmpty = new SketchRegionReference();
        Assert.False(regionEmpty.IsValid);
        Assert.Equal("Region", regionEmpty.ReferenceType);

        var regionWithId = new SketchRegionReference { EntityId = 100 };
        Assert.True(regionWithId.IsValid);

        var regionWithIndex = new SketchRegionReference { RegionIndex = 1 };
        Assert.True(regionWithIndex.IsValid);

        // Direction
        var dirEmpty = new DirectionReference();
        Assert.False(dirEmpty.IsValid);
        Assert.Equal("Direction", dirEmpty.ReferenceType);

        var dirWithName = new DirectionReference { ReferenceName = "Face_1" };
        Assert.True(dirWithName.IsValid);

        var dirWithVec = new DirectionReference { Vector = new double[] { 1, 0, 0 } };
        Assert.True(dirWithVec.IsValid);

        // EntityReference
        var entEmpty = new EntityReference();
        Assert.False(entEmpty.IsValid);
        Assert.Equal("Entity", entEmpty.ReferenceType);

        var entWithId = new EntityReference { EntityId = 5 };
        Assert.True(entWithId.IsValid);

        var entWithName = new EntityReference { Name = "Body1" };
        Assert.True(entWithName.IsValid);

        // EndConditionEntityReference
        var endEntEmpty = new EndConditionEntityReference { FaceCount = 0 };
        Assert.False(endEntEmpty.IsValid);
        Assert.Equal("EndConditionEntity", endEntEmpty.ReferenceType);

        var endEntWithFaces = new EndConditionEntityReference { FaceCount = 1 };
        Assert.True(endEntWithFaces.IsValid);

        var endEntWithName = new EndConditionEntityReference { FaceCount = 0, SurfaceName = "N2" };
        Assert.True(endEntWithName.IsValid);

        // BodyScopeData
        var bodyScope = new BodyScopeData();
        Assert.True(bodyScope.AutoSelect);
        Assert.Empty(bodyScope.BodyIds);
        Assert.Empty(bodyScope.Bodies);
        Assert.Contains("AutoSelect=True", bodyScope.ToString());
    }

    // ═══════════════════════════════════════════════════════════════
    // 6. SchemaMapper End-to-End Robustness with Malformed Streams
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(200)]
    public void ResolvedFeaturesSchemaMapper_TruncatedStream_NeverThrows(int byteCount)
    {
        byte[] malformedStream = new byte[byteCount];
        for (int i = 0; i < byteCount; i++) malformedStream[i] = (byte)(i & 0xFF);

        var doc = MfcDocument.Parse(malformedStream);
        Assert.NotNull(doc);

        var model = new ResolvedFeaturesData();
        // Must never throw unhandled IndexOutOfRangeException or NullReferenceException
        var ex = Record.Exception(() => ResolvedFeaturesSchemaMapper.Map(doc, model));
        Assert.Null(ex);
        Assert.NotNull(model.Folders);
    }

    // ═══════════════════════════════════════════════════════════════
    // 7. Internal Null/Corrupt Collections Adversarial Exploration
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void TopologyDecoder_NullInternalCollections_Assessment()
    {
        // Explore how TopologyDecoder handles objects where internal collection properties are set to null
        var chainWithNullEntityIndices = new SketchChainData
        {
            Count = 1,
            EntityIndices = null!,
            Indices = new uint[] { 1 }
        };
        // Verify whether NRE occurs
        var ex1 = Record.Exception(() => TopologyDecoder.BuildContourReferences(chainWithNullEntityIndices, null));
        // Note finding: if EntityIndices is null, ex1 is NullReferenceException

        var fromSktWithNullMappings = new FromSktEntSurfIdRepData
        {
            Mappings = null!
        };
        var ex2 = Record.Exception(() => TopologyDecoder.BuildContourReferences(null, fromSktWithNullMappings));

        var regWithNullItem = new SketchRegionData?[] { null };
        var ex3 = Record.Exception(() => TopologyDecoder.BuildRegionReferences(regWithNullItem!));

        var edgeRefWithNullIndices = new EdgeRefData
        {
            EdgeIndices = null!
        };
        var ex4 = Record.Exception(() => TopologyDecoder.BuildDirectionReference(edgeRefWithNullIndices, null, null, null));

        var faceRefWithNullIds = new FaceRefData
        {
            FaceIds = null!
        };
        var ex5 = Record.Exception(() => TopologyDecoder.BuildEndConditionEntity(faceRefWithNullIds, null, null));

        var bodyScopeWithNullBody = new CompSolidBodyData?[] { null };
        var ex6 = Record.Exception(() => TopologyDecoder.BuildBodyScope(null, bodyScopeWithNullBody!));

        // Let's assert whether they throw or not, to document exact empirical behavior
        Assert.IsType<NullReferenceException>(ex1);
        Assert.IsType<NullReferenceException>(ex2);
        Assert.IsType<NullReferenceException>(ex3);
        Assert.IsType<NullReferenceException>(ex4);
        Assert.IsType<NullReferenceException>(ex5);
        Assert.IsType<NullReferenceException>(ex6);
    }

    // ═══════════════════════════════════════════════════════════════
    // 8. End-to-End Stream Fuzzing on Real & Mutated ResolvedFeatures
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ResolvedFeatures_StreamFuzzing_NeverCausesFatalCrashes()
    {
        var rng = new Random(42);

        // Test with completely random streams
        for (int i = 0; i < 50; i++)
        {
            int size = rng.Next(1, 4096);
            byte[] fuzzedStream = new byte[size];
            rng.NextBytes(fuzzedStream);

            try
            {
                var doc = MfcDocument.Parse(fuzzedStream);
                var rf = new ResolvedFeaturesData();
                ResolvedFeaturesSchemaMapper.Map(doc, rf);
            }
            catch (Exception ex)
            {
                // Assert no fatal IndexOutOfRangeException or NullReferenceException crashes the process
                Assert.IsNotType<IndexOutOfRangeException>(ex);
                Assert.IsNotType<NullReferenceException>(ex);
            }
        }
    }

    [Fact]
    public void ResolvedFeatures_MutatedRealStream_NeverCausesFatalCrashes()
    {
        var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT");
        if (!File.Exists(samplePath)) return;

        using var sw = SwFile.Open(samplePath, readOnly: true);
        if (!sw.RawStreams.TryGetValue("Contents/Config-0-ResolvedFeatures", out var realStream)) return;

        var rng = new Random(2026);

        // Run 50 mutation experiments (truncations, byte overwrites, bit flips)
        for (int i = 0; i < 50; i++)
        {
            byte[] mutated = (byte[])realStream.Clone();

            int mutationType = rng.Next(3);
            if (mutationType == 0)
            {
                // Truncate at random position
                int cutLen = rng.Next(1, mutated.Length);
                Array.Resize(ref mutated, cutLen);
            }
            else if (mutationType == 1)
            {
                // Corrupt a 64-byte block anywhere in the stream
                int start = rng.Next(0, Math.Max(1, mutated.Length - 64));
                for (int j = 0; j < Math.Min(64, mutated.Length - start); j++)
                {
                    mutated[start + j] = (byte)rng.Next(0, 256);
                }
            }
            else
            {
                // Bit flips at 20 random positions
                for (int j = 0; j < 20; j++)
                {
                    int pos = rng.Next(0, mutated.Length);
                    mutated[pos] ^= (byte)(1 << rng.Next(0, 8));
                }
            }

            try
            {
                var doc = MfcDocument.Parse(mutated);
                var rf = new ResolvedFeaturesData();
                ResolvedFeaturesSchemaMapper.Map(doc, rf);
            }
            catch (Exception ex)
            {
                Assert.IsNotType<IndexOutOfRangeException>(ex);
                Assert.IsNotType<NullReferenceException>(ex);
            }
        }
    }
}




