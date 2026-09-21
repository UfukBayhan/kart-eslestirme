using UnityEngine;
using UnityEngine.SceneManagement;

public class ModeNavigation : MonoBehaviour
{
    public string targetScene;
    private bool loading;

    public void Open()
    {
        if (loading) return;
        loading = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(targetScene);
    }
}
