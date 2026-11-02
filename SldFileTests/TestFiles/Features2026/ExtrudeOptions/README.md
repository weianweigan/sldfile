# Extrusion options: evidence slice, 2026-10-30

This slice establishes ReverseDirection reading for native moExtrusion_c streams version 0x70 only. Reverse authoring and the option authoring slices (two directions, draft, start offset) were implemented later; see the Reverse authoring validation and Authoring extension sections below.

## Controlled evidence

Source inputs: FeatureLab/Program.cs extrude_reverse sets Dir=true; the seven controls set Dir=false. Draft cases set Dchk1=true and Dang1=pi/60 or pi/30. Offset cases set T0=3 and StartOffset=.002 or .004. JSON snapshots alongside this report record a native reopen, rebuild=true, physical properties and SHA-256. The reopen happened in the same SOLIDWORKS process; process isolation was not enforced for this reading slice. Fresh-process isolation of authoring is covered separately by the Agent 5 audit (`reverse_workspace/validation/agent5-audit-20261030/manifest.json`).

The relevant field is a uint32 at offset 10 relative to the data immediately after the moEndSpec_c class-name bytes (data includes the two initial zero bytes). The schema starts u16=0, u32 at 2 is 0 or 1, u32 at 6=0, u16 at 14=0. Reverse is exactly 0 or 1; unknown schema/version/class/flag values are not promoted to a value.

| Sample | moEndSpec tag offset | data offset | reverse at data+10 |
| --- | ---: | ---: | ---: |
| baseline | 10105 | 10122 | 0 |
| baseline resave | 10335 | 10352 | 0 |
| reverse | 10105 | 10122 | 1 |
| draft 3deg | 10105 | 10122 | 0 |
| draft 6deg | 10105 | 10122 | 0 |
| offset 2mm | 10105 | 10122 | 0 |
| offset 4mm | 10105 | 10122 | 0 |
| two directions | 10105 | 10122 | 0 |

Baseline resave inserts objects before the end specification, moving its absolute location by 230 bytes. The field and surrounding header remain identical. Reverse also changes the repeated boolean within moFromEndSpec_c, independently supporting the interpretation. The implementation reads the parsed EndSpec child owned by the extrusion, never a file-wide float or byte search.

## Candidates deliberately left unimplemented

Draft3/6 insert moDisplayAngularDim_c -> AngleDim_c -> moAngleParameter_c before moFromEndSpec_c. The angle parameter named D3 has the common named-parameter signature and values pi/60 and pi/30 respectively. The role/enable/direction relationship still needs structural isolation; no generic angle is labelled draft based on its value alone.

Offset2/4 extend moFromEndSpec_c by 1747 bytes. A D19 length parameter through indexed class references has values .002/.004 at relative offset 713; the other copies are geometry/display data. Explicit parameter/reference ownership and start-type mapping remain unfinished.

Both candidates above were promoted to reading and authoring in the second slice and the Authoring extension section below.

## Test assets

Eight native samples were copied to SldFileTests/TestFiles/Features2026/ExtrudeOptions/. ExtrudeOptionReadingTests reads all controls and the reverse sample, also asserting the main depth remains .01m. It additionally rejects unknown stream versions, Cut class, truncated data and non-boolean flag values. Source samples and extracted *.resolved.bin streams remain in samples/ for reproducibility.

Verification: focused ExtrudeOptionReadingTests passed 9/9, zero skipped (2026-10-30). Build succeeded; test execution required the established local testhost socket escalation.

## Second slice: draft and start offset (implemented)

The earlier candidates above are now promoted only under these structural restrictions:

