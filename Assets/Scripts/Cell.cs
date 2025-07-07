using UnityEngine;
using DG.Tweening;
using UnityEngine.EventSystems;

[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class Cell : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector] public int row, col;
    private GameManager gm;
    private SpriteRenderer sr;

    [Header("Pulse Settings")]
    [Tooltip("Scale multiplier for pulse effect")] public float pulseScale = 1.15f;
    [Tooltip("Duration for one half of the pulse cycle (scale up or down)")] public float pulseHalfDuration = 0.15f;

    [Header("Highlight Settings")]
    [Tooltip("Alpha when highlighted")]
    public float highlightAlpha = 0.5f;
    [Tooltip("Fade duration for highlight")] public float highlightFadeDuration = 0.1f;

    private Vector3 baseScale;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<BoxCollider2D>().isTrigger = true;

        // Capture the starting scale from the prefab
        baseScale = transform.localScale;

        // Start fully transparent
        sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0f);
    }

    public void Initialize(int r, int c, GameManager gameManager)
    {
        row = r;
        col = c;
        gm = gameManager;
    }

    void OnMouseDown() => OnCellTapped();
    public void OnPointerClick(PointerEventData eventData) => OnCellTapped();
    private void OnCellTapped()
    {
        if (gm != null)
            gm.OnCellClicked(row, col);
    }

    /// <summary>
    /// Toggle pulsing highlight via DOTween loops.
    /// </summary>
    public void Highlight(bool on)
    {
        // Kill any existing tweens on this GameObject
        transform.DOKill();
        sr.DOKill();

        if (on)
        {
            // Fade in to highlightAlpha
            sr.DOFade(highlightAlpha, highlightFadeDuration);

            // Continuous pulse scale
            transform.DOScale(baseScale * pulseScale, pulseHalfDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
        else
        {
            // Fade out
            sr.DOFade(0f, highlightFadeDuration);

            // Return to base scale
            transform.DOScale(baseScale, highlightFadeDuration)
                .SetEase(Ease.InOutSine);
        }
    }

    /// <summary>
    /// Set a solid color and stop any pulsing highlight.
    /// </summary>
    public void SetColor(Color c)
    {
        // Kill tweens
        transform.DOKill();
        sr.DOKill();

        // Apply color with full alpha
        sr.color = new Color(c.r, c.g, c.b, 1f);

        // Reset scale
        transform.localScale = baseScale;
    }
}
