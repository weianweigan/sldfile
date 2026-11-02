# Hole Wizard controlled native samples (2026-10-30)

Source: `reverse_workspace/validation/agent5-feature-samples-20261030/samples-holewizard/`
(copied without modification). These are native SolidWorks 2026 parts created with
`IFeatureManager.HoleWizard5` on the controlled 20x20x10 mm box. Do not overwrite.

| Sample | SHA-256 | Native readback |
| --- | --- | --- |
| `hole_simple_d5_throughall.SLDPRT` | `22ca310d3c71099c234dda8c688f030b9fbcb4a18407621d05eaa8a2df6142eb` | legacy simple, Ø5, EndCondition=1, 1 point, V=3.80365045915064e-6 |
| `hole_simple_d5_blind10.SLDPRT` | `3666e8c9e24b647744d72ec589181538a82e9f0fcc9985e445554e6054ea5c04` | legacy simple, Ø5, EndCondition=0, depth 0.010 |
| `hole_counterbore_d8_d4_depth5.SLDPRT` | `9aa94dd0590189fc210c46ce34579be982e02366af78a7f2f0f8cd32774f085b` | legacy, Ø4 through; **CounterBoreDiameter/Depth read 0/0 (BLOCKED)** |
| `hole_countersink_d8_d4_90.SLDPRT` | `f29aafd999ee0e930db4fe6becd91d7c366f09913ec870c5aa8f153148f041a1` | legacy, Ø4 through; **CounterSinkDiameter/Angle read 0/0 (BLOCKED)** |
| `hole_tapped_m6.SLDPRT` | `039e88a022221c9b46dded547e5674b1587dea75d2a04882e004e9567d0a4ef7` | Type=48 (tap), Standard2=8 ISO, FastenerType2=147, size M6, thread Ø6/10 mm, EndCondition=1 |
| `hole_up_to_next.SLDPRT` | `d06135e24cd3d2b9e1b635178b1d17aad091372a35dfb1b9dc5e45258823f1dd` | legacy simple, EndCondition=11 UpToNext |
| `hole_on_face_point.SLDPRT` | `ff22d127841cbc55b8f6b3466ee53bcb87eae3c66b57b38b4c65f33eaaab0cf2` | Ø5 through at off-center face point; centroid x/y ~0.01036/0.01015 |

Resaves: `*_resave.SLDPRT` with SHAs `69e279b7…`, `62a3397f…`,
`2920d058…`, `ac3303c2…`, `c50b17a4…`, `52aa5cb6…`, `2b258763…`.

All created files/resaves: OpenDoc6 errors/warnings 0/0, `ForceRebuild3(false)=true`,
hash unchanged, 1 body, target feature error/warning 0/0. Selection mark 0 was used
for the face/location picks; the creation manifest records the picked face object and
`GetPersistReference3` bytes. Native `IWizardHoleFeatureData2` snapshots are in
`verify-holewizard.json` (Type, Standard2, FastenerType2/Size, EndCondition,
Diameter/Depth, CounterBore/CounterSink, Thread*, sketch-point count, face/vertex).

## Blocked / not usable

- `hole_tapped_m6_blind12`: HoleWizard5 returned NULL. No file.
- `hole_two_points`: two face location picks did not produce a Hole Wizard feature
  (HoleWizard5 NULL). No file.
- `hole_ansi_vs_iso`: modern ANSI/ISO drill-size simple hole returned NULL. No file.
- Counterbore/countersink legacy parameters were ignored: the API created plain Ø4
  through holes with CounterBore/CounterSink = 0. These two files are retained only as
  evidence of the API limitation, not as usable counterbore/countersink specimens.
- `IWizardHoleFeatureData2.Face` is null after `AccessSelections` (the face is consumed
  by the feature); the pre-creation face selection and persist reference are recorded
  in `create-holewizard.json`.

## Binary layout (reading, 2026-10-30)

Measured serialized class: **`moHoleWzd_c`**. Two layouts coexist:
- legacy simple hole: FolderHeader **TypeId 0x54**, ResolvedFeatures stream **0x71**;
- tapped hole: FolderHeader **TypeId 0x52**, stream **0x84**.

Both have a 505-byte header rest after schema/WChar name/double 2.0/TypeId/u32 0/empty WChar.
The single sketch point is at rest **+261 (X)** and **+269 (Y)**.

Legacy fields:
- `sgLLDist` payload 140, diameter at **+38**; exact copy in
  `moSkDimHandleValG2_c` at **+453** (both copies must be bit-identical).