- Single-direction moEndSpec_c: u16+0=0, u32+2=1, u32+6=0, u32+10 in {0,1}, u16+14=0. Other direction layouts are excluded.
- Draft: exactly one moAngleParameter_c, preceded in the same feature by moFavoriteHandle_c -> moDisplayAngularDim_c -> AngleDim_c. The u32 at 24 bytes before the display object's tag must be 1 and must lie within the Favorite object's range; its following 20 bytes must be zero. This flag is zero in baseline/resave/reverse/offset controls and one in both draft amplitudes. The angle parameter must have schema 0x8004, a D<number> WChar name, and the full parameter metadata header: double 2.0, u32 FFFFFFFF, u32 zero, empty WChar, u16 zero, then finite double value. Read results are pi/60 and pi/30 radians. Both DraftWhileExtruding and DraftAngle are bound. DraftOutward and direction-2 draft are still unknown.
- Offset: the feature-owned moFromEndSpec_c starts with internal discriminator 5 (API FromType=3), an indexed reference, and sixteen zero header bytes. Exactly one complete named parameter with the header above is required. At relative offset 713 its scalar is .002/.004; these values are structurally decoded from the named parameter, not selected by floating-point value. FromType and FromOffsetDistance are bound. Flipped offset remains unimplemented.

The generic named-object scanner truncates angle RawData at a FFFF sentinel in metadata (DataStart=12788, DataEnd=12808). Therefore this reader bounds the angle parameter by the next named object within the same feature, and the FromEndSpec block by feature end. It does not alter the generic scanner and never searches outside the owning feature.

All eight native samples are exercised for draft/offset values and unchanged main .01m depth. Added malformed tests cover missing draft enable, truncated offset metadata/value, unknown offset discriminator, duplicate complete parameter blocks and malformed WChar count.

Verification: build succeeded, zero warnings/errors; ExtrudeOptionReadingTests passed 19/19, zero skipped. Two-direction depth was subsequently established with the 3/5/7 mm controls and the resave; see two-direction-evidence.md and the Authoring extension section below.

## Reverse authoring validation
The Boss serializer now emits both the moEndSpec direction flag and the repeated moFromEndSpec flag at data+8, and reverses the bounding-box center. Native injection tests verify depth, API ReverseDirection, 1.5e-6 m3 volume, z centroid -0.0075 m and clean feature status for a 15 mm extrusion. The forward control also passes. See local reverse_workspace/validation/agent5/reverse-authoring.trx (2 passed).

## Authoring extension: two directions, draft, start offset (2026-10-30, Agent 1)

The validated read slices above are now writable through `SldPart.AddExtrude(ExtrudeFeatureDefinitionData)`. Only the controlled combinations are accepted; every other option combination still raises `NotSupportedException` without touching the stream. This supersedes the earlier "two-direction depth is still unfinished" note for the 3/5/7 mm controlled slice.

| Option | Emitted structure (relative offsets) | Tests |
| --- | --- | --- |
| `BothDirections` + `DepthDirection2` | First `moEndSpec_c` data u32@2=0. `moFavoriteHandle_c` data +436: `0x810B` + 16 zero (second indexed EndSpec, Blind/no-reverse); mirrored 556-byte display payload, 107-byte feature-dim-handle payload, `0x810F` + u32 2 + `0x8111`; D2 named parameter at favorite+1111 (schema 0x8004, full header double 2.0/FFFFFFFF/0/empty WChar/u16 0; +D2 at parameter+32, -D2 at +104, +D2 at +430/+454/+592/+616; 628 bytes); `0x8114` + repeated direction-1 display state; 74-byte tail. Favorite data 2251 bytes. BBox center `(D1-D2)/2`, radius `sqrt(0.0002+(D1+D2)^2)`. | `ExtrudeOptionAuthoringTests.TwoDirectionAuthoring_*`; native `InjectTwoDirectionExtrudeOntoSketch_BuildsBodyOnBothSides` |
| `DraftWhileExtruding` + `DraftAngle` | Favorite data 476 bytes: measured prefix [0:342] + marker `2D 00 5C 00` + prefix [346:452] + u32 enable 1 + 20 zeros, immediately followed by `moDisplayAngularDim_c` (692-byte tag+payload), `AngleDim_c` (4-byte `02 00 00 00`) and `moAngleParameter_c` (schema 0x8004, D3 header, angle at parameter+32 and +570, 1089 bytes). `DraftOutward` and direction-2 draft remain unsupported. | `ExtrudeOptionAuthoringTests.DraftAuthoring_*`; native `InjectDraftExtrudeOntoSketch_BuildsTaperedBody` |
| `FromType=3` + `FromOffsetDistance` | `moFromEndSpec_c` data: u32 discriminator 5, `0x810B` + display stack, `0x810D` + handle, `0x810F` + u32 2, `0x8111` + D19 named parameter (schema 0x8004; value at parameter+34/+106/+271/+384/+408/+594/+618; 630 bytes), `0x8114` + favorite prefix [0:436], standard 40-byte tail; 1787 bytes. BBox center `D/2+offset`. `FromOffsetReverse` remains unsupported. | `ExtrudeOptionAuthoringTests.StartOffsetAuthoring_*`; native `InjectOffsetExtrudeOntoSketch_StartsFromOffsetPlane` |

