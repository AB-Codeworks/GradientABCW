# 3D Gradient

Add `GradientABCW3DSample` to a GameObject with a `Renderer`. On `Awake` it bakes the gradient to a
32×32×32 `Texture3D` (`GradientTexture3DUtility.CreateTexture`) and hands it to the renderer through
a `MaterialPropertyBlock`, alongside the scale and bias a shader needs to sample it. If you assign a
`Light`, the sample also tints its colour every frame by evaluating the gradient along a diagonal
path through the cube.

Use this as a starting point for:

- Baking a 3D gradient to a `Texture3D` and sampling it in a shader. The package ships no shader —
  it is render-pipeline agnostic — so this is the pair of lines to add to your own, which must
  declare `TEXTURE3D(_GradientVolume)` and `float4 _GradientVolumeScaleOffset`. Correct the
  position first:

  ```hlsl
  float3 uvw = p * _GradientVolumeScaleOffset.x + _GradientVolumeScaleOffset.y;
  half4 c = SAMPLE_TEXTURE3D(_GradientVolume, sampler_GradientVolume, uvw);
  ```

  The bake puts cells on the domain's endpoints, matching `Evaluate`, while a GPU samples cell
  centres — without the correction a shader reads half a cell off along every axis.
- Evaluating a 3D gradient directly in script (`Evaluate` / `EvaluateBase`), which is exact and does
  not need a bake.
- Editing the gradient in the Inspector via the `GradientABCW3D` field, including opening the 3D
  picker.

Baking on the CPU instead? `GradientLut3D.Bake` fills a flat `Color32[]` of `size³` cells, and
`GradientLut3D.SampleTrilinear` reads it back at any position — the same filtering the GPU does, so
the two paths agree.
