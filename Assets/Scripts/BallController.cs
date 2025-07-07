using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(SpriteRenderer))]
public class BallController : MonoBehaviour
{
    [Tooltip("Speed at which the ball moves between cells (units per second)")]
    public float moveSpeed = 4f;

    [Tooltip("Easing function for movement")]
    public Ease moveEase = Ease.InOutQuad;

    /// <summary>
    /// Animate the ball moving smoothly from its current position to the target using DOTween.
    /// Returns the Tween so you can chain it.
    /// </summary>
    public Tween MoveToCell(Vector3 targetPosition)
    {
        transform.DOKill();  // stop any in-flight tweens

        float distance = Vector3.Distance(transform.position, targetPosition);
        float duration = distance / moveSpeed;

        return transform
            .DOMove(targetPosition, duration)
            .SetEase(moveEase);
    }
}