All option scalars are emitted field-by-field by `ExtrudeOptionEmitter`; `ExtrudeSegments` only holds the measured surrounding class payload/display metadata, and a static layout guard fails fast if those segment lengths change.

Native verification (2026-10-30): `ExtrudeIntegrationTests` 7/7 and `ChallengerExtrudeIntegrationTests` 9/9 passed in-process; the Agent 5 audit repeated the option cases in fresh processes and independently confirmed the same byte offsets, zero open errors/warnings, `ForceRebuild3`, API read-back and analytic physics. `IExtrudeFeatureData2` read back D1/D2, draft enable/angle and FromType/offset. Measured volumes and Z centroids matched analytic values (two directions 2.0e-6 m3 at Z=0.005; 6 degree draft 9.227e-7 m3; offset 1.0e-6 m3 at Z=0.009). Unit coverage: `ExtrudeOptionAuthoringTests` 30/30 plus `ExtrudeAuthoringBoundaryTests` 7/7 (including the two exact audit regression assets).

Authoring support boundary (post-audit): basic Boss/Cut authoring is limited to ResolvedFeatures streams version 0x70/0x71 whose target sketch has a structurally parsed `moSketchChain_c` and a discoverable 0x67 sketch child-count marker. Option authoring is limited to version 0x70. Version 0x6E parts, unconsumed sketches without a chain (including Sketch2 of the 0x71 cut sample), and sketches without the marker are rejected with `NotSupportedException` before any stream mutation; no [1..4] or byte-pattern entity fallback is ever written. The audit's failing probes are kept as exact regression assets: `extrude_sketch_only_0x6e.SLDPRT` (0x6E sketch-only) and `extrude_unconsumed_sketch.SLDPRT` (native 0x70 with unconsumed Sketch2); both are rejected instead of producing native-unloadable files.

## Additional controlled API samples (2026-10-30)

Source: `reverse_workspace/validation/agent5-feature-samples-20261030/samples-extrude-options/`.
Copied without modification. All were independently reopened in a fresh SolidWorks
process; every source and resave has OpenDoc6 0/0, `ForceRebuild3(true)`, hash
unchanged, 1 body (merge_false has 2), and feature error/warning 0/0.

| Sample | SHA-256 (source) | Native definition |
| --- | --- | --- |
| `extrude_thin_one_dir_1mm` | `78947f1d849ad1e0…` | thin=true, wall 0.001, D1=0.010, V=1e-7 |
| `extrude_thin_midplane_1mm` | `56d3d992940576c1…` | thin=true, wall 0.001, D1=0.010, V=1e-7 |
| `extrude_thin_two_dir_1_2mm` | `2a68ec47e4241952…` | thin=true, wall1/2=0.001/0.002, D1/D2=0.010/0.005, V=4.5e-7 |
| `extrude_thin_cap_1mm` | `3e826c904edc72b9…` | thin=true, CapEnds=true, wall 0.001, V=1e-7 |
| `extrude_draft_outward_5deg` | `5435e917cb056597…` | draft angle 0.0872664626 rad, DraftOutward=false, V=4.83522836127556e-6 |
| `extrude_draft_dir2_3deg` | `fee771e73cc59e9a…` | draft angle direction 2 0.0523598776 rad, DraftOutward=false, D2=0.005, V=5e-6 |
| `extrude_merge_false` | `a7fb021b91190e55…` | Merge=false, 2 bodies, V=5e-6 |
| `extrude_flip_side_cut` | `19f6b13703ad890d…` | cut, FlipSideToCut=true, ThroughAll, V=1e-6, body box 0.005..0.015 (material kept inside profile) |
| `extrude_normal_cut` | `3594037d66a2c4b7…` | sheet-metal normal cut, NormalCut=true, ThroughAll, V=1.87433629385641e-7, centroid (0.01,0.005,-0.0005) |
| `extrude_upto_surface_modify` | `b08fc6034227c24c…` | blind seed cut then ModifyDefinition UpToSurface, EndCondition=4, end-condition reference=Face, V=3.71725666117692e-6, centroid (0.01,0.01,0.005) |

