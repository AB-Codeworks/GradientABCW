# Changelog

All notable changes to this package are documented in this file.

## [2.1.0]

### Changed

- The inspector field now leads with the **final** gradient — modulation included — instead of the
  unmodulated base. The Modulation section is collapsed by default, so leading with the base meant
  the one strip most users ever saw was not what the object renders: a hue shift or a repeat count
  was invisible unless you knew to expand a section you had no reason to suspect. The base is still
  there, at the end of the foldout under **Unmodulated Base**, where it is useful for comparison
  while you are actually editing modulation.
- The collapsed Modulation heading reads **Modulation — active** when modulation actually changes
  the output, so an authored look can be told apart from a modulated one without expanding it.
- Library tiles in the picker show each saved gradient modulated, for the same reason: an asset
  serializes its modulation along with its keys, so a tile drawn from the base was answering a
  different question from the one a library is asked.

Previews of gradients that use modulation will therefore look different from 2.0.0. That is the
point of the change, but it is worth knowing before diffing screenshots. Nothing about how a
gradient evaluates has changed, and no API changed.

The picker's key bar deliberately still shows the unmodulated ramp. It is an editing surface: key
handles sit at base-gradient positions, and drawing the modulated result under them would put the
handles out of correspondence with what they appear to rest on.

## [2.0.0]

A correctness, performance and structure pass over 1.0.0. Evaluation output is unchanged except
where called out under "Visual changes" below.

### Fixed

- `InsertSorted` and `SetAndResort` derived their return index by searching the array for a key
  with a matching time, using `Mathf.Approximately`. Adding a key at, or dragging one onto, a
  time another key already occupied returned the wrong key, and the picker's drag manipulator
  re-latches onto that index. Both now derive the index from the insertion or shift position.
- Key sorting used `Array.Sort`, which is not stable, so keys sharing a time reordered
  arbitrarily between calls. Replaced with a stable insertion sort.
- `Modulation`, `SetColorKey` and `SetAlphaKey` bumped `Version` even when handed the value the
  gradient already held. `Version` invalidates the native snapshot, every `GradientLutCache` and
  every editor preview, so a control re-sending its current value rebuilt all of them.
- `GradientPreviewElement.Gradient` and `GradientBarElement.Gradient` cleared their version stamp
  when assigned the instance they already held, which is exactly how callers request a refresh,
  so the version check could never early-out.
- `GradientLut.Bake` rebuilt a `NativeGradient` per call rather than using the version-cached
  `GradientABCW.Native`.
- `GradientABCWField` raised both `GradientChangedEvent` and a `ChangeEvent` for one in-place
  edit. The property drawer handles both, so every edit serialized twice and left two undo
  records. `ChangeEvent` now means the field points at a different gradient instance.
- A preview with no resolved geometry fell back to baking at 256px, so the final preview inside a
  collapsed modulation foldout re-baked on every edit.
- The picker pushed a full gradient clone, and through the drawer a serialize and an undo record,
  on every pointer-move. Coalesced to one push per editor frame.
- The checkerboard texture is `HideAndDontSave` but was held in a static that domain reload
  clears, stranding one texture per reload.
- `SetKeys` discarded input shorter than `MinKeys` in favour of the default ramp, and truncated
  input longer than `MaxKeys` from the tail, silently deleting the high-time end. Short input is
  now spanned across the domain and long input decimated evenly, keeping both ends.
- `ClampColorKeyTime` and `ClampAlphaKeyTime` never prevented a key crossing its neighbours, as
  their names and summaries claimed. Documented as what they do: clamp into the gap under the
  pointer, which is what makes reordering by dragging work.

### Performance

Measured against a frozen copy of the 1.0.0 evaluation core, in the same run. See
`Tests/Editor/Benchmarks`.

- LUT bakes at or above 32 entries now run as a Burst job writing 8-bit colour directly:
  +74% to +89% at 256 entries, +93% to +99% at 4096. The `Span`-taking entry point stages
  through a temporary native array to reach Burst (9x at 256, 30x at 4096, 146x at 65536).
- Modulation skips the RGB/HSV round trip when hue and saturation are neutral: +26% to +30% for
  brightness- or alpha-only modulation.
- Stepped blending binary-searches key midpoints instead of scanning them: +40% at 32 keys.
- A two-key fast path in the smooth sampler, that being the most common gradient shape.
- Previews bake at a fixed resolution instead of the element's pixel width, removing a Texture2D
  destroy and recreate on every Inspector resize.
- The key lists re-bind rows instead of rebuilding them when only values changed.
- The library grid no longer rebuilds when an unrelated `.asset` is imported elsewhere.

Measured and rejected, with numbers recorded in the benchmarks: Burst `FloatMode.Fast`
(inconsistent, and moves output by 1/255), an sRGB-to-linear lookup table (12% slower than
`pow`), and sharing the colour and alpha span searches through a pointer helper (7% slower,
because the Editor's Mono JIT will not inline it).

### Visual changes

- Editor preview strips are stored as sRGB rather than 8-bit linear. In Linear colour-space
  projects the dark end of a gradient no longer bands. Baked runtime textures and evaluated
  colour values are unaffected.
- Keys sharing a time now keep a defined order rather than an arbitrary one.

### Changed

- `GradientLut.ScheduleBake` and `ScheduleEvaluate` moved to a new `GradientJobs` class, next to
  the job structs they schedule.
- Colour-space conversions moved from `GradientMath` to a new public `ColorSpaceMath`.
- `GradientABCW` split into three partials by concern; `EditorTextures` split into
  `CheckerTexture` and `GradientPreviewTexture`; asset IO split out of `GradientLibraryPanel`
  into `GradientLibrary`.
- The `PreviewResolution` setting is now actually read, defaults to 512, and is exposed in
  Project Settings.

### Migration from 1.0.0

- `RepeatMode` values differ from the pre-1.0.0 legacy implementation (`Mirror=0, Wrap=1,
  Clamp=2` there; `Clamp=0, Wrap=1, Mirror=2` here). Data serialized by the original
  VertexColourAnims implementation remaps and needs its repeat mode re-checked. Data saved by
  1.0.0 is unaffected.
- `GradientLut.ScheduleBake`/`ScheduleEvaluate` callers move to `GradientJobs`.
- `GradientMath.SrgbToLinear` callers move to `ColorSpaceMath`.

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
