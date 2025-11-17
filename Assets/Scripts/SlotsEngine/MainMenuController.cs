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
		DataPersistenceManager.Instance.LoadGame();
		StateMachine.Instance.BeginStateMachine();
		MainMenuGroup.SetActive(false);
	}

	private void OnNewGamePressed()
	{
		DataPersistenceManager.Instance.NewGame();
		StateMachine.Instance.BeginStateMachine();
		MainMenuGroup.SetActive(false);
	}

	private void OnSettingsPressed()
	{

	}

	private void OnExitPressed()
	{

	}
}