Resaves are present for each sample. The thin samples use an open line profile
(5 mm half-length) with `FeatureExtrusionThin2`; the draft/merge samples use
closed rectangular profiles on the top face. These are API-produced controlled
samples, not outputs of the library's `AddExtrude` option emitter. `DraftOutward`
is recorded as false (native API readback); the direction-2 draft sample is a
separate double-ended extrusion. The library currently documents `DraftOutward`
authoring as unsupported; direction-2 draft is now readable but not writable.
See the 2026-10-30 thin/draft/Merge section below for the measured fields.

## Thin, CapEnds, draft direction 2 and Merge (2026-10-30, Agent 1)

Single-variable source/resave diffing established these layouts. All anchors below are checked
structurally; a missing/mutated anchor makes the parser return unresolved instead of promoting a
default. Implementation: `ExtrudeThinFeatureParser`, `ExtrudeCutOptionParser`,
`ExtrudeFeatureDefinitionData.ResolvedProperties/IsResolved`.

### Thin extrusion: `moExtruThin_c`, FolderHeader TypeId 0x52, stream version 0x70

Thin extrusions are a distinct class, not `moExtrusion_c` with an option bit. `IsThinFeature` is
true from the class/TypeId pair. The measured block immediately before `moDisplayRadialDim_c`
(`displayRadialTag`) is 28 bytes:

| Offset | Field | Evidence |
| --- | --- | --- |
| `displayRadialTag - 28` | u32 `CapEnds` (0/1) | 1 only in `extrude_thin_cap_1mm` (+resave), 0 in the other three (+resaves) |
| `displayRadialTag - 24` | f64 wall thickness (`0.001`) | equals D5 in every sample; cross-checked before accepting |
| `displayRadialTag - 16 .. -10` | seven zero bytes | identical in all thin samples/resaves |
| `displayRadialTag - 9` | `0xC0` marker | required; mutation rejected |
| `displayRadialTag - 8` | u8 `ThinWallType` | 0 OneDirection, 2 MidPlane, 3 TwoDirection; 1 (OppDirection) has no sample and is rejected |
| `displayRadialTag - 7 .. -1` | seven zero bytes | identical in all thin samples/resaves |

Complete named parameters (schema 0x8004, full metadata header; located by exact name and header,
not by absolute offset) carry the numbers:

| Parameter | Meaning | Values |
| --- | --- | --- |
| D1 | direction-1 depth | 0.010 in all four |
| D2 | direction-2 depth | present only in TwoDirection; 0.005; its presence is required for type 3 and rejected for 0/2 |
| D5 | wall thickness direction 1 | 0.001 |
| D6 | wall thickness direction 2 | 0.001 (one-dir/midplane/cap), 0.002 (two-dir) |

`BothDirections` is true only for ThinWallType=3; the two-direction sample's direction-1
`moEndSpec_c` is zeroed, while the other three use the measured single-direction Blind header
(u16@0=0, u32@2=1, u32@6=0, u32@10 in {0,1}, u16@14=0). `CapThickness` remains unresolved (all
native samples report 0 and no non-zero cap-thickness sample exists). `DraftOutward` is not claimed.

### Draft on version-0x71 `moICE_c` cut features

