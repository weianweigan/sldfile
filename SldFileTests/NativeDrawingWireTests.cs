using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class NativeDrawingWireTests
{
    private sealed class AnnotationHeaderBoundary : Exception { }
    private sealed class AnnotationHeaderPrefix : IMfcSerializable
    {
        internal bool IncludesItem;
        internal bool IncludesDisplay;
        internal DrNoteMfcModel Display = new DrNoteMfcModel { NativeDisplay = new DrNoteMfcModel.NativeDisplayState() };
        private NativeDrawingAnnotationItemModel _item = new NativeDrawingAnnotationItemModel();
        internal NativeDrawingAnnotationItemModel Item => IncludesDisplay ? Display.NativeDisplay!.Item : _item;
        internal NativeDrawingAnnotationHeaderModel Header => Item.Header;
        public void Serialize(MfcArchive ar)
        {
            if (IncludesDisplay) Display.Serialize(ar); else if (IncludesItem) Item.Serialize(ar); else Header.Serialize(ar);
            // Deliberate fixture prefix at a header/base/full-note boundary.
            // Stop both directions before the parent continues; retain the
            // actual parent counts and graph reference identities.
            throw new AnnotationHeaderBoundary();
        }
    }

    [Theory]
    [InlineData("Solidworks2012", 5000, 9330, false, false)]
    [InlineData("Solidworks2026", 19000, 10879, false, false)]
    [InlineData("Solidworks2012", 5000, 9606, true, false)]
    [InlineData("Solidworks2026", 19000, 11337, true, false)]
    [InlineData("Solidworks2012", 5000, 9744, true, true)]
    [InlineData("Solidworks2026", 19000, 11515, true, true)]
    public void DefinitionAnnotationPrefixes_DecodeABCDFromTypedLogicalTextAndCharacterObjects(
        string directory, int version, int prefixEnd, bool includesItem, bool includesDisplay)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, "cube.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        var model = new PlanePrefixRoot(); var sheet = new SheetPlanePrefix { IncludesProfile = true, IncludesDetail = true };
        var note = new AnnotationHeaderPrefix { IncludesItem = includesItem, IncludesDisplay = includesDisplay };
        NativeDrawingObjectGraph Graph(PlanePrefixRoot root, SheetPlanePrefix firstSheet, AnnotationHeaderPrefix firstNote) =>
            NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" ? root : n == "moDrSheet" ? firstSheet : n == "moDisplayNote_c" ? firstNote : null);
        void Context(MfcArchive ar)
        {
            ar.DocumentVersion = version; ar.Context.NativeSketchIncludesOwnerProfile = false;
            ar.Context.NativeAnnotationReferenceIsExternal = false;
        }
        using var input = MfcArchive.CreateReader(bytes); Context(input); input.Position = 36;
        NativeDrawingObjectGraph.Node? rootNode = null;
        Assert.Throws<AnnotationHeaderBoundary>(() => Graph(model, sheet, note).Object(input, ref rootNode));
        Assert.Equal(prefixEnd, input.Position); Assert.True(input.Remaining > 0);
        if (includesItem)
        {
            Assert.Equal(0, note.Item.HasInline288);
            Assert.IsType<NativeDrawingAttachPointDataModel>(Assert.Single(note.Item.Objects298.Values)!.Payload);
        }
        if (includesDisplay)
        {
            Assert.Null(note.Display.RawData);
            Assert.Equal(version == 5000 ? 0 : 2, note.Display.NativeDisplay!.FlagWords.Length);
            var favorite = Assert.IsType<SldFile.Binary.FavoriteHandleData>(note.Display.NativeDisplay.Object680!.Payload);
            Assert.Empty(favorite.RawData);
        }
        Assert.Same(sheet.Detail, note.Header.Owner);
        var component = Assert.IsType<DrNoteMfcModel>(note.Header.Component!.Payload);
        Assert.Null(component.RawData);
        var data = Assert.IsType<DrNoteMfcModel>(component.NativeComponent!.Item!.Payload);
        Assert.Null(data.RawData);
        Assert.IsType<NativeDrawingAttachPointModel>(Assert.Single(data.NativeData!.Attachments)!.Payload);
        var text = Assert.IsType<NativeDrawingDimTextModel>(data.NativeData.Text!.Payload);
        Assert.True(text.Generic); Assert.Equal("ABCD", text.Text); Assert.Same(note, text.DisplayItem!.Payload);
        var list = Assert.IsType<NativeDrawingGenericTextListModel>(text.GenericData!.Payload);
        var objects = Assert.IsType<NativeDrawingObjectListModel>(list.Objects!.Payload);
        Assert.IsType<NativeDrawingTextLineHeadModel>(objects.Values[0]!.Payload);
        var characters = objects.Values.Select(v => v!.Payload).OfType<NativeDrawingTextCharacterModel>().ToArray();
        Assert.Equal("ABCD", new string(characters.Select(c => (char)c.Character).ToArray()));
        using var output = MfcArchive.CreateWriter(); Context(output);
        Assert.Throws<AnnotationHeaderBoundary>(() => NativeDrawingModelFactory.CreateGraph().Object(output, ref rootNode));
        Assert.Equal(bytes.AsSpan(36, prefixEnd - 36).ToArray(), output.ToArray());

        text.Text = "ZBCD"; characters[0].Character = 'Z';
        using var edited = MfcArchive.CreateWriter(); Context(edited);
        Assert.Throws<AnnotationHeaderBoundary>(() => NativeDrawingModelFactory.CreateGraph().Object(edited, ref rootNode));
        using var reload = MfcArchive.CreateReader(edited.ToArray()); Context(reload);
        var changed = new PlanePrefixRoot(); var changedSheet = new SheetPlanePrefix { IncludesProfile = true, IncludesDetail = true };
        var changedNote = new AnnotationHeaderPrefix { IncludesItem = includesItem, IncludesDisplay = includesDisplay }; NativeDrawingObjectGraph.Node? changedRoot = null;
        Assert.Throws<AnnotationHeaderBoundary>(() => Graph(changed, changedSheet, changedNote).Object(reload, ref changedRoot));
        Assert.Equal(0, reload.Remaining);
        var changedData = Assert.IsType<DrNoteMfcModel>(Assert.IsType<DrNoteMfcModel>(changedNote.Header.Component!.Payload).NativeComponent!.Item!.Payload);
        var changedText = Assert.IsType<NativeDrawingDimTextModel>(changedData.NativeData!.Text!.Payload);
        Assert.Equal("ZBCD", changedText.Text);
        var changedObjects = Assert.IsType<NativeDrawingObjectListModel>(Assert.IsType<NativeDrawingGenericTextListModel>(changedText.GenericData!.Payload).Objects!.Payload);
        Assert.Equal('Z', (char)Assert.IsType<NativeDrawingTextCharacterModel>(changedObjects.Values[1]!.Payload).Character);
    }

    [Theory]
    [InlineData("offset.SLDPRT", 7711)]
    [InlineData("slot.SLDPRT", 8483)]
    [InlineData("rectangle.SLDPRT", 7835)]
    [InlineData("ellipse.SLDPRT", 7402)]
    [InlineData("spline.SLDPRT", 7406)]
    [InlineData("text.SLDPRT", 58872)]
    public void NativeOrdinarySketch_ConsumesAndRebuildsTheContinuousPayload(string name, int end)
    {
        // Parent archive state recorded at native sgSketch::Serialize entry.
        // For offset, native entry/return also independently measured 1580
        // bytes. Other ends are codec/roundtrip assertions, not native traces.
        using var file = SwFile.Open(TestFileProvider.Resolve("Sketches2026", name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Config-0-ResolvedFeatures")!;
        NativeDrawingObjectGraph Graph()
        {
            var graph = NativeDrawingModelFactory.CreateGraph(initialIndex: 189);
            graph.MapExistingClass(4, "moNodeName_c", 1); graph.MapExistingClass(98, "suObList", 1);
            graph.MapExistingClass(173, "sgPointHandle", 1); return graph;
        }
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000;
        input.Position = 6131; input.Context.NativeDrawingObjects = Graph();
        var sketch = new SldFile.Binary.SketchData(); sketch.SerializeNativeOrdinary(input, false, false);
        Assert.Equal(end, input.Position); Assert.Null(input.Context.NativeSketchOwnerIs3D);
        Assert.Empty(sketch.NativeExternalBlocks); Assert.Empty(sketch.NativeSilhouettes);
        Assert.Empty(sketch.BlockManager.Blocks); Assert.Empty(sketch.BlockManager.States);
        Assert.Empty(sketch.BlockManager.Handles); Assert.Empty(sketch.LoopManager.Slots);
        if (name == "offset.SLDPRT" || name == "slot.SLDPRT")
        {
            var grouped = Assert.IsType<NativeDrawingObjectListModel>(sketch.NativeGroupedObjects!.Payload);
            var region = Assert.IsType<MfcSketchRegionModel>(Assert.Single(grouped.Values)!.Payload);
            Assert.True(region.NativeWire); Assert.Null(region.RawData);
            Assert.IsType<NativeDrawingObjectListModel>(region.NativeBoundary!.Payload);
        }
        if (name == "slot.SLDPRT")
        {
            var entry = Assert.Single(sketch.SlotManager.Slots); Assert.Equal(0, entry.Index);
            Assert.Equal("sgSlot_c", entry.Object!.ClassName);
            var slot = Assert.IsType<SldFile.Binary.SketchData.NativeSlot>(entry.Object.Payload);
            Assert.Equal(new[] { "sgLineHandle", "sgPointHandle", "sgLineHandle", "sgLineHandle", "sgArcHandle", "sgArcHandle" },
                slot.Handles.Select(h => h!.ClassName).ToArray());
        }
        else Assert.Empty(sketch.SlotManager.Slots);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 19000;
        output.Context.NativeDrawingObjects = Graph(); sketch.SerializeNativeOrdinary(output, false, false);
        Assert.Null(output.Context.NativeSketchOwnerIs3D);
        Assert.Equal(bytes.AsSpan(6131, end - 6131).ToArray(), output.ToArray());
    }

    [Theory]
    [InlineData("Solidworks2012", 5000, 7780, 226, 8004, true)]
    [InlineData("Solidworks2026", 19000, 24650, 280, 32274, false)]
    public void NativeDrawingSketchDisplayGroups_FollowTheVersionedArchiveOrder(
        string directory, int version, int start, int nextIndex, int end, bool entities)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, "cube.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        NativeDrawingObjectGraph Graph()
        {
            var graph = NativeDrawingModelFactory.CreateGraph(initialIndex: (uint)nextIndex);
            graph.MapExistingClass(8, "suObList", 1);
            if (version != 5000) graph.MapExistingClass(164, "moNodeName_c", 1);
            return graph;
        }
        var sketch = new SldFile.Binary.SketchData();
        void Serialize(MfcArchive ar)
        {
            if (entities) sketch.SerializeNativeEntityTables(ar, false);
            sketch.SerializeNativeRelations(ar); sketch.SerializeNativePostRelationTables(ar);
            sketch.SerializeNativeSettingsPrefix(ar); sketch.SerializeNativeVisualAndTools(ar);
            sketch.SerializeNativeDisplayGroups(ar, false);
        }
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = version; input.Position = start;
        input.Context.NativeDrawingObjects = Graph(); Serialize(input); Assert.Equal(end, input.Position);
        Assert.Empty(sketch.NativeExternalBlocks); Assert.Null(sketch.NativeGroupedObjects);
        Assert.All(sketch.DisplayGroups.Groups, group => Assert.Empty(group));
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        output.Context.NativeDrawingObjects = Graph(); Serialize(output);
        Assert.Equal(bytes.AsSpan(start, end - start).ToArray(), output.ToArray());
    }

    [Theory]
    [InlineData(0x1C4E, false, 4)]
    [InlineData(0x1C4E, true, 16)]
    [InlineData(0x1C4F, false, 16)]
    [InlineData(0x1C54, false, 20)]
    public void NativeDisplayGroupVersionGates_DoNotConsumeTheFollowingField(int version, bool is3D, int sectionBytes)
    {
        // Empty external-block table followed by three/four map counts.
        byte[] bytes = new byte[sectionBytes + 4]; BitConverter.GetBytes(0x12345678).CopyTo(bytes, sectionBytes);
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = version;
        var sketch = new SldFile.Binary.SketchData(); sketch.SerializeNativeDisplayGroups(input, is3D);
        Assert.Equal(sectionBytes, input.Position); int sentinel = 0; input.I32(ref sentinel); Assert.Equal(0x12345678, sentinel);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        sketch.SerializeNativeDisplayGroups(output, is3D); Assert.Equal(bytes.AsSpan(0, sectionBytes).ToArray(), output.ToArray());
    }

    [Theory]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 7780, 226, 7932, 8000, true)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 24650, 280, 32182, 32252, false)]
    [InlineData("Sketches2026", "offset.SLDPRT", 19000, 6131, 189, 7549, 7619, true)]
    public void NativeSketchVisualAndManager_PreserveTheContinuousVersionedWireSection(
        string directory, string name, int version, int start, int nextIndex, int suffixStart, int end, bool entities)
    {
        bool drawing = directory != "Sketches2026";
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes(drawing ? "Contents/Definition" : "Contents/Config-0-ResolvedFeatures")!;
        NativeDrawingObjectGraph Graph()
        {
            var graph = NativeDrawingModelFactory.CreateGraph(initialIndex: (uint)nextIndex);
            graph.MapExistingClass(drawing ? 8u : 98u, "suObList", 1);
            if (!drawing) { graph.MapExistingClass(4, "moNodeName_c", 1); graph.MapExistingClass(173, "sgPointHandle", 1); }
            else if (version != 5000) graph.MapExistingClass(164, "moNodeName_c", 1);
            return graph;
        }
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = version; input.Position = start;
        input.Context.NativeDrawingObjects = Graph(); var sketch = new SldFile.Binary.SketchData();
        if (entities) sketch.SerializeNativeEntityTables(input, false);
        sketch.SerializeNativeRelations(input); sketch.SerializeNativePostRelationTables(input); sketch.SerializeNativeSettingsPrefix(input);
        Assert.Equal(suffixStart, input.Position); sketch.SerializeNativeVisualAndTools(input); Assert.Equal(end, input.Position);
        Assert.True(input.Remaining > 0); Assert.Equal(string.Empty, sketch.LineVisual.Style);
        Assert.Equal(-1, sketch.LineVisual.Width.Type); Assert.Equal(-1f, sketch.LineVisual.Width.Width);
        Assert.Equal(-1, sketch.LineVisual.Color); Assert.Equal(1, sketch.LineVisual.State14);
        Assert.Empty(sketch.NativeToolObjects); Assert.Empty(sketch.RelationManager.Objects); Assert.Empty(sketch.RelationManager.Entries);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version; output.Context.NativeDrawingObjects = Graph();
        if (entities) sketch.SerializeNativeEntityTables(output, false);
        sketch.SerializeNativeRelations(output); sketch.SerializeNativePostRelationTables(output); sketch.SerializeNativeSettingsPrefix(output);
        sketch.SerializeNativeVisualAndTools(output); Assert.Equal(bytes.AsSpan(start, end - start).ToArray(), output.ToArray());
    }

    [Fact]
    public void NativeRelationManager_UsesCStringDoubleMapConfirmedByNativeControlledReadback()
    {
        // Controlled ignored copy: native 0x4C629FE0 consumes 50 bytes and
        // returns one actual map entry with this exact key and double value.
        // This is a wire-injection vector, not native-generated geometry.
        byte[] bytes = Convert.FromHexString("00000100fffeff11"
            + "6e00610074006900760065002d0077006900720065002d00700072006f0062006500"
            + "000000000000c03f");
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000;
        input.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph();
        var manager = new SldFile.Binary.SketchData.NativeRelationManager(); manager.Serialize(input);
        Assert.Equal(50, input.Position); Assert.Equal(0, input.Remaining); Assert.Empty(manager.Objects);
        var entry = Assert.Single(manager.Entries); Assert.Equal("native-wire-probe", entry.Key); Assert.Equal(0.125, entry.Value);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 19000;
        output.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph();
        manager.Serialize(output); Assert.Equal(bytes, output.ToArray());
    }

    [Theory]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 7780, 226, 7868, 7932, true)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 24650, 280, 32114, 32182, false)]
    [InlineData("Sketches2026", "offset.SLDPRT", 19000, 6131, 189, 7481, 7549, true)]
    public void NativeSketchSettings_RespectTheVersionedFieldAndNativeTrailingFieldOrder(
        string directory, string name, int version, int start, int nextIndex, int settingsStart, int end, bool entities)
    {
        bool drawing = directory != "Sketches2026";
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes(drawing ? "Contents/Definition" : "Contents/Config-0-ResolvedFeatures")!;
        NativeDrawingObjectGraph Graph()
        {
            var graph = NativeDrawingModelFactory.CreateGraph(initialIndex: (uint)nextIndex);
            graph.MapExistingClass(drawing ? 8u : 98u, "suObList", 1);
            if (!drawing) { graph.MapExistingClass(4, "moNodeName_c", 1); graph.MapExistingClass(173, "sgPointHandle", 1); }
            else if (version != 5000) graph.MapExistingClass(164, "moNodeName_c", 1);
            return graph;
        }
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = version; input.Position = start;
        input.Context.NativeDrawingObjects = Graph(); var sketch = new SldFile.Binary.SketchData();
        if (entities) sketch.SerializeNativeEntityTables(input, false);
        sketch.SerializeNativeRelations(input); sketch.SerializeNativePostRelationTables(input);
        Assert.Equal(settingsStart, input.Position); sketch.SerializeNativeSettingsPrefix(input);
        Assert.Equal(end, input.Position); Assert.True(input.Remaining > 0); Assert.Empty(sketch.NativeIntersections);
        Assert.Equal(version == 5000 ? 56 : 60, end - settingsStart - 8);
        Assert.Equal(0, sketch.Settings.State40); Assert.Equal(1, sketch.Settings.State3C);
        using (var output = MfcArchive.CreateWriter())
        {
            output.DocumentVersion = version; output.Context.NativeDrawingObjects = Graph();
            if (entities) sketch.SerializeNativeEntityTables(output, false);
            sketch.SerializeNativeRelations(output); sketch.SerializeNativePostRelationTables(output); sketch.SerializeNativeSettingsPrefix(output);
            Assert.Equal(bytes.AsSpan(start, end - start).ToArray(), output.ToArray());
        }
        sketch.Settings.State40 = 23; sketch.Settings.State3C = 17;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = version; sketch.SerializeNativeSettingsPrefix(edited);
        byte[] editedBytes = edited.ToArray();
        Assert.Equal(23, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(editedBytes.AsSpan(editedBytes.Length - 8)));
        Assert.Equal(17, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(editedBytes.AsSpan(editedBytes.Length - 4)));
        using var reload = MfcArchive.CreateReader(editedBytes); reload.DocumentVersion = version;
        var changed = new SldFile.Binary.SketchData(); changed.SerializeNativeSettingsPrefix(reload);
        Assert.Equal(0, reload.Remaining); Assert.Equal(23, changed.Settings.State40); Assert.Equal(17, changed.Settings.State3C);
        Assert.Equal(sketch.NativeState130, changed.NativeState130);
    }

    [Fact]
    public void NativeIntersection_NonzeroState68_StopsAtItsNativeConfirmedShortBoundary()
    {
        // Controlled native experiment inserts State68=1 into the existing
        // offset fixture's empty intersection table. SOLIDWORKS reads exactly
        // two bytes and reopens that ignored task copy without errors/warnings.
        byte[] bytes = Convert.FromHexString("010078563412");
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000;
        var model = new SldFile.Binary.SketchData.NativeIntersection(); model.Serialize(input);
        Assert.Equal(1, model.State68); Assert.Equal(2, input.Position);
        int sentinel = 0; input.I32(ref sentinel); Assert.Equal(0x12345678, sentinel);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 19000; model.Serialize(output);
        Assert.Equal(bytes.AsSpan(0, 2).ToArray(), output.ToArray());
    }

    [Fact]
    public void NativeSketchOffset_DecodesItsNonemptyProfileLoopAndEdge_WithoutOpaqueBytes()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Sketches2026", "offset.SLDPRT"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Config-0-ResolvedFeatures")!;
        NativeDrawingObjectGraph Graph()
        {
            var graph = SketchFixtureGraph(); graph.MapExistingClass(98, "suObList", 1);
            // Native ReadClass on this exact part confirms moNodeName_c=4.
            graph.MapExistingClass(4, "moNodeName_c", 1); return graph;
        }
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000; input.Position = 6131;
        input.Context.NativeDrawingObjects = Graph(); var sketch = new SldFile.Binary.SketchData();
        sketch.SerializeNativeEntityTables(input, false); sketch.SerializeNativeRelations(input);
        Assert.Equal(7283, input.Position); sketch.SerializeNativeOffsetAndStyleTables(input);
        Assert.Equal(7479, input.Position); Assert.True(input.Remaining > 0);
        Assert.Empty(sketch.NativeOffsets); Assert.Empty(sketch.NativeStyledCurves);
        var offset = Assert.Single(sketch.NativeSketchOffsets); Assert.Equal(new[] { 0 }, offset.EntityIndices);
        Assert.Equal(0.003, offset.Distance, 12); Assert.Equal(2, offset.State90);
        var profile = Assert.IsType<NativeSketchOffsetProfileModel>(offset.ProfileData!.Payload);
        var loop = Assert.IsType<NativeSketchOffsetLoopModel>(Assert.Single(profile.Loops)!.Payload);
        var edge = Assert.IsType<NativeSketchOffsetEdgeModel>(Assert.Single(loop.Edges)!.Payload);
        Assert.Equal(0, loop.State8); Assert.Equal(0, loop.StateC); Assert.Equal(0x6E0, edge.State8); Assert.Null(edge.Objects);
        Assert.Equal(0x241, profile.State28); Assert.Equal(offset.Distance, profile.Distance);
        using (var output = MfcArchive.CreateWriter())
        {
            output.DocumentVersion = 19000; output.Context.NativeDrawingObjects = Graph();
            sketch.SerializeNativeEntityTables(output, false); sketch.SerializeNativeRelations(output); sketch.SerializeNativeOffsetAndStyleTables(output);
            Assert.Equal(bytes.AsSpan(6131, 7479 - 6131).ToArray(), output.ToArray());
        }
        // The independent native sgSketchOffset_c entry/return captures 190
        // bytes, between the outer sketch-offset and styled-curve counts.
        using (var payload = MfcArchive.CreateReader(bytes))
        {
            payload.DocumentVersion = 19000; payload.Position = 7287;
            payload.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph();
            var isolated = new SldFile.Binary.SketchData.NativeSketchOffset(); isolated.Serialize(payload);
            Assert.Equal(7477, payload.Position); Assert.Equal(offset.Distance, isolated.Distance);
            using var rebuilt = MfcArchive.CreateWriter(); rebuilt.DocumentVersion = 19000;
            rebuilt.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph(); isolated.Serialize(rebuilt);
            Assert.Equal(bytes.AsSpan(7287, 190).ToArray(), rebuilt.ToArray());
        }
        offset.Distance += 0.001; profile.Distance += 0.001;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = 19000;
        edited.Context.NativeDrawingObjects = Graph(); sketch.SerializeNativeEntityTables(edited, false);
        sketch.SerializeNativeRelations(edited); sketch.SerializeNativeOffsetAndStyleTables(edited);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = 19000;
        reload.Context.NativeDrawingObjects = Graph(); var changed = new SldFile.Binary.SketchData();
        changed.SerializeNativeEntityTables(reload, false); changed.SerializeNativeRelations(reload); changed.SerializeNativeOffsetAndStyleTables(reload);
        Assert.Equal(0, reload.Remaining); var changedOffset = Assert.Single(changed.NativeSketchOffsets);
        Assert.Equal(offset.Distance, changedOffset.Distance);
        Assert.Equal(profile.Distance, Assert.IsType<NativeSketchOffsetProfileModel>(changedOffset.ProfileData!.Payload).Distance);
        Assert.Equal(offset.EntityIndices, changedOffset.EntityIndices); Assert.Equal(offset.State90, changedOffset.State90);
    }

    [Theory]
    [InlineData("rectangle.SLDPRT", 7561, 4)]
    [InlineData("slot.SLDPRT", 8039, 1)]
    [InlineData("ellipse.SLDPRT", 7202, 0)]
    [InlineData("spline.SLDPRT", 7206, 0)]
    [InlineData("text.SLDPRT", 58672, 0)]
    public void NativeSketchEntitiesAndAllRelationTables_RoundtripTheContinuousSection(string name, int end, int g1Count)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Sketches2026", name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Config-0-ResolvedFeatures")!;
        NativeDrawingObjectGraph Graph()
        {
            var graph = SketchFixtureGraph(); graph.MapExistingClass(98, "suObList", 1); return graph;
        }
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000; input.Position = 6131;
        input.Context.NativeDrawingObjects = Graph(); var sketch = new SldFile.Binary.SketchData();
        sketch.SerializeNativeEntityTables(input, false); sketch.SerializeNativeRelations(input);
        Assert.Equal(end, input.Position); Assert.True(input.Remaining > 0);
        Assert.Equal(g1Count, sketch.NativeLogicalG1Relations.Length); Assert.Empty(sketch.NativeLogicalG2Relations);
        Assert.All(sketch.NativeValueRelations, table => Assert.Empty(table));
        Assert.Empty(Assert.IsType<NativeDrawingObjectListModel>(sketch.NativeRelationObjects!.Payload).Values);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 19000;
        output.Context.NativeDrawingObjects = Graph(); sketch.SerializeNativeEntityTables(output, false); sketch.SerializeNativeRelations(output);
        Assert.Equal(bytes.AsSpan(6131, end - 6131).ToArray(), output.ToArray());
    }

    [Fact]
    public void NativeDrawingRelations_TraverseAllTables_WithNonemptyValueAndLogicalDimensions()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Solidworks2026", "cube.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        NativeDrawingObjectGraph Graph()
        {
            var graph = NativeDrawingModelFactory.CreateGraph(initialIndex: 280);
            // suObList=8 is independently established by the decoded drawing
            // header. moNodeName=164 is also verified by native ReadClass.
            graph.MapExistingClass(8, "suObList", 1); graph.MapExistingClass(164, "moNodeName_c", 1); return graph;
        }
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000; input.Position = 24650;
        input.Context.NativeDrawingObjects = Graph(); var model = new SldFile.Binary.SketchData();
        model.SerializeNativeRelations(input); Assert.Equal(32106, input.Position); Assert.True(input.Remaining > 0);
        Assert.Equal(new[] { 0, 2, 0, 7 }, model.NativeValueRelations.Select(table => table.Length));
        Assert.All(model.NativeValueRelations[1], node => Assert.IsType<SldFile.Binary.PntPntDistData>(node!.Payload));
        Assert.All(model.NativeValueRelations[3], node => Assert.IsType<NativeSketchOffsetDimensionModel>(node!.Payload));
        Assert.Equal(10, model.NativeLogicalG1Relations.Length); Assert.Equal(53, model.NativeLogicalG2Relations.Length);
        Assert.Empty(model.NativeLogicalG3Relations); Assert.Empty(model.NativeIsoRelations);
        Assert.Empty(model.NativeLinearPatterns); Assert.Empty(model.NativeCircularPatterns);
        Assert.Empty(model.NativeDerivedA8); Assert.Empty(model.NativeDerivedB0);
        Assert.Empty(model.NativeEntityRelations); Assert.Empty(model.NativeGroupRelations); Assert.Empty(model.NativeLogicalGroups);
        Assert.Empty(Assert.IsType<NativeDrawingObjectListModel>(model.NativeRelationObjects!.Payload).Values);
        using (var output = MfcArchive.CreateWriter())
        {
            output.DocumentVersion = 19000; output.Context.NativeDrawingObjects = Graph(); model.SerializeNativeRelations(output);
            Assert.Equal(bytes.AsSpan(24650, 7456).ToArray(), output.ToArray());
        }
        var offset = Assert.IsType<NativeSketchOffsetDimensionModel>(model.NativeValueRelations[3][0]!.Payload);
        offset.State130 = 123;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = 19000;
        edited.Context.NativeDrawingObjects = Graph(); model.SerializeNativeRelations(edited);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = 19000;
        reload.Context.NativeDrawingObjects = Graph(); var changed = new SldFile.Binary.SketchData();
        changed.SerializeNativeRelations(reload); Assert.Equal(0, reload.Remaining);
        var changedOffset = Assert.IsType<NativeSketchOffsetDimensionModel>(changed.NativeValueRelations[3][0]!.Payload);
        Assert.Equal(123, changedOffset.State130); Assert.Equal(offset.State134, changedOffset.State134);
        Assert.Equal(53, changed.NativeLogicalG2Relations.Length);
    }

    [Fact]
    public void NativePointDistanceObject_ReusesExistingMfcType_WithItsEntirePayload()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Solidworks2026", "cube.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        NativeDrawingObjectGraph Graph()
        {
            // Root class 280/object 281, parameter class 282/object 283,
            // node-name object 284, point-handle class 285. Only the prior
            // node-name class is mapped; every object is actually decoded.
            var graph = NativeDrawingModelFactory.CreateGraph(initialIndex: 280);
            graph.MapExistingClass(164, "moNodeName_c", 1); return graph;
        }
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000; input.Position = 24654;
        NativeDrawingObjectGraph.Node? root = null; Graph().Object(input, ref root);
        Assert.Equal("sgPntPntDist", root!.ClassName);
        // Native runtime class/vtable verifies sgPntPntDist uses 0x4C5C8120,
        // whose entry/return consumes 180 bytes after this 18-byte class tag.
        Assert.Equal(24852, input.Position);
        var model = Assert.IsType<SldFile.Binary.PntPntDistData>(root.Payload); Assert.Empty(model.RawData);
        var row = model.Native; Assert.Equal(0, row.HasTransform);
        Assert.Equal(2, Assert.IsType<SldFile.Binary.PointHandleData>(row.First!.Payload).EntityIndex);
        Assert.Equal(1, Assert.IsType<SldFile.Binary.PointHandleData>(row.Second!.Payload).EntityIndex);
        Assert.Null(row.Plane); Assert.Equal(1, row.State180); Assert.Equal(1, row.State184);
        Assert.Equal(0.841, Assert.IsType<MoLengthParameterModel>(row.Dimension.Dimension.Parameter!.Payload).Value);
        using (var output = MfcArchive.CreateWriter())
        {
            output.DocumentVersion = 19000; Graph().Object(output, ref root);
            Assert.Equal(bytes.AsSpan(24654, 198).ToArray(), output.ToArray());
        }
        var parameter = (MoLengthParameterModel)row.Dimension.Dimension.Parameter!.Payload;
        parameter.Value += 0.001;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = 19000; Graph().Object(edited, ref root);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = 19000;
        NativeDrawingObjectGraph.Node? changed = null; Graph().Object(reload, ref changed); Assert.Equal(0, reload.Remaining);
        var changedRow = Assert.IsType<SldFile.Binary.PntPntDistData>(changed!.Payload).Native;
        Assert.Equal(parameter.Value, Assert.IsType<MoLengthParameterModel>(changedRow.Dimension.Dimension.Parameter!.Payload).Value);
        Assert.Equal(row.State1B8, changedRow.State1B8); Assert.Equal(row.Direction, changedRow.Direction);
    }

    [Fact]
    public void NativeDimensionParameter_ReusesExistingLengthModel_AndPreservesItsNativeNodeFields()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Solidworks2026", "cube.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        NativeDrawingObjectGraph Graph()
        {
            // The first parameter declares its class at 282; its node name
            // references the previously declared moNodeName_c class at 164.
            var graph = NativeDrawingModelFactory.CreateGraph(initialIndex: 282);
            graph.MapExistingClass(164, "moNodeName_c", 1); return graph;
        }
        var dimension = new SldFile.Binary.SketchData.NativeDimension();
        using (var input = MfcArchive.CreateReader(bytes))
        {
            input.DocumentVersion = 19000; input.Position = 24672;
            input.Context.NativeDrawingObjects = Graph(); dimension.Serialize(input);
            // Native Dimension_c entry/return independently captured 93 bytes.
            Assert.Equal(24765, input.Position);
        }
        var parameter = Assert.IsType<MoLengthParameterModel>(dimension.Parameter!.Payload);
        Assert.True(parameter.NativeWire); Assert.Equal("D1", parameter.Name); Assert.Equal(0.841, parameter.Value);
        Assert.Equal(0, parameter.NativeHasOverride); Assert.Null(parameter.NativeParameterName);
        Assert.Equal(18, dimension.State50); Assert.Equal(string.Empty, parameter.NativeNode.State20);
        using (var output = MfcArchive.CreateWriter())
        {
            output.DocumentVersion = 19000; output.Context.NativeDrawingObjects = Graph(); dimension.Serialize(output);
            Assert.Equal(bytes.AsSpan(24672, 93).ToArray(), output.ToArray());
        }
        parameter.Value += 0.001; parameter.Name = "D2";
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = 19000;
        edited.Context.NativeDrawingObjects = Graph(); dimension.Serialize(edited);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = 19000;
        reload.Context.NativeDrawingObjects = Graph(); var changed = new SldFile.Binary.SketchData.NativeDimension();
        changed.Serialize(reload); Assert.Equal(0, reload.Remaining);
        var changedParameter = Assert.IsType<MoLengthParameterModel>(changed.Parameter!.Payload);
        Assert.Equal(parameter.Value, changedParameter.Value); Assert.Equal("D2", changedParameter.Name);
        Assert.Equal(parameter.NativeNode.State28, changedParameter.NativeNode.State28);
        Assert.Equal(parameter.NativeNode.State2C, changedParameter.NativeNode.State2C);
        Assert.Equal(parameter.NativeNode.State20, changedParameter.NativeNode.State20);
        Assert.Equal(dimension.State50, changed.State50);
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 11739, 66, 0)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 7780, 66, 0)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 9093, 66, 0)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, 9217, 137, 1)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, 10333, 137, 1)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, 9883, 137, 1)]
    [InlineData("Drawings", "MacroBenchmark/Mold/bezel moldbase.slddrw", 19000, 14767, 66, 0)]
    public void NativeDrawingEmptyEntityTables_CanStillHaveLogicalRelations(
        string directory, string name, int version, int start, int length, int g1Count)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = version; input.Position = start;
        input.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph();
        var sketch = new SldFile.Binary.SketchData(); sketch.SerializeNativeEntityTables(input, false);
        Assert.Equal(start + 52, input.Position); sketch.SerializeNativeRelationPrefix(input);
        Assert.Equal(start + length, input.Position); Assert.True(input.Remaining > 0);
        Assert.All(sketch.NativeValueRelations, table => Assert.Empty(table));
        Assert.Equal(g1Count, sketch.NativeLogicalG1Relations.Length); Assert.Empty(sketch.NativeLogicalG2Relations);
        Assert.Empty(sketch.NativeLogicalG3Relations);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        output.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph();
        sketch.SerializeNativeEntityTables(output, false); sketch.SerializeNativeRelationPrefix(output);
        Assert.Equal(bytes.AsSpan(start, length).ToArray(), output.ToArray());
    }

    [Fact]
    public void NativeLogicalRelations_ReuseExistingHandles_AndPreserveTheDimensionBase()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Solidworks2026", "cube.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        // Native Serialize entry/return captures independently delimit these
        // nonempty payloads (71 / 111 bytes). The first G1 declares sgLineHandle;
        // G2 references that class at index 315. No earlier objects are invented.
        var g1 = new SldFile.Binary.SketchData.NativeLogicalG1();
        using (var input = MfcArchive.CreateReader(bytes))
        {
            input.DocumentVersion = 19000; input.Position = 25633;
            input.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph(initialIndex: 315);
            g1.Serialize(input); Assert.Equal(25704, input.Position);
        }
        Assert.Equal(5, g1.Dimension.State80); Assert.Null(g1.Dimension.Dimension.Parameter);
        Assert.Null(g1.Dimension.Dimension.Tolerance); Assert.Null(g1.Dimension.Dimension.Object60);
        Assert.Equal(59, Assert.IsType<SldFile.Binary.LineHandleData>(g1.Entity!.Payload).EntityIndex);
        using (var output = MfcArchive.CreateWriter())
        {
            output.DocumentVersion = 19000;
            output.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph(initialIndex: 315);
            g1.Serialize(output); Assert.Equal(bytes.AsSpan(25633, 71).ToArray(), output.ToArray());
        }
        NativeDrawingObjectGraph G2Graph()
        {
            var graph = NativeDrawingModelFactory.CreateGraph(initialIndex: 317);
            graph.MapExistingClass(315, "sgLineHandle", 1); return graph;
        }
        var g2 = new SldFile.Binary.SketchData.NativeLogicalG2();
        using (var input = MfcArchive.CreateReader(bytes))
        {
            input.DocumentVersion = 19000; input.Position = 26201;
            input.Context.NativeDrawingObjects = G2Graph(); g2.Serialize(input); Assert.Equal(26312, input.Position);
        }
        Assert.Equal(27, g2.Dimension.State80); Assert.Equal(-1, g2.State154);
        Assert.Equal(78, Assert.IsType<SldFile.Binary.LineHandleData>(g2.First!.Payload).EntityIndex);
        Assert.Equal(77, Assert.IsType<SldFile.Binary.LineHandleData>(g2.Second!.Payload).EntityIndex);
        Assert.Null(g2.Objects); Assert.Null(g2.Third); Assert.Null(g2.Fourth); Assert.Null(g2.Fifth);
        using (var output = MfcArchive.CreateWriter())
        {
            output.DocumentVersion = 19000; output.Context.NativeDrawingObjects = G2Graph(); g2.Serialize(output);
            Assert.Equal(bytes.AsSpan(26201, 111).ToArray(), output.ToArray());
        }
        // Native loading normalizes this u16 to a bool; wire serialization
        // must retain its actual value rather than silently change the bytes.
        g2.Dimension.State94 = 2; g2.State118 = 0.125;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = 19000;
        edited.Context.NativeDrawingObjects = G2Graph(); g2.Serialize(edited);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = 19000;
        reload.Context.NativeDrawingObjects = G2Graph(); var changed = new SldFile.Binary.SketchData.NativeLogicalG2();
        changed.Serialize(reload); Assert.Equal(0, reload.Remaining);
        Assert.Equal(2, changed.Dimension.State94); Assert.Equal(0.125, changed.State118);
        Assert.Equal(g2.State154, changed.State154); Assert.Equal(g2.State110, changed.State110);
        Assert.Equal(78, Assert.IsType<SldFile.Binary.LineHandleData>(changed.First!.Payload).EntityIndex);
    }

    [Theory]
    [InlineData("rectangle.SLDPRT", 7307)]
    [InlineData("slot.SLDPRT", 7950)]
    [InlineData("ellipse.SLDPRT", 7168)]
    [InlineData("spline.SLDPRT", 7172)]
    [InlineData("text.SLDPRT", 58638)]
    public void NativeSketchEntitySection_TraversesAllThirteenTables_WithoutSkippingUnknownBodies(string name, int end)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Sketches2026", name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Config-0-ResolvedFeatures")!;
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000; input.Position = 6131;
        input.Context.NativeDrawingObjects = SketchFixtureGraph(); var sketch = new SldFile.Binary.SketchData();
        sketch.SerializeNativeEntityTables(input, false); Assert.Equal(end, input.Position); Assert.True(input.Remaining > 0);
        Assert.Empty(sketch.NativeHatches); Assert.Empty(sketch.NativePlanes); Assert.Empty(sketch.NativeCylinders);
        Assert.Empty(sketch.NativeSpheres); Assert.Empty(sketch.NativeCones); Assert.Empty(sketch.NativeParametricSurfaces);
        Assert.Empty(sketch.RawData); Assert.Empty(sketch.SketchBody);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 19000;
        output.Context.NativeDrawingObjects = SketchFixtureGraph(); sketch.SerializeNativeEntityTables(output, false);
        Assert.Equal(bytes.AsSpan(6131, end - 6131).ToArray(), output.ToArray());
        // At this point 0x4C5C9F70 starts the relation section. Native tracing
        // independently confirms 1041 bytes from sgSketch entry for spline.
        if (name == "spline.SLDPRT") Assert.Equal(1041, input.Position - 6131);
    }

    [Theory]
    [InlineData("Solidworks2012", 5000, 3170, 3356)]
    [InlineData("Solidworks2026", 19000, 4596, 4790)]
    public void NativeOriginSketch_TraversesEntitySection_WithItsActualVersionAndNoPriorClassContext(
        string directory, int version, int start, int end)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, "cube.SLDPRT"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Config-0-ResolvedFeatures")!;
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = version; input.Position = start;
        input.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph(); var sketch = new SldFile.Binary.SketchData();
        sketch.SerializeNativeEntityTables(input, false); Assert.Equal(end, input.Position);
        Assert.Single(sketch.NativePoints); Assert.Empty(sketch.NativeLines);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        output.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph(); sketch.SerializeNativeEntityTables(output, false);
        Assert.Equal(bytes.AsSpan(start, end - start).ToArray(), output.ToArray());
    }

    [Fact]
    public void NativeSplineControlCurve_UsesNativeHeaderAndSeparateCoordinateKnotArrays()
    {
        // Independent wire-layout vector from 0x4C5C8DE0: dimension=3,
        // order=2, two control points, explicit knots. This is a protocol
        // vector, not a claimed SOLIDWORKS-generated control-polygon fixture.
        byte[] bytes = Convert.FromHexString("0300020002000000"
            + "000000000000000000000000000000000000000000000000"
            + "000000000000F03F00000000000000000000000000000000"
            + "00000000000000000000000000000000000000000000F03F000000000000F03F");
        using var input = MfcArchive.CreateReader(bytes);
        var curve = new SldFile.Binary.SketchData.NativeSpline.ControlCurve(); curve.Serialize(input);
        Assert.Equal(0, input.Remaining); Assert.Equal(6, curve.Coordinates.Length); Assert.Equal(4, curve.Knots.Length);
        Assert.Equal(new double[] { 0, 0, 0, 1, 0, 0 }, curve.Coordinates);
        Assert.Equal(new double[] { 0, 0, 1, 1 }, curve.Knots);
        using var output = MfcArchive.CreateWriter(); curve.Serialize(output); Assert.Equal(bytes, output.ToArray());
        using var empty = MfcArchive.CreateReader(new byte[8]); curve.Serialize(empty);
        Assert.Empty(curve.Coordinates); Assert.Empty(curve.Knots); Assert.Equal(0, empty.Remaining);
        using var bad = MfcArchive.CreateReader(new byte[] { 2, 0, 2, 0, 2, 0, 0, 0 });
        Assert.Throws<InvalidDataException>(() => curve.Serialize(bad));
    }

    [Fact]
    public void NativeSplineTable_DecodesFitPointsAndInterpolation_AndSerializesEdits()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Sketches2026", "spline.SLDPRT"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Config-0-ResolvedFeatures")!;
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000; input.Position = 6131;
        input.Context.NativeDrawingObjects = SketchFixtureGraph(); var sketch = new SldFile.Binary.SketchData();
        sketch.SerializeNativePointTable(input, false); sketch.SerializeNativeLineTable(input, false);
        sketch.SerializeNativeTextTable(input, false); int hatches = -1; input.I32(ref hatches); Assert.Equal(0, hatches);
        sketch.SerializeNativeArcTable(input, false); sketch.SerializeNativeEllipseTable(input, false);
        int parabolas = -1; input.I32(ref parabolas); Assert.Equal(0, parabolas); Assert.Equal(6623, input.Position);
        // Native 0x4C5C3F70 consumes exactly 523 bytes (6629..7152).
        // The outer table count/discriminator add six bytes.
        sketch.SerializeNativeSplineTable(input, false); Assert.Equal(7152, input.Position); Assert.True(input.Remaining > 0);
        var row = Assert.Single(sketch.NativeSplines); Assert.Equal(SketchSegmentKind.Spline, row.Geometry.Kind);
        Assert.Equal(3, row.FitPoints.Length); Assert.Equal(6, row.Values148.Length); Assert.Empty(row.Tangents);
        Assert.NotNull(row.FirstInterpolation); Assert.Equal(3, row.FirstInterpolation!.Parameters.Length);
        Assert.Null(row.SecondInterpolation); Assert.Equal(0, row.State314); Assert.Empty(row.FixedPoints);
        NativeDrawingObjectGraph SplineGraph()
        {
            var graph = NativeDrawingModelFactory.CreateGraph(initialIndex: 192);
            graph.MapExistingClass(173, "sgPointHandle", 1); return graph;
        }
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 19000;
        output.Context.NativeDrawingObjects = SplineGraph(); sketch.SerializeNativeSplineTable(output, false);
        Assert.Equal(bytes.AsSpan(6623, 7152 - 6623).ToArray(), output.ToArray());
        var original = row.FitPoints[1];
        row.FitPoints[1] = new SldFile.Drawing.DrawingPreviewPoint(original.X, original.Y + 0.001, original.Z);
        row.FirstInterpolation.Values[0] += 0.002;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = 19000;
        edited.Context.NativeDrawingObjects = SplineGraph(); sketch.SerializeNativeSplineTable(edited, false);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = 19000;
        reload.Context.NativeDrawingObjects = SplineGraph(); var changed = new SldFile.Binary.SketchData { NativePoints = sketch.NativePoints };
        changed.SerializeNativeSplineTable(reload, false); Assert.Equal(0, reload.Remaining);
        var changedRow = Assert.Single(changed.NativeSplines);
        Assert.Equal(row.FitPoints[1], changedRow.FitPoints[1]);
        Assert.Equal(row.FirstInterpolation.Values, changedRow.FirstInterpolation!.Values);
        Assert.Equal(row.FirstInterpolation.Indices, changedRow.FirstInterpolation.Indices);
        Assert.Equal(row.Values148, changedRow.Values148);
        Assert.Same(sketch.NativePoints[row.Geometry.StartPointIndex].Geometry, changedRow.Geometry.StartPoint);
    }

    [Fact]
    public void NativeSplineControlPointList_ReusesExistingListAndHandlePayloads()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Sketches2026", "spline.SLDPRT"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Config-0-ResolvedFeatures")!;
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000; input.Position = 6131;
        var graph = SketchFixtureGraph(); input.Context.NativeDrawingObjects = graph;
        var sketch = new SldFile.Binary.SketchData();
        sketch.SerializeNativePointTable(input, false); sketch.SerializeNativeLineTable(input, false);
        sketch.SerializeNativeTextTable(input, false); int hatch = -1; input.I32(ref hatch); Assert.Equal(0, hatch);
        sketch.SerializeNativeArcTable(input, false); sketch.SerializeNativeEllipseTable(input, false);
        int parabolas = -1, count = -1; input.I32(ref parabolas); Assert.Equal(0, parabolas); input.I32(ref count); Assert.Equal(1, count);
        ushort kind = 0; input.U16(ref kind); Assert.Equal(8, kind);
        var lineBase = new SldFile.Binary.SketchData.NativeLine(); lineBase.SerializeBase(input, false);
        int state32C = 0; ushort state2FC = 0; input.I32(ref state32C); input.U16(ref state2FC);
        Assert.Equal(2, state32C); Assert.Equal(3, state2FC); Assert.Equal(6723, input.Position);
        NativeDrawingObjectGraph.Node? node = null; graph.Object(input, ref node); Assert.Equal(6784, input.Position);
        Assert.True(input.Remaining > 0); // This is not the complete spline table.
        Assert.Contains(sketch.NativePoints.SelectMany(p => new[] { p.FirstHandle, p.SecondHandle }),
            n => n?.ClassName == "sgSplineHandle" && n.Payload is SldFile.Binary.EntHandleData);
        Assert.Equal("modifSplineList_c", node!.ClassName);
        var list = Assert.IsType<NativeDrawingObjectListModel>(node.Payload); Assert.Equal(3, list.Values.Length);
        for (int i = 0; i < 3; i++) Assert.Equal(i, Assert.IsType<SldFile.Binary.PointHandleData>(list.Values[i]!.Payload).EntityIndex);
        // Native ReadClass capture for this exact fixture: next index 192 at
        // the list tag, class allocated at 192, object at 193. Prior point
        // handle class 173 is the only external mapping needed by this list.
        NativeDrawingObjectGraph ListGraph()
        {
            var mapped = NativeDrawingModelFactory.CreateGraph(initialIndex: 192);
            mapped.MapExistingClass(173, "sgPointHandle", 1); return mapped;
        }
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 19000; ListGraph().Object(output, ref node);
        Assert.Equal(bytes.AsSpan(6723, 6784 - 6723).ToArray(), output.ToArray());
        Assert.IsType<SldFile.Binary.PointHandleData>(list.Values[1]!.Payload).EntityIndex = 50000;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = 19000; ListGraph().Object(edited, ref node);
        Assert.Equal(65, edited.Length);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = 19000;
        NativeDrawingObjectGraph.Node? changed = null; ListGraph().Object(reload, ref changed); Assert.Equal(0, reload.Remaining);
        var changedList = Assert.IsType<NativeDrawingObjectListModel>(changed!.Payload);
        Assert.Equal(50000, Assert.IsType<SldFile.Binary.PointHandleData>(changedList.Values[1]!.Payload).EntityIndex);
        Assert.Equal(2, Assert.IsType<SldFile.Binary.PointHandleData>(changedList.Values[2]!.Payload).EntityIndex);
    }

    [Fact]
    public void NativeSketchEllipseTable_ReusesGeometryAndPreservesAxisPointIndices()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Sketches2026", "ellipse.SLDPRT"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Config-0-ResolvedFeatures")!;
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000; input.Position = 6131;
        input.Context.NativeDrawingObjects = SketchFixtureGraph(); var sketch = new SldFile.Binary.SketchData();
        sketch.SerializeNativePointTable(input, false); sketch.SerializeNativeLineTable(input, false);
        sketch.SerializeNativeTextTable(input, false); int hatch = -1; input.I32(ref hatch); Assert.Equal(0, hatch);
        sketch.SerializeNativeArcTable(input, false); Assert.Equal(7032, input.Position);
        sketch.SerializeNativeEllipseTable(input, false); Assert.Equal(7140, input.Position);
        Assert.Equal(6, sketch.NativePoints.Length); var row = Assert.Single(sketch.NativeEllipses);
        Assert.Equal(SketchSegmentKind.Ellipse, row.Geometry.Kind);
        Assert.Equal(0, row.Geometry.CenterPointIndex); Assert.Equal(2, row.Geometry.MajorPointIndex); Assert.Equal(3, row.Geometry.MinorPointIndex);
        Assert.Equal(4, row.MajorAntiPointIndex); Assert.Equal(5, row.MinorAntiPointIndex);
        Assert.Same(sketch.NativePoints[0].Geometry, row.Geometry.CenterPoint);
        Assert.Same(sketch.NativePoints[2].Geometry, row.Geometry.MajorPoint);
        Assert.Same(sketch.NativePoints[3].Geometry, row.Geometry.MinorPoint);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 19000;
        output.Context.NativeDrawingObjects = SketchFixtureGraph();
        sketch.SerializeNativePointTable(output, false); sketch.SerializeNativeLineTable(output, false);
        sketch.SerializeNativeTextTable(output, false); output.I32(ref hatch); sketch.SerializeNativeArcTable(output, false);
        sketch.SerializeNativeEllipseTable(output, false);
        Assert.Equal(bytes.AsSpan(6131, 7140 - 6131).ToArray(), output.ToArray());
        row.Geometry.MajorPointIndex = 70000; row.MinorAntiPointIndex = -2;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = 19000;
        edited.Context.NativeDrawingObjects = SketchFixtureGraph(); sketch.SerializeNativeEllipseTable(edited, false);
        Assert.Equal(7140 - 7032 + 8, edited.Length);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = 19000;
        reload.Context.NativeDrawingObjects = SketchFixtureGraph();
        var changed = new SldFile.Binary.SketchData { NativePoints = sketch.NativePoints };
        changed.SerializeNativeEllipseTable(reload, false); Assert.Equal(0, reload.Remaining);
        var changedRow = Assert.Single(changed.NativeEllipses);
        Assert.Equal(70000, changedRow.Geometry.MajorPointIndex); Assert.Null(changedRow.Geometry.MajorPoint);
        Assert.Equal(-2, changedRow.MinorAntiPointIndex); Assert.True(changedRow.MajorExtended); Assert.True(changedRow.MinorAntiExtended);
        Assert.Same(sketch.NativePoints[3].Geometry, changedRow.Geometry.MinorPoint);
    }

    [Fact]
    public void NativeSketchTextTable_DecodesFontAndGlyphTopology_AndSerializesOwnedEdits()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Sketches2026", "text.SLDPRT"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Config-0-ResolvedFeatures")!;
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000; input.Position = 6131;
        input.Context.NativeDrawingObjects = SketchFixtureGraph();
        var sketch = new SldFile.Binary.SketchData();
        sketch.SerializeNativePointTable(input, false); sketch.SerializeNativeLineTable(input, false);
        Assert.Equal(6307, input.Position); sketch.SerializeNativeTextTable(input, false);
        Assert.Equal(58598, input.Position);
        var row = Assert.Single(sketch.NativeTexts);
        Assert.Equal(SketchSegmentKind.Text, row.Geometry.Kind); Assert.Equal("Sketch 草图 123", row.Geometry.Text);
        var text = Assert.IsType<NativeDrawingDimTextModel>(row.DimensionText!.Payload);
        var format = Assert.IsType<NativeDrawingCharacterFormatModel>(text.Format!.Payload);
        Assert.Equal("汉仪长仿宋体", format.FontName); Assert.Equal(row.Geometry.Text, text.Text);
        Assert.Equal(19, text.GroupSizes.Length); Assert.Equal(401, text.CurveTypes.Length);
        Assert.Equal(401, text.PointCounts.Length); Assert.Equal(2025, text.GlyphPoints.Length); Assert.Equal(0, text.StateA0);
        var handle = Assert.IsType<SldFile.Binary.EntHandleData>(Assert.Single(sketch.NativePoints).FirstHandle!.Payload);
        Assert.Equal("sgTextHandle", handle.ClassName); Assert.Empty(handle.RawData);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 19000;
        output.Context.NativeDrawingObjects = SketchFixtureGraph();
        sketch.SerializeNativePointTable(output, false); sketch.SerializeNativeLineTable(output, false); sketch.SerializeNativeTextTable(output, false);
        Assert.Equal(bytes.AsSpan(6131, 58598 - 6131).ToArray(), output.ToArray());

        // Edit decoded wire-owned values, not opaque replay data. This test
        // does not claim that changing a string regenerates cached glyphs.
        var position = text.GlyphPoints[0];
        text.GlyphPoints[0] = new SldFile.Drawing.DrawingPreviewPoint(position.X + 0.001, position.Y, position.Z);
        format.State10 += 0.002; row.Geometry.Text = text.Text = "Edited 草图";
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = 19000;
        edited.Context.NativeDrawingObjects = SketchFixtureGraph();
        sketch.SerializeNativePointTable(edited, false); sketch.SerializeNativeLineTable(edited, false); sketch.SerializeNativeTextTable(edited, false);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = 19000;
        reload.Context.NativeDrawingObjects = SketchFixtureGraph(); var changed = new SldFile.Binary.SketchData();
        changed.SerializeNativePointTable(reload, false); changed.SerializeNativeLineTable(reload, false); changed.SerializeNativeTextTable(reload, false);
        Assert.Equal(0, reload.Remaining); var changedRow = Assert.Single(changed.NativeTexts);
        var changedText = Assert.IsType<NativeDrawingDimTextModel>(changedRow.DimensionText!.Payload);
        Assert.Equal("Edited 草图", changedRow.Geometry.Text); Assert.Equal(changedRow.Geometry.Text, changedText.Text);
        Assert.Equal(text.GlyphPoints[0], changedText.GlyphPoints[0]);
        Assert.Equal(format.State10, Assert.IsType<NativeDrawingCharacterFormatModel>(changedText.Format!.Payload).State10);
        Assert.Equal(text.GroupSizes, changedText.GroupSizes); Assert.Equal(text.PointCounts, changedText.PointCounts);
    }

    [Theory]
    [InlineData("rectangle.SLDPRT", 7263, 4, 5)]
    [InlineData("slot.SLDPRT", 7682, 8, 3)]
    [InlineData("polygon.SLDPRT", 8197, 9, 7)]
    public void NativeSketchPointAndLineTables_ReuseGeometryAndParentClassContext(
        string name, int end, int points, int lines)
    {
        // Context captured from the native parent archive at sgSketch entry,
        // not inferred from a tag or used as a production decoding rule.
        // See TestFiles/Sketches2026/native-sketch-context.json.
        const int start = 6131;
        using var file = SwFile.Open(TestFileProvider.Resolve("Sketches2026", name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Config-0-ResolvedFeatures")!;
        using var input = MfcArchive.CreateReader(bytes);
        input.DocumentVersion = 19000; input.Position = start;
        input.Context.NativeDrawingObjects = SketchFixtureGraph();
        var sketch = new SldFile.Binary.SketchData();
        sketch.SerializeNativePointTable(input, false); sketch.SerializeNativeLineTable(input, false);
        Assert.Equal(end, input.Position); Assert.True(input.Remaining > 0);
        Assert.Equal(points, sketch.NativePoints.Length); Assert.Equal(lines, sketch.NativeLines.Length);
        Assert.Equal(name == "slot.SLDPRT", sketch.NativeLines[0].Geometry.IsConstruction);
        Assert.Empty(sketch.RawData); Assert.Empty(sketch.SketchBody);
        foreach (var line in sketch.NativeLines)
        {
            Assert.IsType<SketchSegmentModel>(line.Geometry);
            Assert.Same(sketch.NativePoints[line.Geometry.StartPointIndex].Geometry, line.Geometry.StartPoint);
            Assert.Same(sketch.NativePoints[line.Geometry.EndPointIndex].Geometry, line.Geometry.EndPoint);
        }
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 19000;
        output.Context.NativeDrawingObjects = SketchFixtureGraph();
        sketch.SerializeNativePointTable(output, false); sketch.SerializeNativeLineTable(output, false);
        Assert.Equal(bytes.AsSpan(start, end - start).ToArray(), output.ToArray());

        sketch.NativePoints[0].Geometry.X += 0.0125;
        sketch.NativeLines[0].Geometry.EndPointIndex = 70000;
        sketch.NativeLines[0].Geometry.IsConstruction = !sketch.NativeLines[0].Geometry.IsConstruction;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = 19000;
        edited.Context.NativeDrawingObjects = SketchFixtureGraph();
        sketch.SerializeNativePointTable(edited, false); sketch.SerializeNativeLineTable(edited, false);
        Assert.Equal(end - start + 4, edited.Length);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = 19000;
        reload.Context.NativeDrawingObjects = SketchFixtureGraph();
        var changed = new SldFile.Binary.SketchData();
        changed.SerializeNativePointTable(reload, false); changed.SerializeNativeLineTable(reload, false);
        Assert.Equal(0, reload.Remaining);
        Assert.Equal(sketch.NativePoints[0].Geometry.X, changed.NativePoints[0].Geometry.X);
        Assert.Equal(70000, changed.NativeLines[0].Geometry.EndPointIndex);
        Assert.Equal(sketch.NativeLines[0].Geometry.IsConstruction, changed.NativeLines[0].Geometry.IsConstruction);
        Assert.Equal(changed.NativeLines[0].Geometry.IsConstruction, (changed.NativeLines[0].KindFlags & 2) != 0);
        Assert.True(changed.NativeLines[0].EndExtended); Assert.Null(changed.NativeLines[0].Geometry.EndPoint);
        Assert.Equal(sketch.NativeLines[0].Entity.Flags54, changed.NativeLines[0].Entity.Flags54);
    }

    private static NativeDrawingObjectGraph SketchFixtureGraph()
    {
        var graph = NativeDrawingModelFactory.CreateGraph(initialIndex: 189);
        graph.MapExistingClass(173, "sgPointHandle", 1);
        return graph;
    }

    [Theory]
    [InlineData("slot.SLDPRT", 7690, 7918, 2)]
    [InlineData("polygon.SLDPRT", 8205, 8321, 1)]
    [InlineData("rectangle.SLDPRT", 7271, 7275, 0)]
    public void NativeSketchArcTable_ReusesSegmentGeometry_AndPreservesCenterIndexEncoding(
        string name, int start, int end, int count)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Sketches2026", name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Config-0-ResolvedFeatures")!;
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 19000; input.Position = 6131;
        input.Context.NativeDrawingObjects = SketchFixtureGraph();
        var sketch = new SldFile.Binary.SketchData();
        sketch.SerializeNativePointTable(input, false); sketch.SerializeNativeLineTable(input, false);
        // These controlled fixtures have empty third/fourth tables. Assert the
        // native counts instead of treating unknown nonempty payloads as padding.
        int textCount = -1, hatchCount = -1; input.I32(ref textCount); input.I32(ref hatchCount);
        Assert.Equal(0, textCount); Assert.Equal(0, hatchCount); Assert.Equal(start, input.Position);
        sketch.SerializeNativeArcTable(input, false);
        Assert.Equal(end, input.Position); Assert.Equal(count, sketch.NativeArcs.Length);
        foreach (var row in sketch.NativeArcs)
        {
            Assert.IsType<SketchSegmentModel>(row.Geometry);
            Assert.Equal(name == "polygon.SLDPRT", row.Geometry.IsCircle);
            Assert.Same(sketch.NativePoints[row.Geometry.CenterPointIndex].Geometry, row.Geometry.CenterPoint);
            Assert.Same(sketch.NativePoints[row.Geometry.StartPointIndex].Geometry, row.Geometry.StartPoint);
            Assert.Same(sketch.NativePoints[row.Geometry.EndPointIndex].Geometry, row.Geometry.EndPoint);
        }
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 19000;
        output.Context.NativeDrawingObjects = SketchFixtureGraph(); sketch.SerializeNativeArcTable(output, false);
        Assert.Equal(bytes.AsSpan(start, end - start).ToArray(), output.ToArray());
        if (count == 0) return;
        sketch.NativeArcs[0].Geometry.CenterPointIndex = 70000;
        sketch.NativeArcs[0].Geometry.IsConstruction = !sketch.NativeArcs[0].Geometry.IsConstruction;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = 19000;
        edited.Context.NativeDrawingObjects = SketchFixtureGraph(); sketch.SerializeNativeArcTable(edited, false);
        Assert.Equal(end - start + 4, edited.Length);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = 19000;
        reload.Context.NativeDrawingObjects = SketchFixtureGraph();
        var changed = new SldFile.Binary.SketchData { NativePoints = sketch.NativePoints };
        changed.SerializeNativeArcTable(reload, false);
        Assert.Equal(0, reload.Remaining); Assert.True(changed.NativeArcs[0].CenterExtended);
        Assert.Equal(70000, changed.NativeArcs[0].Geometry.CenterPointIndex);
        Assert.Equal(sketch.NativeArcs[0].Geometry.IsConstruction, changed.NativeArcs[0].Geometry.IsConstruction);
        Assert.Null(changed.NativeArcs[0].Geometry.CenterPoint);
        Assert.Equal(sketch.NativeArcs[0].StateEC, changed.NativeArcs[0].StateEC);
    }

    [Fact]
    public void NativeParentClassContext_RejectsMissingObjectsAndInvalidMappings()
    {
        var graph = SketchFixtureGraph();
        Assert.Throws<InvalidDataException>(() => graph.MapExistingClass(173, "sgLineHandle", 1));
        Assert.Throws<InvalidDataException>(() => graph.MapExistingClass(0, "sgLineHandle", 1));
        Assert.Throws<InvalidDataException>(() => graph.MapExistingClass(189, "sgLineHandle", 1));
        Assert.Throws<InvalidDataException>(() => graph.MapExistingClass(172, "sgPointHandle", 1));
        using var input = MfcArchive.CreateReader(new byte[] { 174, 0 });
        Assert.Throws<InvalidDataException>(() =>
        {
            NativeDrawingObjectGraph.Node? node = null; graph.Object(input, ref node);
        });
    }

    [Theory]
    [InlineData("Solidworks2012", "cube.SLDPRT", 5000, 3170, 3308)]
    [InlineData("Solidworks2026", "cube.SLDPRT", 19000, 4596, 4742)]
    public void NativeSketchPointTable_ExtendsExistingSketchData_AndEditsExistingPointGeometry(
        string directory, string name, int version, int start, int end)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Config-0-ResolvedFeatures")!;
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = version; input.Position = start;
        input.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph();
        var data = new SldFile.Binary.SketchData(); data.SerializeNativePointTable(input, false);
        Assert.Equal(end, input.Position); Assert.True(input.Remaining > 0); // Point table is not the complete sgSketch.
        var record = Assert.Single(data.NativePoints);
        Assert.IsType<SketchPointModel>(record.Geometry); Assert.Equal(0, record.Geometry.TableIndex);
        Assert.Equal(1u, record.Geometry.Id); Assert.Empty(data.RawData); Assert.Empty(data.SketchBody);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        output.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph();
        data.SerializeNativePointTable(output, false);
        Assert.Equal(bytes.AsSpan(start, end - start).ToArray(), output.ToArray());
        uint originalColor = record.Entity.Visual.Color;
        record.Geometry.X = 0.0125; record.Geometry.Y = -0.025;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = version;
        edited.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph();
        data.SerializeNativePointTable(edited, false);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = version;
        reload.Context.NativeDrawingObjects = NativeDrawingModelFactory.CreateGraph();
        var changed = new SldFile.Binary.SketchData(); changed.SerializeNativePointTable(reload, false);
        Assert.Equal(0, reload.Remaining);
        var point = Assert.Single(changed.NativePoints);
        Assert.Equal(0.0125, point.Geometry.X); Assert.Equal(-0.025, point.Geometry.Y); Assert.Equal(0, point.Geometry.Z);
        Assert.Equal(1u, point.Geometry.Id); Assert.Equal(originalColor, point.Entity.Visual.Color);
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, "sgPointHandle", 34960, 34989)]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, "sgLineHandle", 34612, 34640)]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, "sgArcHandle", 160925, 160952)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, "sgPointHandle", 22131, 22156)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, "sgLineHandle", 22893, 22917)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, "sgPointHandle", 24777, 24806)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, "sgLineHandle", 25676, 25704)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, "sgPointHandle", 52596, 52625)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, "sgLineHandle", 9322, 9350)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, "sgArcHandle", 75099, 75126)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, "sgPointHandle", 37267, 37296)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, "sgLineHandle", 10438, 10466)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, "sgArcHandle", 40033, 40060)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, "sgPointHandle", 504820, 504849)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, "sgLineHandle", 9988, 10016)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, "sgArcHandle", 499291, 499318)]
    [InlineData("Drawings", "MacroBenchmark/Mold/bezel moldbase.slddrw", 19000, "sgPointHandle", 1598024, 1598053)]
    [InlineData("Drawings", "MacroBenchmark/Mold/bezel moldbase.slddrw", 19000, "sgLineHandle", 1597607, 1597635)]
    public void NativeSketchHandles_ReuseExistingDataTypes_AndSerializeExtendedIndexEdits(
        string directory, string name, int version, string className, int start, int end)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        // Isolated native handle fixtures contain only scalar payload fields.
        // These test offsets are not used by the runtime decoder.
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = version; input.Position = start;
        NativeDrawingObjectGraph.Node? node = null; NativeDrawingModelFactory.CreateGraph().Object(input, ref node);
        Assert.Equal(className, node!.ClassName); Assert.Equal(end, input.Position);
        var handle = Assert.IsAssignableFrom<SldFile.Binary.EntHandleData>(node.Payload);
        Type expected = className == "sgPointHandle" ? typeof(SldFile.Binary.PointHandleData)
            : className == "sgLineHandle" ? typeof(SldFile.Binary.LineHandleData) : typeof(SldFile.Binary.ArcHandleData);
        Assert.Equal(expected, handle.GetType()); Assert.Empty(handle.RawData);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        NativeDrawingModelFactory.CreateGraph().Object(output, ref node);
        Assert.Equal(bytes.AsSpan(start, end - start).ToArray(), output.ToArray());
        int originalState1C = handle.State1C, originalState08 = handle.State08;
        handle.EntityIndex = 50000;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = version;
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref node);
        Assert.Equal(end - start + 4, edited.Length);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = version;
        NativeDrawingObjectGraph.Node? changed = null; NativeDrawingModelFactory.CreateGraph().Object(reload, ref changed);
        var changedHandle = Assert.IsAssignableFrom<SldFile.Binary.EntHandleData>(changed!.Payload);
        Assert.Equal(0, reload.Remaining); Assert.Equal(expected, changedHandle.GetType());
        Assert.Equal(50000, changedHandle.EntityIndex); Assert.True(changedHandle.ExtendedIndex);
        Assert.Equal(originalState1C, changedHandle.State1C); Assert.Equal(originalState08, changedHandle.State08);
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 157960, 658, 5)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 37970, 341, 11)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 45284, 421, 11)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, 36049, 2, 7)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, 25429, 104, 6)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, 152915, 104, 5)]
    [InlineData("Drawings", "MacroBenchmark/Mold/bezel moldbase.slddrw", 19000, 317051, 3877, 8)]
    public void NativeDisplayStream_ConsumesRootAndOuterSettings_AndRoundtripsEveryByte(
        string directory, string fileName, int version, int expectedEnd, int formatItems, int viewCount)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, fileName));
        Assert.Equal(version, file.GetNativeDrawingDocumentVersion());
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/DisplayLists")!;
        using var ar = MfcArchive.CreateReader(bytes); ar.DocumentVersion = version;
        var model = new NativeDrawingDisplayArchive(); model.Serialize(ar);
        Assert.Equal(expectedEnd, model.RootEnd);
        Assert.Equal(bytes.Length, ar.Position);
        Assert.Equal(formatItems, Assert.Single(model.Formats).Items.Length);
        Assert.Equal(viewCount, model.Views.Length);
        Assert.Equal(bytes, MfcArchive.Serialize(model));
        Assert.Equal(0, ar.Remaining);
        Assert.Empty(model.Tail.TexturedPlanes);
        Assert.Equal(string.Empty, model.Tail.CustomPropertyTemplate);
        if (version >= 0x42DB) Assert.Equal(0x00FFFF80u, model.Tail.EnvelopeColor);
    }

    [Fact]
    public void NativeSymbols_KeepTheirChildObjectsInsideTheOwningSymbol()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        using var ar = MfcArchive.CreateReader(file.GetDrawingPreviewStreamBytes("Contents/DisplayLists")!);
        ar.DocumentVersion = 18000;
        var model = new NativeDrawingDisplayArchive(); model.Serialize(ar);
        var items = model.Formats.SelectMany(f => f.Items).Concat(model.Views.SelectMany(v => v.Items.Concat(v.DetailItems))).ToArray();
        var symbol = Assert.Single(items.Where(item => item.Offset == 146905));
        Assert.Equal(19, symbol.Type);
        Assert.Contains(symbol.Children, item => item.Type == 4 && item.Offset == 147091);
        Assert.DoesNotContain(items, item => item.Offset == 147091);
    }

    [Fact]
    public void NativeSymbolCache_UsesRecordedCoordinates_AndEditsTheOwnedArc()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/DisplayLists")!;
        var stream = new DrawingPreviewStreamModel { DecodeCommands = true };
        using (var ar = MfcArchive.CreateReader(bytes)) { ar.DocumentVersion = 18000; stream.Serialize(ar); }
        var preview = stream.ToDefinition(0, 1);
        var line = Assert.Single(preview.Primitives.Where(p => p.Id == "SYM:00023DD9/00023E93"));
        Assert.Equal(0.09708859863283904, line.Points[0].X);
        Assert.Equal(0.06651991271337285, line.Points[0].Y);
        var arc = Assert.Single(preview.Primitives.Where(p => p.Id.StartsWith("SYM:00023DD9/") && p.Kind == SldFile.Drawing.DrawingPreviewPrimitiveKind.Arc));
        Assert.Equal(0.0980315255255513, arc.Points[2].X);
        var points = arc.Points.ToArray();
        points[2] = new SldFile.Drawing.DrawingPreviewPoint(points[2].X + 0.001, points[2].Y, points[2].Z);
        arc.Points = points;
        stream.FromDefinition(preview);
        using var decoded = MfcArchive.CreateReader(MfcArchive.Serialize(stream)); decoded.DocumentVersion = 18000;
        var native = new NativeDrawingDisplayArchive(); native.Serialize(decoded);
        var symbol = native.Views.SelectMany(v => v.Items).Concat(native.Formats.SelectMany(f => f.Items)).Single(i => i.Offset == 146905);
        Assert.Equal(0.09634772750285084, symbol.Points[0].X);
        Assert.Equal(points[2].X, symbol.Children.Single(i => i.Type == 101).Points[2].X);
        Assert.Equal(0.09708859863283904, symbol.Children.Single(i => i.Type == 4).Points[0].X);
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 385478, 5)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 1478, 11)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 11831, 11)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, 9150023, 7)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, 716115, 6)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, 64915291, 5)]
    [InlineData("Drawings", "MacroBenchmark/Mold/bezel moldbase.slddrw", 19000, 13958077, 8)]
    public void NativeVbLists_ConsumesTheEntireObjectGraph_AndRoundtripsEveryByte(
        string directory, string name, int version, int expectedLength, int expectedViews)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/VBLists")!;
        using var ar = MfcArchive.CreateReader(bytes); ar.DocumentVersion = version;
        var model = new NativeDrawingVbArchive();
        model.Serialize(ar);
        Assert.Equal(expectedLength, bytes.Length);
        Assert.Equal(expectedLength, ar.Position);
        Assert.Equal(0, ar.Remaining);
        Assert.Equal(expectedViews, model.Views.Length);
        Assert.All(model.Views.Where(view => view != null), view => Assert.IsType<NativeDrawingViewBucketModel>(view!.Payload));
        Assert.Equal(bytes, MfcArchive.Serialize(model));
        Assert.All(model.Tessellations, tess =>
            Assert.IsType<NativeDrawingVisualPropertiesModel>(tess.VisualProperties!.Payload));
    }

    [Fact]
    public void NativeVbLists_FacetEditsAreSerializedFromTheObjectModel()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/VBLists")!;
        using var reader = MfcArchive.CreateReader(bytes); reader.DocumentVersion = 18000;
        var model = new NativeDrawingVbArchive(); model.Serialize(reader);
        NativeDrawingFacetModel FirstFacet(NativeDrawingVbArchive archive) => archive.Tessellations
            .SelectMany(t => t.Part.Bodies).Select(n => n?.Payload).OfType<NativeDrawingBodyTessModel>()
            .SelectMany(b => b.Faces).Select(n => n?.Payload).OfType<NativeDrawingFaceTessModel>()
            .Select(f => f.Facet).First(f => f.Channels[1].Words.Length > 0);
        var facet = FirstFacet(model);
        uint updated = facet.Channels[1].Words[0] ^ 1u;
        facet.Channels[1].Words[0] = updated;
        byte[] changed = MfcArchive.Serialize(model);
        Assert.Equal(bytes.Length, changed.Length);
        Assert.False(bytes.SequenceEqual(changed));
        using var secondReader = MfcArchive.CreateReader(changed); secondReader.DocumentVersion = 18000;
        var second = new NativeDrawingVbArchive(); second.Serialize(secondReader);
        Assert.Equal(0, secondReader.Remaining);
        Assert.Equal(updated, FirstFacet(second).Channels[1].Words[0]);
    }

    private sealed class Scalar : IMfcSerializable
    {
        internal int Value;
        public void Serialize(MfcArchive ar) => ar.I32(ref Value);
    }

    private sealed class HeaderRoot : IMfcSerializable
    {
        internal NativeDrawingObjectGraph.Node? Header;
        public void Serialize(MfcArchive ar) => NativeDrawingModelFactory.Graph(ar).Object(ar, ref Header);
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 9817)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 6322)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 7113)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, 7201)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, 8317)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, 7867)]
    [InlineData("Drawings", "MacroBenchmark/Mold/bezel moldbase.slddrw", 19000, 12751)]
    public void DefinitionModelCommon_PreservesSettingsAndSerializesEdits_BeforeTheFeatureGraph(
        string directory, string name, int version, int commonEnd)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        using var ar = MfcArchive.CreateReader(bytes); ar.DocumentVersion = version; ar.Position = 36;
        var model = new NativeDrawingModelCommonModel();
        var graph = NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" && s == 1 ? model : null);
        NativeDrawingObjectGraph.Node? root = null; graph.Object(ar, ref root);
        Assert.Equal(commonEnd, ar.Position);
        Assert.True(ar.Remaining > 0); // Feature graph remains a separate, required payload.
        Assert.Equal(2, model.DictionaryKind);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        NativeDrawingModelFactory.CreateGraph().Object(output, ref root);
        Assert.Equal(bytes.AsSpan(36, commonEnd - 36).ToArray(), output.ToArray());

        model.Scalar1EC = 0.125;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = version;
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref root);
        using var reloaded = MfcArchive.CreateReader(edited.ToArray()); reloaded.DocumentVersion = version;
        var changed = new NativeDrawingModelCommonModel();
        NativeDrawingObjectGraph.Node? changedRoot = null;
        NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" && s == 1 ? changed : null)
            .Object(reloaded, ref changedRoot);
        Assert.Equal(0, reloaded.Remaining);
        Assert.Equal(0.125, changed.Scalar1EC);
        Assert.Same(changedRoot, Assert.IsType<NativeDrawingComponentReferenceModel>(changed.Header.Component!.Payload).Owner);
    }

    private sealed class PlanePrefixRoot : IMfcSerializable
    {
        internal NativeDrawingModelCommonModel Common = new NativeDrawingModelCommonModel();
        internal uint FeatureCount;
        internal NativeDrawingObjectGraph.Node?[] Features = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal bool IncludesModelManagers;
        internal int ResolvedTerminator;
        internal NativeDrawingModelManagersModel Managers = new NativeDrawingModelManagersModel();
        internal NativeDrawingObjectGraph.Node? Relations;
        internal bool IncludesDetailDefinitions;
        internal NativeDrawingObjectGraph.Node?[] Configurations = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal NativeDrawingDetailDefinitionsModel DetailDefinitions = new NativeDrawingDetailDefinitionsModel();
        public void Serialize(MfcArchive ar)
        {
            Common.Serialize(ar); ar.Count(ref FeatureCount);
            // Fixture prefix through the first sheet; its payload may be
            // replaced by a shorter test prefix. The drawing root tail is excluded.
            if (ar.IsLoading) Features = new NativeDrawingObjectGraph.Node?[Math.Min(7, FeatureCount)];
            for (int i = 0; i < Features.Length; i++) NativeDrawingModelFactory.Graph(ar).Object(ar, ref Features[i]);
            if (IncludesModelManagers)
            {
                if (Features.Length != FeatureCount) throw new InvalidOperationException("The manager prefix requires the complete resolved list.");
                ar.I32(ref ResolvedTerminator); Managers.Serialize(ar);
                NativeDrawingModelFactory.Graph(ar).Object(ar, ref Relations);
                if (IncludesDetailDefinitions)
                {
                    NativeDrawingObjectArray.Serialize(ar, ref Configurations);
                    DetailDefinitions.SerializePrefix(ar);
                }
            }
        }
    }

    [Fact]
    public void DrawingPlaneData_RoundtripsTheNativeD800Payload_AndEditsItsAdditionalTransform()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        // Independently located fixture object, not a runtime offset or seek
        // rule. Its native reference fields are all null, so it can be tested
        // in isolation without the earlier Definition reference table.
        const int start = 203699, end = 203905;
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = 18000; input.Position = start;
        NativeDrawingObjectGraph.Node? node = null; NativeDrawingModelFactory.CreateGraph().Object(input, ref node);
        Assert.Equal("moDrawingRefPlnData_c", node!.ClassName); Assert.Equal(end, input.Position);
        var data = Assert.IsType<NativeDrawingPlaneDataModel>(node.Payload);
        Assert.True(data.Drawing); Assert.Null(data.Edge); Assert.Null(data.Axis); Assert.Null(data.FacetFin); Assert.Null(data.SketchEntity);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 18000;
        NativeDrawingModelFactory.CreateGraph().Object(output, ref node);
        Assert.Equal(bytes.AsSpan(start, end - start).ToArray(), output.ToArray());
        var originalTranslation = data.Transform.Translation;
        data.DrawingTransform.Translation = new SldFile.Drawing.DrawingPreviewPoint(0.125, 0.25, 0.5);
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = 18000;
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref node);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = 18000;
        NativeDrawingObjectGraph.Node? changed = null; NativeDrawingModelFactory.CreateGraph().Object(reload, ref changed);
        Assert.Equal(0, reload.Remaining);
        var changedData = Assert.IsType<NativeDrawingPlaneDataModel>(changed!.Payload);
        Assert.Equal(0.125, changedData.DrawingTransform.Translation.X);
        Assert.Equal(0.25, changedData.DrawingTransform.Translation.Y);
        Assert.Equal(0.5, changedData.DrawingTransform.Translation.Z);
        Assert.Equal(originalTranslation, changedData.Transform.Translation);
    }
    private sealed class SheetPlanePrefix : IMfcSerializable
    {
        internal NativeDrawingNodeLightweightModel Lightweight = new NativeDrawingNodeLightweightModel();
        internal uint ChildCount;
        internal NativeDrawingObjectGraph.Node? Plane;
        internal bool IncludesProfile;
        internal NativeDrawingObjectGraph.Node? Profile;
        internal bool IncludesDetail;
        internal NativeDrawingObjectGraph.Node? Detail;
        internal bool IncludesTemplate;
        internal NativeDrawingObjectGraph.Node? Template;
        internal bool IncludesView;
        internal NativeDrawingObjectGraph.Node? View;
        internal bool IncludesSecondView;
        internal NativeDrawingObjectGraph.Node? SecondView;
        internal NativeDrawingObjectGraph.Node?[] MoreViews = Array.Empty<NativeDrawingObjectGraph.Node?>();
        public void Serialize(MfcArchive ar)
        {
            Lightweight.Serialize(ar); ar.Count(ref ChildCount);
            NativeDrawingModelFactory.Graph(ar).Object(ar, ref Plane);
            if (IncludesProfile) NativeDrawingModelFactory.Graph(ar).Object(ar, ref Profile);
            if (IncludesDetail) NativeDrawingModelFactory.Graph(ar).Object(ar, ref Detail);
            if (IncludesTemplate) NativeDrawingModelFactory.Graph(ar).Object(ar, ref Template);
            if (IncludesView) NativeDrawingModelFactory.Graph(ar).Object(ar, ref View);
            if (IncludesSecondView) NativeDrawingModelFactory.Graph(ar).Object(ar, ref SecondView);
            for (int i = 0; i < MoreViews.Length; i++) NativeDrawingModelFactory.Graph(ar).Object(ar, ref MoreViews[i]);
        }
    }

    [Theory]
    [InlineData("Solidworks2012", 5000, 85967)]
    [InlineData("Solidworks2026", 19000, 104201)]
    public void DefinitionAbsoluteView_DecodesItsScopedGeometryAndReferenceCycle_AndRebuildsEdits(
        string directory, int version, int prefixEnd)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, "cube.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        SheetPlanePrefix Prefix() => new SheetPlanePrefix { IncludesProfile = true, IncludesDetail = true, IncludesTemplate = true, IncludesView = true };
        NativeDrawingObjectGraph Graph(PlanePrefixRoot root, SheetPlanePrefix sheet) =>
            NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" ? root : n == "moDrSheet" ? sheet : null);
        void Context(MfcArchive ar)
        {
            ar.DocumentVersion = version; ar.Context.NativeSketchIncludesOwnerProfile = false;
            ar.Context.NativeAnnotationReferenceIsExternal = false;
        }
        var root = new PlanePrefixRoot(); var sheet = Prefix();
        using var input = MfcArchive.CreateReader(bytes); Context(input); input.Position = 36;
        NativeDrawingObjectGraph.Node? rootNode = null; Graph(root, sheet).Object(input, ref rootNode);
        Assert.Equal(prefixEnd, input.Position); Assert.True(input.Remaining > 0);
        Assert.Equal(0u, input.Context.NativeDrawingArchiveConditions);
        var view = Assert.IsType<DrViewMfcModel>(sheet.View!.Payload); Assert.Null(view.RawData);
        var state = Assert.IsType<DrViewMfcModel.NativeState>(view.NativeView);
        var children = state.Feature.Feature.ResolvedChildren.Values;
        Assert.Equal(3, children.Length);
        var plane = Assert.IsType<NativeDrawingReferencePlaneModel>(children.Single(node => node?.ClassName == "moRefPlane_c")!.Payload);
        Assert.True(Assert.IsType<NativeDrawingPlaneDataModel>(plane.PlaneData!.Payload).Drawing);
        Assert.IsType<SketchFeatureModel>(children.Single(node => node?.ClassName == SketchFeatureModel.ClassName2D)!.Payload);
        var reference = Assert.IsType<NativeDrawingReferenceModel>(state.Reference!.Payload);
        var external = Assert.IsType<NativeDrawingExternalFeatureModel>(reference.ExternalFeature!.Payload);
        Assert.Same(state.Reference, external.Reference);
        Assert.IsType<NativeDrawingViewAlignmentModel>(state.Alignment!.Payload);
        using var output = MfcArchive.CreateWriter(); Context(output);
        NativeDrawingModelFactory.CreateGraph().Object(output, ref rootNode);
        Assert.Equal(0u, output.Context.NativeDrawingArchiveConditions);
        Assert.Equal(bytes.AsSpan(36, prefixEnd - 36).ToArray(), output.ToArray());
        state.Point750 = new SldFile.Drawing.DrawingPreviewPoint(0.125, 0.25, 0);
        reference.Configuration = "WireRoundtripConfiguration";
        using var edited = MfcArchive.CreateWriter(); Context(edited);
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref rootNode);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); Context(reload);
        var changedRoot = new PlanePrefixRoot(); var changedSheet = Prefix(); NativeDrawingObjectGraph.Node? changedNode = null;
        Graph(changedRoot, changedSheet).Object(reload, ref changedNode); Assert.Equal(0, reload.Remaining);
        var changed = Assert.IsType<DrViewMfcModel>(changedSheet.View!.Payload).NativeView!;
        Assert.Equal(0u, reload.Context.NativeDrawingArchiveConditions);
        Assert.Equal(0.125, changed.Point750.X); Assert.Equal(0.25, changed.Point750.Y);
        Assert.Equal("WireRoundtripConfiguration", Assert.IsType<NativeDrawingReferenceModel>(changed.Reference!.Payload).Configuration);
    }

    [Fact]
    public void DefinitionView_RestoresParentConditions_WhenAChildLayoutIsRejected()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        using var input = MfcArchive.CreateReader(file.GetDrawingPreviewStreamBytes("Contents/Definition")!);
        input.DocumentVersion = 18000; input.Position = 36;
        input.Context.NativeSketchIncludesOwnerProfile = false; input.Context.NativeAnnotationReferenceIsExternal = false;
        var root = new PlanePrefixRoot();
        var sheet = new SheetPlanePrefix { IncludesProfile = true, IncludesDetail = true, IncludesTemplate = true, IncludesView = true };
        var graph = NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDetailCircle_c"
            ? throw new NotSupportedException("Deliberately rejected detail-circle layout.")
            : n == "moDrawing_c" ? root : n == "moDrSheet" ? sheet : null);
        NativeDrawingObjectGraph.Node? node = null;
        var error = Assert.Throws<NotSupportedException>(() => graph.Object(input, ref node));
        Assert.Contains("Deliberately rejected", error.Message); Assert.Equal(218063, input.Position);
        Assert.Equal(0u, input.Context.NativeDrawingArchiveConditions);
        Assert.Null(input.Context.NativeDrawingOwnerIsDrawing);
    }

    [Theory]
    [InlineData(1, 219369)]
    [InlineData(2, 317754)]
    [InlineData(3, 378711)]
    [InlineData(4, 395347)]
    [InlineData(5, 418618)]
    public void DefinitionD800Views_DecodeDimensionsExternalSketchAndDetailCircle_AndRebuildEdits(int viewCount, int prefixEnd)
    {
        bool includesSecondView = viewCount > 1;
        using var file = SwFile.Open(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        void Context(MfcArchive ar)
        {
            ar.DocumentVersion = 18000; ar.Context.NativeSketchIncludesOwnerProfile = false;
            ar.Context.NativeAnnotationReferenceIsExternal = false;
        }
        SheetPlanePrefix Prefix() => new SheetPlanePrefix { IncludesProfile = true, IncludesDetail = true, IncludesTemplate = true, IncludesView = true, IncludesSecondView = includesSecondView, MoreViews = new NativeDrawingObjectGraph.Node?[Math.Max(0, viewCount - 2)] };
        NativeDrawingObjectGraph Graph(PlanePrefixRoot root, SheetPlanePrefix sheet) =>
            NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" ? root : n == "moDrSheet" ? sheet : null);
        NativeDrawingDetailCircleModel Circle(SheetPlanePrefix sheet)
        {
            var view = Assert.IsType<DrViewMfcModel>(sheet.View!.Payload).NativeView!;
            var circle = Assert.IsType<NativeDrawingDetailCircleModel>(view.Feature.Feature.ResolvedChildren.Values.Single(n => n?.ClassName == "moDetailCircle_c")!.Payload);
            Assert.IsType<SldFile.Binary.SketchData>(circle.Sketch!.Payload);
            Assert.NotEmpty(circle.Handles);
            Assert.All(circle.Handles, handle => Assert.IsAssignableFrom<SldFile.Binary.EntHandleData>(handle!.Payload));
            return circle;
        }
        var root = new PlanePrefixRoot(); var sheet = Prefix(); NativeDrawingObjectGraph.Node? node = null;
        using var input = MfcArchive.CreateReader(bytes); Context(input); input.Position = 36;
        Graph(root, sheet).Object(input, ref node);
        Assert.Equal(prefixEnd, input.Position); Assert.True(input.Remaining > 0);
        var circle = Circle(sheet);
        for (int i = 0; i < sheet.MoreViews.Length; i++)
        {
            var view = Assert.IsType<DrViewMfcModel>(sheet.MoreViews[i]!.Payload).NativeView!;
            if (i < 2) Assert.True(view.Unfolded);
            else { Assert.True(view.Detail); Assert.Same(sheet.View, view.ParentViewFE0); }
        }
        if (includesSecondView)
        {
            var second = Assert.IsType<DrViewMfcModel>(sheet.SecondView!.Payload).NativeView!;
            Assert.True(second.Unfolded); Assert.Same(sheet.View, second.ParentViewFE0);
        }
        using var output = MfcArchive.CreateWriter(); Context(output);
        NativeDrawingModelFactory.CreateGraph().Object(output, ref node);
        Assert.Equal(bytes.AsSpan(36, prefixEnd - 36).ToArray(), output.ToArray());
        circle.Point4C0 = new SldFile.Drawing.DrawingPreviewPoint(0.125, 0.25, 0);
        circle.Text.Position = new SldFile.Drawing.DrawingPreviewPoint(0.5, 0.75, 0);
        if (includesSecondView) Assert.IsType<DrViewMfcModel>(sheet.SecondView!.Payload).NativeView!.UnfoldedTransform1000.Translation = new SldFile.Drawing.DrawingPreviewPoint(0.25, 0.5, 0.75);
        using var edited = MfcArchive.CreateWriter(); Context(edited);
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref node);
        var changedRoot = new PlanePrefixRoot(); var changedSheet = Prefix(); NativeDrawingObjectGraph.Node? changedNode = null;
        using var reload = MfcArchive.CreateReader(edited.ToArray()); Context(reload);
        Graph(changedRoot, changedSheet).Object(reload, ref changedNode);
        Assert.Equal(0, reload.Remaining);
        Assert.Equal(0.125, Circle(changedSheet).Point4C0.X); Assert.Equal(0.25, Circle(changedSheet).Point4C0.Y);
        Assert.Equal(0.5, Circle(changedSheet).Text.Position.X); Assert.Equal(0.75, Circle(changedSheet).Text.Position.Y);
        if (includesSecondView)
        {
            var second = Assert.IsType<DrViewMfcModel>(changedSheet.SecondView!.Payload).NativeView!;
            Assert.Same(changedSheet.View, second.ParentViewFE0);
            Assert.Equal(0.25, second.UnfoldedTransform1000.Translation.X); Assert.Equal(0.75, second.UnfoldedTransform1000.Translation.Z);
        }
        Assert.Equal(0u, input.Context.NativeDrawingArchiveConditions); Assert.Null(input.Context.NativeDrawingOwnerIsDrawing);
        Assert.Equal(0u, output.Context.NativeDrawingArchiveConditions); Assert.Null(output.Context.NativeDrawingOwnerIsDrawing);
        Assert.Equal(0u, reload.Context.NativeDrawingArchiveConditions); Assert.Null(reload.Context.NativeDrawingOwnerIsDrawing);
    }

    [Fact]
    public void DefinitionD800Sheet_UsesCompleteProductionSheetCodec_AndRebuildsPaperEdits()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        void Context(MfcArchive ar)
        {
            ar.DocumentVersion = 18000; ar.Context.NativeSketchIncludesOwnerProfile = false;
            ar.Context.NativeAnnotationReferenceIsExternal = false;
        }
        NativeDrawingObjectGraph Graph(PlanePrefixRoot root) =>
            NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" ? root : null);
        DrSheetMfcModel.NativeState Sheet(PlanePrefixRoot root) =>
            Assert.IsType<DrSheetMfcModel>(root.Features.Single(n => n?.ClassName == "moDrSheet")!.Payload).NativeSheet!;
        var root = new PlanePrefixRoot(); NativeDrawingObjectGraph.Node? node = null;
        using var input = MfcArchive.CreateReader(bytes); Context(input); input.Position = 36;
        Graph(root).Object(input, ref node);
        Assert.Equal(419039, input.Position); Assert.True(input.Remaining > 0);
        var sheet = Sheet(root);
        var children = sheet.Feature.Feature.ResolvedChildren.Values;
        Assert.Equal(9, children.Length);
        Assert.Equal(5, children.Count(n => n?.Payload is DrViewMfcModel));
        Assert.True(Assert.IsType<DrViewMfcModel>(children.Last()!.Payload).NativeView!.Detail);
        using var output = MfcArchive.CreateWriter(); Context(output);
        NativeDrawingModelFactory.CreateGraph().Object(output, ref node);
        Assert.Equal(bytes.AsSpan(36, input.Position - 36).ToArray(), output.ToArray());
        sheet.State3F0 = 0.42; sheet.State3F8 = 0.297;
        using var edited = MfcArchive.CreateWriter(); Context(edited);
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref node);
        var changedRoot = new PlanePrefixRoot(); NativeDrawingObjectGraph.Node? changedNode = null;
        using var reload = MfcArchive.CreateReader(edited.ToArray()); Context(reload);
        Graph(changedRoot).Object(reload, ref changedNode);
        Assert.Equal(0, reload.Remaining);
        Assert.Equal(0.42, Sheet(changedRoot).State3F0); Assert.Equal(0.297, Sheet(changedRoot).State3F8);
        Assert.Equal(0u, reload.Context.NativeDrawingArchiveConditions); Assert.Null(reload.Context.NativeDrawingOwnerIsDrawing);
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 419113)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 151123)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 128686)]
    public void DefinitionModelManagers_PreserveNativePrefixes_AndRebuildNamedValuesAndBodyIds(
        string directory, string name, int version, int prefixEnd)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        void Context(MfcArchive ar)
        {
            ar.DocumentVersion = version; ar.Context.NativeSketchIncludesOwnerProfile = false;
            ar.Context.NativeAnnotationReferenceIsExternal = false;
        }
        NativeDrawingObjectGraph Graph(PlanePrefixRoot root) =>
            NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" ? root : null);
        var root = new PlanePrefixRoot { IncludesModelManagers = true }; NativeDrawingObjectGraph.Node? node = null;
        using var input = MfcArchive.CreateReader(bytes); Context(input); input.Position = 36;
        Graph(root).Object(input, ref node);
        Assert.Equal(prefixEnd, input.Position); Assert.True(input.Remaining > 0);
        Assert.Empty(root.Managers.Atoms.Atoms); Assert.Empty(root.Managers.GhostBodies.Bodies);
        Assert.Equal(1u, root.Managers.GhostBodies.Flags);
        var relations = Assert.IsType<NativeDrawingRelationManagerModel>(root.Relations!.Payload);
        using var output = MfcArchive.CreateWriter(); Context(output);
        NativeDrawingModelFactory.CreateGraph().Object(output, ref node);
        Assert.Equal(bytes.AsSpan(36, prefixEnd - 36).ToArray(), output.ToArray());
        root.Managers.Atoms.State260 = 17; root.Managers.Atoms.StateA8 = 42;
        root.Managers.GhostBodies.Flags = 5;
        root.Managers.GhostBodies.Bodies = new[] { new NativeDrawingGhostBodyManagerModel.Body
        {
            State8 = 21, StateC = 23, State10 = 25, StateC0 = -1,
            Ids = new uint[] { 31, 33 },
            Points = new[] { new SldFile.Drawing.DrawingPreviewPoint(0.125, 0.25, 0.5) }
        } };
        relations.NamedValues = new[] { new NativeDrawingRelationManagerModel.NamedValue { Name = "WireModelValue", Value = 0.125 } };
        relations.State170 = "WireRelationState"; relations.Time180 = 1234567890;
        using var edited = MfcArchive.CreateWriter(); Context(edited);
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref node);
        var changed = new PlanePrefixRoot { IncludesModelManagers = true }; NativeDrawingObjectGraph.Node? changedNode = null;
        using var reload = MfcArchive.CreateReader(edited.ToArray()); Context(reload);
        Graph(changed).Object(reload, ref changedNode);
        Assert.Equal(0, reload.Remaining);
        Assert.Equal(17, changed.Managers.Atoms.State260); Assert.Equal(42u, changed.Managers.Atoms.StateA8);
        Assert.Equal(5u, changed.Managers.GhostBodies.Flags);
        var body = Assert.Single(changed.Managers.GhostBodies.Bodies);
        Assert.Equal(21, body.State8); Assert.Equal(23, body.StateC); Assert.Equal(25, body.State10); Assert.Equal(-1, body.StateC0);
        Assert.Equal(new uint[] { 31, 33 }, body.Ids); Assert.Null(body.Geometry);
        Assert.Equal(0.125, Assert.Single(body.Points).X); Assert.Equal(0.5, body.Points[0].Z);
        var changedRelations = Assert.IsType<NativeDrawingRelationManagerModel>(changed.Relations!.Payload);
        Assert.Equal("WireModelValue", Assert.Single(changedRelations.NamedValues).Name);
        Assert.Equal(0.125, changedRelations.NamedValues[0].Value);
        Assert.Equal("WireRelationState", changedRelations.State170); Assert.Equal(1234567890u, changedRelations.Time180);
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 431034)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 137139)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 163804)]
    public void DefinitionDetailDefaults_ReuseFontsUnitsAndLineStyles_AndRebuildOwnedChanges(
        string directory, string name, int version, int prefixEnd)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        void Context(MfcArchive ar)
        {
            ar.DocumentVersion = version; ar.Context.NativeSketchIncludesOwnerProfile = false;
            ar.Context.NativeAnnotationReferenceIsExternal = false;
        }
        NativeDrawingObjectGraph Graph(PlanePrefixRoot root) =>
            NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" ? root : null);
        var root = new PlanePrefixRoot { IncludesModelManagers = true, IncludesDetailDefinitions = true };
        NativeDrawingObjectGraph.Node? node = null;
        using var input = MfcArchive.CreateReader(bytes); Context(input); input.Position = 36;
        Graph(root).Object(input, ref node);
        Assert.Equal(prefixEnd, input.Position); Assert.True(input.Remaining > 0);
        var details = root.DetailDefinitions;
        Assert.Equal("Century Gothic", details.CharacterFormat.FontName);
        Assert.Equal(version == 5000 ? 0 : 48, details.LayoutStates.Length);
        using var output = MfcArchive.CreateWriter(); Context(output);
        NativeDrawingModelFactory.CreateGraph().Object(output, ref node);
        Assert.Equal(bytes.AsSpan(36, prefixEnd - 36).ToArray(), output.ToArray());
        details.State13A0 = "WireStandard";
        details.CharacterFormat.FontName = "Consolas";
        details.Dimensions.Linear.Font.FontName = "Arial";
        details.Dimensions.Linear.Length.Precision = 7;
        details.Dimensions.Linear.String48 = "WireLinear";
        details.Dimensions.ArcLength.Value18 = 1.25;
        details.Annotations.Balloon.String70 = "WireBalloon";
        details.Annotations.SurfaceFinish.Value68 = 0.0015;
        details.Tables.General.Font.FontName = "Consolas";
        details.Tables.Hole.Value48 = 0.0025;
        details.Labels.Detail.Font98.FontName = "Arial";
        details.Labels.Section.SectionC8 = 0.025;
        if (version > 0x13F9) details.Labels.Ortho.String88 = "WireLabelDefault";
        if (version >= 0x17FF) details.Exclusions = new[] { "WireExcludeOne", "WireExcludeTwo" };
        if (details.LayoutStates.Length != 0) details.LayoutStates[0] = 3;
        using var edited = MfcArchive.CreateWriter(); Context(edited);
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref node);
        var changed = new PlanePrefixRoot { IncludesModelManagers = true, IncludesDetailDefinitions = true };
        NativeDrawingObjectGraph.Node? changedNode = null;
        using var reload = MfcArchive.CreateReader(edited.ToArray()); Context(reload);
        Graph(changed).Object(reload, ref changedNode); Assert.Equal(0, reload.Remaining);
        details = changed.DetailDefinitions;
        Assert.Equal("WireStandard", details.State13A0); Assert.Equal("Consolas", details.CharacterFormat.FontName);
        Assert.Equal("Arial", details.Dimensions.Linear.Font.FontName);
        Assert.Equal(7, details.Dimensions.Linear.Length.Precision); Assert.Equal("WireLinear", details.Dimensions.Linear.String48);
        Assert.Equal(1.25, details.Dimensions.ArcLength.Value18);
        Assert.Equal("WireBalloon", details.Annotations.Balloon.String70);
        Assert.Equal(0.0015, details.Annotations.SurfaceFinish.Value68);
        Assert.Equal("Consolas", details.Tables.General.Font.FontName); Assert.Equal(0.0025, details.Tables.Hole.Value48);
        Assert.Equal("Arial", details.Labels.Detail.Font98.FontName); Assert.Equal(0.025, details.Labels.Section.SectionC8);
        if (version > 0x13F9) Assert.Equal("WireLabelDefault", details.Labels.Ortho.String88);
        if (version >= 0x17FF) Assert.Equal(new[] { "WireExcludeOne", "WireExcludeTwo" }, details.Exclusions);
        else Assert.Empty(details.Exclusions);
        if (details.LayoutStates.Length != 0) Assert.Equal(3, details.LayoutStates[0]);
    }

    private sealed class EdgeBoundary : Exception { }
    private sealed class EdgePrefix : IMfcSerializable
    {
        internal MfcCompEdgeModel Model = new MfcCompEdgeModel { NativeEdge = new MfcCompEdgeModel.NativeState() };
        public void Serialize(MfcArchive ar) { Model.Serialize(ar); throw new EdgeBoundary(); }
    }

    private sealed class CenterMarkPrefix : IMfcSerializable
    {
        internal NativeDrawingCenterMarkModel Model = new NativeDrawingCenterMarkModel();
        public void Serialize(MfcArchive ar) { Model.Serialize(ar); throw new EdgeBoundary(); }
    }

    private sealed class DistanceDimensionPrefix : IMfcSerializable
    {
        internal MoDisplayDistanceDimModel Model = new MoDisplayDistanceDimModel { NativeDimension = new MoDisplayDimModel.NativeState() };
        public void Serialize(MfcArchive ar) { Model.Serialize(ar); throw new EdgeBoundary(); }
    }

    private sealed class NativeObjectPrefix : IMfcSerializable
    {
        internal IMfcSerializable Model;
        internal NativeObjectPrefix(IMfcSerializable model) => Model = model;
        public void Serialize(MfcArchive ar) { Model.Serialize(ar); throw new EdgeBoundary(); }
    }

    [Fact]
    public void NativeReferenceWrapper_RequiresExplicitArchiveContextBeforeWriting()
    {
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = 18000;
        var wrapper = new NativeDrawingReferenceWrapperModel();
        Assert.Throws<InvalidOperationException>(() => wrapper.Serialize(output));
        Assert.Empty(output.ToArray());
        output.Context.NativeSketchIncludesOwnerProfile = true;
        Assert.Throws<NotSupportedException>(() => wrapper.Serialize(output));
        Assert.Empty(output.ToArray());
    }

    [Theory]
    [InlineData("moVertexRef_c", 171831, 193443)]
    [InlineData("moDisplayProfileFeature_c", 193471, 204569)]
    [InlineData("sgExtEnt_c", 204831, 218063)]
    [InlineData("moDetailViewLabel_c", 395762, 400590)]
    [InlineData("moDisplayAngularDim_c", 401386, 404527)]
    [InlineData("moDisplayRadialDim_c", 404528, 418250)]
    public void DefinitionReferences_ReuseExistingSketchAndTopologyCodecs_AndRebuildOwnedEdits(string className, int minimumEnd, int maximumEnd)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        NativeObjectPrefix Prefix() => new NativeObjectPrefix(className switch
        {
            "moVertexRef_c" => new NativeDrawingVertexReferenceModel(),
            "moDisplayProfileFeature_c" => new NativeDrawingDisplayProfileModel(),
            "sgExtEnt_c" => new SldFile.Binary.ExtEntData(),
            "moDetailViewLabel_c" => new NativeDrawingViewLabelModel(),
            "moDisplayAngularDim_c" => new MoDisplayAngularDimModel { NativeDimension = new MoDisplayDimModel.NativeState { Angular = true } },
            "moDisplayRadialDim_c" => new MoDisplayRadialDimModel { NativeDimension = new MoDisplayDimModel.NativeState { RadialDimension = new MoDisplayDimModel.NativeRadialState() } },
            _ => throw new InvalidOperationException()
        });
        void Context(MfcArchive ar)
        {
            ar.DocumentVersion = 18000; ar.Context.NativeSketchIncludesOwnerProfile = false;
            ar.Context.NativeAnnotationReferenceIsExternal = false;
        }
        NativeDrawingObjectGraph Graph(NativeObjectPrefix prefix)
        {
            var root = new PlanePrefixRoot();
            bool needsDetailView = className == "moDetailViewLabel_c" || className == "moDisplayAngularDim_c" || className == "moDisplayRadialDim_c";
            var sheet = new SheetPlanePrefix { IncludesProfile = true, IncludesDetail = true, IncludesTemplate = true, IncludesView = true,
                IncludesSecondView = needsDetailView, MoreViews = new NativeDrawingObjectGraph.Node?[needsDetailView ? 3 : 0] };
            bool first = true;
            return NativeDrawingModelFactory.CreateGraph((n, s) =>
            {
                if (n == "moDrawing_c") return root;
                if (n == "moDrSheet") return sheet;
                if (n == className && first) { first = false; return prefix; }
                return null;
            });
        }
        double? editedAngleValue = null;
        void CheckOrEdit(NativeObjectPrefix prefix, bool edit, bool changed)
        {
            switch (prefix.Model)
            {
                case NativeDrawingVertexReferenceModel vertex:
                    Assert.Contains(vertex.Reference.EntityIds, id => id?.Payload is FromSktEnt3IntSurfIdRepData);
                    if (edit) { vertex.HasPoint = 1; vertex.Point = new SldFile.Drawing.DrawingPreviewPoint(0.125, 0.25, 0.5); }
                    if (changed) { Assert.Equal(1, vertex.HasPoint); Assert.Equal(0.125, vertex.Point.X); Assert.Equal(0.5, vertex.Point.Z); }
                    break;
                case NativeDrawingDisplayProfileModel profile:
                    Assert.IsType<NativeDrawingComponentFeatureReferenceModel>(profile.Header.Component!.Payload);
                    if (edit) profile.Header.Visual.Color = 0x123456;
                    if (changed) Assert.Equal(0x123456u, profile.Header.Visual.Color);
                    break;
                case SldFile.Binary.ExtEntData external:
                    Assert.Empty(external.RawData);
                    var reference = Assert.IsType<SldFile.Binary.SketchExtRefData>(external.NativeReference!.Payload);
                    var backup = Assert.IsType<NativeDrawingPointBackupModel>(reference.NativeBackup!.Payload);
                    Assert.Empty(reference.RawData);
                    if (edit) { external.NativeName = "WireExternalName"; backup.Point = new SldFile.Drawing.DrawingPreviewPoint(0.25, 0.5, 0.75); }
                    if (changed) { Assert.Equal("WireExternalName", external.NativeName); Assert.Equal(0.25, backup.Point.X); Assert.Equal(0.75, backup.Point.Z); }
                    break;
                case NativeDrawingViewLabelModel label:
                    Assert.NotNull(Assert.IsType<DrNoteMfcModel>(label.Note.Item.Header.Component!.Payload).NativeComponent);
                    Assert.True(Assert.IsType<DrViewMfcModel>(label.View!.Payload).NativeView!.Detail);
                    if (edit) { label.State7B8 = "WireViewLabel"; label.Labels[0] = "LabelPrefix"; label.Note.Item.Point290 = new SldFile.Drawing.DrawingPreviewPoint(0.125, 0.25, 0); }
                    if (changed) { Assert.Equal("WireViewLabel", label.State7B8); Assert.Equal("LabelPrefix", label.Labels[0]); Assert.Equal(0.125, label.Note.Item.Point290.X); }
                    break;
                case MoDisplayAngularDimModel angular:
                    var state = Assert.IsType<MoDisplayDimModel.NativeState>(angular.NativeDimension);
                    Assert.True(state.Angular);
                    var handle = Assert.IsType<NativeDrawingDimensionHandleModel>(Assert.Single(state.Handles)!.Payload);
                    var dimension = Assert.IsType<NativeDrawingReferenceDimensionModel>(handle.Dimension!.Payload);
                    var parameter = Assert.IsType<MoAngleParameterModel>(dimension.Dimension.Parameter!.Payload);
                    Assert.True(parameter.NativeWire);
                    if (edit) { parameter.Value += 0.01; editedAngleValue = parameter.Value; state.AngularVectorFB8 = new SldFile.Drawing.DrawingPreviewPoint(0.6, 0.8, 0); }
                    if (changed) { Assert.Equal(editedAngleValue!.Value, parameter.Value); Assert.Equal(0.6, state.AngularVectorFB8.X); Assert.Equal(0.8, state.AngularVectorFB8.Y); }
                    break;
                case MoDisplayRadialDimModel radial:
                    var radialState = Assert.IsType<MoDisplayDimModel.NativeState>(radial.NativeDimension);
                    var tail = Assert.IsType<MoDisplayDimModel.NativeRadialState>(radialState.RadialDimension);
                    var radialHandle = Assert.IsType<NativeDrawingDimensionHandleModel>(Assert.Single(radialState.Handles)!.Payload);
                    var radialDimension = Assert.IsType<NativeDrawingReferenceDimensionModel>(radialHandle.Dimension!.Payload);
                    Assert.True(radialDimension.SingleEntity);
                    var tolerance = Assert.IsType<SldFile.Binary.SketchData.NativeDimensionTolerance>(radialDimension.Dimension.Tolerance!.Payload);
                    if (edit) { tolerance.Value10 = 0.000125; tolerance.String20 = "WireTolerance"; tail.Point1010 = new SldFile.Drawing.DrawingPreviewPoint(0.125, 0.25, 0); }
                    if (changed) { Assert.Equal(0.000125, tolerance.Value10); Assert.Equal("WireTolerance", tolerance.String20); Assert.Equal(0.125, tail.Point1010.X); }
                    break;
                default: throw new InvalidOperationException();
            }
        }
        var original = Prefix(); NativeDrawingObjectGraph.Node? node = null;
        using var input = MfcArchive.CreateReader(bytes); Context(input); input.Position = 36;
        Assert.Throws<EdgeBoundary>(() => Graph(original).Object(input, ref node));
        Assert.InRange(input.Position, minimumEnd, maximumEnd); CheckOrEdit(original, false, false);
        using var output = MfcArchive.CreateWriter(); Context(output);
        Assert.Throws<EdgeBoundary>(() => NativeDrawingModelFactory.CreateGraph().Object(output, ref node));
        Assert.Equal(bytes.AsSpan(36, input.Position - 36).ToArray(), output.ToArray());
        CheckOrEdit(original, true, false);
        using var edited = MfcArchive.CreateWriter(); Context(edited);
        Assert.Throws<EdgeBoundary>(() => NativeDrawingModelFactory.CreateGraph().Object(edited, ref node));
        var changed = Prefix(); NativeDrawingObjectGraph.Node? changedNode = null;
        using var reload = MfcArchive.CreateReader(edited.ToArray()); Context(reload);
        Assert.Throws<EdgeBoundary>(() => Graph(changed).Object(reload, ref changedNode));
        Assert.Equal(0, reload.Remaining); CheckOrEdit(changed, false, true);
        Assert.Equal(0u, input.Context.NativeDrawingArchiveConditions); Assert.Null(input.Context.NativeDrawingOwnerIsDrawing);
        Assert.Equal(0u, reload.Context.NativeDrawingArchiveConditions); Assert.Null(reload.Context.NativeDrawingOwnerIsDrawing);
    }

    [Fact]
    public void DefinitionDistanceDimension_ReusesSketchDimensionAndParameter_AndRebuildsValueAndPosition()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        void Context(MfcArchive ar)
        {
            ar.DocumentVersion = 18000; ar.Context.NativeSketchIncludesOwnerProfile = false;
            ar.Context.NativeAnnotationReferenceIsExternal = false;
        }
        NativeDrawingObjectGraph Graph(DistanceDimensionPrefix dimension)
        {
            var root = new PlanePrefixRoot();
            var sheet = new SheetPlanePrefix { IncludesProfile = true, IncludesDetail = true, IncludesTemplate = true, IncludesView = true };
            bool first = true;
            return NativeDrawingModelFactory.CreateGraph((n, s) =>
            {
                if (n == "moDrawing_c") return root;
                if (n == "moDrSheet") return sheet;
                if (n == MoDisplayDistanceDimModel.MfcClassName && first) { first = false; return dimension; }
                return null;
            });
        }
        MoLengthParameterModel Parameter(DistanceDimensionPrefix dimension)
        {
            var handle = Assert.IsType<NativeDrawingDimensionHandleModel>(Assert.Single(dimension.Model.NativeDimension!.Handles)!.Payload);
            var reference = Assert.IsType<NativeDrawingReferenceDimensionModel>(handle.Dimension!.Payload);
            Assert.IsType<SldFile.Binary.SketchData.NativeDimension>(reference.Dimension);
            return Assert.IsType<MoLengthParameterModel>(reference.Dimension.Parameter!.Payload);
        }
        var prefix = new DistanceDimensionPrefix(); NativeDrawingObjectGraph.Node? node = null;
        using var input = MfcArchive.CreateReader(bytes); Context(input); input.Position = 36;
        Assert.Throws<EdgeBoundary>(() => Graph(prefix).Object(input, ref node));
        Assert.InRange(input.Position, 170658, 171830);
        var state = prefix.Model.NativeDimension!; Assert.NotNull(Assert.IsType<DrDimensionMfcModel>(state.Backup!.Payload).NativeBackup);
        var parameter = Parameter(prefix); Assert.True(parameter.NativeWire);
        using var output = MfcArchive.CreateWriter(); Context(output);
        Assert.Throws<EdgeBoundary>(() => NativeDrawingModelFactory.CreateGraph().Object(output, ref node));
        Assert.Equal(bytes.AsSpan(36, input.Position - 36).ToArray(), output.ToArray());
        parameter.Value += 0.001;
        state.Point958 = new SldFile.Drawing.DrawingPreviewPoint(0.25, 0.5, 0);
        using var edited = MfcArchive.CreateWriter(); Context(edited);
        Assert.Throws<EdgeBoundary>(() => NativeDrawingModelFactory.CreateGraph().Object(edited, ref node));
        var changed = new DistanceDimensionPrefix(); NativeDrawingObjectGraph.Node? changedNode = null;
        using var reload = MfcArchive.CreateReader(edited.ToArray()); Context(reload);
        Assert.Throws<EdgeBoundary>(() => Graph(changed).Object(reload, ref changedNode));
        Assert.Equal(0, reload.Remaining); Assert.Equal(parameter.Value, Parameter(changed).Value);
        Assert.Equal(0.25, changed.Model.NativeDimension!.Point958.X); Assert.Equal(0.5, changed.Model.NativeDimension.Point958.Y);
        Assert.Equal(0u, input.Context.NativeDrawingArchiveConditions); Assert.Null(input.Context.NativeDrawingOwnerIsDrawing);
        Assert.Equal(0u, reload.Context.NativeDrawingArchiveConditions); Assert.Null(reload.Context.NativeDrawingOwnerIsDrawing);
    }

    [Fact]
    public void DefinitionCenterMark_ReusesSketchHandlesAndAnnotationBases_AndRebuildsCircleRadius()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        void Context(MfcArchive ar)
        {
            ar.DocumentVersion = 18000; ar.Context.NativeSketchIncludesOwnerProfile = false;
            ar.Context.NativeAnnotationReferenceIsExternal = false;
        }
        NativeDrawingObjectGraph Graph(CenterMarkPrefix mark)
        {
            var root = new PlanePrefixRoot();
            var sheet = new SheetPlanePrefix { IncludesProfile = true, IncludesDetail = true, IncludesTemplate = true, IncludesView = true };
            bool first = true;
            return NativeDrawingModelFactory.CreateGraph((n, s) =>
            {
                if (n == "moDrawing_c") return root;
                if (n == "moDrSheet") return sheet;
                if (n == "moDisplayCenterMarkSym_c" && first) { first = false; return mark; }
                return null;
            });
        }
        NativeDrawingCircleReferenceModel Circle(CenterMarkPrefix mark)
        {
            var component = Assert.IsType<DrNoteMfcModel.NativeComponentState>(mark.Model.Item.Header.Component!.Payload);
            var data = Assert.IsType<NativeDrawingCenterMarkDataModel>(component.Item!.Payload);
            // This symbol links an existing sketch entity through its circle
            // reference; its optional owned profile is null in this fixture.
            Assert.Null(data.Profile);
            Assert.Equal(14, data.Detail.Attachments.Length);
            var point = Assert.IsType<NativeDrawingAttachPointModel>(data.Detail.Attachments[0]!.Payload);
            Assert.True(point.Parametric);
            var circle = Assert.IsType<NativeDrawingCircleReferenceModel>(point.Reference118!.Payload);
            var handle = Assert.IsType<SldFile.Binary.CompSketchEntHandleData>(circle.Target50!.Payload);
            Assert.IsAssignableFrom<SldFile.Binary.EntHandleData>(handle.NativeHandle!.Payload);
            return circle;
        }
        var prefix = new CenterMarkPrefix(); NativeDrawingObjectGraph.Node? node = null;
        using var input = MfcArchive.CreateReader(bytes); Context(input); input.Position = 36;
        Assert.Throws<EdgeBoundary>(() => Graph(prefix).Object(input, ref node));
        Assert.InRange(input.Position, 169878, 170657);
        Assert.Equal(0u, input.Context.NativeDrawingArchiveConditions); Assert.Null(input.Context.NativeDrawingOwnerIsDrawing);
        var circle = Circle(prefix); Assert.True(circle.Radius128 > 0);
        using var output = MfcArchive.CreateWriter(); Context(output);
        Assert.Throws<EdgeBoundary>(() => NativeDrawingModelFactory.CreateGraph().Object(output, ref node));
        Assert.Equal(bytes.AsSpan(36, input.Position - 36).ToArray(), output.ToArray());
        circle.Radius128 += 0.001;
        using var edited = MfcArchive.CreateWriter(); Context(edited);
        Assert.Throws<EdgeBoundary>(() => NativeDrawingModelFactory.CreateGraph().Object(edited, ref node));
        var changed = new CenterMarkPrefix(); NativeDrawingObjectGraph.Node? changedNode = null;
        using var reload = MfcArchive.CreateReader(edited.ToArray()); Context(reload);
        Assert.Throws<EdgeBoundary>(() => Graph(changed).Object(reload, ref changedNode));
        Assert.Equal(0, reload.Remaining); Assert.Equal(circle.Radius128, Circle(changed).Radius128);
        Assert.Equal(0u, reload.Context.NativeDrawingArchiveConditions); Assert.Null(reload.Context.NativeDrawingOwnerIsDrawing);
    }

    [Fact]
    public void DefinitionDimensionEdge_ReusesNativeTopologyAndSketchDimensionBase_AndRebuildsEdits()
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        void Context(MfcArchive ar)
        {
            ar.DocumentVersion = 18000; ar.Context.NativeSketchIncludesOwnerProfile = false;
            ar.Context.NativeAnnotationReferenceIsExternal = false;
        }
        NativeDrawingObjectGraph Graph(EdgePrefix edge)
        {
            var root = new PlanePrefixRoot();
            var sheet = new SheetPlanePrefix { IncludesProfile = true, IncludesDetail = true, IncludesTemplate = true, IncludesView = true };
            bool first = true;
            return NativeDrawingModelFactory.CreateGraph((n, s) =>
            {
                if (n == "moDrawing_c") return root;
                if (n == "moDrSheet") return sheet;
                if (n == MfcCompEdgeModel.MfcClassName && first) { first = false; return edge; }
                return null;
            });
        }
        var prefix = new EdgePrefix(); NativeDrawingObjectGraph.Node? node = null;
        using var input = MfcArchive.CreateReader(bytes); Context(input); input.Position = 36;
        Assert.Throws<EdgeBoundary>(() => Graph(prefix).Object(input, ref node));
        Assert.InRange(input.Position, 159458, 160315);
        Assert.Equal(0u, input.Context.NativeDrawingArchiveConditions); Assert.Null(input.Context.NativeDrawingOwnerIsDrawing);
        var edge = prefix.Model.NativeEdge!; Assert.Null(prefix.Model.RawData);
        var reference = Assert.IsType<MfcEdgeRefModel>(edge.EntityReference!.Payload);
        Assert.NotNull(reference.NativeReference); Assert.Null(reference.RawData);
        Assert.Contains(reference.NativeReference!.EntityIds, id => id?.Payload is SldFile.Binary.EndFaceSurfIdRepData);
        using var output = MfcArchive.CreateWriter(); Context(output);
        Assert.Throws<EdgeBoundary>(() => NativeDrawingModelFactory.CreateGraph().Object(output, ref node));
        Assert.Equal(bytes.AsSpan(36, input.Position - 36).ToArray(), output.ToArray());
        Assert.Equal(0u, output.Context.NativeDrawingArchiveConditions); Assert.Null(output.Context.NativeDrawingOwnerIsDrawing);
        edge.Point158 = new SldFile.Drawing.DrawingPreviewPoint(0.125, 0.25, 0.5);
        using var edited = MfcArchive.CreateWriter(); Context(edited);
        Assert.Throws<EdgeBoundary>(() => NativeDrawingModelFactory.CreateGraph().Object(edited, ref node));
        var changed = new EdgePrefix(); NativeDrawingObjectGraph.Node? changedNode = null;
        using var reload = MfcArchive.CreateReader(edited.ToArray()); Context(reload);
        Assert.Throws<EdgeBoundary>(() => Graph(changed).Object(reload, ref changedNode));
        Assert.Equal(0, reload.Remaining); Assert.Equal(0.125, changed.Model.NativeEdge!.Point158.X);
        Assert.Equal(0.25, changed.Model.NativeEdge.Point158.Y); Assert.Equal(0.5, changed.Model.NativeEdge.Point158.Z);
    }

    [Theory]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 83364)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 101185)]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 156827)]
    public void DefinitionTemplate_ReusesSheetSketchAndAnchorCodecs_AndRebuildsOwnedEdits(
        string directory, string name, int version, int prefixEnd)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        SheetPlanePrefix Prefix() => new SheetPlanePrefix { IncludesProfile = true, IncludesDetail = true, IncludesTemplate = true };
        NativeDrawingObjectGraph Graph(PlanePrefixRoot root, SheetPlanePrefix sheet) =>
            NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" ? root : n == "moDrSheet" ? sheet : null);
        void Context(MfcArchive ar)
        {
            ar.DocumentVersion = version; ar.Context.NativeSketchIncludesOwnerProfile = false;
            ar.Context.NativeAnnotationReferenceIsExternal = false;
        }
        var root = new PlanePrefixRoot(); var firstSheet = Prefix();
        using var input = MfcArchive.CreateReader(bytes); Context(input); input.Position = 36;
        NativeDrawingObjectGraph.Node? rootNode = null; Graph(root, firstSheet).Object(input, ref rootNode);
        Assert.Equal(prefixEnd, input.Position); Assert.True(input.Remaining > 0);
        var template = Assert.IsType<DrTemplateMfcModel>(firstSheet.Template!.Payload);
        Assert.Equal(DrTemplateMfcModel.NativeClassName, firstSheet.Template.ClassName); Assert.Null(template.RawData);
        var state = Assert.IsType<DrTemplateMfcModel.NativeState>(template.NativeTemplate);
        var children = state.Sheet.Feature.Feature.ResolvedChildren.Values;
        Assert.Contains(children, node => node?.Payload is SketchFeatureModel { NativeProfile: not null });
        Assert.Contains(children, node => node?.ClassName == "moGeneralTableAnchor_c");
        var anchor = Assert.IsType<NativeDrawingTableAnchorModel>(children.Single(node => node?.ClassName == "moGeneralTableAnchor_c")!.Payload);
        var bom = Assert.IsType<DrBomTableMfcModel>(children.Single(node => node?.ClassName == "moBomTemplate_c")!.Payload);
        Assert.NotNull(bom.NativeAnchor); Assert.Null(bom.RawData);
        using var output = MfcArchive.CreateWriter(); Context(output);
        NativeDrawingModelFactory.CreateGraph().Object(output, ref rootNode);
        Assert.Equal(bytes.AsSpan(36, prefixEnd - 36).ToArray(), output.ToArray());
        state.Sheet.State3E0 += 0.001;
        anchor.Position = new SldFile.Drawing.DrawingPreviewPoint(0.125, 0.25, 0.5);
        using var edited = MfcArchive.CreateWriter(); Context(edited);
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref rootNode);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); Context(reload);
        var changedRoot = new PlanePrefixRoot(); var changedSheet = Prefix(); NativeDrawingObjectGraph.Node? changedNode = null;
        Graph(changedRoot, changedSheet).Object(reload, ref changedNode); Assert.Equal(0, reload.Remaining);
        var changed = Assert.IsType<DrTemplateMfcModel>(changedSheet.Template!.Payload).NativeTemplate!;
        Assert.Equal(state.Sheet.State3E0, changed.Sheet.State3E0);
        var changedAnchor = Assert.IsType<NativeDrawingTableAnchorModel>(changed.Sheet.Feature.Feature.ResolvedChildren.Values.Single(node => node?.ClassName == "moGeneralTableAnchor_c")!.Payload);
        Assert.Equal(0.125, changedAnchor.Position.X); Assert.Equal(0.25, changedAnchor.Position.Y); Assert.Equal(0.5, changedAnchor.Position.Z);
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 12239)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 8204)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 9593)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, 9788)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, 10904)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, 10454)]
    [InlineData("Drawings", "MacroBenchmark/Mold/bezel moldbase.slddrw", 19000, 15267)]
    public void DefinitionProfile_UsesExistingSketchAndComponentPlaneTypes_WithoutRawFallback(
        string directory, string name, int version, int prefixEnd)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        var model = new PlanePrefixRoot(); var sheet = new SheetPlanePrefix { IncludesProfile = true };
        NativeDrawingObjectGraph Graph(PlanePrefixRoot root, SheetPlanePrefix firstSheet) =>
            NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" ? root : n == "moDrSheet" ? firstSheet : null);
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = version; input.Position = 36;
        input.Context.NativeSketchIncludesOwnerProfile = false;
        NativeDrawingObjectGraph.Node? rootNode = null; Graph(model, sheet).Object(input, ref rootNode);
        Assert.Equal(prefixEnd, input.Position); Assert.True(input.Remaining > 0);
        Assert.Null(input.Context.NativeSketchOwnerIs3D);
        var profile = Assert.IsType<SketchFeatureModel>(sheet.Profile!.Payload);
        Assert.Empty(profile.RawData); var native = Assert.IsType<SketchFeatureModel.NativeProfileState>(profile.NativeProfile);
        Assert.Equal(0, native.State3D4); Assert.Equal("sgSketch", native.Sketch!.ClassName);
        var sketch = Assert.IsType<SldFile.Binary.SketchData>(native.Sketch.Payload);
        Assert.Empty(sketch.RawData); Assert.Empty(sketch.SketchBody);
        Assert.Equal("moCompRefPlane_c", native.Plane!.ClassName);
        var plane = Assert.IsType<SldFile.Binary.CompRefPlaneData>(native.Plane.Payload);
        Assert.Empty(plane.RawData); Assert.Empty(plane.UnparsedTail);
        Assert.NotEqual(0, native.Body.Marker);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        output.Context.NativeSketchIncludesOwnerProfile = false;
        NativeDrawingModelFactory.CreateGraph().Object(output, ref rootNode);
        Assert.Equal(bytes.AsSpan(36, prefixEnd - 36).ToArray(), output.ToArray());

        // Edit owned typed fields and rebuild through the same graph; this
        // checks wire ownership, not native geometric rebuild or COM save.
        plane.NativeFeatureIndex = 42; native.Transform.Translation = new SldFile.Drawing.DrawingPreviewPoint(0.125, 0.25, 0.5);
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = version;
        edited.Context.NativeSketchIncludesOwnerProfile = false;
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref rootNode);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = version;
        reload.Context.NativeSketchIncludesOwnerProfile = false;
        var changed = new PlanePrefixRoot(); var changedSheet = new SheetPlanePrefix { IncludesProfile = true };
        NativeDrawingObjectGraph.Node? changedRoot = null; Graph(changed, changedSheet).Object(reload, ref changedRoot);
        Assert.Equal(0, reload.Remaining);
        var changedProfile = Assert.IsType<SketchFeatureModel>(changedSheet.Profile!.Payload).NativeProfile!;
        Assert.Equal(42, Assert.IsType<SldFile.Binary.CompRefPlaneData>(changedProfile.Plane!.Payload).NativeFeatureIndex);
        Assert.Equal(0.125, changedProfile.Transform.Translation.X);
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 12528, false)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 9846, true)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 11625, true)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, 10099, false)]
    public void DefinitionDetailFolder_UsesItsFeatureBaseAndInlineLists_AndReusesTheNativeTransform(
        string directory, string name, int version, int prefixEnd, bool hasNote)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        var model = new PlanePrefixRoot(); var sheet = new SheetPlanePrefix { IncludesProfile = true, IncludesDetail = true };
        NativeDrawingObjectGraph Graph(PlanePrefixRoot root, SheetPlanePrefix firstSheet) =>
            NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" ? root : n == "moDrSheet" ? firstSheet : null);
        using var input = MfcArchive.CreateReader(bytes); input.DocumentVersion = version; input.Position = 36;
        input.Context.NativeSketchIncludesOwnerProfile = false;
        input.Context.NativeAnnotationReferenceIsExternal = false;
        NativeDrawingObjectGraph.Node? rootNode = null; Graph(model, sheet).Object(input, ref rootNode);
        Assert.Equal(prefixEnd, input.Position); Assert.True(input.Remaining > 0);
        var detail = Assert.IsType<NativeDrawingDetailFolderModel>(sheet.Detail!.Payload);
        Assert.Equal(34, detail.Lists.Length);
        var owned = detail.Lists.SelectMany(list => list.Values).ToArray();
        if (hasNote)
        {
            var noteNode = Assert.Single(owned)!; Assert.Equal("moDisplayNote_c", noteNode.ClassName);
            var note = Assert.IsType<DrNoteMfcModel>(noteNode.Payload); Assert.Null(note.RawData);
            var native = Assert.IsType<DrNoteMfcModel.NativeDisplayState>(note.NativeDisplay);
            Assert.Same(sheet.Detail, native.Item.Header.Owner);
            var component = Assert.IsType<DrNoteMfcModel>(native.Item.Header.Component!.Payload);
            var data = Assert.IsType<DrNoteMfcModel>(component.NativeComponent!.Item!.Payload);
            var text = Assert.IsType<NativeDrawingDimTextModel>(data.NativeData!.Text!.Payload);
            Assert.Equal("ABCD", text.Text); Assert.Same(noteNode, text.DisplayItem);
        }
        else Assert.Empty(owned);
        Assert.Null(detail.Bom); Assert.Equal(0, detail.HasBucket);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        output.Context.NativeSketchIncludesOwnerProfile = false;
        output.Context.NativeAnnotationReferenceIsExternal = false;
        NativeDrawingModelFactory.CreateGraph().Object(output, ref rootNode);
        Assert.Equal(bytes.AsSpan(36, prefixEnd - 36).ToArray(), output.ToArray());
        detail.Transform.Translation = new SldFile.Drawing.DrawingPreviewPoint(0.25, 0.5, 0.75);
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = version;
        edited.Context.NativeSketchIncludesOwnerProfile = false;
        edited.Context.NativeAnnotationReferenceIsExternal = false;
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref rootNode);
        using var reload = MfcArchive.CreateReader(edited.ToArray()); reload.DocumentVersion = version;
        reload.Context.NativeSketchIncludesOwnerProfile = false;
        reload.Context.NativeAnnotationReferenceIsExternal = false;
        var changed = new PlanePrefixRoot(); var changedSheet = new SheetPlanePrefix { IncludesProfile = true, IncludesDetail = true };
        NativeDrawingObjectGraph.Node? changedRoot = null; Graph(changed, changedSheet).Object(reload, ref changedRoot);
        Assert.Equal(0, reload.Remaining);
        Assert.Equal(0.25, Assert.IsType<NativeDrawingDetailFolderModel>(changedSheet.Detail!.Payload).Transform.Translation.X);
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 11532)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 7581)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 8878)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, 9002)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, 10118)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, 9668)]
    [InlineData("Drawings", "MacroBenchmark/Mold/bezel moldbase.slddrw", 19000, 14552)]
    public void DefinitionReferencePlane_TraversesGeometryBase_AndSerializesOwnedBoundsEdits(
        string directory, string name, int version, int prefixEnd)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        using var ar = MfcArchive.CreateReader(bytes); ar.DocumentVersion = version; ar.Position = 36;
        var model = new PlanePrefixRoot();
        var sheet = new SheetPlanePrefix(); NativeDrawingObjectGraph.Node? root = null;
        NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" && s == 1 ? model : n == "moDrSheet" && s == 1 ? sheet : null).Object(ar, ref root);
        Assert.Equal(prefixEnd, ar.Position); Assert.True(ar.Remaining > 0);
        Assert.Equal("moRefPlane_c", sheet.Plane!.ClassName);
        var plane = Assert.IsType<NativeDrawingReferencePlaneModel>(sheet.Plane.Payload);
        var data = Assert.IsType<NativeDrawingPlaneDataModel>(plane.PlaneData!.Payload);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        NativeDrawingModelFactory.CreateGraph().Object(output, ref root);
        Assert.Equal(bytes.AsSpan(36, prefixEnd - 36).ToArray(), output.ToArray());
        double editedBound = data.Bounds[0] + 0.001, originalOtherBound = data.Bounds[1];
        data.Bounds[0] = editedBound;
        plane.Geometry.Feature.Feature.Visual.Color = 0x00345678;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = version;
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref root);
        using var input = MfcArchive.CreateReader(edited.ToArray()); input.DocumentVersion = version;
        var changed = new PlanePrefixRoot(); var changedSheet = new SheetPlanePrefix(); NativeDrawingObjectGraph.Node? changedRoot = null;
        NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" && s == 1 ? changed : n == "moDrSheet" && s == 1 ? changedSheet : null).Object(input, ref changedRoot);
        Assert.Equal(0, input.Remaining);
        var changedPlane = Assert.IsType<NativeDrawingReferencePlaneModel>(changedSheet.Plane!.Payload);
        var changedData = Assert.IsType<NativeDrawingPlaneDataModel>(changedPlane.PlaneData!.Payload);
        Assert.Equal(editedBound, changedData.Bounds[0]); Assert.Equal(originalOtherBound, changedData.Bounds[1]);
        Assert.Equal(0x00345678u, changedPlane.Geometry.Feature.Feature.Visual.Color);
    }

    private sealed class FolderPrefixRoot : IMfcSerializable
    {
        internal NativeDrawingModelCommonModel Common = new NativeDrawingModelCommonModel();
        internal uint FeatureCount;
        internal NativeDrawingObjectGraph.Node?[] FirstFolders = new NativeDrawingObjectGraph.Node?[3];
        public void Serialize(MfcArchive ar)
        {
            Common.Serialize(ar); ar.Count(ref FeatureCount);
            // Deliberate prefix fixture: later feature types are not decoded here.
            for (int i = 0; i < FirstFolders.Length; i++)
                NativeDrawingModelFactory.Graph(ar).Object(ar, ref FirstFolders[i]);
        }
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 10579)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 7056)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 7915)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, 8019)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, 9135)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, 8685)]
    [InlineData("Drawings", "MacroBenchmark/Mold/bezel moldbase.slddrw", 19000, 13569)]
    public void DefinitionDetailCabinet_UsesInlineObjectArrays_AndSerializesScaleChanges(
        string directory, string name, int version, int prefixEnd)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        using var ar = MfcArchive.CreateReader(bytes); ar.DocumentVersion = version; ar.Position = 36;
        var model = new FolderPrefixRoot { FirstFolders = new NativeDrawingObjectGraph.Node?[4] };
        NativeDrawingObjectGraph.Node? root = null;
        NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" && s == 1 ? model : null).Object(ar, ref root);
        Assert.Equal(prefixEnd, ar.Position);
        var cabinet = Assert.IsType<NativeDrawingDetailCabinetModel>(model.FirstFolders[3]!.Payload);
        Assert.Empty(cabinet.Objects); Assert.Empty(cabinet.Details); Assert.Empty(cabinet.ImportedAnnotations);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        NativeDrawingModelFactory.CreateGraph().Object(output, ref root);
        Assert.Equal(bytes.AsSpan(36, prefixEnd - 36).ToArray(), output.ToArray());
        double originalScaleY = cabinet.ScaleY;
        cabinet.ScaleX = 2.5;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = version;
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref root);
        using var input = MfcArchive.CreateReader(edited.ToArray()); input.DocumentVersion = version;
        var changed = new FolderPrefixRoot { FirstFolders = new NativeDrawingObjectGraph.Node?[4] };
        NativeDrawingObjectGraph.Node? changedRoot = null;
        NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" && s == 1 ? changed : null).Object(input, ref changedRoot);
        Assert.Equal(0, input.Remaining);
        var changedCabinet = Assert.IsType<NativeDrawingDetailCabinetModel>(changed.FirstFolders[3]!.Payload);
        Assert.Equal(2.5, changedCabinet.ScaleX); Assert.Equal(originalScaleY, changedCabinet.ScaleY);
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 10371)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 6854)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 7689)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, 7793)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, 8909)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, 8459)]
    [InlineData("Drawings", "MacroBenchmark/Mold/bezel moldbase.slddrw", 19000, 13343)]
    public void DefinitionFolders_DeserializeNativeFeatureBases_AndSerializeOwnedDisplayPropertyEdits(
        string directory, string name, int version, int prefixEnd)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        using var ar = MfcArchive.CreateReader(bytes); ar.DocumentVersion = version; ar.Position = 36;
        var model = new FolderPrefixRoot();
        var graph = NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" && s == 1 ? model : null);
        NativeDrawingObjectGraph.Node? root = null; graph.Object(ar, ref root);
        Assert.Equal(prefixEnd, ar.Position);
        Assert.True(ar.Remaining > 0);
        Assert.Equal(new[] { "moCommentsFolder_c", "moBlockFolder_c", "moDocsFolder_c" },
            model.FirstFolders.Select(folder => folder!.ClassName));
        var folders = model.FirstFolders.Select(folder => Assert.IsType<NativeDrawingFolderModel>(folder!.Payload)).ToArray();
        Assert.All(folders, folder => Assert.Empty(folder.Feature.ResolvedChildren.Values));
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        NativeDrawingModelFactory.CreateGraph().Object(output, ref root);
        Assert.Equal(bytes.AsSpan(36, prefixEnd - 36).ToArray(), output.ToArray());

        folders[1].Feature.Visual.Color = 0x00123456;
        using var edited = MfcArchive.CreateWriter(); edited.DocumentVersion = version;
        NativeDrawingModelFactory.CreateGraph().Object(edited, ref root);
        using var input = MfcArchive.CreateReader(edited.ToArray()); input.DocumentVersion = version;
        var changed = new FolderPrefixRoot(); NativeDrawingObjectGraph.Node? changedRoot = null;
        NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" && s == 1 ? changed : null).Object(input, ref changedRoot);
        Assert.Equal(0, input.Remaining);
        Assert.Equal(0x00123456u, Assert.IsType<NativeDrawingFolderModel>(changed.FirstFolders[1]!.Payload).Feature.Visual.Color);
        Assert.Equal(folders[0].Feature.Visual.Color, Assert.IsType<NativeDrawingFolderModel>(changed.FirstFolders[0]!.Payload).Feature.Visual.Color);
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 9671)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 6260)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 6959)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, 7047)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, 8163)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, 7713)]
    [InlineData("Drawings", "MacroBenchmark/Mold/bezel moldbase.slddrw", 19000, 12597)]
    public void DefinitionModelHeader_PreservesUnitsAndSelfReferences_WithoutClaimingTheRemainingGraph(
        string directory, string name, int version, int headerEnd)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        using var ar = MfcArchive.CreateReader(bytes); ar.DocumentVersion = version; ar.Position = 36;
        var model = new NativeDrawingModelHeaderModel();
        var graph = NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" && s == 1 ? model : null);
        NativeDrawingObjectGraph.Node? root = null; graph.Object(ar, ref root);
        Assert.Equal(headerEnd, ar.Position);
        Assert.True(ar.Remaining > 0);
        var component = Assert.IsType<NativeDrawingComponentReferenceModel>(model.Component!.Payload);
        Assert.Same(root, component.Owner);
        var units = Assert.IsType<NativeDrawingObjectListModel>(model.Units!.Payload);
        Assert.NotEmpty(units.Values);
        Assert.Contains(units.Values, unit => unit?.ClassName == "moLengthUserUnits_c");
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        NativeDrawingModelFactory.CreateGraph().Object(output, ref root);
        Assert.Equal(bytes.AsSpan(36, headerEnd - 36).ToArray(), output.ToArray());
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", 18000, 7934)]
    [InlineData("Solidworks2012", "cube.SLDDRW", 5000, 4932)]
    [InlineData("Solidworks2026", "cube.SLDDRW", 19000, 5304)]
    [InlineData("Drawings", "MacroBenchmark/carrier.slddrw", 19000, 5318)]
    [InlineData("Drawings", "MacroBenchmark/cavity.slddrw", 19000, 6504)]
    [InlineData("Drawings", "MacroBenchmark/punch_holder.slddrw", 19000, 6042)]
    [InlineData("Drawings", "MacroBenchmark/Mold/bezel moldbase.slddrw", 19000, 10854)]
    public void DefinitionHeader_TraversesSharedMetadataObjects_AndRoundtripsTheHeaderOnly(
        string directory, string name, int version, int headerEnd)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(directory, name));
        byte[] bytes = file.GetDrawingPreviewStreamBytes("Contents/Definition")!;
        using var ar = MfcArchive.CreateReader(bytes); ar.DocumentVersion = version;
        // This test begins at the fixture's root tag, after its 36-byte document
        // preamble. HeaderRoot deliberately stops before the drawing body.
        ar.Position = 36;
        var root = new HeaderRoot();
        var graph = NativeDrawingModelFactory.CreateGraph((n, s) => n == "moDrawing_c" && s == 1 ? root : null);
        NativeDrawingObjectGraph.Node? node = null; graph.Object(ar, ref node);
        Assert.Equal(headerEnd, ar.Position);
        var header = Assert.IsType<NativeDrawingHeaderModel>(root.Header!.Payload);
        var users = Assert.IsType<NativeDrawingStringArrayModel>(header.Users!.Payload);
        Assert.NotEmpty(users.Values);
        using var writer = MfcArchive.CreateWriter(); writer.DocumentVersion = version;
        NativeDrawingModelFactory.CreateGraph().Object(writer, ref node);
        Assert.Equal(bytes.AsSpan(36, headerEnd - 36).ToArray(), writer.ToArray());
    }

    [Fact]
    public void ObjectAndClassReferences_ShareTheNativeIndexSpace()
    {
        // Native WriteObject order: class=1, first object=2, second object=3.
        byte[] bytes = Convert.FromHexString("FFFF01000400746573742A00000001802B00000002000000");
        var graph = new NativeDrawingObjectGraph((name, schema) =>
        {
            Assert.Equal("test", name); Assert.Equal(1, schema); return new Scalar();
        });
        NativeDrawingObjectGraph.Node? first = null, second = null, reference = null, empty = null;
        using (var ar = MfcArchive.CreateReader(bytes))
        {
            graph.Object(ar, ref first); graph.Object(ar, ref second);
            graph.Object(ar, ref reference); graph.Object(ar, ref empty);
            Assert.Equal(0, ar.Remaining);
        }
        Assert.Equal(42, ((Scalar)first!.Payload).Value);
        Assert.Equal(43, ((Scalar)second!.Payload).Value);
        Assert.Same(first, reference); Assert.Null(empty);
        var writerGraph = new NativeDrawingObjectGraph((_, _) => throw new InvalidOperationException());
        using var output = MfcArchive.CreateWriter();
        writerGraph.Object(output, ref first); writerGraph.Object(output, ref second);
        writerGraph.Object(output, ref reference); writerGraph.Object(output, ref empty);
        Assert.Equal(bytes, output.ToArray());
    }

    [Fact]
    public void NullArrayEntries_DoNotInventALegacyPrefixOrConsumeClassIndices()
    {
        byte[] bytes = Convert.FromHexString("0000000000000000FFFF01000400746573742A000000");
        var graph = new NativeDrawingObjectGraph((_, _) => new Scalar());
        using var ar = MfcArchive.CreateReader(bytes);
        NativeDrawingObjectGraph.Node? node = null;
        for (int i = 0; i < 4; i++) { graph.Object(ar, ref node); Assert.Null(node); }
        graph.Object(ar, ref node);
        Assert.Equal(42, ((Scalar)node!.Payload).Value);
        Assert.Equal(0, ar.Remaining);
    }

    [Fact]
    public void ObjectReferences_CannotReferToClassEntries()
    {
        byte[] bytes = Convert.FromHexString("FFFF01000400746573742A0000000100");
        var graph = new NativeDrawingObjectGraph((_, _) => new Scalar());
        using var ar = MfcArchive.CreateReader(bytes);
        NativeDrawingObjectGraph.Node? node = null;
        graph.Object(ar, ref node);
        Assert.Throws<InvalidDataException>(() => graph.Object(ar, ref node));
    }

    [Theory]
    [InlineData(0x00, 0, 34)]
    [InlineData(0x10, 4, 66)]
    [InlineData(0x20, 6, 82)]
    [InlineData(0x40, 25, 238)]
    public void NativeTransform_ConsumesOnlyItsFlagSelectedPayload(int flags, int doubles, int length)
    {
        // Independent native layout: rotation byte, xyz/scale doubles, state byte,
        // optional Matrix5 version, then the extension selected by that state.
        using var data = new MemoryStream();
        using (var writer = new BinaryWriter(data, System.Text.Encoding.UTF8, true))
        {
            writer.Write((byte)0);
            foreach (double value in new[] { 0.125, -0.25, 0.5, 2.0 }) writer.Write(value);
            writer.Write((byte)flags);
            if (flags == 0x40) writer.Write(0);
            for (int i = 0; i < doubles; i++) writer.Write(i + 0.25);
            writer.Write(0x12345678);
        }
        byte[] bytes = data.ToArray();
        var model = new NativeDrawingTransformModel();
        using var ar = MfcArchive.CreateReader(bytes);
        model.Serialize(ar);
        Assert.Equal(length, ar.Position);
        Assert.Equal(0.125, model.Translation.X); Assert.Equal(2, model.Scale);
        Assert.Equal(doubles, model.Extension.Length);
        int sentinel = 0; ar.I32(ref sentinel); Assert.Equal(0x12345678, sentinel);
        Assert.Equal(bytes.AsSpan(0, length).ToArray(), MfcArchive.Serialize(model));
    }

    [Theory]
    [InlineData(3904, 3, "78563412", 0x12345678)]
    [InlineData(3904, 1, "FE", -2)]
    [InlineData(3904, 2, "FEFF", -2)]
    public void LegacyLineWidth_RetainsItsDeclaredIntegerEncoding(int version, int restoreType, string hex, int expected)
    {
        byte[] bytes = Convert.FromHexString(hex);
        var model = new NativeDrawingLineWidthModel { RestoreType = restoreType };
        using (var ar = MfcArchive.CreateReader(bytes))
        {
            ar.DocumentVersion = version; model.Serialize(ar);
            Assert.Equal(0, ar.Remaining);
        }
        Assert.Equal(expected, model.LegacyValue);
        using var output = MfcArchive.CreateWriter(); output.DocumentVersion = version;
        model.Serialize(output); Assert.Equal(bytes, output.ToArray());
    }
}
