using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Blend mode, key utilities, and mesh/texture colour sampling for the picker window.</summary>
    internal sealed class GradientActionsPanel : VisualElement
    {
        private readonly Toggle steppedToggle;
        private readonly ObjectField textureField;
        private readonly ObjectField meshField;
        private readonly UnsignedIntegerField seedField;

        private GradientABCW gradient;
        private uint seed = 1u;

        /// <summary>Raised after an in-place mutation (blend mode, flip, distribute).</summary>
        public event Action Changed;

        /// <summary>Raised with a freshly sampled gradient to adopt as the new working value.</summary>
        public event Action<GradientABCW> Sampled;

        public GradientActionsPanel()
        {
            AddToClassList("abcw-actions");

            steppedToggle = new Toggle("Stepped Blend") { name = "stepped", tooltip = "Stepped (banded) interpolation using midpoint partition." };
            steppedToggle.RegisterValueChangedCallback(evt =>
            {
                if (gradient == null) return;
                gradient.BlendMode = evt.newValue ? BlendMode.Stepped : BlendMode.Smooth;
                Changed?.Invoke();
            });
            Add(steppedToggle);

            Add(new Button(() => Mutate(g => g.FlipKeys())) { text = "Flip Keys", tooltip = "Permanently flip (1 - t) key times." });
            Add(new Button(() => Mutate(g => g.DistributeColorKeysEvenly())) { text = "Distribute Colours" });
            Add(new Button(() => Mutate(g => g.DistributeAlphaKeysEvenly())) { text = "Distribute Alphas" });

            var samplingGroup = new VisualElement { style = { marginTop = 8 } };
            samplingGroup.Add(new Label("Sampling") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            Add(samplingGroup);

            textureField = new ObjectField("Texture") { name = "texture", objectType = typeof(Texture2D), allowSceneObjects = false };
            samplingGroup.Add(textureField);

            var textureButtons = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            textureButtons.Add(new Button(SampleTextureStrict) { name = "textureStrict", text = "Texture Strict", tooltip = "Build keys from a 1xN vertical LUT (first column), up to 16 entries." });
            textureButtons.Add(new Button(SampleTextureRandom) { name = "textureRandom", text = "Texture Random", tooltip = "Build a 16-key gradient by sampling and organizing colors from the entire texture." });
            samplingGroup.Add(textureButtons);

            meshField = new ObjectField("Mesh") { name = "mesh", objectType = typeof(Mesh), allowSceneObjects = true };
            samplingGroup.Add(meshField);
            samplingGroup.Add(new Button(SampleMesh) { name = "sampleMesh", text = "Sample Mesh" });

            var seedRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 } };
            seedField = new UnsignedIntegerField("Seed") { name = "seed", value = seed, style = { flexGrow = 1 } };
            seedField.RegisterValueChangedCallback(evt => seed = evt.newValue == 0 ? 1u : evt.newValue);
            seedRow.Add(seedField);
            seedRow.Add(new Button(Reroll) { name = "reroll", text = "Reroll" });
            samplingGroup.Add(seedRow);
        }

        public void SetGradient(GradientABCW value)
        {
            gradient = value;
            if (gradient != null)
                steppedToggle.SetValueWithoutNotify(gradient.BlendMode == BlendMode.Stepped);
        }

        private void Reroll()
        {
            seed = unchecked((uint)Environment.TickCount) | 1u;
            seedField.SetValueWithoutNotify(seed);
        }

        private void Mutate(Action<GradientABCW> mutation)
        {
            if (gradient == null)
                return;
            mutation(gradient);
            Changed?.Invoke();
        }

        private void SampleTextureStrict()
        {
            var texture = textureField.value as Texture2D;
            if (!ColorSampler.TrySampleStrict(texture, out var result, out string error))
            {
                EditorUtility.DisplayDialog("Texture Strict", error, "OK");
                return;
            }
            Sampled?.Invoke(result);
        }

        private void SampleTextureRandom()
        {
            var texture = textureField.value as Texture2D;
            var source = new TextureColorSource(texture);
            if (source.Error != null)
            {
                EditorUtility.DisplayDialog("Texture Random", source.Error, "OK");
                return;
            }
            if (!ColorSampler.TrySampleRandom(source, 16, seed, out var result, out string error))
            {
                EditorUtility.DisplayDialog("Texture Random", error, "OK");
                return;
            }
            Sampled?.Invoke(result);
        }

        private void SampleMesh()
        {
            var mesh = meshField.value as Mesh;
            var source = new MeshColorSource(mesh);
            if (!ColorSampler.TrySampleRandom(source, 16, seed, out var result, out string error))
            {
                EditorUtility.DisplayDialog("Sample Mesh", error, "OK");
                return;
            }
            Sampled?.Invoke(result);
        }
    }
}
