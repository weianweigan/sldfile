# Surface options: controlled native samples (2026-10-30)

Source: `reverse_workspace/validation/agent5-feature-samples-20261030/samples-surface/`.
Copied without modification.

| Sample | SHA-256 | Native `ISurfExtrudeFeatureData` |
| --- | --- | --- |
| `surf_extrude_10mm.SLDPRT` | `2b6f7adc2e3cef6f827497f47e3a7eb58a30e38d918860b528cd58cffe54da53` | depth .010, end condition 0, CapEnd1/2=false, Draft on/off false; surface body |
| `surf_extrude_10mm_resave.SLDPRT` | `4b9868d192a6d51b518c13c600e2ecfb27e2920e5648d54b4895557894351e85` | same definition; rebuild verified |
| `surf_revolve_360.SLDPRT` | `7c6b11950272de48027309c3a975f2969e254fe87f31d38ef3810a5c058bdcb7` | `ISurfRevolveFeatureData` type=0, angle=2π, reverse=false; surface body |
| `surf_revolve_360_resave.SLDPRT` | `ef2a1ddf04231587daaf3802d95361aeb778a694bda83635116a837ca9c7e689` | same definition; rebuild verified |

Both OpenDoc6 0/0, `ForceRebuild3(true)`, feature error/warning 0, hash unchanged. The result is a surface body (no solid mass properties). Full `ISurfExtrudeFeatureData` snapshot is in `verify-surface.json`.

## Not yet sampled

Surface revolve, knit, trim, extend, offset, fill, planar surface; weldment structural member/end cap/gusset/weld bead; assembly mates.
