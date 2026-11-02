# Pattern options: controlled native samples (2026-10-30)

Source: `reverse_workspace/validation/agent5-feature-samples-20261030/samples/`
(copied without modification). These four samples are the controlled evidence for
Task 1.5 linear/circular pattern reading (and any later controlled authoring).
Do not overwrite them.

| Sample | Source file | SHA-256 |
| --- | --- | --- |
| `lpattern_2dir_2x3_10_15.SLDPRT` | `lpattern_2dir_2x3_10_15.SLDPRT` | `8a6512c841c11b6f5975c80035591efeaffeec86cb119d246b4d0de2ee526ade` |
| `lpattern_2dir_2x3_10_15_resave.SLDPRT` | `lpattern_2dir_2x3_10_15_resave.SLDPRT` | `15fdad0a2936cb8abe86d281a3de7d37b10aea6f362af8480516b295d2cf353f` |
| `cirpattern_4x90.SLDPRT` | `cirpattern_4x90.SLDPRT` | `d07aa43307fd4ae956818654309f3a08f65a6433dc74cea2af0a0d860eb8da7f` |
| `cirpattern_4x90_resave.SLDPRT` | `cirpattern_4x90_resave.SLDPRT` | `87fd31a01fc3eb5c9e8776a734f52e1dbbf6b2d7b3eb77ed0da7b7f59fcdbb3e` |
| `mirror_plane.SLDPRT` | `mirror_plane.SLDPRT` | `2addfeb21cc5b4f1fcb08d6b64b6183b7a35c7685842e8ec4afc05db2745f133` |
| `mirror_plane_resave.SLDPRT` | `mirror_plane_resave.SLDPRT` | `852001539b4ae49531485f48a20b4b19fa6b641b6711b341562b26dbf758de2d` |

The mirror sample mirrors the valid base extrusion across the Right Plane
(`右视基准面`), not the ICE seed: 2 mirror instances / 1 source feature,
OpenDoc6 0/0, rebuild true, V=8.00628318530718e-6, centroid
(1.5696e-6, 0.0099937, 0.0050047), feature error/warning 0/0. Mirroring the
ICE boss seed produced error 1/warning and no geometry, so it was rejected.

All four are native SolidWorks 2026 parts with ResolvedFeatures stream version 0x72.
Linear: 2 directions, 2 x 3 instances, spacings 10 mm / 15 mm. Circular: 4 instances, 90°.
The mirror sample (`mirror_plane`, 0x72) mirrors `BaseBox` across the Right Plane; see below.

## Controlled authoring scope (2026-10-30)

`SldPart.AddLinearPattern` / `AddCircularPattern` inject one measured feature block at the end of
the stripped base body (truncated immediately before the sample's pattern class tag, folder count
decremented). The controlled base prefix signatures (SHA-256 with the folder-count u16 at offset 4
zeroed) are `8c116424…` (linear) and `652ab7b7…` (circular); any other body, an existing pattern,
a non-0x72 stream, a missing `moSketchChain_c` or a missing 0x67 marker is rejected without
modifying the stream.

- **Native audit (2026-10-30) and measured-only restriction.** Agent 5 found that authored
  non-measured parameters read back correctly but left the body volume at the template
  count: linear 3×4/12/18 stayed at the native 2×3 volume `4.01256637061436e-6 m³`, and
  circular 6×60 stayed near the native 4×90 volume. A whole-file comparison shows the
  native and authored files contain 43 OLE streams; **only
  `Contents/Config-0-ResolvedFeatures` differs**, while all DisplayLists/Config-0/sketch/
  seed/cache streams remain byte-identical to the native 2×3 or 4×90 source. The instance
  geometry/caches are therefore outside the parameterized feature block; no native 3×4 or
  6×60 samples exist in the workspace to measure those streams. The non-measured files are kept
  as evidence in `reverse_workspace/validation/agent1-pattern-nonmeasured-20261030/` with
  `manifest.json` and `sldfile-readback.txt`:
  - SldFile readback is correct: linear D1=3/0.012, D2=4/0.018; circular total=6,
    spacing=π/3, EqualSpacing=true (source and resave).
  - Expected volumes/instances: linear 3×4 = 12 instances and `8.02513274122872e-6 m³`;
    circular 6×60 = 6 instances and `6.028303471921959e-6 m³`. Observed volumes remain
    `4.01256637061436e-6` and `4.0159654767092206e-6`, i.e. the native 6/4-instance geometry.
  - Agent 5 previously reopened/rebuild these files with OpenDoc6 0/0 and hash unchanged, but in
    `independentProcess=false`; the manifest requests a fresh-process independent recheck.
  Until such samples are provided, authoring is restricted to the specifically measured values:
  - Linear: only D1TotalInstances=2, D2TotalInstances=3, D1Spacing=0.010 m,
    D2Spacing=0.015 m. Written to `moIntegerParameter_c` D1 count +32, D2 count +3274,
    D1 spacing +1533 plus copies {1581,1605,1923,1947,2093,2117,3009,4823,4847}, D2
    spacing +4775 plus copies {5165,5189,5335,5359}; display at `moDisplayDim_c` +440.
  - Circular: only TotalInstances=4, Spacing=π/2 (90°), equal spacing preserved.
    Written to `moIntegerParameter_c` count +32, `moCompEdge_c` spacing +1121 and copy
    +1659, and `moAngleParameter_c` per-instance angle +32 = π/6.
