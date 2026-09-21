using System;
using UnityEngine;

[Serializable] public class PortfolioConfig
{
    public string title, turkish, repo, kind;
    public PortfolioMode[] modes;
    public static PortfolioConfig Load() => JsonUtility.FromJson<PortfolioConfig>(Resources.Load<TextAsset>("PortfolioConfig").text);
}
[Serializable] public class PortfolioMode
{
    public string scene, title, english;
    public int stemGameType, isRandom, isDynamicMode, isReverseMode, targetType, cardMatchGameTargetType, bubbleGameMode, step, x, y;
    public string[] operators;
}
