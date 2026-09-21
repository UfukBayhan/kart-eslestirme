using System;
using System.Collections.Generic;

// Session-only data. This preview has no account or server dependency.
[Serializable]
public class GameData
{
    public string UserId;
    public string SectionCode;
    public List<DataEntry> Data = new List<DataEntry>();
    public string version;
}

[Serializable]
public class DataEntry
{
    public string Part;
    public int Wrong;
    public float Time;
}

[Serializable]
public class ModeConfiguration
{
    public ModeSettings[] modes;
}

[Serializable]
public class ModeSettings
{
    public string scene;
    public int x, y, goal;
    public string[] operators;
}
