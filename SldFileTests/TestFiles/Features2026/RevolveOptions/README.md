# Revolution reading evidence (SolidWorks 2026, 2026-10-30)

Scope: first verified Task 1.2 read slice, not full FeatureData/Selection/authoring completion.

## Native controlled samples

FeatureLab creates the same rectangle (r=5..15mm, height=10mm) and construction axis. Only revolution angle or ReverseDir varies. Five samples were independently reopened and ForceRebuild3 succeeded. Snapshots include native IRevolveFeatureData2.GetRevolutionAngle(true/false), Type and ReverseDirection, volume, surface area, centroid, SHA-256 and operations. JSON/manifest and native SLDPRT files are preserved here alongside tests, with original experiment copies under reverse_workspace/experiments/revolve-options-20261030.

| Sample | native angle radians | native Type | native ReverseDirection | expected volume m3 |
| --- | ---: | ---: | --- | ---: |
| revolve_90 | pi/2 | 0 | false | pi*0.5e-6 |
| revolve_180 | pi | 0 | false | pi*1e-6 |
| revolve_180_reverse | pi | 0 | true | pi*1e-6 |
| revolve_360 | 2*pi | 0 | false | pi*2e-6 |
| revolve_360_resave | 2*pi | 0 | false | pi*2e-6 |

## Structural binding

The reader only accepts stream version 0x70, moRevolution_c and Folder TypeId=0x4E. One moRevEndSpec_c is required; its first u32 is 1 (single-direction), followed by 24 zero bytes in the measured Blind layout. Then the owned named objects follow moDisplayAngularDim_c -> moFeatureDimHandle_c -> AngleDim_c -> moAngleParameter_c. Exactly one angle parameter is accepted. Its schema 0x8004, D<number> WChar name and full named-parameter metadata are validated before reading the following double. Angles pi/2, pi, 2*pi are independently confirmed by native API; the value is not selected by floating-point search. RawData's generic scanner truncation at FFFF metadata is avoided by bounding to the next named object in the same feature.

ReverseDirection is u32 at moLineRef_w tag-19. In the 180-degree single-variable pair it changes 0->1; all other controls have 0. The preceding thirteen bytes must be zero, and the following structure must match zero padding then u16 0x8064, u16 0, u32 3, u32 1. Unknown reference/header/flag layouts do not produce a typed definition. These bytes are a measured axis-selection-layout restriction, not a general persistent topology decoder.

Native 360 resave moves moRevEndSpec data from 10687 to 10913 (+226) and moAngleParameter data from 11499 to 11725 (+226); matching named-object relationships and values persist. Partial revolution introduces additional end-face objects relative to full revolution, so no fixed feature-relative angle offset is used.

## API and limits

Public root namespace SldFile now exposes RevolveFeatureDefinitionData with Angle (radians), AngleDirection2=0, Type=0, BothDirections=false and ReverseDirection for this measured single-direction Blind solid layout. The read-only model does not pretend to implement creation. Public Features classify moRevolution as FeatureKind.Revolve and carry the typed Definition.

Thin revolutions, cuts and independent second-direction reading were added in the 2026-10-30
section below. Axis/contour persistent Selection decoding and authoring for thin/both/cut remain
unfinished. An unrecognized shape returns no definition; it is not filled with guessed defaults.

## Verification

Native generation/reopen: 5/5 verified. Targeted revolve and API-purity tests: 8/8 passed, zero skipped. Build: zero warnings/errors. Tests cover three angle amplitudes, resave, reverse, byte-preserving save/reopen, unknown schema/TypeId and non-boolean direction flags. Original user RevolveFeatureReverseTests is preserved.

## Authoring (Task 1.2, 2026-10-30, Agent 1)

`SldPart.AddRevolve(RevolveFeatureDefinitionData)` now creates a single-direction Blind solid revolution. `RevolveFeatureDefinitionData` exposes settable `Angle`, `ReverseDirection`, `AngleDirection2`, `Type`, `BothDirections`, `IsCut`, `IsThinFeature`, `Thickness1`, `Thickness2`, `Merge` and `SketchName`; only the measured combination is accepted.