- EndCondition is the final u32 of `moCompSolidBody_c` (payload 351, **+347**):
  0 = blind, 1 = through all, 11 = up to next; Type = 0.
- Other measured objects: `moFaceRefPlnData_c` 145, `moCompFace_c` 2301,
  `sgPntPntDist` 1385, `moSkDimHandleValG2_c` 2448, `moFixedRefPlnData_c` 544,
  `moSingleFaceRef_w` 659.
- Blind **depth** is not yet bound: no depth-varying sample exists, so `Depth` stays
  unresolved even though the native API reports 0.010 m for these files.
- The `counterbore`/`countersink` samples are actually plain Ø4 through holes (native
  CounterBore/CounterSink = 0); the parser reads them as legacy Type 0 / Ø4 / through
  and intentionally leaves counterbore/countersink fields null.

Tapped fields:
- EndCondition = final u32 of `moCompSolidBody_c` (payload 653, **+649**) = 1.
- Thread diameter: `ThreeDdiameterDim_c` payload 1122, named D2 value at **+38**
  (`0.005999999999999975`); `moCThreadData_c` payload 2091 has the `0.006` copy at +29.
- Thread depth: `moCThreadData_c` +65 (`0.01`), copies at +1105/+1286.
- Type = 48 at `moAngleParameter_c` payload 19426, offset **+14811**.
- Standard/fastener selection in `moSingleFaceRef_w` payload 719: UTF-16 `ISO` chars at
  +432, `螺纹孔` at +442, `M6` at +452; Standard2=8 at +532, FastenerType2=147 at +536,
  CosmeticThreadType=1 at +548.
- Other measured objects: `moFaceRefPlnData_c` 4228, `sgPntPntDist` 140, `sgLLDist` 424,
  `sgPntLineDist` 2126, `sgAnglDim` 4, `moSkDimHandleValG2_c` 2458,
  `moFixedRefPlnData_c` 357, `moCosmeticThread_c` 165, `moCompEdge_c` 86,
  `moEdgeRef_c` 212, `moDisplayRadialDim_c` 661, `moCylinderRef_w` 615.

`HoleWizardFeatureParser.ParseHoleWizard` accepts only these two controlled layouts (exact
object set/lengths, header, copies and point bounds 0..0.02 m); unknown layouts, incorrect
type/version, tampered diameter/type/thread/standard/end-condition and non-hole streams return
null without modifying the stream. `HoleWizardFeatureDefinitionData` exposes FolderTypeId, Type,
EndCondition, Diameter (legacy), Standard/Standard2/FastenerType/FastenerType2/FastenerSize,
ThreadDiameter/ThreadDepth, CosmeticThreadType and one sketch-point location.

Authoring is not implemented. Needed samples: blind depth variants (e.g. 5/15 mm), real
counterbore/countersink specimens with native nonzero parameters, modern ANSI/ISO drill-size
holes, multi-point holes, and tapped blind-thread specimens.

## Controlled legacy authoring (2026-10-30)

`SldPart.AddHoleWizard` writes one legacy simple hole on the matching hole-stripped base:
- target = `hole_simple_d5_throughall` or `hole_simple_d5_blind10` truncated at the
  `moHoleWzd_c` tag (folder count 21 → 20, stream 0x71);
- normalized base SHA-256 (folder count zeroed): through-all
  `d1134369631e102f04d077f25562a83d40d5cc8ac13871a995b433b5a48fd592`, blind-10
  `4283c22e82cfefdc50b20005d855d798e0225d7ad188a80d46ba16a9bcb2604b`;
- measured header rest 505 B (feature id, sketch point, face context) + tail 9270 B;
  diameter copies at tail **+3071** (`sgLLDist+38`) and **+5055**
  (`moSkDimHandleValG2+453`) are patched together; EndCondition/depth/face/point stay
  template-measured (through-all template EndCondition=1; blind template EndCondition=0
  with fixed 0.010 m depth);
- authoring accepts Type=0 (or null), FolderTypeId=0x54 (or null), EndCondition 0/1,
  positive finite Diameter and Depth null/0.010. Wrong/mismatched base, existing hole,
  non-0x71 stream, wrong folder count, EndCondition 11/2, invalid diameter, other depth,
  sketch-point/face/vertex overrides, counterbore/countersink/thread/scope fields and all
  other unverified properties are rejected before the stream changes.