| Layout | Structural flag | Angle parameter | Bound fields |
| --- | --- | --- | --- |
| Direction 1 | u32 1 at `displayTag - 24`, 20 zero bytes after it, no D2 depth parameter | D3, value 0.08726646259971647 (5 deg) | `Depth` (D1=0.010), `DraftWhileExtruding`, `DraftAngle`, `Merge` |
| Direction 2 | u32 1 at `displayTag - 22`, 18 zero bytes after it, one D2 depth parameter | D4, value 0.05235987755982988 (3 deg) | `Depth` (D1=0.010), `BothDirections`, `DepthDirection2` (D2=0.005), `EndConditionDirection2=0`, `DraftWhileExtrudingDirection2`, `DraftAngleDirection2`, `Merge` |

The two flag layouts are mutually exclusive; both or neither leaves draft unresolved. Direction-1 +
D2, direction-2 + D3, missing enable flag and NaN/non-D3/D4 angle are rejected by tests.

**DraftOutward is not measured.** `extrude_draft_outward_5deg` was created by the harness with
`DraftOutward1=false` (the harness case only sets Draft1/angle), and the native API readback is
false. The file is therefore an ordinary inward 5-degree draft; it provides no evidence for an
outward=true encoding. `DraftOutward`/`DraftOutwardDirection2` stay unresolved and authoring stays
rejected.

### Merge

In the measured `moEndFace3IntSurfIdRep_c` reference record, data+14 is a u32 flag. The surrounding
header is validated: `00 00`, `EE 80`, `F1 00`, TypeId 0x56, persistent id, then the flag. It is 1
in `extrude_cut_5mm`, `extrude_draft_outward_5deg`, `extrude_draft_dir2_3deg` (native Merge=true)
and in their resaves, and 0 in `extrude_merge_false`/resave (native Merge=false, two bodies).
Through-all `extrude_flip_side_cut` has no EndFace3Int record, so its Merge is left unresolved
rather than inferred. A flag outside 0/1 or a broken reference header leaves Merge unresolved.

### Implementation matrix and gaps

| Option | Read | Resolution | Authoring |
| --- | --- | --- | --- |
| Thin OneDirection / CapEnds | yes (3 samples + resaves) | marked | supported: complete measured-block templates, Depth/WallThickness patched |
| Thin MidPlane | yes | marked | supported: MidPlane template, Depth/WallThickness patched; CapEnds rejected |
| Thin TwoDirection / DepthDirection2 | yes | marked | supported: TwoDirection template, D1/D2 depth and wall1/wall2 patched |
| CapEnds | yes | marked | supported only with the OneDirection template (`CapEnds=true` selects its own block) |
| CapThickness | no (all native 0) | unmarked | rejected |
| Draft direction 1 | yes (0x71 cut) | marked | already supported on the measured 0x70 solid Boss slice; cut/0x71 direction-1 draft not authored |
| Draft direction 2 | yes (0x71 cut) | marked | supported on the exact draft_dir2 stripped base; D4 angle patched, D1/D2/face context fixed |
| DraftOutward | no positive sample | unmarked | rejected |
| Merge=false | yes (measured EndFace3Int flag) | marked | supported as a fixed measured multi-body block on the merge_false stripped base |
| Merge=true with EndFace3Int | yes | marked | existing Boss authoring writes the default measured block; no new emitter |
| UpToSurface (EndCondition=4) | yes (source + resave) | marked | supported as fixed measured face/body-scope blocks on their exact source/resave bases |
| Through-all (no EndFace3Int) | no merge evidence | unmarked | unchanged |

### Thin authoring (template method, 2026-10-30)

`AddExtrude` routes `IsThinFeature=true, ThinWallType=0/2/3` to `ExtrudeThinFeatureBuilder` instead
of the solid serializer. The target must be the exact native stripped base for the selected
combination:
- base = native sample truncated at `moExtruThin_c`, header folder count 20 -> 19, preceding
  0x67 value zeroed; base length 7231 bytes for all four thin templates;
- normalized base SHA-256 (folder count and 0x67 value zeroed):
  OneDirection `0927027d10b44fac6c931b2f98e68f81f59183d08f9af644f19ca5b899072866`,
  CapEnds `8b1b4f44d2cd2c9635d77f5b46feb561f77d08af45cee9e525d1da9d92202b69`,
  MidPlane `0719f8842cf5c9378f91f61a4cba104c121d25fbbee17154cbbd33fecc30d9a9`,
  TwoDirection `54a131d0b427556eb1b5af689ac850059a65c746611db451fc601026911a83b6`;
