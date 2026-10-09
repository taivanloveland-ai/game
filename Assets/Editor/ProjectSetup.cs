using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Creates and opens the planet 1 scene the first time the project is opened, so Play works straight away.
[InitializeOnLoad]
public static class ProjectSetup
{
    const string ScenePath = "Assets/Scenes/PlanetOne.unity";

    static ProjectSetup()
    {
        EditorApplication.delayCall += CreateSceneIfMissing;
    }

    public static void CreateSceneIfMissing()
    {
        if (File.Exists(ScenePath) || EditorApplication.isPlayingOrWillChangePlaymode) return;

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Planet One").AddComponent<PlanetOne>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
