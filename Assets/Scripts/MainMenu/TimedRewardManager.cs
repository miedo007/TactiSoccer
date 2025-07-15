using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CozyFramework;

public class TimedRewardManager : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("How often players can claim, in seconds (e.g. 3600 = 1h).")]
    public float rewardIntervalSeconds = 3600f;
    [Tooltip("How much GOLD to give each claim.")]
    public int rewardAmount = 5;
    public string currencyId = "GOLD";

    [Header("UI")]
    public Button rewardButton;
    public TextMeshProUGUI rewardTimerText;

    private DateTime _nextClaimTime;
    private const string PrefKey = "NextTimedReward";

    private void Awake()
    {
        rewardButton.onClick.RemoveAllListeners();
        rewardButton.onClick.AddListener(OnClaimPressed);
    }

    private void Start()
    {
        LoadNextClaimTime();
        StartCoroutine(CountdownRoutine());
    }

    private void LoadNextClaimTime()
    {
        if (PlayerPrefs.HasKey(PrefKey))
        {
            long ticks = Convert.ToInt64(PlayerPrefs.GetString(PrefKey));
            _nextClaimTime = new DateTime(ticks, DateTimeKind.Utc);
        }
        else
        {
            // allow immediate claim on first run
            _nextClaimTime = DateTime.UtcNow;
        }
    }

    private void SaveNextClaimTime()
    {
        PlayerPrefs.SetString(PrefKey, _nextClaimTime.Ticks.ToString());
        PlayerPrefs.Save();
    }

    private IEnumerator CountdownRoutine()
    {
        while (true)
        {
            var now = DateTime.UtcNow;
            if (now >= _nextClaimTime)
            {
                rewardButton.interactable = true;
                rewardTimerText.text = "Reward Ready!";
                yield break; // stop updating
            }
            else
            {
                rewardButton.interactable = false;
                TimeSpan remaining = _nextClaimTime - now;
                rewardTimerText.text = $"{remaining.Hours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private void OnClaimPressed()
{
    // 1) Grant the currency via CozyAPI:
    _ = CozyAPI.Instance.GainCurrency(currencyId, rewardAmount);

    // 2) Compute next claim time
    _nextClaimTime = DateTime.UtcNow.AddSeconds(rewardIntervalSeconds);
    SaveNextClaimTime();

    // 3) Restart the countdown
    StopAllCoroutines();
    StartCoroutine(CountdownRoutine());
}
}