- With the native name `孔1` and diameter 0.005, the emitted feature block is byte-identical
  to the native through-all/blind block. The 0x67 marker (value 1) is retained; folder count
  becomes 21.
- Fresh validation files: `reverse_workspace/validation/agent1-holewizard-verify-20261030/`
  (through-all Ø6 and blind Ø4.5 at fixed 10 mm depth, with resaves and manifest).

Native verification pending: OpenDoc6 0/0, ForceRebuild3, Type=0/EndCondition/Diameter and
point readback, expected volumes (through Ø6 ≈ 3.7172566611769185e-6 m³; blind Ø4.5 with
the measured 10 mm depth ≈ 3.84095687212337e-6 m³). Authoring for UpToNext, arbitrary blind
depth, counterbore/countersink and threads remains unsupported.

## Controlled UpToNext authoring (2026-10-30)

`SldPart.AddHoleWizard` also supports the measured UpToNext legacy layout:
- target = `hole_up_to_next` truncated at the `moHoleWzd_c` tag (folder count 21 → 20,
  stream 0x71 / TypeId 0x54); normalized base SHA-256 (folder count zeroed)
  `9714924e14b7fab57b39ad09946d879272c86a9c47770980ae47d04202c71e10`;
- measured header rest 505 B + tail 9270 B; `moCompSolidBody_c` final u32 = 11
  (EndCondition), diameter copies at tail +3071/+5055, sketch point/face template;
- only Diameter is parameterized; EndCondition=11, Type=0, point/face and all other fields
  are fixed/rejected exactly like the ThroughAll/Blind legacy authoring; Depth may be null
  or the fixed template value 0.010 m but is not used;
- native name/diameter (孔1, 0.005) reproduce the native UpToNext block byte-for-byte.
- Fresh validation files: `reverse_workspace/validation/agent1-holewizard-uptonext-verify-20261030/`
  (`base_hole_uptonext`, native-values and Ø6 probes, resaves, manifest). Expected volumes:
  native Ø5 ≈ 3.803650459150637e-6 m³; Ø6 ≈ 3.7172566611769185e-6 m³.

Tapped M6 authoring: see below.

## Controlled tapped M6 authoring (2026-10-30)

`SldPart.AddHoleWizard` also accepts the tapped layout:
- target = `hole_tapped_m6` truncated at the `moHoleWzd_c` tag (folder count 21 → 20,
  stream 0x84); normalized base SHA-256 (folder count zeroed)
  `0a0f85d6ed608cbf8c1c89b6ebc2496e34ec1c12336978cb5b8bdc570df825a6`;
- measured header rest 505 B (feature id, sketch point, face context) + tail 36976 B;
  TypeId 0x52;
- fixed measured values: Type=48, Standard=ISO, Standard2=8, FastenerType=螺纹孔,
  FastenerType2=147, FastenerSize=M6, EndCondition=1, CosmeticThreadType=1, plus the
  template sketch point/face references;
- parameterized values: none. ThreadDiameter must be null or the measured
  **0.006 m**, and ThreadDepth must be null or the measured **0.010 m**.
  Agent 5's native audit showed that writing ThreadDepth=0.012 left the native
  thread depth at 0.010, and writing ThreadDiameter=0.008 changed the API
  callout/definition but produced the same rebuilt geometry and volume as
  0.006. Both fields are therefore fixed to measured values; null uses the
  template values. Patched tail slots:
  - `ThreeDdiameterDim_c` +38/+336/+570/+594 (tail +33838/+34136/+34370/+34394);
  - `moCThreadData_c` diameter display copy +29 (tail +31026);
  - `moCThreadData_c` depth slots +65/+1105/+1286 (tail +31062/+32102/+32283) remain at
    the measured 0.010 template value.
- The `moCThreadData_c` named D1 value, the tap-drill/cylinder geometry and the
  display objects are not parameterized. The native-values authored block differs from
  the native block only at the rounded display-diameter copy (0.006 vs 0.005999999999999975).
- Rejections (before any stream change): wrong/mismatched base, existing hole, non-0x84,
  folder count≠20, Type≠48, FolderTypeId≠0x52, EndCondition≠1, non-ISO/8/147/M6 flag,
  CosmeticThreadType≠1, ThreadDiameter outside measured 0.006±1e-12, ThreadDepth outside
  measured 0.010, invalid/zero/NaN/infinite values, any face/point/scope/other field.
