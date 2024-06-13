using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameEntityView : MonoBehaviour
{
	public Image Portrait;
	public TextMeshProUGUI EntityName;
	public TextMeshProUGUI HP;
	public TextMeshProUGUI MP;
	public TextMeshProUGUI XP;
	public TextMeshProUGUI SwingTimerText;
	public Image SwingTimerBar;
	public Image HealthBar;
	public Image ManaBar;
	public Image ExperienceBar;
	public Transform BuffGroupHolder;
	public GameObject BuffPrefab;
}
