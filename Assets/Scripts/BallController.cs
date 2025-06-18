using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class BallController : MonoBehaviour
{
    [Tooltip("Speed at which the ball moves between cells (units per second)")]
    public float moveSpeed = 4f;

    /// <summary>
    /// Animate the ball moving smoothly from its current position to the target.
    /// </summary>
    public IEnumerator MoveToCell(Vector3 targetPosition)
    {
        Vector3 startPos = transform.position;
        float distance = Vector3.Distance(startPos, targetPosition);
        float duration = distance / moveSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPos, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;
    }
}
