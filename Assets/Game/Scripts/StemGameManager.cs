using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StemGameManager : MonoBehaviour
{
    public CardMatchGameType cardMatchGameTargetType;
    [Header("Game Settings")]
    public GameData gamedata;
    public float currentTime = 0f;
    public int levelIndex = 0;
    public int currentPage = 0;

    private bool isTimeActive = false;
    private bool isLoadingBackScene = false;

    [SerializeField]
    private TextMeshProUGUI _UST;

    [Header("Common Settings")]
    public StemGameType stemGameType;

    [Header("Common UI References")]
    public TextMeshProUGUI yonergeText;
    public GameObject WrongSFX;
    public GameObject WinSFX;
    public GameObject WinSFXFinal;

    [Header("Common Game Settings")]
    public GameObject[] trueObjects;
    public GameObject[] falseObjects;
    public GameObject[] etcPrefab;
    public GameObject opsionelPrefab;
    public Vector2Int numberRangeXY = new Vector2Int(0, 20);
    public bool isEvent = false;
    public bool isRandom = false;
    public bool isDynamicMode = false;

    [Header("CardMatchGame Settings")]

    [Header("Odd Or Even Settings")]

    [Header("InputGames Settings")]
    public TMP_InputField[] inputFields;
    public int[] answerBoxesNum;

    public string[] answerBoxesString;
    public int step; //Artis/azalis miktari
    public bool isReverseMode;

    [Tooltip(
        "InputSequential cevaplarinda 016 gibi bastaki sifirli yazimlari reddeder. "
            + "Sahne bazinda eski int.TryParse davranisina donmek icin kapatin."
    )]
    public bool requireCanonicalIntegerInput = true;

    [Header("BubblePopMathGame")]
    public bool isDEMO = false;
    private void Awake()
    {
        switch (stemGameType)
        {
            case StemGameType.CardMatchGame:
                if (!TryGetComponent<CardMatchGame>(out _))
                    gameObject.AddComponent<CardMatchGame>();
                break;
        }
    }

    private void Start()
    {
        if (gamedata == null)
            gamedata = new GameData();

        gamedata.UserId = "local-player";
        gamedata.SectionCode = SceneManager.GetActiveScene().name;
        gamedata.version = Application.version;

        if (gamedata.Data == null)
            gamedata.Data = new List<DataEntry>();

        AddLevelEntry();
        ResetCurrentLevelTimer();
    }

    private void Update()
    {
        if (isTimeActive)
            currentTime += Time.deltaTime;
    }

    public void AddUst()
    {
        int GuncelUst = PlayerPrefs.GetInt("kart-eslestirme.Score", 0) + 1;
        PlayerPrefs.SetInt("kart-eslestirme.Score", GuncelUst);

        if (_UST != null)
            _UST.text = GuncelUst.ToString();
    }

    public void DestroyAllSFX()
    {
        var allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var obj in allObjects)
        {
            if (obj.name.Contains("SFX"))
                Destroy(obj);
        }
    }

    public void LoadBackScene()
    {
        if (isLoadingBackScene) return;
        PauseLevelTimer();
        RecordCurrentLevelTime();
        isLoadingBackScene = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene("ModeSelect");
    }

    public void StartNewLevel()
    {
        levelIndex++;
        AddLevelEntry();
        ResetCurrentLevelTimer();

        Debug.Log($"Yeni Level {levelIndex} başladı. CurrentPage={currentPage}");
    }

    public void WrongAnswer()
    {
        if (HasCurrentLevelEntry())
        {
            gamedata.Data[currentPage].Wrong++;
            Debug.Log(
                $"Wrong Answer on Level {levelIndex}, Wrong={gamedata.Data[currentPage].Wrong}"
            );
        }
    }

    public void FinalAnswer()
    {
        PauseLevelTimer();
        RecordCurrentLevelTime();

        Debug.Log($"Final Answer on Level {levelIndex}, Time={currentTime:F2}");

        StartNewLevel();
    }

    public void PauseLevelTimer()
    {
        isTimeActive = false;
    }

    public void ResetCurrentLevelTimer()
    {
        currentTime = 0f;
        isTimeActive = true;
    }

    private void AddLevelEntry()
    {
        if (gamedata == null)
            gamedata = new GameData();
        if (gamedata.Data == null)
            gamedata.Data = new List<DataEntry>();

        gamedata.Data.Add(
            new DataEntry
            {
                Part = $"Level_{levelIndex}",
                Wrong = 0,
                Time = 0f,
            }
        );
        currentPage = gamedata.Data.Count - 1;
    }

    private void RecordCurrentLevelTime()
    {
        if (HasCurrentLevelEntry())
            gamedata.Data[currentPage].Time = currentTime;
    }

    private bool HasCurrentLevelEntry()
    {
        return gamedata != null
            && gamedata.Data != null
            && currentPage >= 0
            && currentPage < gamedata.Data.Count;
    }
}

[System.Serializable]
public enum StemGameType
{
    MathGame,
    OddOrEvenGame,
    InputSequential,
    CardMatchGame,
    BubblePopMathGame,
}
