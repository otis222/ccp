using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArduinoUnityGame.Editor
{
    public static class SerialStarRunnerSceneSaver
    {
        private const string TargetScenePath = "Assets/Scenes/SampleScene.unity";

        [InitializeOnLoadMethod]
        private static void AutoBuildSampleSceneAfterCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying)
                {
                    return;
                }

                Scene activeScene = SceneManager.GetActiveScene();
                if (!activeScene.isLoaded || activeScene.path != TargetScenePath)
                {
                    return;
                }

                if (Object.FindFirstObjectByType<SerialStarRunnerGame>() != null)
                {
                    if (SerialStarRunnerBootstrap.UpgradeScenePresentation())
                    {
                        EditorSceneManager.MarkSceneDirty(activeScene);
                        EditorSceneManager.SaveScene(activeScene);
                        Debug.Log("Serial Star Runner scene upgraded with imported MyProject_B runner visuals.");
                    }

                    return;
                }

                BuildAndSave(false);
            };
        }

        [MenuItem("Tools/Serial Star Runner/Build Saved Scene")]
        public static void BuildSavedScene()
        {
            BuildAndSave(true);
        }

        private static void BuildAndSave(bool rebuildExisting)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Stop Play Mode before building the saved Serial Star Runner scene.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            bool built = SerialStarRunnerBootstrap.BuildGame(rebuildExisting);
            if (!built)
            {
                Debug.Log("Serial Star Runner scene was already present.");
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("Serial Star Runner scene saved into: " + activeScene.path);
        }

        [MenuItem("Tools/Serial Star Runner/Rebuild Saved Scene")]
        public static void RebuildSavedScene()
        {
            BuildSavedScene();
        }
    }
}