- Any other non-measured parameter set (even within the former 2..1000 / >0 ranges) throws
  `NotSupportedException` without touching the stream. Invalid ranges/NaN/≤0 still throw
  `ArgumentOutOfRangeException`.
- Unit test `PatternAuthoringTests` authors the native values with the native names and
  verifies the emitted feature block is byte-for-byte identical to the native block, and
  asserts that 3×4/12/18 and 6×60 are rejected with the stream unchanged.
- Not supported: non-measured instance counts/spacings/angles, geometry pattern, vary
  sketch, instances to vary, symmetric/reverse/direction-2, end conditions, feature scope,
  body/face patterns, and any non-default flag or reference. Needed samples to lift the
  restriction: native `lpattern_1dir_3x15`, `lpattern_2dir_2x3_20_25`, geometry-pattern
  on/off, `cirpattern_6x60`, equal-spacing on/off, each with resaves and per-stream diffs.

## Mirror pattern (2026-10-30)

The mirror sample serializes as **`moMirrorPattern_c` with FolderHeader TypeId 0x57** (the
catalog's 0x55 was not observed) on stream version 0x72. Native feature name `Mirror`; payload
1201 B, of which 1163 B follow the schema/name/double/TypeId/empty-WChar header.

Measured anchors:
- feature-id magic `56 68 dd 01` at payload +143, id `0x1E5F19F1` at +147 (rest +109).
- source-feature persistent-reference blob at payload +360..480 (rest +322): five records of
  `f2 80 f5 00 4f 00 00 00 <serial 0x6AE46BC8> <id> ...` with ids 4/0/1/1/3, one source
  extrusion (`BaseBox`).
- Right-Plane reference block at payload +514 (rest +476):
  `01 00 00 00 6C 80 33 00 00 00`.
- plane-transform tail doubles 1.0 / 1.0 / -1.0 / 1.0 (payload +1084/+1100/+1116/+1164).
- main and resave payloads are identical except the header double (2.0 / -2.0) and one byte.

`ParseMirrorPattern` accepts only this controlled layout: plane-reference form
(`MirrorPlaneType=1`), a presence-only `Plane` reference and one presence-only source-feature
reference (`GetPatternFeatureCount()=1`). GeometryPattern, FeatureScope, mirror faces and
visual-property propagation remain unresolved. Unknown layouts, tampered source/plane blocks,
wrong class/TypeId/version and non-mirror streams return null.

`SldPart.AddMirrorPattern` emits the measured 1163-B rest template on the extrusion-stripped
base (truncated at the mirror tag, folder count 22, normalized base SHA-256
`8e911328454eb414ab917297f669cb4fc06ff89b1d187520f07f40efd259305e`), patching only the feature
name and allocated id `0x1E5F19F1`; folder count becomes 23. Authoring the native name/values
reproduces the native feature block byte-for-byte. GeometryPattern/feature scope/mirror faces/
PropagateVisualProperty, other planes or sources, other bases and non-0x72 streams are rejected
before the stream is modified.

## Native variant parameterization (2026-10-31)

Native API-created variants (not ResolvedFeatures edits) are stored in
`reverse_workspace/validation/agent5-feature-samples-20261030/samples-pattern-native/`
and copied here as `lpattern_native_*`, `lpattern_1dir_3x15`,
`lpattern_2dir_2x3_20_25`, `lpattern_geometry_on/off`, `cirpattern_native_4x90`,
`cirpattern_6x60`, and `cirpattern_equal_on/off` (all with resaves). The legacy
2x3/4x90 baselines remain unchanged under their original names.

Measured slots (ResolvedFeatures 0x72, main/resave stable):
- Linear: D1 count = `moIntegerParameter_c` +32, D2 count = +3274; D1 spacing D3
  named value +1533 plus copies {1581,1605,1923,1947,2093,2117,3009,4823,4847};
  D2 spacing D4 +4775 plus copies {5165,5189,5335,5359}; display dim +440.
  One-direction samples have D2TotalInstances=1. Geometry-pattern on/off has no
  unique scalar: it changes the feature block length (11592/11518) and layout;
  `GeometryPattern` stays null and unresolved.
- Circular: total count `moIntegerParameter_c` +32; D3 spacing in `moCompEdge_c`
  (native equal layout +0x465, old +0x461; value is 2*pi for equal-off);
  per-instance angle D2 in `moAngleParameter_c` +32. Equal-on: angle = D3/(N-1),
  API Spacing = D3. Equal-off: angle = D3/N, API Spacing = D2; `EqualSpacing=false`.
- `NativePatternAuthoringTests` covers all 9 main + resave variants, values,
  tampering, direction/source strictness, and native-block equality.

Authoring uses one complete measured native block per parameter/base signature
(`PatternNativeAuthoringTemplates`). The stripped base is created from the matching
native sample, so the other 42 OLE streams (DisplayLists, Config-0-Partition and
instance caches) already contain the correct instance geometry. AddLinearPattern /
AddCircularPattern select the native template by normalized base SHA, reject
mismatched values/custom names, and emit a block byte-identical to the native sample.
Only the measured parameter sets are authorable: arbitrary counts/spacings/angles
cannot be synthesized because the instance caches live outside ResolvedFeatures.
Artifacts: `reverse_workspace/validation/agent1-native-pattern-authoring-20261030/`
(18 authored files + resave copies + manifest with expected volumes/areas).
