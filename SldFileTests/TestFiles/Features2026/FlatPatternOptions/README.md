# Flat pattern option variants (2026-10-30)

Source: `reverse_workspace/validation/agent5-flatpattern-variants2-20261030/samples/`.
Copied without modification. All files use ResolvedFeatures version 0x73,
class `moFlatPattern_c`, FolderHeader TypeId 0x5D, one native fixed-face
selection. `flatpattern.json` and `verify.json` contain the IFlatPatternFeatureData
snapshots and rebuild results.

| Sample | MergeFace | SimplifyBends | CornerTreatment | BreakCornerType/Radius | CornerTrimReliefType/Distance | Volume |
| --- | --- | --- | --- | --- | --- | --- |
| `flat_merge_face_true` | true | true | true | 0 / 0.005 | 0 / 0.005 | 2.0e-7 |
| `flat_merge_face_false` | false | true | true | 0 / 0.005 | 0 / 0.005 | 2.0e-7 |
| `flat_simplify_bends_false` | true | false | true | 0 / 0.005 | 0 / 0.005 | 2.0e-7 |
| `flat_corner_treatment_false` | true | true | false | 0 / 0.005 | 0 / 0.005 | 2.0e-7 |
| `flat_break_corner_chamfer` | true | true | true | 1 / 0.002 | 0 / 0.005 | 2.0e-7 |
| `flat_corner_trim_square` | true | true | true | 0 / 0.005 | 1 / 0.002 | 2.0e-7 |
| `flat_ratio_false_2` | true | true | true | 0 / 0.005 | 0 / 2.0 | 2.0e-7 |

All samples also report `UseRatioToThickness=false`,
`ShowSlitInCornerRelief=true`; `ExcludedFaces` raises COMException and
`featureBody=null` in the native snapshot.

## Verified option slots

The option block starts immediately after `moFaceRef_c` (anchor =
`moFaceRef_c.DataEnd`); all offsets below are relative to that anchor. The
`moFlatPattern_c` class payload itself is identical across variants.

| API value | Serialized evidence |
| --- | --- |
| `MergeFace` | bit 0 of flags byte at +0x21 (1=true) |
| `CornerTreatment` | bit 1 inverse: `(flags & 2) == 0`; baseline true=0x01, corner false=0x03 |
| `CornerTrimReliefType` | u32 at +0x37 (0 circular, 1 square) |
| `CornerTrimReliefDistance` | double at +0x3B (0.005 baseline, 0.002 square, 2.0 ratio sample) |
| `BreakCornerType` | u32 at +0x43 (0 fillet, 1 chamfer) |
| `BreakCornerRadius` | double at +0x47 (0.005 baseline, 0.002 chamfer) |
| `SimplifyBends` | 0/1 byte at +0x57 |

Layout gates: u16 0xFFFF at +0, u32 0x4A38 at +0x16, u32 1 at +0x1C, byte 1 at
+0x2B and +0x5B. `CornerTrimRatioToThickness` is not separately distinguishable
(the ratio sample stores distance and ratio as the same 2.0 and only one double
changes), and `UseRatioToThickness`/`ShowSlitInCornerRelief` are constant; all
three stay unresolved.
