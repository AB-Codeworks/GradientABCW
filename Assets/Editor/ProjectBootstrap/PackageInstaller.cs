using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProjectBootstrap
{
    /// <summary>
    /// Adds the Universal Render Pipeline through the Package Manager Client API rather than by editing
    /// <c>Packages/manifest.json</c> by hand, so UPM resolves the version compatible with this editor and
    /// pulls in the dependencies URP needs (render-pipelines.core, shadergraph) itself.
    /// </summary>
    /// <remarks>
    /// Must be invoked without <c>-quit</c>: <see cref="Client.AddAndRemove"/> is asynchronous and only
    /// completes on a later <see cref="EditorApplication.update"/> tick, so the editor has to stay alive
    /// until the request finishes. This method quits itself with the right exit code when it is done.
    /// </remarks>
    public static class PackageInstaller
    {
        private static readonly string[] PackagesToAdd =
        {
            // Deliberately unpinned: let UPM pick the release compatible with the editor in
            // ProjectVersion.txt rather than guessing a version number here.
            "com.unity.render-pipelines.universal",
        };

        private static readonly string[] PackagesToRemove = { };

        private const double TimeoutSeconds = 600;

        private static AddAndRemoveRequest request;
        private static double deadline;

        public static void Install()
        {
            Debug.Log($"[PackageInstaller] Adding: {string.Join(", ", PackagesToAdd)}");
            request = Client.AddAndRemove(packagesToAdd: PackagesToAdd, packagesToRemove: PackagesToRemove);
            deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (request == null)
                return;

            if (!request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > deadline)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[PackageInstaller] Timed out waiting for UPM.");
                    EditorApplication.Exit(2);
                }
                return;
            }

            EditorApplication.update -= Poll;

            if (request.Status == StatusCode.Success)
            {
                foreach (var p in request.Result.OrderBy(p => p.name))
                    Debug.Log($"[PackageInstaller] Resolved: {p.name}@{p.version}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[PackageInstaller] Failed: {request.Error?.message}");
                EditorApplication.Exit(1);
            }
        }
    }
}
