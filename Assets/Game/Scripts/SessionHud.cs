using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SessionHud : MonoBehaviour
{
    public StemGameManager manager;
    public TMP_Text timer;
    public TMP_Text level;
    public TMP_Text mistakes;

    private void Update()
    {
        if (!manager) return;
        timer.text = $"SÜRE  {Mathf.FloorToInt(manager.currentTime / 60f):00}:{Mathf.FloorToInt(manager.currentTime % 60):00}";
        level.text = $"SEVİYE {manager.levelIndex + 1:00}";
        int wrong = 0;
        if (manager.gamedata != null && manager.gamedata.Data.Count > manager.currentPage)
            wrong = manager.gamedata.Data[manager.currentPage].Wrong;
        mistakes.text = $"HATA  {wrong}";
        if (Input.GetKeyDown(KeyCode.Escape)) manager.LoadBackScene();
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
