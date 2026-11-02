# Fillet options: controlled native samples (2026-10-30)

Source: `reverse_workspace/validation/agent5-feature-samples-20261030/samples2/`
(copied without modification). These four samples are controlled evidence for
Task 1.3 fillet reading/authoring. Do not overwrite them.

| Sample | Source file | SHA-256 |
| --- | --- | --- |
| `fillet_5mm.SLDPRT` | `fillet_5mm.SLDPRT` | `4bb28cb4b15c780ad2e073d80a1bb638e9e4b2d67b9e8c69677aedd9dca9ff95` |
| `fillet_5mm_resave.SLDPRT` | `fillet_5mm_resave.SLDPRT` | `7bfcb6fb1e48f2b98c1f7ac20e7be6665ba72212493f1ae93a566c5b39e39ed7` |
| `fillet_variable_2_4mm.SLDPRT` | `fillet_variable_2_4mm.SLDPRT` | `79c25efff557338ff063020a6e8c1fcf27cd55e394ddf91d469c2792c9efa578` |
| `fillet_variable_2_4mm_resave.SLDPRT` | `fillet_variable_2_4mm_resave.SLDPRT` | `ae160d8a3063c0332a3b99c1e0455acdfe483b4dabd549f325e234aef2a2c27` |

Both sources and resaves are native SolidWorks 2026 parts with ResolvedFeatures
stream version 0x70, independently reopened with OpenDoc6 errors/warnings 0 and
`ForceRebuild3(false)=true`. Constant fillet: radius 5 mm, `ISimpleFilletFeatureData2`
with one edge; volume `3.94634954084936e-7 m^3`. Variable fillet: 2 mm/4 mm control
radii, `IVariableFilletFeatureData2` with one edge; volume `3.97996103904772e-7 m^3`.
Persist references for the fillet seed edge are captured before feature creation
(349 bytes).

## Variable-radius layout (reading, 2026-10-30)

`fillet_variable_2_4mm` (and its resave) serialize as **`VarFillet_c` with FolderHeader
TypeId 0x52** — the same numeric TypeId as `Chamfer_c`; the class name is authoritative.
It does not contain `moCompFace_c` or `edgeRadiusObject_c`. Measured element sequence
(main and resave payload lengths are identical):

| Element | Length |
| --- | ---: |
| `VarFillet_c` | 311 |
| `moSurfaceIdRep_c` | 14 |
| `moFilletSurfIdRep_c` | 20 |
| `moEdgeRef_c` | 146 |
| `moEdgeDim_c` | 0 |
| `moCompEdge_c` | 162 |
| `moVertDim_c` | 0 |
| `moDisplayRadialDim_c` | 665 |
| `ThreeDRadiusDim_c` | 1100 |
| `moCompVertex_c` | 86 |
| `moVertexRef_c` | 2228 |

Two control-point radii are stored as named parameters:
- first control point: `ThreeDRadiusDim_c` schema at +6, name `D0`, value 0.002 at +38;
  positive copies at +38/+70/+94/+219/+318/+552/+576/+1024/+1048.
- second control point: `moVertexRef_c` schema at +830, name `D01`, value 0.004 at +864;
  positive copies at +864/+904/+928/+1045/+1144/+1378/+1402/+1850/+1874; the block also
  carries a negative copy of the first radius (-0.002) at +1136.

`SldFile.Binary.FeatureData.FilletChamferFeatureParser.ParseVariableFillet` accepts only
stream 0x70, the exact object set/lengths, the D0/D01 names and all measured copies; it
populates `VariableFilletFeatureDefinitionData.ControlPointRadii` (stream order, mapping to
start/end vertices not decoded). `DefaultRadius`, `DefaultDistance`, transition/overflow
flags, edge count and control-point positions remain unresolved. Constant `Fillet_c`
(`Fillet_c`/0x51) parsing is unchanged and the two parsers reject each other's layouts.

Authoring: `SldPart.AddVariableFillet(VariableFilletFeatureDefinitionData)` now performs the fixed
measured 2 mm/4 mm, one-edge, two-control-point injection on the exact stripped source or resave
base (stream 0x70, stripped folder count 20, 0x67 sketch marker value 1 retained). The complete
4953-byte measured block is appended and the folder count restored to 21. The definition must
contain exactly `ControlPointRadii = [0.002, 0.004]` and may not override edge/default/transition/
vertex fields; custom feature names, wrong radii or a mismatched base signature/folder/marker are
rejected before the stream is modified. Native-value authoring is byte-identical to the measured
stream (tests and `reverse_workspace/validation/agent1-variable-fillet-authoring-20261030/`).
`VariableFilletAuthoringTests` covers source/resave fidelity, write→read `ControlPointRadii`,
resave, wrong radii/edge/default/transition overrides, custom names, wrong bases and folder/0x67
tampering.
The radius pair is not parameterized yet: no 1/3 mm or 3/5 mm variation samples exist to establish
which cross-copies/derived slots scale. Needed for parameterization: variable pairs such as 1/3 mm
and 3/5 mm (with resaves), 3+ control points, multiple edges, and native
`GetControlPointsCount`/`GetControlPointRadiusAtIndex` snapshots.

## Persistent edge selection (reading, 2026-10-30)

