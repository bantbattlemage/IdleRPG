using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple main menu controller wiring UI buttons to start or continue a session.
/// Hides the menu group once a game begins. Settings/Exit hooks are present for future implementation.
/// </summary>
public class MainMenuController : MonoBehaviour
{
	public GameObject MainMenuGroup;
	public Button NewGameButton;
	public Button ContinueButton;
	public Button SettingsButton;
	public Button ExitButton;

	void Start()
	{
		MainMenuGroup.SetActive(true);

		NewGameButton.onClick.AddListener(OnNewGamePressed);
		ContinueButton.onClick.AddListener(OnContinuePressed);
		SettingsButton.onClick.AddListener(OnSettingsPressed);
		ExitButton.onClick.AddListener(OnExitPressed);
	}

	private void OnContinuePressed()
	{
		GameMaster.Instance.BeginGame(false);
		MainMenuGroup.SetActive(false);
	}

	private void OnNewGamePressed()
	{
		GameMaster.Instance.BeginGame();
		MainMenuGroup.SetActive(false);
	}

	private void OnSettingsPressed()
	{

	}

	private void OnExitPressed()
	{

	}
}
