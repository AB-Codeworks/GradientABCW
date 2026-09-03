using UnityEditor;
using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Resolves built-in editor icons once, with a graceful fallback to a themed name.</summary>
    internal static class EditorIcons
    {
        private static Texture2D Find(string darkName, string lightName) =>
            EditorGUIUtility.FindTexture(darkName) ?? EditorGUIUtility.FindTexture(lightName);

        public static Texture2D Info => Find("d__Help", "_Help");
        public static Texture2D Reset => Find("d_Refresh", "Refresh");
        public static Texture2D Delete => Find("d_TreeEditor.Trash", "TreeEditor.Trash");
        public static Texture2D Load => Find("d_Import", "Import");
        public static Texture2D Save => Find("d_SaveAs", "SaveAs");
        public static Texture2D Swap => Find("d_RotateTool", "RotateTool");
    }
}
