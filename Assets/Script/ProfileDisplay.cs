using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the persistent profile level (and optional XP bar / text).
/// Re-pulls the live values every time it's enabled, so it shows correctly
/// even when recreated on a level swap.
/// </summary>
public class ProfileDisplay : MonoBehaviour
{
    [Header("Level number (e.g. the 'Lv 1' text)")]
    [SerializeField] private TMP_Text levelText;

    [Header("Optional XP bar (Image set to Filled) and XP text")]
    [SerializeField] private Image xpFillBar;
    [SerializeField] private TMP_Text xpText;

    [Tooltip("Optional prefix shown before the number, e.g. 'Lv '. Leave blank for just the number.")]
    [SerializeField] private string levelPrefix = "";

    private bool subscribed = false;

    void OnEnable()
    {
        TryRefresh();
    }

    void Update()
    {
        if (!subscribed)
            TryRefresh();
    }

    void TryRefresh()
    {
        if (ProfileManager.Instance == null) return;

        if (!subscribed)
        {
            ProfileManager.Instance.OnProfileChanged += UpdateDisplay;
            subscribed = true;
        }

        var pm = ProfileManager.Instance;
        UpdateDisplay(pm.Level, pm.CurrentXP, pm.XPForNextLevel);
    }

    void OnDestroy()
    {
        if (subscribed && ProfileManager.Instance != null)
            ProfileManager.Instance.OnProfileChanged -= UpdateDisplay;
    }

    void UpdateDisplay(int level, int currentXP, int xpForNext)
    {
        if (levelText != null)
            levelText.text = $"{levelPrefix}{level}";

        if (xpFillBar != null)
            xpFillBar.fillAmount = xpForNext > 0 ? (float)currentXP / xpForNext : 0f;

        if (xpText != null)
            xpText.text = $"{currentXP}/{xpForNext}";
    }
}