`Fillet_c` requires exactly one `moEdgeRef_c` (payload 146/148/152 B) and one
`moCompEdge_c` (178 B); `VarFillet_c` requires its measured `moEdgeRef_c` (146 B) and
`moCompEdge_c` (162 B). The edge reference header validates `u32 +0 = 1`, `+4 = 0`,
`+8 = 4`, class word `+12` 0 or 0x200 and the 16-byte archive GUID
`7dc39425ad49b2547dc39425ad49b254`, then four 20-byte records
(`0x80F1`/`0x80E8`, `0x80EA`, `0x00ED`, `0x004F`, u16 0, nonzero serial, u32 id).
Two measured record orders occur: A `F1/1, F1/4, E8/1, E8/0` (fillet_5mm, variable
fillet, chamfer) and B `E8/1, F1/2, F1/1, F1/3` (fillet_2mm).

The single decoded edge is exposed in `Edges` as `EntityType=Edge`, `EntityId=4`,
`RecordCount=4` with the opaque class word, persistent id and GUID verbatim.
Fillet/variable samples carry class word 0x200; measured persistent ids are
fillet_5mm `0x20D0` (main) / `0x232D` (resave), fillet_2mm `0x38D9`, variable
`0x254A` main and resave. Chamfer samples carry class word 0 and persistent id 0 but the
same edge id 4 and GUID. These values are opaque: fillet_5mm's persistent id changes on
resave while the entity id stays 4. Unknown record orders, sizes, class words, GUIDs or
edge ids are rejected (no guessing).

Chamfer selection details are in `../ChamferOptions/README.md`.

## Type=2 face fillet (reading/authoring, 2026-10-31)

Samples: `fillet_face_3mm(.SLDPRT/_resave)`, `fillet_face_5mm(.SLDPRT/_resave)` from
`reverse_workspace/validation/agent5-feature-samples-20261030/samples-fillet-chamfer-extra/`
and `samples-fillet-face5/`. Native snapshot: `ISimpleFilletFeatureData2.Type=2`,
defaultRadius 0.003/0.005 m, edgeCount 0, two faces; V=3.980685834705771e-6
(3 mm) and 3.946349540849362e-6 (5 mm).

Layout (stream 0x70, block 3190 B): `Fillet_c` 309 | `moFaceBlendSurfIdRep_c` 20 |
`moFaceBlendItemPair_c` 0 | `moFaceBlendItem_c` 354 | `moCompFace_c` 536 |
`moDisplayRadialDim_c` 665 | `ThreeDRadiusDim_c` 1147. There is no Type scalar in
the Fillet_c header; Type=2 is the measured mapping of this face-blend layout.
- `moCompFace_c` has two 268-B blocks with 47-B face references:
  faceIds 0x5D2 then 0x5CE, classWord 0x300, faceCount 6, surface 0x80E6,
  mapping 0x80F1, archive GUID `7dc39425ad49b2547dc39425ad49b254`.
- `moFaceBlendItem_c` independently carries two 40-B 0x80E6 records with the same
  faceIds/order and classWord 0x200. `FilletFeatureDefinitionData.FaceReferences`
  exposes the two compFace references in order with `RecordCount=6`.
- Radius D1 is the named parameter at ThreeDRadiusDim +6/+0x26; measured copies:
  +0x26,+0x46,+0x4E,+0x5E,+0x66,+0xF6,+0x1E0,+0x1F8 (positive), -r at
  +0x93,+0x9B,+0x3B8,+0x3D0, -r/2 at +0xEE. Derived tiny/tolerance slots
  +0xD6/+0x198 and +0x3C0/+0x3D8 differ non-linearly between 3 and 5 mm, so general
  radius parameterization is not exposed; reading accepts the two measured radii.
- Type=1 edge fillet remains `Type=null` and cannot contain face-blend objects;
  face layouts reject edge objects/`moFaceRef_c`/`moSingleFaceRef_w`. Unknown,
  missing or tampered face/radius/header data return null.

Authoring: `SldPart.AddFillet(Type=2, DefaultRadius=3/5mm)` selects the exact source
or resave measured base (SHA/length/folder 20/0x67 marker) and appends the 3190-B
native block verbatim. Custom names, other radii, face overrides, and other fields
are rejected before stream changes. `FaceFilletAuthoringTests` covers source/resave
3/5 mm, exact native-block equality, parsed-pair reuse and rejections. Artifacts:
`reverse_workspace/validation/agent1-face-fillet-authoring-20261030/`.

## Native variable-fillet pairs 1/3, 2/4, 3/5 mm (2026-10-31)

Samples: `fillet_variable_native_{1_3,2_4,3_5}(_resave)` from
`samples-fillet-variable-native/`; native volumes 3.9906936393240605e-6,
3.9799610390477166e-6, 3.964938366865182e-6. The existing 2/4 baseline is unchanged.

Parser correction: the negative copy at `moVertexRef_c` +1136 is **-D01/2**
(-0.0015 / -0.002 / -0.0025), not -D0; this is why the old 2/4-only check happened
to pass. All three pairs now parse with D0/D01 named values and measured copies.

Authoring uses six exact native templates (3 pairs x source/resave, 4953-B blocks,
folder 20, marker value 1). 1/3, 2/4 and 3/5 diffs also change derived
control-point/session fields (e.g. +0x70, tiny tolerance/session bytes), so arbitrary
pairs or single-radius scaling remain rejected. `SldPart.AddVariableFillet` selects
the template by exact base SHA and pair, rejects mismatched pair/custom name/base,
and reproduces the native block byte-for-byte. Tests:
`NativeVariableFilletAuthoringTests`. Artifacts:
`reverse_workspace/validation/agent1-native-variable-fillet-authoring-20261030/`.
