using UnityEditor;
using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    internal static class MeshSelectionUtility
    {
        public static bool TryGetSelectedMesh(out Mesh mesh, out string error)
        {
            mesh = null;
            error = null;

            var go = Selection.activeGameObject;
            if (go == null)
            {
                error = "Select a GameObject with a MeshFilter (or child) that has a mesh with vertex colors.";
                return false;
            }

            var filter = go.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                filter = go.GetComponentInChildren<MeshFilter>(true);

            if (filter == null || filter.sharedMesh == null)
            {
                error = "Select a GameObject with a MeshFilter (or child) that has a mesh with vertex colors.";
                return false;
            }

            mesh = filter.sharedMesh;
            return true;
        }
    }
}
