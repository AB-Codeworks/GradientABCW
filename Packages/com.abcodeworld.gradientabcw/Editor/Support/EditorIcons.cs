using UnityEditor;
using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Resolves built-in editor icons once, with a graceful fallback to a themed name.</summary>
    internal static class EditorIcons
    {
        private static Texture2D Find(string darkName, string lightName) =>
            EditorGUIUtility.FindTexture(darkName) ?? EditorGUIUtility.FindTexture(lightName);

        public static Texture2D Reset => Find("d_Refresh", "Refresh");
        public static Texture2D Delete => Find("d_TreeEditor.Trash", "TreeEditor.Trash");
    }
}
