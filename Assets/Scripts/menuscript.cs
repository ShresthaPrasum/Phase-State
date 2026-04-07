using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu: MonoBehaviour
{
    [Header("Level Progression")]
    [SerializeField] private int defaultUnlockedLevel = 1;
    [SerializeField] private Button[] levelButtons;

    private void Start()
    {
        LevelProgression.EnsureInitialized(defaultUnlockedLevel);
        RefreshLevelButtons();
    }

    public void Play()
    {
        SceneManager.LoadScene("Level1");
    }
    public void OpenLevels()
    {
        SceneManager.LoadScene("LevelSelect");
    }
    public void Guide()
    {
        SceneManager.LoadScene("Guide");
    }

    public void LoadLevelByNumber(int levelNumber)
    {
        if (!LevelProgression.IsLevelUnlocked(levelNumber, defaultUnlockedLevel))
        {
            Debug.Log("Level " + levelNumber + " is locked.");
            return;
        }

        SceneManager.LoadScene("Level" + levelNumber);
    }

    public void RefreshLevelButtons()
    {
        if (levelButtons == null || levelButtons.Length == 0)
        {
            return;
        }

        int unlockedLevel = LevelProgression.GetUnlockedLevel(defaultUnlockedLevel);
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] != null)
            {
                int levelNumber = i + 1;
                levelButtons[i].interactable = levelNumber <= unlockedLevel;
            }
        }
    }
}