- the complete measured native block is appended (8388 B for OneDir/Cap/MidPlane, 10285 B for
  TwoDirection); folder count and 0x67 value are restored to 20 and 4. Native-value authoring is
  byte-identical to the measured source file for all four templates.
- patched slots:
  - D1 depth at +32/+0x40/+0x58/+0x68/+0x250/+0x268 (half-depth at +0x40/+0x58).
  - D5 wall1 at +32/+0x1A6/+0x1BE/+0x250/+0x268. The +0x60 slot is -wall for OneDirection/
    TwoDirection and -wall/2 for MidPlane.
  - TwoDirection only: D2 depth at +32/+0x40/+0x58/+0x250/+0x268 and -D2 at +0x68; D6 wall2 at
    +32/+0x60/+0x1A6/+0x1BE/+0x250/+0x268.
  - radial wall block at `moDisplayRadialDim_c - 24` = wall1.
  - both BBox copies (`data+0x0C/+0x14/+0x1C` and `+0xEE/+0xF6/+0xFE`):
    OneDirection Y=-wall/2, Z=depth/2; MidPlane Y=0, Z=depth/2; TwoDirection
    Y=(wall2-wall1)/2, Z=(D1-D2)/2; diameter =
    `2*sqrt(0.005^2 + halfY^2 + halfZ^2)` with halfY=wall/2 (or (wall1+wall2)/2) and
    halfZ=depth/2 (or (D1+D2)/2).
- feature names are fixed to the measured block names (`Thin_0_0.001`, `Thin_2_0.001`,
  `Thin_3_0.001`); custom names are rejected rather than resizing the block. Selected contours/
  regions, OneDirection-with-MidPlane/TwoDirection option mixes and MidPlane CapEnds are rejected
  before stream mutation. OneDirection/MidPlane accept `WallThicknessDirection2` 0 or 0.001;
  TwoDirection requires BothDirections=true, DepthDirection2>0 and WallThicknessDirection2>0.
- validation files:
  `reverse_workspace/validation/agent1-template-authoring-20261030/`
  (`thin_extrude_one_dir_depth20_wall2`, `thin_extrude_cap_depth20_wall2`,
  `thin_extrude_midplane_depth20_wall2`, `thin_extrude_two_dir_d15_d4_w15_w25`, resaves and
  manifest). Expected analytic volumes (0.010 m open line):
  OneDir/Cap/MidPlane 0.010*0.002*0.020 = 4.0e-7 m3; TwoDirection
  0.010*(0.0015+0.0025)*(0.015+0.004) = 7.6e-7 m3. Agent 5 must confirm native rebuild/
  recompute; the D1-block 0.001 cache and some display/tolerance copies are not patched.

Focused verification: `ExtrudeThinOptionReadingTests`, `ExtrudeCutOptionReadingTests`,
`ThinExtrudeAuthoringTests` and the existing extrusion option/authoring suites passed; full suite
after integration is reported separately. No native test was executed in this slice.

### Direction-2 draft / Merge=false / UpToSurface authoring (2026-10-30)

All three use complete measured 0x71 `moICE_c` blocks on exact stripped bases (truncate at the
`moICE_c` tag, folder count -1, preceding 0x67 value zeroed; restore folder/marker and append block).
Feature names are fixed to the measured block names; custom names are rejected.

| Template | base len | stripped folders | marker | base SHA-256 | block | patched |
| --- | ---: | ---: | ---: | --- | ---: | --- |
| DraftDir2 | 17982 | 21 | 3 | `43aac99ad328b9f2c85e8701ea8aff0a2664a696584a0ae3bf1bd9a284b12181` | 6772 | D4 direction-2 angle at schema+0x20/+0x23A |
| MergeFalseBoss | 15181 | 21 | 3 | `1c25a7d33c814eb383756443dd5563ca82608a3a48960b1a6b439293f8e51d8e` | 2954 | none |
| UpToSurface source | 16330 | 21 | 11 | `7fc8b4112b002081367d3cdb92293561ca3f3affbaed1d9e0b3db2b76c9a85da` | 1784 | none; face `0x7CB`, body 2479 |
| UpToSurface resave | 16757 | 21 | 11 | `15686e08d7714232f3191e764ccf1f890b68bc551680e1ffb495e43f451d435c` | 1784 | none; face `0x0AFA`, body 2479 |

