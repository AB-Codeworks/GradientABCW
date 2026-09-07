using System;
using UnityEditor;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// Notifies subscribers when a <c>.asset</c> file inside a watched folder changes, so the picker's
    /// library grid can refresh.
    /// </summary>
    /// <remarks>
    /// Subscribers register the folder they care about. Previously any <c>.asset</c> import anywhere in
    /// the project rebuilt the grid, and rebuilding it recreates a preview element — and therefore a
    /// texture — per gradient in the library. Saving an unrelated ScriptableObject should not cost that.
    /// </remarks>
    internal static class GradientAssetWatcher
    {
        /// <summary>Raised when a <c>.asset</c> under <paramref name="folder"/> is imported, deleted or moved.</summary>
        public static event Action<string> LibraryChanged;

        private sealed class Postprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                var handler = LibraryChanged;
                if (handler == null)
                    return;

                NotifyFor(imported, handler);
                NotifyFor(deleted, handler);
                NotifyFor(moved, handler);
                NotifyFor(movedFrom, handler);
            }

            private static void NotifyFor(string[] paths, Action<string> handler)
            {
                foreach (var path in paths)
                {
                    if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                        continue;

                    int slash = path.LastIndexOf('/');
                    handler(slash > 0 ? path.Substring(0, slash) : string.Empty);
                }
            }
        }

        /// <summary>True when <paramref name="changedFolder"/> is <paramref name="watchedFolder"/> or below it.</summary>
        public static bool Affects(string watchedFolder, string changedFolder)
        {
            if (string.IsNullOrEmpty(watchedFolder) || changedFolder == null)
                return false;

            return changedFolder.Equals(watchedFolder, StringComparison.OrdinalIgnoreCase)
                || changedFolder.StartsWith(watchedFolder + "/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
