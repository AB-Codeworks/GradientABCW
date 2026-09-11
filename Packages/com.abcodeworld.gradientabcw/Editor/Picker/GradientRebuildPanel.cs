using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// The Rebuild tab's chrome: a texture source, a mesh source, a seed, and whatever buttons the
    /// window hangs off each source. Knows nothing about gradients of either dimension.
    /// </summary>
    /// <remarks>
    /// Shared between the two pickers rather than written twice, which is safe for a reason worth
    /// stating: the sampling half of the old actions panels never touched the gradient at all. It
    /// read two object fields and a seed, called a sampler, and handed back a whole new gradient —
    /// so the only typed thing about it was the payload of an event. The seed row and its Reroll
    /// were byte-identical between the two panels before this existed.
    /// <para>
    /// The same split the package already makes elsewhere: <see cref="AssetFolders"/> holds the part
    /// of the two libraries that names no asset type, and each library keeps the part that does.
    /// Here the window keeps the sampler call and the typed <c>Sampled</c> event; this keeps the form.
    /// </para>
    /// </remarks>
    internal sealed class GradientRebuildPanel : VisualElement
    {
        private readonly ObjectField textureField;
        private readonly ObjectField meshField;
        private readonly UnsignedIntegerField seedField;
        private readonly VisualElement textureActions;
        private readonly VisualElement meshActions;

        private uint seed = 1u;

        /// <summary>The texture to rebuild from, or null.</summary>
        public Texture2D Texture => textureField.value as Texture2D;

        /// <summary>The mesh to rebuild from, or null.</summary>
        public Mesh Mesh => meshField.value as Mesh;

        /// <summary>The seed the scattering samplers should use.</summary>
        public uint Seed => seed;

        public GradientRebuildPanel()
        {
            AddToClassList("abcw-tab-pane");
            style.flexDirection = FlexDirection.Column;

            // Rebuilding throws every key away, which the old Sampling group never said anywhere.
            var warning = new Label("Rebuilding replaces every key in the gradient.") { name = "rebuildWarning" };
            warning.AddToClassList("abcw-warn");
            Add(warning);

            Add(Section("From a texture"));
            textureField = new ObjectField("Texture") { name = "texture", objectType = typeof(Texture2D), allowSceneObjects = false };
            Add(textureField);
            textureActions = ActionRow();
            Add(textureActions);

            Add(Section("From a mesh"));
            meshField = new ObjectField("Mesh") { name = "mesh", objectType = typeof(Mesh), allowSceneObjects = true };
            Add(meshField);
            meshActions = ActionRow();
            Add(meshActions);

            var seedRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 6 } };
            seedField = new UnsignedIntegerField("Seed") { name = "seed", value = seed, style = { flexGrow = 1 } };
            seedField.RegisterValueChangedCallback(evt => seed = evt.newValue == 0 ? 1u : evt.newValue);
            seedRow.Add(seedField);
            seedRow.Add(new Button(Reroll)
            {
                name = "reroll",
                text = "Reroll",
                tooltip = "Pick a new seed. The scattering samplers read it the next time you press one.",
            });
            Add(seedRow);
        }

        /// <summary>Hangs a button off the texture source.</summary>
        public void AddTextureAction(string elementName, string text, string tooltip, Action action) =>
            textureActions.Add(MakeAction(elementName, text, tooltip, action));

        /// <summary>Hangs a button off the mesh source.</summary>
        public void AddMeshAction(string elementName, string text, string tooltip, Action action) =>
            meshActions.Add(MakeAction(elementName, text, tooltip, action));

        private static Button MakeAction(string elementName, string text, string tooltip, Action action)
        {
            var button = new Button(action) { name = elementName, text = text, tooltip = tooltip };
            button.style.flexGrow = 1;
            button.style.flexBasis = 0;
            return button;
        }

        private static VisualElement ActionRow() =>
            new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };

        private static Label Section(string text)
        {
            var label = new Label(text);
            label.AddToClassList("abcw-tab-pane__section");
            return label;
        }

        private void Reroll()
        {
            seed = unchecked((uint)Environment.TickCount) | 1u;
            seedField.SetValueWithoutNotify(seed);
        }
    }
}