Supported scope:
- ResolvedFeatures stream version 0x70 only.
- Target sketch must expose a parsed `moSketchChain_c` with entity ids `[2,3,4,5]`, the controlled `sgLineHandle` geometry (SHA-256 `9c1b69c5da6118966d652ba9fdc2d179e9e179bc475052984c195c6936f1bf18`), and the 0x67 child-count marker. This pins the rectangle (5..15 mm from the axis, 10 mm high) plus construction-axis specimen that the measured topology and bounding-box formulas assume.
- The target must be the extrusion-stripped base matching the layout (folder count 19, 0x67 child count 0, normalized base SHA-256 with those two fields zeroed):
  - forward partial: `revolve_180`-derived base, SHA `e11b5077364d5b5c926071c738912825980be0b23a957dba1124966dc49d88ae`, serial pair `0x6AE40CAD/0x6AE40CAD`.
  - reverse partial: `revolve_180_reverse`-derived base, SHA `b797b2a94b7645169b537a241f895f19530ec4ac797f7006d37c57669e9e3307`, serial pair `0x6AE40CB0/0x6AE40CB1`.
  - full `2*pi`: `revolve_360`-derived base, SHA `c97b7ee90f214ff1d6058388c9c64c2e7cf6fb2c97bf6536b0d44e6687aa3c7a`, serial pair `0x6AE40CA3/0x6AE40CA3`.
  The sketch/feature serials are read from the base's `moCompFeature_c` records (first id 0x49 = sketch, second id 0x4E = feature) and patched into every surface-reference slot; a fixed allocation is never used.
- Angle in `(0, pi]` or exactly `2*pi` (the controlled 90/180 partial and 360 full topology classes); partial sweeps in `(pi, 2*pi)` have no measured face/selection structure and are rejected. `ReverseDirection` is allowed for partial revolutions only (no full-reverse control). Cuts, thin features, both directions, non-Blind termination, multi-body scope, other sketches, mismatched/non-controlled bases and other stream versions are rejected with `NotSupportedException` before the stream is modified.

The restrictions in this section apply to the original solid serializer path. Thin 90/360,
both-directions and cut 360 now use separate complete measured-block templates; see the template
authoring section below.

Emitted structure (measured element sequence, partial vs full selected by `Angle`):
- `moRevolution_c` header: prefix written field-by-field (schema 0x8004, WChar name, double 2.0, TypeId 0x4E, empty WChar) plus the measured 173-byte suffix; the feature-id slot is allocated per injection.
- Partial: `moPerBodyChooserData_c`, `moFaceRef_c`, `moEndFaceSurfIdRep_c`, `moFR_c`, `moExtObject_c`, `moCStringHandle_c`, `moFromSktEntSurfIdRep_c` (270-byte forward/reverse block), `moBBoxCenterData_c`, `moLineRef_w`, `moCompSketchEntHandle_c`.
- Full: `moPerBodyChooserData_c`, `moFaceRef_c`, empty `moFromSktEntSurfIdRep_c` (2 bytes, **before** FR/ExtObject), `moFR_c`, `moExtObject_c`, `moCStringHandle_c` (281-byte full payload), `moBBoxCenterData_c`, `moLineRef_w`, `moCompSketchEntHandle_c`.
- Surface slot policy: CString surface slots and partial FromSkt slots receive the feature serial; CString offset +19 receives `sketchSerial - 1`; `moCompSketchEntHandle_c +89` receives the sketch serial. The reverse CString archive byte +14 is 0x39 (forward 0x38).
- `moRevEndSpec_c` (u32 1 + measured Blind header), `moDisplayAngularDim_c`, `moFeatureDimHandle_c`, `AngleDim_c`.
- `moAngleParameter_c`: angle is written at parameter +32 and +570; the remaining bytes are the measured display/tolerance graph.
- `moFavoriteHandle_c`: measured display metadata (forward/reverse and partial/full variants).

Bounding box: center/radius are computed analytically for the controlled profile and written into both measured copies; the partial/full layouts use the measured slot offsets. The header bbox-X display cache, angle-parameter derived sin/cos values and favorite display values are taken from the 180-degree (partial) / 360-degree (full) references; SolidWorks is expected to recompute them on rebuild.

Measured fidelity (offline comparison of authored vs native feature blocks):
- pi forward on the controlled 180 base: byte-identical to `revolve_180` (including the measured feature id and surface serials).
- pi reverse on the controlled 180_reverse base: byte-identical to `revolve_180_reverse` except the allocated feature id.
- 2*pi forward on the controlled 360 base: byte-identical to `revolve_360` except the allocated feature id.
- pi/2 forward uses the 180 forward template/serials with the angle/bbox parameterized; the native 90 sample is a different controlled base and is rejected rather than mixing serial families.

