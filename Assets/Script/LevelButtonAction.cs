using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-wiring button action. Put this on a Next or Retry button that lives
/// INSIDE a level prefab (or anywhere). On Start it finds the button on its
/// own GameObject and the persistent LevelManager.Instance at runtime — so it
/// keeps working even though the prefab is destroyed and respawned each level.
///
/// No Inspector references needed. Just add the component and pick the action.
/// </summary>
[RequireComponent(typeof(Button))]
public class LevelButtonAction : MonoBehaviour
{
    public enum Action { NextLevel, RetryLevel }

    [Header("What should this button do?")]
    [SerializeField] private Action action = Action.NextLevel;

    private void Start()
    {
        Button btn = GetComponent<Button>();
        btn.onClick.AddListener(DoAction);
    }

    private void DoAction()
    {
        if (LevelManager.Instance == null)
        {
            Debug.LogError("[LevelButtonAction] LevelManager.Instance is null — " +
                           "is the LevelManager object in the scene?");
            return;
        }

        Time.timeScale = 1f; // unfreeze (win/over panel set it to 0)

        switch (action)
        {
            case Action.NextLevel:
                Debug.Log("▶️ Next button → LevelManager.NextLevel()");
                LevelManager.Instance.NextLevel();
                break;

            case Action.RetryLevel:
                Debug.Log("🔁 Retry button → LevelManager.RetryLevel()");
                LevelManager.Instance.RetryLevel();
                break;
        }
    }
}