using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Anyone can call this to get a live box by color
    public Transform GetBox(BallColor color)
    {
        string tag = color switch
        {
            BallColor.Red => "RedBox",
            BallColor.Blue => "BlueBox",
            BallColor.Green => "GreenBox",
            _ => ""
        };

        if (string.IsNullOrEmpty(tag)) return null;

        GameObject found = GameObject.FindWithTag(tag);
        return found != null ? found.transform : null;
    }
}