- Fresh validation files: `reverse_workspace/validation/agent1-holewizard-tapped-verify-20261030/`
  (`base_hole_tapped`, native-values and measured-D6/measured-depth-10 probes, with resaves and
  manifest). The native-values and D6 probes should rebuild to V≈3.80365045915064e-6.
  Pre-restriction D8/depth-12 probes are kept under `failed-threaddiameter/` and
  `failed-threaddepth/` as native-audit evidence; they are not accepted by the builder.

## Selection references (reading, 2026-10-30)

Legacy holes carry two feature-local face records, both stable across each main/resave pair:

- `moSingleFaceRef_w` (659 B) at payload **+0x58** and `moCompFace_c` (2301 B) at **+0x56**;
- each is a 40-byte record: `+0 u16 0x80E7`, `+2 u16 1`, `+4 u32 0`, `+8 u16 0`,
  `+10 u32 face id` (6 in every controlled top-face specimen), `+14 u32 class word`
  (**0x200**), `+18 u32 persistent selection id` (nonzero), `+22` 16-byte archive GUID
  `7dc39425ad49b2547dc39425ad49b254`, `+38 u16 0`.
- `HoleWizardFeatureDefinitionData.Face` is the `moSingleFaceRef_w` record;
  `IFace` is the `moCompFace_c` record. Both expose `EntityId=6`, `ClassWord=0x200`,
  `ArchiveGuid`, `PersistentId` and `RecordCount=1`, and both are marked resolved.
  Persistent ids are opaque, nonzero and stable across the main/resave pair; they differ
  between the two wrappers (through-all Face `0x027A` / IFace `0x05EA`, blind
  `0x0A90` / `0x0E00`, on-face-point `0x3749` / `0x3ABB`, up-to-next
  `0x2F33` / `0x33A3`).
- The 38-byte `moFaceRef_c` at stream offset **0x245D** is located inside the preceding
  base extrusion folder (`moExtrusion_c`, tag 0x2357), is byte-identical in all legacy
  samples and is **not** attributed to the hole.
- Tapped holes contain neither face record; `Face`/`IFace` stay null and unresolved.
  `moSingleFaceRef_w` in the tapped layout is the measured ISO/M6 string/flag container.

Sketch point and termination:

- The only `sgPointHandle` (130 B at 0x148B) belongs to the base sketch (`sgSketch`), not
  the hole feature. The legacy/tapped feature header stores the one point location at
  rest **+261 (X) / +269 (Y)**; these are exposed as `SketchPointLocations`.
  `SketchPoints` is one opaque `Sketch`/`HoleSketchPoint` context entry with no decoded id.
- `moCompSolidBody_c` (351 B) contains a 40-byte record at payload **+0x55** with face id 6,
  **class word 0 and persistent id 0** in every legacy sample (through-all, blind,
  up-to-next and resaves); only its final u32 differs: the EndCondition (0/1/11). It is the
  measured end-condition carrier, not a body selection. `EndConditionReference` is therefore
  null and unresolved even for UpToNext. Tapped's 653-byte block has the same zero-id record.

Parser guards: exact object set/lengths, exact face-record offsets/fields, nonzero id and
persistent id, fixed class word and archive GUID, and the existing header/copy/point checks.
Tampering any face field, the GUID, or the record tail returns null without stream changes.

## Native API retry: real counterbore/countersink (2026-10-31)

The earlier counterbore/countersink attempts used ISO **DrillSizes** (143) and produced plain
drill holes. The working recipe uses a screw fastener type, not a drill-size table:

- counterbore: `HoleWizard5(GenericHoleType=0, Standard=8 ISO, FastenerType=139 ISO socket
  head cap screw, SSize="M6", EndType=ThroughAll, Diameter=.006, Depth=.010,
  Value1=.011, Value2=.006, ...)`. API returns a persistent `HoleWzd` feature with
  `Type=14`, Standard=ISO, FastenerType2=139, FastenerSize=M6, ThruHoleDiameter=.006,
  ThruHoleDepth=.010, CounterBoreDiameter=.011, CounterBoreDepth=.006, error 0,
  V=3.3167035978442214e-6 m³, A=.0018261946710584646 m².
- ISO hex-cap (136) and ANSI-metric socket (standard 1 / fastener 33) also produced real
  counterbores. Drill-size/screw-clearance fastener tables and ANSI-inch sizes returned null.
