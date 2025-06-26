using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using CozyFramework;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public Image entryFeeIcon;             // The Image component of EntryFeeIcon
    public TextMeshProUGUI entryFeeText;   // The TextMeshProUGUI of EntryFeeText
    public Button playButton;              // Your Play button
    public TextMeshProUGUI errorText;      // Error text below it

    [Header("Settings")]
    [Tooltip("Currency ID to spend (must match your CurrencyDisplay ID)")]
    public string currencyId = "GOLD";
    [Tooltip("How many of that currency it costs")]
    public int entryFee = 10;
    [Tooltip("Gameplay scene name")]
    public string gameSceneName = "GameScene";

    private IEnumerator Start()
    {
        // 1) Hide error + disable Play until ready
        playButton.interactable = false;
        errorText.gameObject.SetActive(false);

        if (CozyAPI.Instance == null)
        {
            Debug.LogError("Missing CozyManager.prefab in this scene!");
            yield break;
        }

        // 2) Wait until the SDK has loaded your currencies
        bool ready = false;
        while (!ready)
        {
            try
            {
                CozyAPI.Instance.GetCurrencyValue(currencyId);
                ready = true;
            }
            catch (System.NullReferenceException)
            {
                // not ready—try again next frame
            }
            yield return null;
        }

        // 3) Set the icon sprite from your database
        var def = CozyDatabase.Instance.GetCozyCurrency(currencyId);
        if (def?.Icon != null)
            entryFeeIcon.sprite = def.Icon;

        // 4) Set the combined text
        entryFeeText.text = $"Entry Fee: {entryFee}";

        // 5) Enable Play if the player has enough
        int bal = CozyAPI.Instance.GetCurrencyValue(currencyId);
        playButton.interactable = (bal >= entryFee);

        // 6) Hook up the Play click
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

        // Deduct the fee
        _ = CozyAPI.Instance.SpendCurrency(currencyId, entryFee);

        // Load the gameplay scene
        SceneManager.LoadScene(gameSceneName);
    }

    private void HideError()
    {
        errorText.gameObject.SetActive(false);
    }
}
