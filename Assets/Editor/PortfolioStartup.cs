using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad] public static class PortfolioStartup
{
    static PortfolioStartup() { EditorApplication.delayCall += OpenInitialScene; }
    static void OpenInitialScene()
    {
        if (UnityEngine.Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.path == "" && !scene.isDirty && scene.rootCount <= 2 && System.IO.File.Exists("Assets/Game/Scenes/ModeSelect.unity"))
            EditorSceneManager.OpenScene("Assets/Game/Scenes/ModeSelect.unity");
    }
    [MenuItem("Portfolio/Open Mode Selection")]
    static void OpenMenu() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene("Assets/Game/Scenes/ModeSelect.unity"); }
}