Native validation of the authoring path is deliberately deferred to Agent 5 (fresh-process reopen, `ForceRebuild3`, `IRevolveFeatureData2.GetRevolutionAngle`/`ReverseDirection`, volume/centroid). Items to verify using the matching stripped bases: pi/2 and pi forward on the 180 base, pi reverse on the 180_reverse base, and 2*pi on the 360 base; arbitrary partial angles up to 180 degrees may use the 180 base. Partial angles in `(pi, 2*pi)` and full reverse are rejected and need no native check.

Unit coverage: `RevolveAuthoringTests` 34/34 (including the three native-block fidelity cases and the base-layout rejection cases).

## Native-audit root cause and fix (2026-10-30, Agent 5 feedback)

Agent 5 found that the previously authored `pi/2`, `pi forward`, `pi reverse` and full `2*pi` parts
opened (`OpenDoc6` 0 for the partial cases) but failed `ForceRebuild3` with feature error 1, zero
bodies and no mass properties. The root cause was the **axis/sketch surface reference**:

- The serializer allocated a fixed surface tag `0x6AE40CB5 + 0x10 * existingRevolveCount`. That
  value is not present anywhere in the controlled extrusion-stripped base.
- The native samples reference the sketch/feature persistent serials stored in
  `moCompFeature_c`: forward 180 = `0x6AE40CAD/0x6AE40CAD`, reverse 180 = `0x6AE40CB0/0x6AE40CB1`
  (sketch/feature), full 360 = `0x6AE40CA3/0x6AE40CA3`. With the wrong tag, the revolve axis/sketch
  reference does not resolve, producing a feature error and no body.
- A second full-layout defect: the empty `moFromSktEntSurfIdRep_c` was emitted after
  `moCStringHandle_c`; the native full control places it before `moFR_c`/`moExtObject_c`.
- The partial reverse `moCStringHandle_c` archive byte at +14 is 0x39 (forward 0x38); it was taken
  from the forward template.

The builder now reads both serials from the base's `moCompFeature_c` records and patches every
surface-reference slot independently; full element order and the reverse archive byte are fixed;
and each layout is gated on its exact normalized stripped-base signature (folder count and 0x67
value zeroed). Unit fidelity tests reproduce the native feature block byte-for-byte:
forward 180 exactly, reverse 180 and full 360 except the allocated feature id. Mismatched bases
(90, 360_resave, forward vs reverse vs full), wrong folder count/child count and modified bases are
rejected before any stream change.

The earlier `OpenDoc6=65536` reports for the stripped 180 baseline and the 2*pi file built on it
are `swFileWithSameTitleAlreadyOpen`: the stripped/authored files derive from `revolve_180`, so
SolidWorks can report a title conflict when the native 180 control is already open in the same
session. Re-verification should use unique filenames, close control documents first, and author
2*pi only on a `revolve_360`-derived stripped base.

## Additional controlled API samples (2026-10-30)

Source: `reverse_workspace/validation/agent5-feature-samples-20261030/samples-revolve-options2/`
and `samples-revolve-cut/`. Copied without modification. These use the measured recipe:
construct the construction axis line first, then the rectangle, exit the sketch, select
the sketch only, and call `FeatureRevolve2`. All were independently reopened in a fresh
SolidWorks process.

| Sample | SHA-256 | Native readback |
| --- | --- | --- |
| `revolve_thin_90` | `FE3B12569F8103060AFFDEA657B418A1045C1450C4B1BA35739A1CA303E95340` | thin=true, angle π/2, wall .001, V=6.91150383789755e-7 |
| `revolve_thin_360` | `5F670B9F1F53ECE99EE9862183C54F9385631341B09D73C7F9812C55D8C8BD81` | thin=true, angle 2π, wall .001, V=2.76460153515902e-6 |
| `revolve_both_directions` | `995D1070A4A63A76A64F0370089E749F3B80E0D6728CB0E023B3A690BE7E1AD1` | singleDir=false, angle1 π/2, angle2 π/4, V=2.35619449019234e-6 |
| `revolve_cut_360` | `BAF5F860A975E165EB2B1968114667947E0FF6687C0DFDB34001B8C89EF0909B` | cut=true, angle 2π, V=2.81638893321908e-6 |

All have OpenDoc6 0/0, `ForceRebuild3(true)`, feature error 0, 1 body, and verified
resaves. `revolve_cut_90` returned NULL and has no file: the same cut profile is not
accepted at 90 degrees. Axis edge/ref-axis and UpToSurface variants are not yet
available; the current samples use the in-sketch construction line axis
(`GetAxisType()=10`). These are native API-produced controlled samples, not library
`AddRevolve` outputs.

## Thin / both-directions / cut reading (2026-10-30, Agent 1)

