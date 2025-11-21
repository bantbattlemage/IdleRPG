using UnityEngine;
using UnityEngine.UI;

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
