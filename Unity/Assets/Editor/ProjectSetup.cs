using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicalDigital.EditorTools
{
    [InitializeOnLoad]
    internal static class ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/S3_SequenceGame.unity";
        private const string LogPrefix = "[PhysicalDigital]";

        static ProjectSetup()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            NamedBuildTarget target = NamedBuildTarget.Standalone;
            if (PlayerSettings.GetApiCompatibilityLevel(target) != ApiCompatibilityLevel.NET_Unity_4_8)
            {
                PlayerSettings.SetApiCompatibilityLevel(target, ApiCompatibilityLevel.NET_Unity_4_8);
                Debug.Log($"{LogPrefix} Api Compatibility Level set to .NET Framework (required by System.IO.Ports).");
            }
            if (!PlayerSettings.runInBackground)
            {
                PlayerSettings.runInBackground = true;
            }

            if (!File.Exists(ScenePath))
            {
                CreateScene();
            }
        }

        [MenuItem("PhysicalDigital/Create S3 Scene")]
        private static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("S3App", typeof(S3App));
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"{LogPrefix} Scene created: {ScenePath} (root object '{root.name}'). Press Play.");
        }
    }
}
