# Gradient ABCW

Gradients for Unity 6000.5+ with evaluation-time modulation (reverse, repeat domain mapping,
hue/saturation/brightness/alpha adjustment), a Burst-compiled evaluation core, and a pure UI Toolkit
editor — an inspector field, a property drawer, and a picker window with an asset library and
mesh/texture colour sampling.

Two kinds, sharing all of that: **`GradientABCW`**, a 32-key colour/alpha gradient along `t`, and
**`GradientABCW3D`**, a 64-key gradient whose keys sit anywhere inside a unit cube.

## Install

Add the package via git URL, pointing at the subfolder it lives in:

```
https://github.com/AB-Codeworks/GradientABCW.git?path=Packages/com.abcodeworld.gradientabcw
```

In the Package Manager window: **+** → **Add package from git URL...** → paste the URL above.

## Features

- Up to 32 colour keys and 32 alpha keys along `t`, smooth or stepped blending.
- **3D gradients**: up to 64 colour keys and 64 alpha keys positioned anywhere in a unit cube.
  Smooth blending is inverse-distance weighting sharpened by a per-gradient **Falloff Power**;
  stepped blending gives each key its own Voronoi cell.
- Evaluation-time modulation — reverse, repeat count, Clamp/Wrap/Mirror domain mapping, offset,
  and hue/saturation/brightness/alpha adjustment — that never mutates key data. A per-gradient
  **Bypass** flag skips modulation entirely (the cheap base path) without discarding the values.
- A Burst-compiled evaluation core (`NativeGradient`, `GradientMath`, and their 3D counterparts) with
  managed and job-scheduled LUT baking, plus caches that only re-bake when content actually changes.
- Texture baking: 1D strips through `GradientTextureUtility`, and `Texture3D` volumes through
  `GradientTexture3DUtility`, where the hardware's own trilinear filtering makes 32³ (128 KB)
  granular enough to sample directly.
- A pure UI Toolkit editor: `GradientABCWField` and `GradientABCW3DField` (both also used by their
  property drawers), `GradientPickerWindow` — a picker with a draggable key bar — and
  `GradientPicker3DWindow`, which replaces that bar with a drag-rotatable cube. Both carry an asset
  library and mesh/texture colour sampling (deterministic given a seed).

## API overview

```csharp
using ABCodeworld.Gradients;

// Author or load a gradient.
var gradient = GradientABCW.CreateDefault();
gradient.AddColorKey(Color.red, 0.5f);
gradient.Modulation = new GradientModulation { repeats = 2f, repeatMode = RepeatMode.Mirror };

// Evaluate.
Color c = gradient.Evaluate(0.25f);       // with modulation
Color b = gradient.EvaluateBase(0.25f);   // ignoring modulation

// Bake a LUT or texture.
var lut = new Color32[256];
GradientLut.Bake(gradient, lut, GradientLutOptions.Final);

Texture2D tex = GradientTextureUtility.CreateTexture(gradient, 256, GradientLutOptions.Project(final: true));

// Or schedule baking/evaluation across the job system for bulk work.
var native = NativeGradient.From(gradient);
var results = new NativeArray<float4>(4096, Allocator.TempJob);
GradientLut.ScheduleBake(in native, results, GradientLutOptions.Final).Complete();
```

```csharp
// A 3D gradient: same modulation, keys positioned in a cube.
var volume = GradientABCW3D.CreateDefault();
volume.AddColorKey(Color.cyan, new Vector3(0.25f, 0.75f, 0.5f));
volume.FalloffPower = 4f;                       // sharper falloff pulls each point towards its nearest key

Color v = volume.Evaluate(new Vector3(0.5f, 0.5f, 0.5f));

// Bake a volumetric LUT: size^3 cells, x fastest, laid out exactly as Texture3D wants it.
var lut3D = new Color32[GradientLut3D.VoxelCount(GradientLut3D.DefaultSize)];
GradientLut3D.Bake(volume, lut3D, GradientLut3D.DefaultSize, GradientLutOptions.Final);

// Granularity comes from filtering rather than resolution.
Color sampled = GradientLut3D.SampleTrilinear(lut3D, GradientLut3D.DefaultSize, new Vector3(0.3f, 0.6f, 0.1f));

// Or hand the GPU the volume and the scale/bias it needs to sample it.
Texture3D volumeTex = GradientTexture3DUtility.CreateTexture(volume);
material.SetTexture("_GradientVolume", volumeTex);
material.SetVector("_GradientVolumeScaleOffset", GradientLut3D.ShaderScaleOffset(volumeTex.width));
```

`GradientABCWAsset` and `GradientABCW3DAsset` are `ScriptableObject` wrappers
(**Create → ABCodeworld → Gradient ABCW** and **Gradient ABCW 3D**) for sharing a gradient across
scenes and assets.

## Editor usage

Add a `[SerializeField] private GradientABCW gradient;` field to a `MonoBehaviour` or
`ScriptableObject` and it renders as a `GradientABCWField` in the Inspector automatically — a
clickable base preview, an **Edit** button that opens the picker, a quick-actions menu (sample
from the selected mesh, flip keys, distribute keys, reset), and a collapsible **Modulation**
section with its own final preview.

A `[SerializeField] private GradientABCW3D volume;` field works the same way, with one difference
that follows from the shape of the data: a line fits in a strip but a cube does not, so its swatch is
four small perspective renders of the gradient as a solid cube, from directions chosen so every face
appears in at least one of them.

Its picker matches the 1D one, with the gradient bar replaced by a drag-rotatable wireframe cube
showing each key as a dot in its own colour — alpha keys as diamonds, colour keys as circles — and a
toggle to see the same cube rendered solid at the same rotation. There is no key dragging in the
cube: a pointer gives two coordinates and a key needs three. So the column that holds the 1D
picker's draggable swatches instead holds **Alpha Keys** / **Color Keys** mode buttons and an **Add
Key** button, and key rows carry `(x, y, z)` where the 1D rows carry `t`.

Modulation applies to all three axes at once, which is worth knowing before reaching for it:
**Repeats** of 2 tiles the cube 2×2×2, eight times over rather than twice, and **Reverse Eval**
mirrors every axis, which reflects the gradient rather than rotating it. Everything else — blend
mode, bypass, hue, saturation, brightness, alpha — behaves exactly as it does in 1D.

The property drawers only implement `CreatePropertyGUI` (UI Toolkit), so they render correctly in
UI Toolkit–based inspectors. An inspector still using `OnInspectorGUI`/`EditorGUILayout` (IMGUI)
will not render a `CreatePropertyGUI`-only drawer's content — move that inspector to UI Toolkit
(or embed `GradientABCWField` directly) to use this package's field.

## Samples

Import from the Package Manager window:

- **Basic Usage** — bakes a gradient to a texture and tints a light by evaluating it over time.
- **3D Gradient** — bakes a 3D gradient to a `Texture3D` for a shader, and tints a light by
  evaluating the same gradient along a path through the cube.