Single-variable source/resave comparison established three additional serialized classes.
`RevolveFeatureParser` now accepts only the exact measured layouts below; unknown classes,
TypeIds, stream versions, EndSpec modes, angle/parameter names and axis headers return
unresolved (`Definition == null`), never a guessed default. `RevolveFeatureDefinitionData`
gained the same `ResolvedProperties`/`IsResolved` contract as the other typed feature data.

| Sample class | Folder TypeId | Stream | EndSpec u32@0 | Named parameters | Axis header | Fields |
| --- | ---: | ---: | ---: | --- | --- | --- |
| `moRevolution_c` single | 0x4E | 0x70 | 1 | D1 = angle | `0x8064`, u32 3 | Angle, Type=0, BothDirections=false, AngleDirection2=0, ReverseDirection |
| `moRevolution_c` both | 0x4E | 0x70 | 0 | D1 = angle1, D2 = angle2 | `0x8064`, u32 3 | Angle=π/2, AngleDirection2=π/4, Type=2, BothDirections=true |
| `moRevolutionThin_c` | 0x50 | 0x70 | 1 | D1 = angle, D3 = thickness1, D4 = thickness2 | `0x8064`, u32 5 | IsThinFeature=true, ThinWallType=0, Thickness1=0.001, Thickness2=0 |
| `moRevCut_c` | 0x55 | 0x71 | 1 | D1 = angle | `0x8065`, u32 3 | IsCut=true, Type=0, angle=2π |

Structural details:

- EndSpec: u32@0 is 1 for single-direction and 0 for both-directions; bytes 4..27 must be zero.
  Both-directions requires exactly one D2 named parameter and no D3/D4.
- Angle parameter: solid/thin/both use `moDisplayAngularDim_c -> moFeatureDimHandle_c ->
  AngleDim_c -> moAngleParameter_c`; the cut layout uses `moDisplayAngularDim_c -> AngleDim_c ->
  moAngleParameter_c` (no feature-dim handle). The complete named-parameter header is validated and
  D1 must equal the angle element value within 1e-12.
- Thin: `moThinSpec_c` has an empty payload (`thinSpec.DataStart == next element tag`) and is
  immediately followed by `moDisplayDistanceDim_c`, `ParallelPlaneDistanceDim_c` and
  `moLengthParameter_c`. This empty one-direction layout is the evidence for ThinWallType=0.
  D3=0.001 (thickness1) and D4=0 (thickness2) are read from the complete parameters. Non-empty
  thin payloads and missing/duplicated D3/D4 are rejected.
- Both-directions: Type=2 is the native API readback for the mode-0 + D2 layout; it is not inferred
  for single-direction samples (Type=0).
- Copy axis: u32 reverse at `moLineRef_w - 19`, thirteen preceding zero bytes and the measured
  `0x8064/0x8065` + u32 3/5 + u32 1 tail are required for every class.
- Cut: `moRevCut_c` is recognized as a Cut feature whose Definition is
  `RevolveFeatureDefinitionData` with IsCut=true; it sits on an ordinary BaseBox extrusion and uses
  stream version 0x71. Full 2π only.

### Implementation matrix and authoring gaps

| Layout | Read/resolution | Authoring |
| --- | --- | --- |
| Thin one-direction 90° / 360° | yes, marked | supported on the exact thin_90/thin_360 stripped base (Thickness1 patched; angle selected by template) |
| Both directions (π/2 + π/4, Type=2) | yes, marked | supported on the exact both-directions stripped base; D1/D2 angle parameter copies patched |
| Cut 360° (`moRevCut_c`, 0x71) | yes, marked | supported on the exact cut_360 stripped base (full 2π only) |
| Single-direction solid (existing) | yes | existing controlled AddRevolve path unchanged (0x70, measured bases) |
| Thin midplane/two-direction or cut 90 | no sample | rejected |
| `Merge` | no explicit field identified | model default only; not marked resolved |

### Template authoring (2026-10-30)

`AddRevolve` now routes thin/both/cut definitions to `RevolveOptionFeatureBuilder`, which appends the
complete measured native block to an exact stripped base. Stripping = truncate at the class tag,
header folder count -1, preceding 0x67 value zeroed; authoring restores the native count/marker and
appends the block:

