using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Spawns one button per level prefab. Destroyed and rebuilt from scratch
/// every time the panel opens, so unlock states are never stale.
/// Zero per-button Inspector wiring.
/// </summary>
public class LevelSelectMenu : MonoBehaviour
{
    [Header("Button prefab (Button + Image + child TMP_Text)")]
    [SerializeField] private GameObject levelButtonPrefab;

    [Header("Parent for spawned buttons (ScrollView → Content)")]
    [SerializeField] private Transform buttonContainer;

    [Header("Lock visuals")]
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(1f, 1f, 1f, 0.35f);

    [Tooltip("Optional lock icon child (searched by name inside the button prefab). Leave blank to skip.")]
    [SerializeField] private string lockIconChildName = "LockIcon";

    private void OnEnable()
    {
        RefreshButtons();
    }

    public void RefreshButtons()
    {
        if (levelButtonPrefab == null || buttonContainer == null)
        {
            Debug.LogWarning("[LevelSelectMenu] Missing button prefab or container.");
            return;
        }

        if (LevelManager.Instance == null)
        {
            Debug.LogWarning("[LevelSelectMenu] LevelManager.Instance is null — is it in the scene?");
            return;
        }

        // ── Wipe old buttons (reverse loop: safe while destroying) ──
        for (int i = buttonContainer.childCount - 1; i >= 0; i--)
            Destroy(buttonContainer.GetChild(i).gameObject);

        int total = LevelManager.Instance.LevelCount;
        int unlockedUpTo = LevelManager.Instance.GetUnlockedLevelIndex();

        for (int i = 0; i < total; i++)
        {
            int levelIndex = i;   // capture per-iteration for the closure

            GameObject go = Instantiate(levelButtonPrefab, buttonContainer);
            go.name = $"LevelButton_{levelIndex + 1}";
            go.SetActive(true);

            bool unlocked = levelIndex <= unlockedUpTo;

            TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = (levelIndex + 1).ToString();

            Image img = go.GetComponent<Image>();
            if (img != null)
                img.color = unlocked ? unlockedColor : lockedColor;

            if (!string.IsNullOrEmpty(lockIconChildName))
            {
                Transform lockIcon = go.transform.Find(lockIconChildName);
                if (lockIcon != null)
                    lockIcon.gameObject.SetActive(!unlocked);
            }

            Button btn = go.GetComponent<Button>();
            if (btn == null) continue;

            btn.interactable = unlocked;
            btn.onClick.RemoveAllListeners();

            if (unlocked)
            {
                btn.onClick.AddListener(() =>
                {
                    if (MenuNavigator.Instance != null)
                        MenuNavigator.Instance.OpenGameplay(levelIndex);
                });
            }
        }

        Debug.Log($"[LevelSelectMenu] Rebuilt {total} buttons — unlocked up to {unlockedUpTo + 1}");
    }
}