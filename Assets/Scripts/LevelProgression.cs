using UnityEngine;

public static class LevelProgression
{
    private const string UnlockedLevelKey = "UnlockedLevel";

    public static void EnsureInitialized(int defaultUnlockedLevel = 1)
    {
        if (!PlayerPrefs.HasKey(UnlockedLevelKey))
        {
            SetUnlockedLevel(defaultUnlockedLevel);
        }
    }

    public static int GetUnlockedLevel(int defaultUnlockedLevel = 1)
    {
        return Mathf.Max(defaultUnlockedLevel, PlayerPrefs.GetInt(UnlockedLevelKey, defaultUnlockedLevel));
    }

    public static bool IsLevelUnlocked(int levelNumber, int defaultUnlockedLevel = 1)
    {
        return levelNumber <= GetUnlockedLevel(defaultUnlockedLevel);
    }

    public static void UnlockLevel(int levelNumber, int defaultUnlockedLevel = 1)
    {
        if (levelNumber <= 0)
        {
            return;
        }

        int unlocked = GetUnlockedLevel(defaultUnlockedLevel);
        if (levelNumber > unlocked)
        {
            SetUnlockedLevel(levelNumber);
        }
    }

    public static void SetUnlockedLevel(int levelNumber)
    {
        PlayerPrefs.SetInt(UnlockedLevelKey, Mathf.Max(1, levelNumber));
        PlayerPrefs.Save();
    }
}
