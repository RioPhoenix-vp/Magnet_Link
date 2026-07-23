using UnityEngine;
using TMPro;

/// <summary>
/// Displays the persistent coin balance. Attach to your coin TMP_Text object.
/// Re-pulls the live value every time it's enabled, so it shows the correct
/// balance even when recreated on a level swap.
/// </summary>
public class CoinDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text coinText;

    private bool subscribed = false;

    void Awake()
    {
        if (coinText == null) coinText = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        // Force a refresh whenever this object becomes active (incl. fresh spawn).
        TryRefresh();
    }

    void Update()
    {
        // Keep trying until the manager exists, then stop polling.
        if (!subscribed)
            TryRefresh();
    }

    void TryRefresh()
    {
        if (CoinManager.Instance == null) return;

        if (!subscribed)
        {
            CoinManager.Instance.OnCoinsChanged += UpdateText;
            subscribed = true;
        }

        // Always pull the current value right now.
        UpdateText(CoinManager.Instance.Coins);
    }

    void OnDestroy()
    {
        if (subscribed && CoinManager.Instance != null)
            CoinManager.Instance.OnCoinsChanged -= UpdateText;
    }

    void UpdateText(int coins)
    {
        if (coinText != null)
            coinText.text = coins.ToString();
    }
}