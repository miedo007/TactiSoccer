using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ModifierDraftPanel : MonoBehaviour
{
    [SerializeField] private GameObject choiceItemPrefab;
    [SerializeField] private RectTransform choicesParent;  // should have a VerticalLayoutGroup + (optional) ContentSizeFitter

    private Action<MatchModifierDefinition> _onPick;

    private void Awake()
    {
        // start hidden
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Show the panel with exactly one "choice" entry per modifier.
    /// </summary>
    public void Show(List<MatchModifierDefinition> options, Action<MatchModifierDefinition> onPick)
    {
        _onPick = onPick;

        // 1) clear out any old choices
        ClearChoices();

        // 2) disable the panel's own Image raycast (so it won't eat touches)
        var panelBg = GetComponent<Image>();
        if (panelBg != null) panelBg.raycastTarget = false;

        // 3) instantiate one button per modifier
        foreach (var mod in options)
        {
            var captured = mod;
            // worldPositionStays = false so we pick up the prefab's layout sizing
            var choiceGO = Instantiate(choiceItemPrefab, choicesParent, false);
            choiceGO.name = $"Choice_{captured.modifierName}";

            // set title & description
            choiceGO.transform.Find("Title")
                    .GetComponent<TMP_Text>().text = captured.modifierName;
            choiceGO.transform.Find("Description")
                    .GetComponent<TMP_Text>().text = captured.description;

            // wire up the button so only its targetGraphic raycasts
            var btn = choiceGO.GetComponentInChildren<Button>();
            if (btn != null)
            {
                // turn off raycasts on everything under this choice
                foreach (var g in choiceGO.GetComponentsInChildren<Graphic>())
                    g.raycastTarget = false;

                // re-enable just the button's target graphic
                if (btn.targetGraphic != null)
                    btn.targetGraphic.raycastTarget = true;

                // bind click
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    _onPick?.Invoke(captured);
                    Hide();
                });
            }
        }

        // 4) force Unity to recalc layout **before** showing
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(choicesParent);

        // 5) finally show
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides panel and cleans up all children.
    /// </summary>
    public void Hide()
    {
        ClearChoices();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Destroys all existing choice items.
    /// </summary>
    public void ClearChoices()
    {
        for (int i = choicesParent.childCount - 1; i >= 0; i--)
        {
            Destroy(choicesParent.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// (Optional) disable all buttons so they can’t be clicked.
    /// </summary>
    public void LockButtons()
    {
        foreach (var btn in choicesParent.GetComponentsInChildren<Button>())
            btn.interactable = false;
    }

    /// <summary>
    /// (Optional) highlight the two picks after draft resolution.
    /// </summary>
    public void Reveal(MatchModifierDefinition playerPick, MatchModifierDefinition aiPick)
    {
        // TODO: e.g. color the two matching choice entries
    }
}
