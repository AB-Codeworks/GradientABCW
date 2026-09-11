using System;
using UnityEditor;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// Validating, testing and creating project-relative asset folders. Pure path arithmetic plus
    /// <see cref="AssetDatabase"/> calls — nothing here knows what kind of asset ends up in the folder.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="GradientLibrary"/> so the 1D and 3D gradient libraries share one
    /// implementation of the part that has nothing to do with either. The typed asset operations
    /// (find, save, overwrite, delete) stay on each library, because those genuinely differ: they name a
    /// concrete asset type in an <see cref="AssetDatabase"/> type filter and in the object they create.
    /// </remarks>
    internal static class AssetFolders
    {
        /// <summary>Normalizes a user-typed folder path, or returns null when it could never be valid.</summary>
        public static string Normalize(string folder)
        {
            string trimmed = folder?.Trim().Replace("\\", "/").TrimEnd('/');
            if (string.IsNullOrEmpty(trimmed))
                return null;

            return trimmed == "Assets" || trimmed.StartsWith("Assets/", StringComparison.Ordinal) ? trimmed : null;
        }

        public static bool Exists(string folder) =>
            !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder);

        /// <summary>Creates <paramref name="folder"/> and any missing parents.</summary>
        public static void Create(string folder)
        {
            var parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
