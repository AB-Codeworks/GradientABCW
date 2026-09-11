using UnityEditor;
using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Placing a utility window relative to the editor's main window.</summary>
    internal static class EditorWindowPlacement
    {
        /// <summary>Sizes <paramref name="window"/> and centres it over the main editor window.</summary>
        public static void CenterOnMainWindow(EditorWindow window, int width, int height)
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(
                main.x + (main.width - width) * 0.5f,
                main.y + (main.height - height) * 0.5f,
                width, height);
        }
    }
}
