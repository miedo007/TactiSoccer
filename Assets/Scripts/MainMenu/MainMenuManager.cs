using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Unity.Services.Economy.Model;
using CozyFramework;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI entryFeeText;   // “Entry Fee: Y”
    public Button playButton;              // Play → GameScene
    public TextMeshProUGUI errorText;      // “Not enough coins!”

    [Header("Settings")]
    [Tooltip("ID of the currency to charge (must match your CurrencyDisplay ID)")]
    public string currencyId = "GOLD";
    [Tooltip("How many of that currency it costs to start a match")]
    public int entryFee = 10;
    [Tooltip("Name of your gameplay Scene")]
    public string gameSceneName = "GameScene";

    private IEnumerator Start()
    {
        // hide error + disable Play until balance is checked
        playButton.interactable = false;
        errorText.gameObject.SetActive(false);

        if (CozyAPI.Instance == null)
        {
            Debug.LogError("Missing CozyManager.prefab! It must be in the Main Menu scene.");
            yield break;
        }

        // poll until the SDK has loaded your balances
        bool ready = false;
        while (!ready)
        {
            try
            {
                // try reading any balance
                CozyAPI.Instance.GetCurrencyValue(currencyId);
                ready = true;
            }
            catch (System.NullReferenceException)
            {
                // not ready yet
            }
            yield return null;
        }

        // set the entry fee label
        entryFeeText.text = $"Entry Fee: {entryFee}";

        // now check the actual balance
        int bal = CozyAPI.Instance.GetCurrencyValue(currencyId);
        playButton.interactable = (bal >= entryFee);

        // hook up Play
        playButton.onClick.AddListener(OnPlayPressed);
    }

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

        // spend it
        _ = CozyAPI.Instance.SpendCurrency(currencyId, entryFee);

        // load your game
        SceneManager.LoadScene(gameSceneName);
    }

    private void HideError()
    {
        errorText.gameObject.SetActive(false);
    }
}
