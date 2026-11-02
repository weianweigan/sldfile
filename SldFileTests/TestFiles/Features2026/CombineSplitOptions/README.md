# Combine/Split/DeleteBody samples (2026-10-30)

Source: `reverse_workspace/validation/agent5-feature-samples-20261030/samples-deletebody/`.
Copied without modification.

| Sample | SHA-256 | Native `IDeleteBodyFeatureData` |
| --- | --- | --- |
| `delete_body_second.SLDPRT` | `d7f6156cc17fcd8facbc8e63d705092a40ce62fe65d118324766b97e4d79ea0f` | KeepBodies=true, bodyCount=1; V=1e-6, area=0.0006, centroid (0.035,0.01,0.005), one body |
| `delete_body_second_resave.SLDPRT` | `2775b8710ca24d2f24b7157d7967bbc6bc21011863ee07563d1f59420a329119` | same geometry/definition |

Both OpenDoc6 0/0, rebuild true, feature error/warning 0, one body, hash unchanged.
The sample deleted the first body and retained the second body (body-centroid x=0.035); the selected-body pick was not accepted, so the API fell back to its default body selection.

## Blocked

- Combine `InsertCombineFeature(0, mainBody, tools)` returned NULL with both `Body2[]` and `Array.CreateInstance(typeof(Body2),1)` tool arrays; no file.
- Split `PreSplitBody2`/`PostSplitBody2` not yet sampled.
- Draft/Rib not yet sampled.
