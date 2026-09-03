using ABCodeworld.Gradients.Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ABCodeworld.Gradients.DevTools
{
    /// <summary>One-off scene builder, run via <c>-executeMethod</c>, for the manual smoke-test scene.</summary>
    internal static class DevSceneBuilder
    {
        private const string ScenePath = "Assets/Dev/DevScene.unity";

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("GradientDevProbe");
            go.AddComponent<GradientDevProbe>();
            SceneManager.MoveGameObjectToScene(go, scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }
    }
}
