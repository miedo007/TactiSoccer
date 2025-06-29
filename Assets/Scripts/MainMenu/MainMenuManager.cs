using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using CozyFramework;
using System.Collections;
using Unity.Services.Economy.Model;   // GetBalancesResult

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public Image entryFeeIcon;
    public TextMeshProUGUI entryFeeText;
    public Button playButton;
    public TextMeshProUGUI errorText;

    [Header("Settings")]
    public string currencyId = "GOLD";
    public int    entryFee   = 10;
    public string gameSceneName = "GameScene";

    // ───────── Awake: add click listener once ─────────
    private void Awake()
    {
        playButton.onClick.RemoveAllListeners();
        playButton.onClick.AddListener(OnPlayPressed);
    }

    // ───────── Currency‐refresh listener ─────────
    private void OnEnable()  => CozyEvents.CurrencyRefresh += OnCurrencyRefresh;
    private void OnDisable() => CozyEvents.CurrencyRefresh -= OnCurrencyRefresh;

    private void OnCurrencyRefresh(GetBalancesResult result)
{
    // If the button is gone (scene unloading) just ignore the event
    if (playButton == null) return;

    foreach (var bal in result.Balances)
    {
        if (bal.CurrencyId != currencyId) continue;
        playButton.interactable = (bal.Balance >= entryFee);
        break;
    }
}


    // ───────── Startup coroutine ─────────
    private IEnumerator Start()
    {
        playButton.interactable = false;
        errorText.gameObject.SetActive(false);

        if (CozyAPI.Instance == null)
        {
            Debug.LogError("Missing CozyManager.prefab in this scene!");
            yield break;
        }

        // Wait until Cozy’s local cache is initialised
        bool cacheReady = false;
        while (!cacheReady)
        {
            try
            {
                CozyAPI.Instance.GetCurrencyValue(currencyId);
                cacheReady = true;
            }
            catch (System.NullReferenceException)
            {
                // still not ready; wait one frame below
            }

            if (!cacheReady)
                yield return null;
        }

        // Guard: object may have been destroyed while waiting
        if (this == null || playButton == null) yield break;

        // Icon + entry‐fee label
        var def = CozyDatabase.Instance.GetCozyCurrency(currencyId);
        //if (def?.Icon != null) entryFeeIcon.sprite = def.Icon;
        entryFeeText.text = $"Entry Fee: {entryFee}";

        // Force fresh balances from the server
        var pull = CozyEconomy.Instance.RefreshCurrencyBalances();
        while (!pull.IsCompleted) yield return null;
        if (pull.IsFaulted) Debug.LogException(pull.Exception);

        // Play button state will be updated by OnCurrencyRefresh
    }

    // ───────── Play click ─────────
    private void OnPlayPressed()
    {
        int bal = CozyAPI.Instance.GetCurrencyValue(currencyId);
        if (bal < entryFee)
        {
            errorText.text = $"Not enough {currencyId}!";
            errorText.gameObject.SetActive(true);
            Invoke(nameof(HideError), 2f);
            return;
        }

        _ = CozyAPI.Instance.SpendCurrency(currencyId, entryFee);
        SceneManager.LoadScene(gameSceneName);
    }

    private void HideError() => errorText.gameObject.SetActive(false);
}
