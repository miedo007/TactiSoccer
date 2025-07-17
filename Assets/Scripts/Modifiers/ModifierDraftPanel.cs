using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class ModifierDraftPanel : MonoBehaviour
{
    [SerializeField] private GameObject choiceItemPrefab;
    [SerializeField] private RectTransform choicesParent;  // must have a VerticalLayoutGroup + ContentSizeFitter
    [SerializeField] private Button toggleButton;          // “Show/Hide choices”
    [SerializeField] private TMP_Text titleText;           // the “Choose a modifier” title

    private RectTransform _panelRect;
    private Action<MatchModifierDefinition> _onPick;

    private void Awake()
    {
        _panelRect = GetComponent<RectTransform>();

        // Center the panel itself
        _panelRect.anchorMin = _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRect.pivot     = new Vector2(0.5f, 0.5f);

        // Prevent the background image from blocking clicks
        if (TryGetComponent<Image>(out var bgImg))
            bgImg.raycastTarget = false;

        // Wire up toggle button
        toggleButton.onClick.RemoveAllListeners();
        toggleButton.onClick.AddListener(ToggleVisibility);
        toggleButton.transform.SetAsLastSibling();
        if (toggleButton.targetGraphic != null)
            toggleButton.targetGraphic.raycastTarget = true;

        // Start hidden
        gameObject.SetActive(false);
        choicesParent.gameObject.SetActive(false);
        toggleButton.gameObject.SetActive(false);
        if (titleText != null)
            titleText.gameObject.SetActive(false);

        // Ensure the layout group centers its children
        if (choicesParent.TryGetComponent<VerticalLayoutGroup>(out var vlg))
            vlg.childAlignment = TextAnchor.MiddleCenter;
        if (choicesParent.TryGetComponent<ContentSizeFitter>(out var csf))
        {
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    /// <summary>Populates and displays the draft choices.</summary>
    public void Show(List<MatchModifierDefinition> options, Action<MatchModifierDefinition> onPick)
    {
        _onPick = onPick;
        ClearChoices();

        // show title
        if (titleText != null)
        {
            titleText.text = "Choose a modifier";
            titleText.gameObject.SetActive(true);
        }

        // Build one choice entry per modifier
        foreach (var mod in options)
        {
            var entry = Instantiate(choiceItemPrefab, choicesParent, false);
            entry.name = $"Choice_{mod.modifierName}";

            // Title & Description
            entry.transform.Find("Title")?.GetComponent<TMP_Text>()?.SetText(mod.modifierName);
            entry.transform.Find("Description")?.GetComponent<TMP_Text>()?.SetText(mod.description);

            // Configure its button
            var btn = entry.GetComponentInChildren<Button>();
            if (btn != null)
            {
                // disable raycasts on all visuals under this entry
                foreach (var g in entry.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
                // re-enable only the button’s targetGraphic
                if (btn.targetGraphic != null) btn.targetGraphic.raycastTarget = true;

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    // hide title once a choice is made
                    if (titleText != null)
                        titleText.gameObject.SetActive(false);

                    _onPick?.Invoke(mod);
                    Hide();    // hide choices (toggle remains)
                });
            }
        }

        // Force a layout rebuild before showing
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(choicesParent);

        // Center panel and content
        _panelRect.anchoredPosition    = Vector2.zero;
        choicesParent.anchoredPosition = Vector2.zero;

        // Show the whole UI + toggle
        gameObject.SetActive(true);
        choicesParent.gameObject.SetActive(true);
        toggleButton.gameObject.SetActive(true);

        // Update toggle label
        toggleButton.GetComponentInChildren<TMP_Text>()?.SetText("Hide choices");
        toggleButton.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Toggles the visibility of the choice list (toggle button remains visible).
    /// </summary>
    public void ToggleVisibility()
    {
        bool showing = !choicesParent.gameObject.activeSelf;
        choicesParent.gameObject.SetActive(showing);
        toggleButton.GetComponentInChildren<TMP_Text>()?
            .SetText(showing ? "Hide choices" : "Show choices");
        toggleButton.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Hides the choice entries (toggle button stays visible).
    /// </summary>
    public void Hide()
    {
        ClearChoices();
        choicesParent.gameObject.SetActive(false);
        toggleButton.GetComponentInChildren<TMP_Text>()?
            .SetText("Show choices");
        toggleButton.transform.SetAsLastSibling();

        // also hide the title for next time
        if (titleText != null)
            titleText.gameObject.SetActive(false);
    }

    private void ClearChoices()
    {
        for (int i = choicesParent.childCount - 1; i >= 0; i--)
            Destroy(choicesParent.GetChild(i).gameObject);
    }

    // These are just stubs so any existing GameManager calls to LockButtons/Reveal still compile
    public void LockButtons()
    {
        foreach (var btn in choicesParent.GetComponentsInChildren<Button>())
            btn.interactable = false;

        if (titleText != null)
            titleText.gameObject.SetActive(false);
    }

    public void Reveal(MatchModifierDefinition p, MatchModifierDefinition a)
    {
        // optional highlight logic
    }

    /// <summary>
    /// Show or hide the “Hide/Show choices” toggle button itself.
    /// </summary>
    public void SetToggleChoicesVisible(bool visible)
    {
        toggleButton.gameObject.SetActive(visible);
    }
}
