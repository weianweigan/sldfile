# Native drawing preview archive

The preview uses one internal wire model and a public domain definition:

- `SldDrawing.GetDrawingPreview()` returns detached geometry in metres/radians.
- `SerializeDrawingPreviewStreams()` serializes `Definition`, `VBLists` and
  `DisplayLists`, retaining all opaque bytes and original unmodified ZLB envelopes.
- `SetDrawingPreview()` queues verified display-cache edits; `Save()` persists
  them. It edits the display cache, not the source annotation or model dimension.
  Same-length text and existing command geometry are supported. Unsupported
  topology, shared font/style, projected-edge and paper-size edits fail before
  the live document is changed.
- WPF and SVG consume the same definition. The compatibility readers no longer
  generate borders, discover geometry by plausible coordinates, or fit model
  bounds to paper. VBLists edges are associated by native view name and projected
  with the saved Definition matrix, translation, scale and clip rectangle.

## Wire evidence

`reverse_workspace/tools/output/mfc_classes.json` identifies `uiViewBucket_c`
and `uiLayerMgr_c` in `slddrwu.dll`; tessellation is owned by
`uoTempFaceTessData_c`. Display commands are command records, not invented MFC
feature classes. Their first two u32 fields are a native declaration value and
command type. All supported payload fields use the same `Serialize(MfcArchive)`
method in both directions. An indexed record is admitted only when serialization
reproduces every byte; noncanonical/unknown variants remain opaque.

| Command type | Payload |
| --- | --- |
| 1 | Point and preserved flags |
| 2 | Start, end, centre, normal and arc flags |
| 4 | Two 3D endpoints and trailing flag |
| 6 | Flags, vertex count and filled polygon vertices |
| 9 | Version-dependent preserved display state |
| 10 | Font metrics, native font flags, CString and width/spacing fields |
| 12 | COLORREF, preserved style fields, layer and line-pattern CString |
| 18 | Position, alignment, rotation, CString, glyph advances and optional tail |
| 29 | Object/group fields and four CStrings |
| 30 | Direction and flags |

The 2012 text declaration uses `114 + 10 * UTF16Length` and has no new tail;
the newer declaration uses `146 + 10 * UTF16Length` and has a u32/f64 tail.
Display-state declarations 56/60 contain 30/34 payload bytes respectively.
No source-level byte offset is treated as a document-wide stable address.

Placement records have a leading rotation flag. Native Serialize omits identity
rotation matrices: the observed identity form has translation, scale, 69
preserved bytes and 11 bounds/reference doubles. The rotated form also has a
3-by-3 matrix and 213 preserved bytes. The preserved bytes include additional
native transformation/state fields whose semantics are not yet established.

Each face table has six channels: strip lengths, float3 positions, float3
normals, endpoint data, per-strip endpoint counts and endpoint flags. Channel
sizes, counts, flags, strip totals and cross-channel consistency are validated.
Inactive/suppressed edge records are retained and are not emitted as active
edges. Face caches are projected and clipped for views that use face geometry.

## Validation and limits

The 2012/2026 cube drawings and `D8001507933` roundtrip all three streams byte
for byte. Tests cover compressed envelopes, malformed counts, atomic rejection,
native-coordinate edits, save/reopen, and WPF rendering without a bitmap.

`TestFiles/Drawings/D8001507933/preview-com-reference.json` records independent
SolidWorks 2026 SP0 (34.0.0) COM output from a read-only sample copy. All five
view matrices, translations and scales are compared numerically. Native opening
reported errors=0/warnings=0. A separate C# display-cache edit copy also opened
with errors=0/warnings=0 and `ForceRebuild3(false)` returned true. This validates
container/cache integrity, not persistence of a logical annotation change after
SolidWorks regenerates its caches.

This is lossless stream serialization, not a claim that every native field is
semantically decoded. Special-symbol records, some group/state fields,
materials/lights and multi-sheet ownership remain unresolved. Face preview
uses neutral fill while material response is unresolved. `IsResolved` remains
false and raster preview stays available. No full-vector fidelity claim is made
for those cases. Legacy drawing MFC models are internal and no longer drive
preview rendering or SVG export.
