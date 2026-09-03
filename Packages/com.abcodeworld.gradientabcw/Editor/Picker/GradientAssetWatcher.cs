using System;
using UnityEditor;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Notifies subscribers when a <c>.asset</c> file changes, so the picker's library grid can refresh.</summary>
    internal static class GradientAssetWatcher
    {
        public static event Action LibraryChanged;

        private sealed class Postprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (LibraryChanged == null)
                    return;

                if (ContainsAssetFile(imported) || ContainsAssetFile(deleted) || ContainsAssetFile(moved))
                    LibraryChanged.Invoke();
            }

            private static bool ContainsAssetFile(string[] paths)
            {
                foreach (var path in paths)
                {
                    if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
        }
    }
}