- countersink: `GenericHoleType=1`, ISO flat-head 141, size M6/M8, angle π/2 produced real
  countersinks (`CounterSinkDiameter=.011`/`.015`); blind M6 produced a .03175 m countersink.
  Native evidence is under
  `reverse_workspace/validation/agent1-holewizard-retry-20261031/artifacts-all/`.

Counterbore stream layout (native sample `hole_counterbore_iso_m6_11_6.SLDPRT`):
`moHoleWzd_c` FolderHeader **TypeId 0x52**, stream **0x71**, header rest 505, point at
rest +261/+269. Elements: anonymous 525, `moFaceRefPlnData_c` 145, `moCompFace_c` 4121,
`sgPntPntDist` 140, `sgLLDist` 424, `sgPntLineDist` 2126, `sgAnglDim` 4,
`moAngleParameter_c` 14642, `moSkDimHandleValG2_c` 5384, anonymous 613,
`moFixedRefPlnData_c` 544, `moCompSolidBody_c` 730.
- `moCompSolidBody_c` carries UTF-16 `ISO` at +0x195, `六角凹头 ISO 4762` at +0x19F,
  `M6` at +0x1BD, Standard2=8 at +0x21F and FastenerType2=139 at +0x223.
- Counterbore values are exact Chinese named parameters in `moAngleParameter_c`:
  `通孔孔直径`=.006, `通孔孔深度`=.010, `柱形沉头孔直径`=.011, `柱形沉头孔深度`=.006.
- `HoleWizardFeatureDefinitionData` exposes Type=14, EndCondition=1, Standard/Standard2,
  FastenerType/FastenerType2/Size, HoleDiameter/Depth/HoleDepth=0, ThruHoleDiameter/Depth,
  CounterBoreDiameter/Depth, CounterSink/Thread fields=0 and the sketch point.
  Unknown/tampered strings, flags, named values, schema, class/type/version return null.

Fixed authoring: `SldPart.AddHoleWizard(Type=14, CounterBoreDiameter=.011,
CounterBoreDepth=.006)` on the exact `hole_counterbore_iso_m6_11_6` stripped base (SHA
`2f4348e193e3f0ab4a8cd333fec62e67798b44ecfcde0c14cbaff3cd67065b74`, length 12266,
folder 20, stream 0x71, measured feature name) appends the complete 30212-B native block.
Other values/names/bases are rejected before stream changes. Artifact:
`reverse_workspace/validation/agent1-holewizard-counterbore-authoring-20261031/`.

### ANSI Metric counterbore (second fixed template)

The ANSI Metric socket sample (`Hole_cb_ansi_metric_socket_m6`, standard=1,
FastenerType2=33, body 760 B) is stable on source and native resave. It has the same
Ø6 through/Ø11 x 6 geometry as the ISO template but a different selection block
(`ANSI Metric` / `凹头盖螺钉 - ANSI B18.3.1M`, Standard2=1 at +0x23D, FastenerType2=33
at +0x241). `ParseCounterBore` accepts body lengths 730/760 and the corresponding
selection words. `HoleWizardCounterBoreAnsiAuthoringTemplates` carries both measured
30252-B blocks; `AddHoleWizard` selects ANSI when Standard/FastenerType2 indicate it.
Artifacts: `reverse_workspace/validation/agent1-holewizard-counterbore-ansi-authoring-20261031/`.

## Countersink reading + fixed authoring (2026-10-31)

Native ISO flat-head countersinks (`Type=44`, stream 0x71, TypeId 0x52) are modeled:
- M6: Ø6 through, Ø11 x 90° (V=3.6419893371846652e-6, A=.0017124754201940065);
- M8: Ø8 through, Ø15 x 90° (V=3.2985085403921812e-6, A=.0017152087870256807).
Layout: header rest 505, point rest +261/+269; `moCompFace_c` 4101,
`sgPntPntDist` 140, `sgLLDist` 424, `sgPntLineDist` 2126, `sgAnglDim` 4,
`moAngleParameter_c` 18475, `moSkDimHandleValG2_c` 3809,
`moDisplayAngularDim_c` 1588, anonymous 601, `moFixedRefPlnData_c` 544,
`moCompSolidBody_c` 734.
- Body: `ISO` +0x195, `锥孔平头 ISO 7046-1` +0x19F, `M6`/`M8` +0x1C1,
  Standard2=8 at +0x223, FastenerType2=141 at +0x227.
- Named parameters in moAngleParameter_c: `通孔孔直径` (.006/.008),
  `通孔孔深度` .010, `近端锥形沉头孔直径` (.011/.015),
  `近端锥形沉头孔角度` π/2.
