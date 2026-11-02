# Two-direction extrusion: controlled native evidence (2026-10-30)

Four samples were created in this new directory; no previous sample was overwritten. FeatureLab snapshots now include native IExtrudeFeatureData2.GetDepth(true/false), both end conditions, BothDirections and ReverseDirection after AccessSelections, before save and after a document reopen + ForceRebuild3 in the same SOLIDWORKS process (process isolation was not enforced for this reading slice). Fresh-process isolation of the later authoring implementation is covered by the Agent 5 audit (`reverse_workspace/validation/agent5-audit-20261030/manifest.json`). Native geometry also agrees with depth arithmetic.

| Sample | native D1 m | native D2 m | BothDirections | volume m3 | reopen/rebuild |
| --- | ---: | ---: | --- | ---: | --- |
| extrude_10mm | .010 | 0 | false | 1e-6 | verified |
| extrude_two_direction_3mm | .010 | .003 | true | 1.3e-6 | verified |
| extrude_two_direction_3mm_resave | .010 | .003 | true | 1.3e-6 | verified |
| extrude_two_direction_7mm | .010 | .007 | true | 1.7e-6 | verified |

Each JSON records source operations, before/after snapshots, rebuild status and SHA-256. Existing earlier extrude_two_direction (.005m) provides an additional amplitude for offline reading.

## Structure

For the controlled stream-version 0x70 moExtrusion_c layout:

1. First moEndSpec_c data has u16 at 0=0, u32 at 2=0 (single-direction control has 1), u32 at 6=0, u32 at 10 in {0,1}, u16 at 14=0, u32 at 16=0 (Blind). The zero flag alone is insufficient to conclude two directions.
2. moFavoriteHandle_c data +436 begins indexed EndSpec reference 0x810B, followed by sixteen zero bytes representing the controlled Blind/no-reverse header.
3. moFavoriteHandle_c data +1111 starts schema 0x8004 and WChar name D2. Immediately before it are u16 0x810F, u32 2 and u16 0x8111 (indexed parallel-distance dimension and length parameter). The value is decoded through the full named-parameter header already established for other options.
4. Parameter storage is bounded by the owning feature's moFromEndSpec_c object. The measured D2 values are .003/.007 and .005 for the earlier control.

The resave shifts Favorite and D2 absolute offsets by +294 bytes (Favorite data 11576 -> 11870, D2 12687 -> 12981), while all relative relationships and native D2 remain unchanged.

## Scope and limitations

This is a deliberately conservative reader for the measured indexed layout, not a general CArchive index resolver. Unknown class-index codes, altered preambles, non-Blind first/second direction or another D2 position are not guessed. The implementation adds DepthDirection2, BothDirections=true and EndConditionDirection2=0 only after the full structure matches. Existing MidPlane compatibility semantics remain unchanged.

Two-direction authoring is implemented from this measured layout (see README.md's Authoring extension section) and was validated by in-process native integration tests plus the Agent 5 fresh-process audit. Broader two-direction layouts (reverse direction 1, cuts, non-Blind second direction, MidPlane combinations) remain unsupported and are rejected. Authoring is bounded to ResolvedFeatures 0x70 streams with a parsed `moSketchChain_c` and the 0x67 sketch marker; option authoring is 0x70 only, while basic Boss/Cut also accepts 0x71. Chain-less sketches (including Sketch2 of the 0x71 cut sample) and 0x6E parts are rejected before any stream mutation.

## Verification

SldFileTests/ExtrudeOptionReadingTests exercises native .003/.005/.007 plus resave, asserting direction-1 depth stays .010m and both-direction semantics; changing the indexed EndSpec reference prevents binding. All option-reading tests passed 24/24, zero skipped. SldFileTests build passed with zero warnings/errors. FeatureLab build succeeded with 14 existing nullable warnings in its research tooling.
