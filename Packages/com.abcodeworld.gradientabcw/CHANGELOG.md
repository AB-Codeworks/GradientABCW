# Changelog

All notable changes to this package are documented in this file.

## [1.0.0]

Extracted from the VertexColourAnims project and rebuilt from the ground up:

- Class-based `GradientABCW` data model with a generic `KeyArray<TKey>` helper shared by colour
  and alpha keys (insert, remove, re-sort, distribute, flip, neighbour-clamp).
- `GradientModulation` with a per-gradient `bypass` flag, replacing the legacy static
  "Activate/Deactivate Mods" toggle that shared one state across every gradient instance.
- A Burst-compiled evaluation core (`NativeGradient`, `GradientMath`) with managed, job-scheduled,
  and cached (`GradientLutCache`) LUT baking, and colour-space-aware texture baking
  (`GradientTextureUtility`).
- A pure UI Toolkit editor: `GradientABCWField`, a `CreatePropertyGUI`-only property drawer, and
  `GradientPickerWindow` with a draggable key bar, an asset library, and deterministic
  (seeded) mesh/texture colour sampling — no IMGUI anywhere in the package.
- A test suite covering the runtime maths (golden-tested against the legacy implementation),
  serialization, LUT baking, and the UI Toolkit editor components.
