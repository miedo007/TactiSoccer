using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class Cell : MonoBehaviour
{
    [HideInInspector] public int row, col;
    private GameManager gm;
    private SpriteRenderer sr;

    // Handle for the pulse coroutine
    private Coroutine pulseCoroutine;

    // Store the prefab's initial scale so we can maintain spacing
    private Vector3 baseScale;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<BoxCollider2D>().isTrigger = true;

        // Capture the starting scale from the prefab
        baseScale = transform.localScale;

        // Make cell fully transparent initially
        var baseColor = sr.color;
        sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
    }

    /// <summary>
    /// Initializes the cell with its coordinates and reference to the GameManager.
    /// </summary>
    public void Initialize(int r, int c, GameManager gameManager)
    {
        row = r;
        col = c;
        gm = gameManager;
    }

    void OnMouseDown()
    {
        gm.OnCellClicked(row, col);
    }

    /// <summary>
    /// Highlights or un-highlights the cell, starting or stopping the pulse effect.
    /// </summary>
    public void Highlight(bool on)
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        if (on)
        {
            // Semi-transparent yellow highlight
            sr.color = new Color(1f, 1f, 0f, 0.5f);
            pulseCoroutine = StartCoroutine(DoPulse());
        }
        else
        {
            // Return to fully transparent and reset scale
            var c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, 0f);
            transform.localScale = baseScale;
        }
    }

    /// <summary>
    /// Sets a solid color on the cell (e.g., attacker/defender markers), stopping any pulse.
    /// </summary>
    public void SetColor(Color c)
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
        sr.color = new Color(c.r, c.g, c.b, 1f);
        transform.localScale = baseScale;
    }

    /// <summary>
    /// Coroutine to pulse the cell scale up then down.
    /// </summary>
    private IEnumerator DoPulse()
    {
        Vector3 original = baseScale;
        Vector3 target = original * 1.15f;
        float t = 0f;

        // Scale up
        while (t < 1f)
        {
            t += Time.deltaTime * 6f;
            transform.localScale = Vector3.Lerp(original, target, t);
            yield return null;
        }

        // Scale down
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 6f;
            transform.localScale = Vector3.Lerp(target, original, t);
            yield return null;
        }

        // Ensure exact base scale at end
        transform.localScale = baseScale;
        pulseCoroutine = null;
    }
}
