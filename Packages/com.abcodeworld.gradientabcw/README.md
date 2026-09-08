# Gradient ABCW

A 32-key colour/alpha gradient for Unity 6000.5+ with evaluation-time modulation (reverse, repeat
domain mapping, hue/saturation/brightness/alpha adjustment), a Burst-compiled evaluation core, and
a pure UI Toolkit editor — an inspector field, a property drawer, and a picker window with an
asset library and mesh/texture colour sampling.

## Install

Add the package via git URL, pointing at the subfolder it lives in:

```
https://github.com/AB-Codeworks/GradientABCW.git?path=Packages/com.abcodeworld.gradientabcw
```

In the Package Manager window: **+** → **Add package from git URL...** → paste the URL above.

## Features

- Up to 32 colour keys and 32 alpha keys, smooth or stepped blending.
- Evaluation-time modulation — reverse, repeat count, Clamp/Wrap/Mirror domain mapping, offset,
  and hue/saturation/brightness/alpha adjustment — that never mutates key data. A per-gradient
  **Bypass** flag skips modulation entirely (the cheap base path) without discarding the values.
- A Burst-compiled evaluation core (`NativeGradient`, `GradientMath`) with managed and job-scheduled
  LUT baking, plus a `GradientLutCache` that only re-bakes when the gradient's content actually changes.
- Texture baking (`GradientTextureUtility`) that respects the active colour space.
- A pure UI Toolkit editor: `GradientABCWField` (also used by the property drawer), and
  `GradientPickerWindow` — a picker with a draggable key bar, an asset library, and mesh/texture
  colour sampling (deterministic given a seed).

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

`GradientABCWAsset` is a `ScriptableObject` wrapper (**Create → ABCodeworld → Gradient ABCW**) for
sharing a gradient across scenes and assets.

## Editor usage

Add a `[SerializeField] private GradientABCW gradient;` field to a `MonoBehaviour` or
`ScriptableObject` and it renders as a `GradientABCWField` in the Inspector automatically — a
clickable preview of the **final** gradient, an **Edit** button that opens the picker, a
quick-actions menu (sample from the selected mesh, flip keys, distribute keys, reset), and a
collapsible **Modulation** section holding the modulation controls and the **Unmodulated Base**
gradient.

The top preview shows the gradient as it actually evaluates, modulation included, because the
Modulation section is collapsed by default — leading with the base would mean the one strip most
users ever see is not what the object renders. When modulation is doing something, the collapsed
section reads **Modulation — active**, so an authored look is distinguishable from a modulated one
at a glance.

The property drawer only implements `CreatePropertyGUI` (UI Toolkit), so it renders correctly in
UI Toolkit–based inspectors. An inspector still using `OnInspectorGUI`/`EditorGUILayout` (IMGUI)
will not render a `CreatePropertyGUI`-only drawer's content — move that inspector to UI Toolkit
(or embed `GradientABCWField` directly) to use this package's field.

## Samples

Import **Basic Usage** from the Package Manager window for a minimal example that bakes a
gradient to a texture and tints a light by evaluating it over time.
