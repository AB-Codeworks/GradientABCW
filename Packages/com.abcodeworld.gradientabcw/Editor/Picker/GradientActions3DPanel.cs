using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Blend mode, falloff, key utilities, and mesh/texture colour sampling for the 3D picker window.</summary>
    internal sealed class GradientActions3DPanel : VisualElement
    {
        private readonly Toggle steppedToggle;
        private readonly ResettableSliderRow falloffRow;
        private readonly ObjectField textureField;
        private readonly ObjectField meshField;
        private readonly UnsignedIntegerField seedField;

        private GradientABCW3D gradient;
        private uint seed = 1u;

        /// <summary>Raised after an in-place mutation (blend mode, falloff, flip, distribute).</summary>
        public event Action Changed;

        /// <summary>Raised with a freshly sampled gradient to adopt as the new working value.</summary>
        public event Action<GradientABCW3D> Sampled;

        public GradientActions3DPanel()
        {
            AddToClassList("abcw-actions");

            steppedToggle = new Toggle("Stepped Blend")
            {
                name = "stepped",
                tooltip = "Each point takes the colour of its nearest key outright, partitioning the cube into cells.",
            };
            steppedToggle.RegisterValueChangedCallback(evt =>
            {
                if (gradient == null) return;
                gradient.BlendMode = evt.newValue ? BlendMode.Stepped : BlendMode.Smooth;
                SyncFalloffEnabled();
                Changed?.Invoke();
            });
            Add(steppedToggle);

            falloffRow = new ResettableSliderRow(
                "Falloff Power",
                GradientABCW3D.MinFalloffPower,
                GradientABCW3D.MaxFalloffPower,
                GradientABCW3D.DefaultFalloffPower)
            {
                name = "falloffPower",
                tooltip = "How sharply a key's influence falls off with distance. Higher pulls each point "
                        + "towards its nearest key. No effect under stepped blending.",
            };
            falloffRow.RegisterValueChangedCallback(evt =>
            {
                if (gradient == null) return;
                gradient.FalloffPower = evt.newValue;
                Changed?.Invoke();
            });
            Add(falloffRow);

            Add(new Button(() => Mutate(g => g.FlipKeys()))
            {
                name = "flipKeys",
                text = "Flip Keys",
                tooltip = "Permanently mirror every key through the centre of the cube, on all three axes.",
            });
            Add(new Button(() => Mutate(g => g.DistributeColorKeysEvenly())) { name = "distributeColors", text = "Distribute Colours" });
            Add(new Button(() => Mutate(g => g.DistributeAlphaKeysEvenly())) { name = "distributeAlphas", text = "Distribute Alphas" });

            var samplingGroup = new VisualElement { name = "sampling", style = { marginTop = 8 } };
            samplingGroup.Add(new Label("Sampling") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            Add(samplingGroup);

            textureField = new ObjectField("Texture") { name = "texture", objectType = typeof(Texture2D), allowSceneObjects = false };
            samplingGroup.Add(textureField);
            samplingGroup.Add(new Button(SampleTexture)
            {
                name = "textureScatter",
                text = "Texture Scatter",
                tooltip = "Take a palette from the texture and scatter it through the cube. "
                        + "Reroll rearranges the positions. (A 1xN strip has no 3D reading, so there is no strict mode.)",
            });

            meshField = new ObjectField("Mesh") { name = "mesh", objectType = typeof(Mesh), allowSceneObjects = true };
            samplingGroup.Add(meshField);
            samplingGroup.Add(new Button(SampleMesh)
            {
                name = "sampleMesh",
                text = "Sample Mesh",
                tooltip = "Rebuild the gradient from the mesh's coloured vertices, using each vertex's position in the mesh bounds.",
            });

            var seedRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
            seedField = new UnsignedIntegerField("Seed") { name = "seed", value = seed, style = { flexGrow = 1 } };
            seedField.RegisterValueChangedCallback(evt => seed = evt.newValue == 0 ? 1u : evt.newValue);
            seedRow.Add(seedField);
            seedRow.Add(new Button(Reroll) { name = "reroll", text = "Reroll" });
            samplingGroup.Add(seedRow);
        }

        public void SetGradient(GradientABCW3D value)
        {
            gradient = value;
            if (gradient == null)
                return;

            steppedToggle.SetValueWithoutNotify(gradient.BlendMode == BlendMode.Stepped);
            falloffRow.SetValueWithoutNotify(gradient.FalloffPower);
            SyncFalloffEnabled();
        }

        /// <summary>
        /// Falloff has no meaning under stepped blending, where the nearest key wins outright. Leaving it
        /// live would present a control that silently does nothing.
        /// </summary>
        private void SyncFalloffEnabled() =>
            falloffRow.SetEnabled(gradient != null && gradient.BlendMode == BlendMode.Smooth);

        private void Reroll()
        {
            seed = unchecked((uint)Environment.TickCount) | 1u;
            seedField.SetValueWithoutNotify(seed);
        }

        private void Mutate(Action<GradientABCW3D> mutation)
        {
            if (gradient == null)
                return;

            mutation(gradient);
            Changed?.Invoke();
        }

        private void SampleTexture()
        {
            var texture = textureField.value as Texture2D;
            if (ColorSampler3D.TrySampleTexture(texture, ColorSampler3D.DefaultKeyCount, seed, out var sampled, out string error))
                Sampled?.Invoke(sampled);
            else
                EditorUtility.DisplayDialog("Sample Texture", error, "OK");
        }

        private void SampleMesh()
        {
            var mesh = meshField.value as Mesh;
            if (ColorSampler3D.TrySampleMesh(mesh, ColorSampler3D.DefaultKeyCount, seed, out var sampled, out string error))
                Sampled?.Invoke(sampled);
            else
                EditorUtility.DisplayDialog("Sample Mesh", error, "OK");
        }
    }
}
