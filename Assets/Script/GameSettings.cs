using UnityEngine;

public enum GameDifficulty
{
    Easy = 0,
    Normal = 1,
    Hard = 2
}

public static class GameSettings
{
    private const string DifficultyPrefKey = "selected_difficulty";

    public static GameDifficulty SelectedDifficulty
    {
        get
        {
            int raw = PlayerPrefs.GetInt(DifficultyPrefKey, (int)GameDifficulty.Normal);
            if (!System.Enum.IsDefined(typeof(GameDifficulty), raw))
            {
                return GameDifficulty.Normal;
            }
            return (GameDifficulty)raw;
        }
        set
        {
            var normalized = NormalizeDifficulty(value);
            PlayerPrefs.SetInt(DifficultyPrefKey, (int)normalized);
            PlayerPrefs.Save();
        }
    }

    public static string GetDifficultyLabel(GameDifficulty difficulty)
    {
        return NormalizeDifficulty(difficulty).ToString();
    }

    private static GameDifficulty NormalizeDifficulty(GameDifficulty difficulty)
    {
        return difficulty switch
        {
            GameDifficulty.Easy => GameDifficulty.Easy,
            GameDifficulty.Hard => GameDifficulty.Hard,
            _ => GameDifficulty.Normal
        };
    }
}
