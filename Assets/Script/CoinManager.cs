using UnityEngine;
using System;

/// <summary>
/// Persistent coin balance, saved across sessions via PlayerPrefs.
/// Single source of truth for coins — level rewards and power-up purchases
/// both go through this.
///
/// SETUP
/// ─────
/// 1. Create an empty GameObject named "CoinManager".
/// 2. Add this component. Persists via DontDestroyOnLoad — put it only in your
///    first-loaded scene, or in every scene (duplicates self-destroy).
/// </summary>
public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }

    const string COIN_KEY = "PlayerCoins";

    public int Coins { get; private set; }
    public event Action<int> OnCoinsChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Coins = PlayerPrefs.GetInt(COIN_KEY, 0);
    }

    void Start()
    {
        // Notify any UI that subscribed after Awake
        OnCoinsChanged?.Invoke(Coins);
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        Coins += amount;
        Save();
    }

    /// <summary>Spends coins if affordable. Returns true on success.</summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || Coins < amount) return false;
        Coins -= amount;
        Save();
        return true;
    }

    public bool CanAfford(int amount) => Coins >= amount;

    public void SetCoins(int amount)
    {
        Coins = Mathf.Max(0, amount);
        Save();
    }

    void Save()
    {
        PlayerPrefs.SetInt(COIN_KEY, Coins);
        PlayerPrefs.Save();
        OnCoinsChanged?.Invoke(Coins);
    }
}