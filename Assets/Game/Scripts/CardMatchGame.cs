using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CardMatchGame : MonoBehaviour
{
    private StemGameManager stem;
    public CardMatchGameType gameMode;
    private TextMeshProUGUI yonergeText;
    private GameObject WinSFX;
    private GameObject WinSFXFinal;
    private GameObject WrongSFX;
    private GameObject SoundFX;
    public GameObject objectParent;
    public GameObject cardPrefab;
    public int pairsCount = 6;
    public float revealDuration = 1.0f;
    public int levelIndex = 0;

    private bool levelTransitioning = false;

    private Queue<GameObject> cardPool = new Queue<GameObject>();
    private List<GameObject> activeCards = new List<GameObject>();
    private const int INITIAL_POOL_SIZE = 20;
    private const int MAX_POOL_SIZE = 30;

    private Dictionary<GameObject, CardInfo> cardMap = new Dictionary<GameObject, CardInfo>();
    private GameObject firstSelected = null;
    private bool isProcessing = false;
    private int matchesFound = 0;

    public Vector2 minCellSize = new Vector2(120, 160);
    public Vector2 maxCellSize = new Vector2(220, 300);
    public Vector2 minSpacing = new Vector2(10, 10);
    public Vector2 maxSpacing = new Vector2(80, 80);

    private GridLayoutGroup gridLayoutGroup;

    private AudioSource currentSfxSource;
    private Dictionary<GameObject, UnityAction> cachedButtonActions =
        new Dictionary<GameObject, UnityAction>();
    private StringBuilder stringBuilder = new StringBuilder(100);

    private List<CardCreateInfo> reusableCreateList = new List<CardCreateInfo>(20);
    private HashSet<int> reusableHashSet = new HashSet<int>();
    private HashSet<string> reusableStringSet = new HashSet<string>();
    private System.Random sharedRandom = new System.Random();

    private static readonly Dictionary<CardMatchGameType, (int, int)[]> LevelRanges =
        new Dictionary<CardMatchGameType, (int, int)[]>
        {
            [CardMatchGameType.RomanNumerals] = new[]
            {
                (1, 10),
                (1, 25),
                (1, 50),
                (1, 75),
                (1, 100),
            },
            [CardMatchGameType.MathOperations] = new[]
            {
                (1, 50),
                (1, 100),
                (1, 300),
                (1, 500),
                (1, 1000),
            },
            [CardMatchGameType.TimeAndClocks] = new[] { (1, 12) },
            [CardMatchGameType.NumberSequences] = new[]
            {
                (1, 30),
                (1, 60),
                (1, 100),
                (1, 150),
                (1, 200),
            },
        };

    private static readonly Dictionary<CardMatchGameType, (int, int)> MaxRanges = new Dictionary<
        CardMatchGameType,
        (int, int)
    >
    {
        [CardMatchGameType.RomanNumerals] = (1, 100),
        [CardMatchGameType.MathOperations] = (1, 9999),
        [CardMatchGameType.TimeAndClocks] = (1, 12),
        [CardMatchGameType.NumberSequences] = (1, 200),
    };

    void Start()
    {
        stem = FindAnyObjectByType<StemGameManager>();
        if (stem != null)
        {
            InitializeComponents();
        }

        InitializeObjectPool();
        ApplyDifficultyForLevel(levelIndex);
        SetupGame();
    }

    private void OnDestroy()
    {
        CleanupObjectPool();
    }

    private void InitializeComponents()
    {
        yonergeText = stem.yonergeText;
        WinSFX = stem.WinSFX;
        WrongSFX = stem.WrongSFX;
        WinSFXFinal = stem.WinSFXFinal;
        gameMode = stem.cardMatchGameTargetType;
        levelIndex = stem.levelIndex;

        if (stem.trueObjects == null || stem.trueObjects.Length == 0 || stem.trueObjects[0] == null)
        {
            Debug.LogError("StemGameManager.trueObjects boş!");
            return;
        }
        objectParent = stem.trueObjects[0];

        cardPrefab = stem.etcPrefab[0];
        if (cardPrefab == null)
        {
            Debug.LogError("Card prefab atanmamış!");
            return;
        }

        if (gridLayoutGroup == null)
            gridLayoutGroup =
                objectParent.GetComponent<GridLayoutGroup>()
                ?? objectParent.AddComponent<GridLayoutGroup>();
    }

    private void InitializeObjectPool()
    {
        if (cardPrefab == null || objectParent == null)
            return;

        for (int i = 0; i < INITIAL_POOL_SIZE; i++)
        {
            GameObject card = Instantiate(cardPrefab, objectParent.transform);
            card.name = $"PooledCard_{i}";
            card.SetActive(false);
            cardPool.Enqueue(card);
        }

        Debug.Log($"[ObjectPool] Initialized with {INITIAL_POOL_SIZE} cards");
    }

    private GameObject GetCardFromPool()
    {
        GameObject card;

        if (cardPool.Count > 0)
        {
            card = cardPool.Dequeue();
        }
        else
        {
            // Pool boşsa yeni kart oluştur (ama limit koy)
            if (activeCards.Count < MAX_POOL_SIZE)
            {
                card = Instantiate(cardPrefab, objectParent.transform);
                card.name = $"PooledCard_Extra_{activeCards.Count}";
                Debug.LogWarning(
                    "[ObjectPool] Created extra card - consider increasing INITIAL_POOL_SIZE"
                );
            }
            else
            {
                Debug.LogError("[ObjectPool] Max pool size reached!");
                return null;
            }
        }

        card.SetActive(true);
        activeCards.Add(card);
        return card;
    }

    private void ReturnCardToPool(GameObject card)
    {
        if (card == null)
            return;

        Button btn = card.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.interactable = true;
        }

        Transform emptyT = card.transform.GetChild(0);
        if (emptyT != null)
        {
            emptyT.gameObject.SetActive(false);
        }

        card.SetActive(false);
        activeCards.Remove(card);

        if (cardPool.Count < MAX_POOL_SIZE)
        {
            cardPool.Enqueue(card);
        }
        else
        {
            Destroy(card);
        }
    }

    private void CleanupObjectPool()
    {
        foreach (var card in activeCards)
        {
            if (card != null)
            {
                Button btn = card.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.RemoveAllListeners();
                Destroy(card);
            }
        }
        activeCards.Clear();

        while (cardPool.Count > 0)
        {
            var card = cardPool.Dequeue();
            if (card != null)
                Destroy(card);
        }

        cardMap.Clear();
        cachedButtonActions.Clear();
    }

    private void SetupGame()
    {
        if (objectParent == null)
            return;

        ClearCards();

        reusableCreateList.Clear();
        GenerateCardsForGameMode(reusableCreateList);

        if (reusableCreateList.Count < pairsCount * 2)
        {
            int actualPairs = reusableCreateList.Count / 2;
            Debug.LogWarning(
                $"Yeterli kombinasyon üretilemedi. pairsCount {pairsCount} -> {actualPairs}"
            );
            pairsCount = actualPairs;
        }

        ShuffleList(reusableCreateList, sharedRandom);
        InstantiateCard(reusableCreateList);
        SetColumnsandRows();
    }

    private void GenerateCardsForGameMode(List<CardCreateInfo> outList)
    {
        switch (gameMode)
        {
            case CardMatchGameType.MathOperations:
                GenerateMathOperationCards(outList);
                break;
            case CardMatchGameType.RomanNumerals:
                GenerateRomanNumeralCards(outList);
                break;
            case CardMatchGameType.TimeAndClocks:
                GenerateTimeAndClockCards(outList);
                break;
            case CardMatchGameType.NumberSequences:
                GenerateNumberSequenceCards(outList);
                break;
        }
    }

    private void ClearCards()
    {
        // Kartları pool'a geri döndür (destroy etme!)
        var cardsToReturn = new List<GameObject>(activeCards);

        foreach (var card in cardsToReturn)
        {
            if (card != null)
            {
                ReturnCardToPool(card);
            }
        }

        cardMap.Clear();
        cachedButtonActions.Clear();
        firstSelected = null;
        isProcessing = false;
        matchesFound = 0;

        Debug.Log($"[ObjectPool] Cleared {cardsToReturn.Count} cards, Pool size: {cardPool.Count}");
    }

    private void InstantiateCard(List<CardCreateInfo> createList)
    {
        for (int i = 0; i < createList.Count; i++)
        {
            var info = createList[i];
            GameObject go = GetCardFromPool();

            if (go == null)
                continue;

            go.name = $"Card_{i}_{info.display}";

            Transform emptyT = go.transform.GetChild(0);
            if (emptyT == null)
            {
                Debug.LogError($"Prefab'ın içinde 'Empty' child bulunamadı: {go.name}");
                continue;
            }

            GameObject emptyObj = emptyT.gameObject;
            emptyObj.SetActive(false);

            TMP_Text label = emptyObj.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = info.display;

            Button btn = go.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.interactable = true;

                if (!cachedButtonActions.TryGetValue(go, out var action))
                {
                    GameObject captured = go;
                    action = () => OnCardClicked(captured);
                    cachedButtonActions[go] = action;
                }

                btn.onClick.AddListener(action);
            }

            cardMap.Add(
                go,
                new CardInfo
                {
                    root = go,
                    value = info.value,
                    display = info.display,
                    isRevealed = false,
                    isMatched = false,
                    emptyObject = emptyObj,
                    label = label,
                }
            );
        }
    }

    private void GenerateNumberSequenceCards(List<CardCreateInfo> outList)
    {
        reusableHashSet.Clear();
        int attempts = 0;
        int maxAttempts = 1000;

        Vector2 range = (stem != null) ? stem.numberRangeXY : new Vector2(1, 20);
        int min = Mathf.Max(1, (int)range.x);
        int max = Mathf.Max(min, (int)range.y);

        string patternType = GetPatternTypeForLevel(levelIndex);

        while (outList.Count < pairsCount * 2 && attempts++ < maxAttempts)
        {
            int length = 5;

            List<int> sequence = GeneratePattern(patternType, length, min, max, out int answer);

            if (sequence == null || sequence.Count != length)
                continue;

            if (answer < min || answer > max)
                continue;

            // Aynı cevabı tekrar etme
            if (reusableHashSet.Contains(answer))
                continue;

            reusableHashSet.Add(answer);

            int missingIndex = sequence.IndexOf(answer);

            stringBuilder.Clear();
            for (int i = 0; i < length; i++)
            {
                if (i == missingIndex)
                    stringBuilder.Append("?");
                else
                    stringBuilder.Append(sequence[i]);

                if (i < length - 1)
                    stringBuilder.Append(" → ");
            }

            string display = stringBuilder.ToString();
            AddCardPair(outList, answer, display, answer.ToString());
            Debug.Log("Deneme sayisi: " + attempts);
        }
    }

    private List<int> GeneratePattern(string type, int length, int min, int max, out int answer)
    {
        List<int> seq = new List<int>();
        answer = 0;

        switch (type)
        {
            // Basit Aritmetik (+1, +2, +5, +10)
            case "arithmetic_easy":
                int[] stepsEasy = { 1, 2, 5, 10 };
                int stepEasy = stepsEasy[sharedRandom.Next(stepsEasy.Length)];

                // Cevabı range'den seç
                answer = sharedRandom.Next(min, max + 1);

                // Cevaba göre geriye giderek diziyi oluştur
                int startPos = sharedRandom.Next(0, length); // ? nerede olacak
                for (int i = 0; i < length; i++)
                {
                    int offset = (i - startPos) * stepEasy;
                    int val = answer + offset;

                    if (val < min || val > max)
                        return null; // Geçersiz dizi

                    seq.Add(val);
                }
                break;

            // Geriye Sayma (-2, -3, -4, -5)
            case "arithmetic_reverse":
                int[] stepsNeg = { -2, -3, -4, -5 };
                int stepNeg = stepsNeg[sharedRandom.Next(stepsNeg.Length)];

                answer = sharedRandom.Next(min, max + 1);
                startPos = sharedRandom.Next(0, length);

                for (int i = 0; i < length; i++)
                {
                    int offset = (i - startPos) * stepNeg;
                    int val = answer + offset;

                    if (val < min || val > max)
                        return null;

                    seq.Add(val);
                }
                break;

            // Çarpım Tablosu (×2, ×3, ×5, ×6)
            case "multiplication":
                int[] mults = { 2, 3, 5, 6 };
                int multiplier = mults[sharedRandom.Next(mults.Length)];

                answer = sharedRandom.Next(min, max + 1);
                startPos = sharedRandom.Next(0, length);

                // Cevabın hangi çarpım adımında olduğunu bul
                int baseVal = (int)(answer / Mathf.Pow(multiplier, startPos));
                if (baseVal < 1)
                    baseVal = 1;

                for (int i = 0; i < length; i++)
                {
                    int val = baseVal * (int)Mathf.Pow(multiplier, i);

                    if (val < min || val > max)
                        return null;

                    seq.Add(val);
                }

                // Gerçek cevabı güncelle (yuvarlama hatası için)
                answer = seq[startPos];
                break;

            // İleri Matematik (Kare, Fibonacci)
            case "advanced_math":
                bool isFibo = sharedRandom.Next(0, 2) == 0;

                if (isFibo)
                {
                    // Fibonacci: Tam diziyi oluştur, sonra range'e uygun kısmı al
                    List<int> fullFibo = new List<int> { 1, 1 };
                    while (fullFibo[fullFibo.Count - 1] < max)
                    {
                        int next = fullFibo[fullFibo.Count - 1] + fullFibo[fullFibo.Count - 2];
                        if (next > max)
                            break;
                        fullFibo.Add(next);
                    }

                    // Range'e uyan bir pencere seç
                    if (fullFibo.Count < length)
                        return null;

                    int startIdx = sharedRandom.Next(0, fullFibo.Count - length + 1);
                    for (int i = 0; i < length; i++)
                    {
                        seq.Add(fullFibo[startIdx + i]);
                    }

                    startPos = sharedRandom.Next(0, length);
                    answer = seq[startPos];
                }
                else
                {
                    // Kareler
                    int maxBase = (int)Mathf.Sqrt(max);
                    int minBase = (int)Mathf.Sqrt(min);

                    if (maxBase - minBase < length - 1)
                        return null;

                    int baseStart = sharedRandom.Next(minBase, maxBase - length + 2);
                    for (int i = 0; i < length; i++)
                    {
                        int val = (baseStart + i) * (baseStart + i);
                        seq.Add(val);
                    }

                    startPos = sharedRandom.Next(0, length);
                    answer = seq[startPos];
                }
                break;

            // Karma (×2+1 veya Geometrik)
            case "complex":
                bool useMultiplyPlus = sharedRandom.Next(0, 2) == 0;

                if (useMultiplyPlus)
                {
                    // ×2+1 deseni: Geriye hesapla
                    answer = sharedRandom.Next(min, max + 1);
                    startPos = sharedRandom.Next(0, length);

                    // Başlangıç değerini bul
                    int current = answer;
                    for (int back = startPos; back > 0; back--)
                    {
                        current = (current - 1) / 2;
                        if (current < 1)
                            return null;
                    }

                    // Diziyi oluştur
                    for (int i = 0; i < length; i++)
                    {
                        if (current > max)
                            return null;
                        seq.Add(current);
                        current = current * 2 + 1;
                    }

                    answer = seq[startPos];
                }
                else
                {
                    // Geometrik + varyasyon
                    int baseVale = sharedRandom.Next(2, 4);
                    int maxSteps = (int)(Mathf.Log(max) / Mathf.Log(baseVale));

                    if (maxSteps < length - 1)
                        return null;

                    for (int i = 0; i < length; i++)
                    {
                        int val = (int)Mathf.Pow(baseVale, i) + sharedRandom.Next(0, 3);
                        seq.Add(val);
                    }

                    startPos = sharedRandom.Next(0, length);
                    answer = seq[startPos];
                }
                break;

            default:
                return null;
        }

        return seq.Count == length ? seq : null;
    }

    private string GetPatternTypeForLevel(int level)
    {
        if (level <= 12)
            return "arithmetic_easy"; // +1, +2, +5, +10
        else if (level <= 24)
            return "arithmetic_reverse"; // -2, -3, -4
        else if (level <= 36)
            return "multiplication"; // ×2, ×3, ×5
        else if (level <= 49)
            return "advanced_math"; // kare, fibonacci
        else
            return "complex"; // geometrik, karma
    }

    private void GenerateTimeAndClockCards(List<CardCreateInfo> outList)
    {
        int minuteStep = GetMinuteStepForLevel(levelIndex);
        reusableStringSet.Clear();

        int attempts = 0;
        int maxAttempts = 10000;

        while (outList.Count < pairsCount * 2 && attempts++ < maxAttempts)
        {
            int hour = sharedRandom.Next(0, 24);
            int minute = sharedRandom.Next(0, 60 / minuteStep) * minuteStep;
            string timeKey = $"{hour:D2}:{minute:D2}";

            if (reusableStringSet.Contains(timeKey))
                continue;
            reusableStringSet.Add(timeKey);

            string digital24 = $"{hour:D2}:{minute:D2}";
            string naturalTurkish = ConvertToNaturalTurkish(hour, minute);
            int totalMinutes = hour * 60 + minute;

            AddCardPair(outList, totalMinutes, digital24, naturalTurkish);
        }
    }

    private int GetMinuteStepForLevel(int level)
    {
        if (level <= 9)
            return 60;
        else if (level <= 19)
            return 30;
        else if (level <= 29)
            return 15;
        else if (level <= 39)
            return 10;
        else
            return 5;
    }

    private string ConvertToNaturalTurkish(int hour, int minute)
    {
        string timePeriod = GetTimePeriod(hour);
        int hour12 = hour % 12;
        if (hour12 == 0)
            hour12 = 12;

        if (hour == 12 && minute == 0)
            return "Öğlen";
        if (hour == 0 && minute == 0)
            return "Gece Yarısı";

        string hourText = NumberToTurkish(hour12);

        if (minute == 0)
            return $"{timePeriod} {hourText}";
        else if (minute == 30)
            return $"{timePeriod} {hourText}buçuk";
        else if (minute == 15)
        {
            string suffix = GetVowelHarmonySuffix(hourText, false);
            return $"{timePeriod} {hourText}{suffix} Çeyrek Geçe";
        }
        else if (minute == 45)
        {
            int nextHour = (hour12 % 12) + 1;
            if (nextHour == 13)
                nextHour = 1;
            string nextHourText = NumberToTurkish(nextHour);
            string suffix = GetVowelHarmonySuffix(nextHourText, true);
            return $"{timePeriod} {nextHourText}{suffix} Çeyrek Var";
        }
        else if (minute < 30)
        {
            string minuteText = NumberToTurkish(minute);
            string suffix = GetVowelHarmonySuffix(hourText, false);
            return $"{timePeriod} {hourText}{suffix} {minuteText} Geçe";
        }
        else
        {
            int remainingMinutes = 60 - minute;
            int nextHour = (hour12 % 12) + 1;
            if (nextHour == 13)
                nextHour = 1;
            string nextHourText = NumberToTurkish(nextHour);
            string minuteText = NumberToTurkish(remainingMinutes);
            string suffix = GetVowelHarmonySuffix(nextHourText, true);
            return $"{timePeriod} {nextHourText}{suffix} {minuteText} Var";
        }
    }

    private string GetTimePeriod(int hour)
    {
        if (hour >= 6 && hour < 12)
            return "Sabah";
        else if (hour == 12)
            return "Öğlen";
        else if (hour >= 13 && hour < 18)
            return "Öğleden Sonra";
        else if (hour >= 18 && hour < 22)
            return "Akşam";
        else
            return "Gece";
    }

    private string NumberToTurkish(int number)
    {
        string[] ones = { "", "Bir", "İki", "Üç", "Dört", "Beş", "Altı", "Yedi", "Sekiz", "Dokuz" };
        string[] tens = { "", "On", "Yirmi", "Otuz", "Kırk", "Elli" };

        if (number == 0)
            return "Sıfır";
        if (number < 10)
            return ones[number];
        if (number < 60)
        {
            int ten = number / 10;
            int one = number % 10;
            return one == 0 ? tens[ten] : tens[ten] + ones[one];
        }
        return number.ToString();
    }

    private string GetVowelHarmonySuffix(string word, bool isDative)
    {
        if (string.IsNullOrEmpty(word))
            return isDative ? "e" : "ı";

        char lastVowel = GetLastVowel(word);
        char lastChar = char.ToLowerInvariant(word[word.Length - 1]);

        string suffix;
        if (isDative)
        {
            suffix = "eiöü".Contains(lastVowel) ? "e" : "a";
            if ("aeıioöuü".Contains(lastChar))
                suffix = "y" + suffix;
        }
        else
        {
            if ("aı".Contains(lastVowel))
                suffix = "ı";
            else if ("ei".Contains(lastVowel))
                suffix = "i";
            else if ("ou".Contains(lastVowel))
                suffix = "u";
            else
                suffix = "ü";
        }
        return suffix;
    }

    private char GetLastVowel(string text)
    {
        for (int i = text.Length - 1; i >= 0; i--)
        {
            char c = char.ToLowerInvariant(text[i]);
            if ("aeıioöuü".Contains(c))
                return c;
        }
        return 'a';
    }

    private void GenerateMathOperationCards(List<CardCreateInfo> outList)
    {
        Vector2 range = (stem != null) ? stem.numberRangeXY : new Vector2(1, 20);
        int min = Mathf.Max(1, (int)range.x);
        int max = Mathf.Max(min, (int)range.y);

        List<string> operators = new List<string>();
        if (stem.answerBoxesString != null)
            operators.AddRange(stem.answerBoxesString);
        else
            operators.AddRange(new[] { "+", "-", "x", "/" });

        reusableHashSet.Clear();

        int attempts = 0;
        int maxAttempts = 20000;

        while (outList.Count < pairsCount * 2 && attempts++ < maxAttempts)
        {
            string op = operators[sharedRandom.Next(operators.Count)];

            if (TryGenerateOperation(op, min, max, sharedRandom, out int result, out string expr))
            {
                if (!reusableHashSet.Contains(result))
                {
                    reusableHashSet.Add(result);
                    AddCardPair(outList, result, expr, result.ToString());
                }
            }
        }
    }

    private bool TryGenerateOperation(
        string op,
        int min,
        int max,
        System.Random rnd,
        out int result,
        out string expr
    )
    {
        result = 0;
        expr = "";

        switch (op)
        {
            case "+":
                return TryGenerateAddition(min, max, rnd, out result, out expr);
            case "-":
                return TryGenerateSubtraction(min, max, rnd, out result, out expr);
            case "x":
                return TryGenerateMultiplication(min, max, rnd, out result, out expr);
            case "/":
                return TryGenerateDivision(min, max, rnd, out result, out expr);
            default:
                return false;
        }
    }

    private bool TryGenerateAddition(
        int min,
        int max,
        System.Random rnd,
        out int result,
        out string expr
    )
    {
        int a = rnd.Next(min, max + 1);
        int b = rnd.Next(min, max + 1);
        long sum = (long)a + (long)b;

        if (sum >= min && sum <= max)
        {
            result = (int)sum;
            expr = $"{a}+{b}";
            return true;
        }

        result = 0;
        expr = "";
        return false;
    }

    private bool TryGenerateSubtraction(
        int min,
        int max,
        System.Random rnd,
        out int result,
        out string expr
    )
    {
        int a = rnd.Next(min, max + 1);
        int b = rnd.Next(min, a + 1);
        int diff = a - b;

        if (diff >= min && diff <= max)
        {
            result = diff;
            expr = $"{a}-{b}";
            return true;
        }

        result = 0;
        expr = "";
        return false;
    }

    private bool TryGenerateMultiplication(
        int min,
        int max,
        System.Random rnd,
        out int result,
        out string expr
    )
    {
        int mulLimit = GetMulDivLimit(levelIndex);
        int minFactor = Mathf.Max(2, min);
        int maxFactor = Mathf.Min(mulLimit, max);

        if (minFactor > maxFactor)
        {
            result = 0;
            expr = "";
            return false;
        }

        for (int inner = 0; inner < 500; inner++)
        {
            int a = rnd.Next(minFactor, maxFactor + 1);
            int b = rnd.Next(minFactor, maxFactor + 1);

            if (a == 1 || b == 1)
                continue;

            long prod = (long)a * (long)b;
            if (prod >= min && prod <= max)
            {
                result = (int)prod;
                expr = $"{a}x{b}";
                return true;
            }
        }

        result = 0;
        expr = "";
        return false;
    }

    private bool TryGenerateDivision(
        int min,
        int max,
        System.Random rnd,
        out int result,
        out string expr
    )
    {
        int divLimit = GetMulDivLimit(levelIndex);
        int minDiv = Mathf.Max(2, min);
        int maxDiv = Mathf.Min(divLimit, max);

        if (minDiv > maxDiv)
        {
            result = 0;
            expr = "";
            return false;
        }

        for (int inner = 0; inner < 500; inner++)
        {
            int b = rnd.Next(minDiv, maxDiv + 1);
            if (b == 1)
                continue;

            int maxQuot = max / b;
            int minQuot = Mathf.Max(min, 2);

            if (maxQuot < minQuot)
                continue;

            int quotient = rnd.Next(minQuot, maxQuot + 1);
            int a = quotient * b;

            if (a >= min && a <= max)
            {
                result = quotient;
                expr = $"{a}÷{b}";
                return true;
            }
        }

        result = 0;
        expr = "";
        return false;
    }

    private int GetMulDivLimit(int level)
    {
        float up;
        if (level <= 9)
            up = Mathf.Lerp(3f, 6f, level / 10f);
        else if (level <= 19)
            up = Mathf.Lerp(6f, 10f, (level - 10) / 10f);
        else if (level <= 29)
            up = Mathf.Lerp(10f, 15f, (level - 20) / 10f);
        else if (level <= 39)
            up = Mathf.Lerp(15f, 20f, (level - 30) / 10f);
        else if (level <= 49)
            up = Mathf.Lerp(20f, 25f, (level - 40) / 10f);
        else
            up = 25f;

        float wave = (Mathf.Sin(level * Mathf.PI / 10f) + 1f) * 0.5f;
        float wobble = Mathf.Lerp(-0.05f, 0.05f, wave);
        up *= (1f + wobble);

        return Mathf.Clamp(Mathf.RoundToInt(up), 3, 25);
    }

    private void GenerateRomanNumeralCards(List<CardCreateInfo> outList)
    {
        Vector2 range = (stem != null) ? stem.numberRangeXY : new Vector2(1, 20);
        int min = Mathf.Max(1, (int)range.x);
        int max = Mathf.Max(min, (int)range.y);
        int availableRange = max - min + 1;

        if (availableRange < pairsCount)
        {
            Debug.LogWarning(
                $"Requested pairsCount={pairsCount} but range only has {availableRange} values."
            );
            pairsCount = Mathf.Min(pairsCount, availableRange);
        }

        reusableHashSet.Clear();

        while (reusableHashSet.Count < pairsCount)
        {
            int v = sharedRandom.Next(min, max + 1);
            reusableHashSet.Add(v);
        }

        foreach (int val in reusableHashSet)
        {
            string roman = ToRoman(val);
            AddCardPair(outList, val, roman, val.ToString());
        }
    }

    private string ToRoman(int number)
    {
        if (number < 1)
            return number.ToString();

        var map = new (int val, string sym)[]
        {
            (1000, "M"),
            (900, "CM"),
            (500, "D"),
            (400, "CD"),
            (100, "C"),
            (90, "XC"),
            (50, "L"),
            (40, "XL"),
            (10, "X"),
            (9, "IX"),
            (5, "V"),
            (4, "IV"),
            (1, "I"),
        };

        string res = "";
        foreach (var pair in map)
        {
            while (number >= pair.val)
            {
                res += pair.sym;
                number -= pair.val;
            }
        }
        return res;
    }

    private void AddCardPair(List<CardCreateInfo> list, int value, string display1, string display2)
    {
        list.Add(
            new CardCreateInfo
            {
                value = value,
                display = display1,
                isRoman = false,
            }
        );
        list.Add(
            new CardCreateInfo
            {
                value = value,
                display = display2,
                isRoman = false,
            }
        );
    }

    private void SetColumnsandRows()
    {
        if (objectParent != null)
        {
            int columns = GetConstraintCountForPairs(pairsCount);
            gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayoutGroup.constraintCount = columns;
            var bounds = ((RectTransform)objectParent.transform).rect;
            int rows = Mathf.CeilToInt(pairsCount * 2f / columns);
            gridLayoutGroup.spacing = new Vector2(18, 18);
            gridLayoutGroup.cellSize = new Vector2(Mathf.Min(260, (bounds.width - 18 * (columns - 1)) / columns), Mathf.Min(200, (bounds.height - 18 * (rows - 1)) / rows));
        }
    }

    private void ShuffleList(List<CardCreateInfo> createList, System.Random rnd)
    {
        for (int i = createList.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            var tmp = createList[i];
            createList[i] = createList[j];
            createList[j] = tmp;
        }
    }

    private int GetConstraintCountForPairs(int pairs)
    {
        switch (pairs)
        {
            case 2:
                return 2;
            case 3:
                return 3;
            case 4:
                return 4;
            case 5:
                return 5;
            case 6:
                return 4;
            case 7:
                return 7;
            case 8:
                return 4;
            case 9:
                return 6;
        }

        int totalCards = pairs * 2;
        int cols = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(totalCards)), 2, 8);
        return cols;
    }

    private void OnCardClicked(GameObject go)
    {
        if (isProcessing || levelTransitioning)
            return;
        if (!cardMap.ContainsKey(go))
            return;

        CardInfo ci = cardMap[go];
        if (ci.isMatched || ci.isRevealed)
            return;

        RevealCard(ci);

        if (firstSelected == null)
        {
            firstSelected = go;
            return;
        }
        else
        {
            if (firstSelected == go)
            {
                return;
            }
            CardInfo firstInfo = cardMap[firstSelected];
            CardInfo secondInfo = ci;

            if (firstInfo.value == secondInfo.value)
            {
                firstInfo.isMatched = true;
                secondInfo.isMatched = true;
                firstInfo.isRevealed = true;
                secondInfo.isRevealed = true;

                DisableButton(firstInfo.root);
                DisableButton(secondInfo.root);

                matchesFound++;
                PlaySfx(WinSFX);

                firstSelected = null;

                if (matchesFound >= pairsCount)
                {
                    StartCoroutine(HandleGameWin());
                }
            }
            else
            {
                StartCoroutine(HandleMismatch(firstSelected, go));
                firstSelected = null;
            }
        }
    }

    private void RevealCard(CardInfo ci)
    {
        if (ci.emptyObject != null)
            ci.emptyObject.SetActive(true);
        ci.isRevealed = true;
    }

    private void HideCard(CardInfo ci)
    {
        if (ci.emptyObject != null)
            ci.emptyObject.SetActive(false);
        ci.isRevealed = false;
    }

    private IEnumerator HandleMismatch(GameObject a, GameObject b)
    {
        isProcessing = true;
        PlaySfx(WrongSFX);
        yield return new WaitForSeconds(revealDuration);

        if (cardMap.ContainsKey(a))
            HideCard(cardMap[a]);
        if (cardMap.ContainsKey(b))
            HideCard(cardMap[b]);

        isProcessing = false;
    }

    private IEnumerator HandleGameWin()
    {
        if (levelTransitioning)
            yield break;
        levelTransitioning = true;

        TriggerFinale();

        yield return StartCoroutine(WaitForSfxComplete());

        levelIndex++;
        stem.FinalAnswer();
        ApplyDifficultyForLevel(levelIndex);
        SetupGame();

        levelTransitioning = false;
    }

    private IEnumerator WaitForSfxComplete()
    {
        if (SoundFX != null && currentSfxSource != null)
        {
            if (currentSfxSource.isPlaying)
            {
                while (currentSfxSource.isPlaying)
                {
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(0.05f);
            }

            if (SoundFX != null)
            {
                Destroy(SoundFX);
                SoundFX = null;
                currentSfxSource = null;
            }
        }

        yield break;
    }

    private void DisableButton(GameObject go)
    {
        Button b = go.GetComponent<Button>();
        if (b != null)
            b.interactable = false;
    }

    private void PlaySfx(GameObject prefab)
    {
        if (prefab == null)
            return;

        if (SoundFX != null)
        {
            Destroy(SoundFX);
            currentSfxSource = null;
        }

        SoundFX = Instantiate(prefab);
        currentSfxSource = SoundFX.GetComponent<AudioSource>();
    }

    private void ApplyDifficultyForLevel(int level)
    {
        pairsCount = GetPairsForLevel(level);
        revealDuration = GetRevealDurationForLevel(level);
        Vector2Int newRange = GetNumberRangeForLevel(level);

        if (stem != null)
        {
            stem.numberRangeXY = newRange;
        }

        Debug.Log(
            $"[CARD MATCH LEVEL {level}] Pairs={pairsCount}, Range={newRange.x}-{newRange.y}, RevealTime={revealDuration:F2}"
        );

        UpdateYonergeText();
    }

    private Vector2Int GetNumberRangeForLevel(int level)
    {
        if (gameMode == CardMatchGameType.TimeAndClocks)
        {
            var range = LevelRanges[CardMatchGameType.TimeAndClocks][0];
            return new Vector2Int(range.Item1, range.Item2);
        }

        // 50. seviyeden sonrası için MaxRanges'ten al
        if (level > 49)
        {
            var maxRange = MaxRanges[gameMode];
            return new Vector2Int(maxRange.Item1, maxRange.Item2);
        }

        // Normal durumlarda bracket hesapla
        int bracket = Mathf.Min(level / 10, 4);
        var ranges = LevelRanges[gameMode];

        bracket = Mathf.Min(bracket, ranges.Length - 1);

        return new Vector2Int(ranges[bracket].Item1, ranges[bracket].Item2);
    }

    private int GetPairsForLevel(int level)
    {
        int basePairs,
            maxPairs;

        if (level <= 9)
        {
            basePairs = 2;
            maxPairs = 3;
        }
        else if (level <= 19)
        {
            basePairs = 3;
            maxPairs = 4;
        }
        else if (level <= 29)
        {
            basePairs = 4;
            maxPairs = 5;
        }
        else if (level <= 39)
        {
            basePairs = 5;
            maxPairs = 6;
        }
        else if (level <= 49)
        {
            basePairs = 6;
            maxPairs = 7;
        }
        else
        {
            return 9;
        }

        int levelInGroup = level % 10;
        float wave = (Mathf.Sin(levelInGroup * Mathf.PI * 2f / 10f) + 1f) * 0.5f;
        float targetPairs = Mathf.Lerp(basePairs, maxPairs, wave);

        return Mathf.RoundToInt(targetPairs);
    }

    private float GetRevealDurationForLevel(int level)
    {
        if (level <= 12)
            return Mathf.Lerp(2.0f, 1.6f, level / 12f);
        if (level <= 25)
            return Mathf.Lerp(1.6f, 1.3f, (level - 12) / 13f);
        if (level <= 37)
            return Mathf.Lerp(1.3f, 1.0f, (level - 25) / 12f);
        if (level <= 50)
            return Mathf.Lerp(1.0f, 0.8f, (level - 37) / 13f);
        return 0.8f;
    }

    private void UpdateYonergeText()
    {
        string modeText = "";
        switch (gameMode)
        {
            case CardMatchGameType.RomanNumerals:
                modeText = "Roma rakamlarını eşleştir";
                break;
            case CardMatchGameType.MathOperations:
                modeText = "Matematik işlemlerini eşleştir";
                break;
            case CardMatchGameType.TimeAndClocks:
                modeText = "Saat ve zamanları eşleştir";
                break;
            case CardMatchGameType.NumberSequences:
                modeText = "Sayı dizilerini eşleştir";
                break;
        }

        if (yonergeText != null)
        {
            stringBuilder.Clear();
            stringBuilder.Append("Seviye ");
            stringBuilder.Append(levelIndex + 1);
            stringBuilder.Append("\n");
            stringBuilder.Append(modeText);

            yonergeText.SetText(stringBuilder);
        }
    }

    private void TriggerFinale()
    {
        if (SoundFX != null)
        {
            Destroy(SoundFX);
            SoundFX = null;
            currentSfxSource = null;
        }
        if (WinSFXFinal != null)
        {
            SoundFX = Instantiate(WinSFXFinal);
            SoundFX.name = "WinSFXFinal";
            currentSfxSource = SoundFX.GetComponent<AudioSource>();
        }
    }

    private class CardInfo
    {
        public GameObject root;
        public int value;
        public string display;
        public bool isRevealed;
        public bool isMatched;
        public GameObject emptyObject;
        public TMP_Text label;
    }

    private struct CardCreateInfo
    {
        public int value;
        public string display;
        public bool isRoman;
    }
}

[System.Serializable]
public enum CardMatchGameType
{
    MathOperations,
    RomanNumerals,
    TimeAndClocks,
    NumberSequences,
}