- Direction-2 draft: `IsCut=true, BothDirections=true, Depth=0.010, DepthDirection2=0.005,
  DraftWhileExtrudingDirection2=true`; `DraftAngleDirection2` in (0, π/2) is patched to both exact D4
  copies. Direction-1 draft/outward and all face/selection context are measured-fixed.
- Merge=false: `IsCut=true, Merge=false, Depth=0.010` fixed measured block. The
  `moEndFace3IntSurfIdRep_c data+14 = 0` flag and multi-body context are carried verbatim; depth and
  other geometry are not parameterized in this slice.
- UpToSurface: `IsCut=true, EndCondition=4, Depth=0`; source and resave bases carry different face
  ids (0x7CB vs 0x0AFA), so the supplied `EndConditionEntity` (if present) must match the selected
  base. Optional `BodyScope` ids, if supplied, must be the measured 2479. The complete block includes
  `moICE_c`, `moCompFace_c`, `moCompSolidBody_c`, `moFromSktEnt3IntSurfIdRep_c` and
  `moSingleFaceRef_w`. OffsetFromSurface (5), UpToVertex (3), UpToBody and other end conditions are
  rejected through the normal cut validation path.
- Native-value authoring is byte-identical to the measured stream for Merge=false and both
  UpToSurface bases; the draft 3° block is byte-identical before the D4 patch.
- Validation files:
  `reverse_workspace/validation/agent1-cut-authoring-20261030/`
  (`extrude_draft_dir2_angle5`, `extrude_merge_false_native`,
  `extrude_upto_surface_source_authored`, `extrude_upto_surface_resave_authored`, resaves, manifest).
- Tests: `ExtrudeCutTemplateAuthoringTests` and `ExtrudeUpToSurfaceAuthoringTests` cover native
  fidelity, patched angle, resave, face/body mismatch, depth/end-condition rejection and wrong bases.


### Flip-side and normal cut (2026-10-30)

`FlipSideToCut`/`NormalCut` have no isolated scalar control in the available samples: the only
flip sample has Flip=true and the only normal sample has Normal=true, with different bases. The
reader therefore recognizes the exact measured feature tails (source and resave) instead of guessing
a boolean offset; unknown layouts leave both flags false/unresolved.

| Template | stream | base len | stripped/native folders | marker | base SHA-256 | tail |
| --- | ---: | ---: | ---: | ---: | --- | ---: |
| Flip source | 0x71 | 17983 | 21 / 22 | 11 | `ba457308668cd951238ec43dbc4f565079878930d0c4f66944ac960fa0a607de` | 1469 |
| Flip resave | 0x71 | 18369 | 21 / 22 | 11 | `ca78b8f6e323cc5bd428a508574cc5df42e4dab885ba3b73a790bb83346ae384` | 1469 |
| Normal source | 0x74 | 24026 | 19 / 24 | 11 | `878d6f504af9e751b30ebc73476f0180f16b53481a928ddaeb6a815ae498c4d1` | 25767 |
| Normal resave | 0x74 | 24408 | 19 / 24 | 11 | `29359a2936f7c438d6f90381ec059cf1acce938f6a5fcee653d996dab5e204d2` | 25767 |

The NormalCut tail includes the following sheet-metal/template/flat-pattern folders; the writer
restores the native folder count (24) and appends the whole tail. Flip-side uses 0x71 and restores
folder count 22. Both are fixed measured combinations: `IsCut=true, EndCondition=1, Depth=0`,
exactly one flag true, measured feature name (`FlipSideCut` / `NormalCut`), no geometry/options
parameterized. Wrong flag combinations, custom names, non-zero depth, non-ThroughAll end condition,
wrong base/signature/folder/marker/version are rejected before mutation.

