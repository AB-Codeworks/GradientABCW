# Basic Usage

Add `GradientABCWSample` to a GameObject with a `Renderer`. On `Awake` it bakes the gradient to a
256-pixel texture (`GradientTextureUtility.CreateTexture`) and assigns it to the renderer's
`_BaseMap` via a `MaterialPropertyBlock`, so no material instance is created. If you assign a
`Light`, the sample also tints its colour every frame by calling `gradient.Evaluate(t)` with a
time value that cycles at `cyclesPerSecond`.

Use this as a starting point for:

- Baking a gradient to a texture once and sampling it in a shader.
- Evaluating a gradient directly in script (`Evaluate` / `EvaluateBase`).
- Editing the gradient in the Inspector via the `GradientABCW` field, including opening the picker.
