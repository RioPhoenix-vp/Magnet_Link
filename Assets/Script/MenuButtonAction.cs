using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-wiring menu button. Add to any Button, pick the action, done.
/// No Inspector references needed.
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButtonAction : MonoBehaviour
{
    public enum Action { Play, LevelSelect, MainMenu, NextLevel, RetryLevel, Quit }

    [SerializeField] private Action action = Action.Play;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(DoAction);
    }

    private void DoAction()
    {
        Time.timeScale = 1f;

        switch (action)
        {
            case Action.Play:
            case Action.LevelSelect:
                if (MenuNavigator.Instance != null)
                    MenuNavigator.Instance.OpenLevelSelect();
                break;

            case Action.MainMenu:
                if (MenuNavigator.Instance != null)
                    MenuNavigator.Instance.OpenMainMenu();
                break;

            case Action.NextLevel:
                if (LevelManager.Instance != null)
                    LevelManager.Instance.NextLevel();
                break;

            case Action.RetryLevel:
                if (LevelManager.Instance != null)
                    LevelManager.Instance.RetryLevel();
                break;

            case Action.Quit:
                Application.Quit();
                break;
        }
    }
}