- Definition exposes Type=44, EndCondition=1, standard/fastener fields,
  ThruHoleDiameter/Depth, CounterSinkDiameter/Angle, counterbore/thread zeros,
  sketch point; M6/M8 and source/native-resave are cross-validated. Tampered
  strings/flags/named values/schema or wrong version return null.
- Authoring uses four exact 34055-B native templates (M6/M8 x source/native resave)
  in `HoleWizardCounterSinkAuthoringTemplates`; `AddHoleWizard(Type=44, FastenerSize,
  ThruHoleDiameter, CounterSinkDiameter)` selects by values/base SHA, rejects other
  sizes/angles/names/bases, and reproduces the native block byte-for-byte.
  Artifacts: `reverse_workspace/validation/agent1-holewizard-countersink-authoring-20261031/`.

## Legacy tutorial HoleWizard families (strict read-only, 2026-10-31)

23 representative legacy tutorial samples are under
`TestFiles/Features2026/HoleWizardLegacyTutorials/` (copied from
`reverse_workspace/validation/agent1-tutorial-scan-20261031/`). The parser
`LegacyHoleWizardTutorialParser` matches a family only by the measured tuple
(ResolvedFeatures first u16 version, folder count, legacy FolderHeader type id,
header rest length), then reads each exposed field from an exact class-tag-relative
payload offset and requires the native-snapshot value. Unknown family, missing class,
offset mismatch, or any value/byte/tag tamper returns null.

Supported: **21 of 23 families** (01–14, 16–21, 23; 15 has no native HoleWizard feature;
22's 0x6B-schema header cannot be strictly matched and stays unsupported).
Field set proven per family (only where a trusted class slot exists):
- `Standard`, `FastenerType`, `FastenerSize` (from `moFixedRefPlnData_c`,
  `moCompSolidBody_c`, `moSingleFaceRef_w`, `moCylinderRef_w`).
- `ThruHoleDiameter/Depth`, `CounterBoreDiameter/Depth`, `CounterSinkDiameter/Angle`,
  `ThreadDiameter/Depth` (from `moAngleParameter_c`, `sgAnglDim`,
  `ThreeDdiameterDim_c`, `moCThreadData_c`).
- `FolderTypeId` (legacy header), `Type` and `EndCondition` (native snapshot per family).
Fields without a trusted unique slot (Standard2/FastenerType2, HoleDiameter/Depth,
sketch points, selection references) stay null/unresolved. **No legacy authoring.**
Tests: `SldFileTests/LegacyHoleWizardTutorialTests.cs` — 23 read cases + one all-family
tamper test. Native snapshots and per-family descriptors:
`reverse_workspace/validation/agent1-tutorial-scan-20261031/legacy-hole-native.json`,
`diag/legacy-family-descriptors.json`, `diag/legacy-family-descriptor-summary.txt`.

## Fixed legacy tutorial authoring (2026-10-31)

Three single trailing `moHoleWzd_c` tutorial families now have fixed Authoring:
- `11_box.sldprt` — ANSI-inch CBORE for #6 Binding Head Machine Screw;
- `12_clamp2.sldprt` — ANSI-inch CBORE for #00 Binding Head Machine Screw;
- `08_die_insert.sldprt` — Drill&CBore for 1/4 SHCS (Shallow).
Each base is truncated at its `moHoleWzd_c` tag with folder count decremented, then the
measured feature block (tag→stream end) is appended verbatim. Gates: exact stream first-u16,
base length, stripped folder count, no existing HoleWizard, exact normalized prefix SHA, and
the measured 0x67 marker where present (clamp2 marker value 3; box/die bases have none).
The measured feature name is required; custom names, other FolderTypeId/Type/EndCondition/
standard/fastener/dimension values, wrong/tampered bases, folder/version/marker changes are
rejected before stream mutation. Existing SW2026 CounterBore/Countersink/tapped/legacy paths
remain separate and unchanged.
Tests: `SldFileTests/LegacyHoleWizardAuthoringTests.cs` — 3 native-block roundtrips + custom/
value/base rejection + folder/version/marker/SHA tamper (5 tests).
Artifacts: `reverse_workspace/validation/agent1-legacy-holewizard-authoring-20261031/`
(3 authored + resave copies + manifest; native Open/rebuild/error/volume/area verification
is pending Agent 5). No blind-counterbore sample was found; that remains BLOCKED.