Validation files: `reverse_workspace/validation/agent1-flip-normal-authoring-20261030/`
(`flip_side_cut_authored`, `flip_side_cut_resave_authored`, `normal_cut_authored`,
`normal_cut_resave_authored`, resaves and manifest). Expected native volumes: flip 1.0e-6,
normal 1.8743362938564083e-7.

### Remaining Task 1.1 field audit

| Field | Classification | Evidence / action |
| --- | --- | --- |
| FlipSideToCut / NormalCut | solvable as measured layout | exact-tail recognition + fixed templates above; no scalar offset proven |
| DraftOutward | blocked | `extrude_draft_outward_5deg` was created with DraftOutward=false and reads back false; no true sample |
| FromOffsetReverse | blocked | offset samples are unflipped only; the offset signature rejects flipped headers |
| CapEnds (solid, non-thin) | blocked | CapEnds evidence exists only on the thin `moExtruThin_c` layout |
| CapThickness | blocked | all samples report 0; no non-zero sample |
| UpToVertex | blocked | no controlled sample |
| UpToBody | blocked | no controlled sample |
| OffsetFromSurface | blocked | no controlled sample |
| TranslateSurface / ReverseOffset | blocked | no controlled sample |
| LinkToThickness | blocked | no controlled sample |
| AutoFillet / OptimizeGeometry / AssemblyFeatureScope | blocked | no controlled sample |
| Merge=true/false | implemented | EndFace3Int data+14 flag + fixed merge-false template |
| UpToSurface (Face) | implemented | fixed source/resave face+body-scope templates |

Blocked fields are left unresolved/default rather than filled from a guessed scalar.

### Native re-verification requested from Agent 5

- Open the four authored flip/normal files in
  `reverse_workspace/validation/agent1-flip-normal-authoring-20261030/`: confirm OpenDoc6 0/0,
  ForceRebuild3, FlipSideToCut/NormalCut readback, ThroughAll/Depth0, volumes (flip 1.0e-6,
  normal 1.8743362938564083e-7) and resave hashes.
- Open the authored cut files in
  `reverse_workspace/validation/agent1-cut-authoring-20261030/`:
  `extrude_draft_dir2_angle5` (D2 draft 5°, D1/D2=0.010/0.005), `extrude_merge_false_native`
  (Merge=false, 2 bodies, V≈5.0e-6), and both UpToSurface authored files (EndCondition=4, face
  0x7CB/0x0AFA, body 2479, V≈3.71725666117692e-6, centroid (0.01,0.01,0.005)). Confirm OpenDoc6
  0/0, ForceRebuild3, API readback and resave hashes.
- Reopen the eight thin source/resave files and assert `IExtrudeFeatureData2`: IsThinFeature=true,
  ThinWallType 0/2/3/0, wall thickness 0.001/0.001/0.002/0.001, CapEnds false/false/false/true,
  D1=0.010 and (two-direction only) D2=0.005.
- Reopen the two draft cut sources/resaves: D1=0.010, draft direction 1 angle 5 deg or direction 2
  angle 3 deg with D2=0.005, DraftOutward=false; confirm bodies/volumes still native.
- Reopen `extrude_merge_false` (2 bodies) and `extrude_cut_5mm`; confirm API Merge false/true.
- Open the authored thin files in
  `reverse_workspace/validation/agent1-template-authoring-20261030/`: confirm OpenDoc6 0/0,
  ForceRebuild3, IsThinFeature, ThinWallType 0/0/2/3, Depth/WallThickness readback, CapEnds false/
  true, and volumes (analytic 4.0e-7 m3 for OneDir/Cap/MidPlane at 0.020 m / 0.002 m;
  7.6e-7 m3 for TwoDirection at D1/D2=0.015/0.004 and wall1/wall2=0.0015/0.0025). Confirm the
  0x67/folder counts and resave hashes.
- The harness `extrude_draft_outward_5deg` creation code should be corrected if a true outward sample
  is wanted; until then outward authoring remains unsupported.

