# GradientABCW

A Unity 6000.5+ project hosting the **Gradient ABCW** package: a 32-key colour/alpha gradient and a
64-key 3D gradient whose keys sit anywhere in a unit cube, both with evaluation-time modulation, a
Burst-compiled evaluation core, and a pure UI Toolkit editor.

The package lives at [`Packages/com.abcodeworld.gradientabcw`](Packages/com.abcodeworld.gradientabcw)
— see its [README](Packages/com.abcodeworld.gradientabcw/README.md) for installation, the API,
and editor usage.

This repository is the package's development project: `Assets/Dev` holds a manual smoke-test
scene and probe component, and the package's own `Tests/` folders hold its EditMode/PlayMode
test suite (`Window > General > Test Runner`, or `unity test` via the Unity CLI).
