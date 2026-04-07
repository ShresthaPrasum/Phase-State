using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalLevelLoader : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string nextSceneName = "Level2";

    [Header("Progression")]
    [SerializeField] private bool unlockNextLevelOnUse = true;
    [SerializeField] private int explicitUnlockLevelNumber = -1;
    [SerializeField] private int defaultUnlockedLevel = 1;

    [Header("Activation")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool allowParentTagCheck = true;
    [SerializeField] private bool allowPlayerControllerCheck = true;
    [SerializeField] private bool useTrigger = true;
    [SerializeField, Min(0f)] private float loadDelay = 0f;

    private bool hasLoaded;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!useTrigger)
        {
            return;
        }

        TryLoad(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (useTrigger)
        {
            return;
        }

        TryLoad(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useTrigger)
        {
            return;
        }

        TryLoad(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (useTrigger)
        {
            return;
        }

        TryLoad(collision.gameObject);
    }

    private void TryLoad(GameObject candidate)
    {
        if (hasLoaded)
        {
            return;
        }

        if (!IsPlayerObject(candidate))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning("PortalLevelLoader has no target scene set.");
            return;
        }

        hasLoaded = true;

        if (unlockNextLevelOnUse)
        {
            RegisterLevelCompletion();
        }

        if (loadDelay > 0f)
        {
            Invoke(nameof(LoadTargetScene), loadDelay);
            return;
        }

        LoadTargetScene();
    }

    private bool IsPlayerObject(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(playerTag) && candidate.CompareTag(playerTag))
        {
            return true;
        }

        if (allowParentTagCheck)
        {
            Transform current = candidate.transform.parent;
            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(playerTag) && current.CompareTag(playerTag))
                {
                    return true;
                }
                current = current.parent;
            }
        }

        if (allowPlayerControllerCheck)
        {
            if (candidate.GetComponent<PlayerInstabilityController>() != null)
            {
                return true;
            }

            if (candidate.GetComponentInParent<PlayerInstabilityController>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void RegisterLevelCompletion()
    {
        int levelToUnlock = explicitUnlockLevelNumber > 0
            ? explicitUnlockLevelNumber
            : ExtractTrailingLevelNumber(nextSceneName);

        if (levelToUnlock <= 0)
        {
            return;
        }

        LevelProgression.EnsureInitialized(defaultUnlockedLevel);
        LevelProgression.UnlockLevel(levelToUnlock, defaultUnlockedLevel);
    }

    private int ExtractTrailingLevelNumber(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return -1;
        }

        int end = sceneName.Length - 1;
        while (end >= 0 && char.IsDigit(sceneName[end]))
        {
            end--;
        }

        if (end == sceneName.Length - 1)
        {
            return -1;
        }

        string numberPart = sceneName.Substring(end + 1);
        if (int.TryParse(numberPart, out int levelNumber))
        {
            return levelNumber;
        }

        return -1;
    }

    private void LoadTargetScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
