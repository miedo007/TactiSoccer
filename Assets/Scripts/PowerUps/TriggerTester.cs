using UnityEngine;

public class TriggerTester : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[TriggerTester] {gameObject.name} ENTER trigger with {other.name} (tag={other.tag})");
    }

    void OnTriggerExit2D(Collider2D other)
    {
        Debug.Log($"[TriggerTester] {gameObject.name} EXIT trigger with {other.name}");
    }
}