| Template | Base length | Stripped folders | Marker | Normalized base SHA-256 | Patched slots |
| --- | ---: | ---: | ---: | --- | --- |
| Thin90 | 9314 | 19 | 7 | `9ebbd13ce19ed469db1bf80ed86b5fe9b1cf395127ce84fc6d2fa99231c5a09c` | D3 thickness +32/+0x1A6/+0x1BE/+0x250/+0x268 |
| Thin360 | 9314 | 19 | 7 | `71eb8a114af4459831391bb292fef91019e23e18a3f6e6e2a3e6e3824da41380` | D3 thickness copies |
| Both | 9314 | 19 | 6 | `6707ee646bfda4a4c4f7ce926dad47aa1b3fcfbd930d4829238036cf57dca33c` | D1/D2 angles at +32/+0x23A each |
| Cut360 | 15567 | 21 | 13 | `8d082e782e4845b9f31db72faf583db589a5e0a9a4ddfd8dd01942521cdc8ed3` | none (full 2π) |

- Thin authoring accepts only angle π/2 (Thin90 template) or 2π (Thin360 template), positive
  Thickness1, Thickness2=0, ThinWallType=0, Type=0. Arbitrary thin angles are rejected because the
  partial/full thin topology and caches differ.
- Both-directions requires Type=2 and two angles in (0,2π]; only the D1/D2 parameter copies are
  patched. BBox and sin/cos display caches remain from the π/2+π/4 template and rely on native
  rebuild/recompute.
- Cut requires IsCut=true and exactly 2π; no angle slot is patched.
- Feature names are fixed to the measured block names; custom names are rejected rather than
  resizing the block. Any mismatched base signature, folder/0x67 count, stream version or parameter
  layout fails before the stream is modified.
- Native-value authoring is byte-identical to the measured source for all four templates (tests),
  and SldFile resave preserves the stream bytes.
- Validation files:
  `reverse_workspace/validation/agent1-template-authoring-20261030/`
  (`revolve_thin_90_wall2`, `revolve_both_a05_a025`, `revolve_cut_360_native`, resaves and manifest).
- Thin thickness changes do not analytically recompute the revolve BBox/display caches; Agent 5 must
  confirm the native rebuild. If those caches prove stale, Thickness1 can be restricted to 0.001 or a
  measured thickness-variation sample must be added.

All new samples and resaves are covered by `RevolveOptionReadingTests`; tampering (wrong class
TypeId/version, non-empty ThinSpec, missing D3/D4/D2, mode 2, zero/renamed angle, wrong axis tag)
returns null. `RevolveTemplateAuthoringTests` covers native-fidelity roundtrips, patched angles/
thickness, resaves, and rejection before mutation.

### Remaining Task 1.2 field audit

| Field / layout | Classification | Evidence / action |
| --- | --- | --- |
| In-sketch construction-line axis | implemented | all controlled samples use `GetAxisType()=10`; axis reference/header decoded |
| Axis edge / reference axis | blocked | no controlled sample; axisType 1/other layouts absent |
| Revolve UpToSurface | blocked | no controlled sample |
| UpToVertex / UpToBody | blocked | no controlled sample |
| Non-Blind termination | blocked | all samples are Blind |
| Thin MidPlane / TwoDirection | blocked | only one-direction thin 90/360 samples |
| Full-reverse | blocked | reverse sample is 180° only; full-reverse authoring rejected |
| Merge flag | blocked | no explicit scalar/field evidence; model default only |
| Thin/both/cut layouts | implemented | measured block readers/templates above |

Blocked layouts are rejected/unresolved; no default value is promoted to a file value.

### Native re-verification requested from Agent 5

- Reopen the four thin source/resave files and assert `IRevolveFeatureData2`: IsThinFeature=true,
  ThinWallType=0, GetWallThickness(true)=0.001, reverse thickness 0, angles π/2 / 2π, volumes
  6.91150383789755e-7 / 2.76460153515902e-6.
- Reopen both-directions source/resave: bothDirections/Type=2, angles π/2 and π/4, volume
  2.35619449019234e-6.
- Reopen cut source/resave: cut=true, full 2π, volume 2.81638893321908e-6, 1 body.
- Confirm the cut feature is exposed as a cut-class feature and the parser's IsCut=true matches the
  native `IRevolveFeatureData2` state.
- Open the template-authored files in
  `reverse_workspace/validation/agent1-template-authoring-20261030/`:
  `revolve_thin_90_wall2` (angle π/2, thickness 0.002), `revolve_both_a05_a025` (angles 0.5/0.25,
  Type=2), `revolve_cut_360_native` (2π, expected V=2.81638893321908e-6). Confirm OpenDoc6 0/0,
  ForceRebuild3, API readback, volume/centroid, and that the resave hashes stay unchanged.

