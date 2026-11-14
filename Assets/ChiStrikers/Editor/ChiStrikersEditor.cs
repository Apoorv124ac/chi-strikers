using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChiStrikers.EditorTools
{
    [InitializeOnLoad]
    public static class ChiStrikersSetup
    {
        public const string ScenePath = "Assets/ChiStrikers/ChiStrikers.unity";

        static ChiStrikersSetup()
        {
            // First open of the project: build the game scene so there is something to press Play on.
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                CreateScene();
            };
        }

        [MenuItem("Chi Strikers/Create Game Scene")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Chi Strikers").AddComponent<Match>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath))
            {
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            Debug.Log("Chi Strikers: game scene ready at " + ScenePath + ". Press Play.");
        }

        [MenuItem("Chi Strikers/Open Game Scene")]
        static void Open()
        {
            if (!File.Exists(ScenePath)) { CreateScene(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
