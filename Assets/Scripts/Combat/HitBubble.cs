using UnityEngine;

public class HitBubble : MonoBehaviour
{
    [Header("HitBubble Settings")]
    public float radius = 0.5f;
    public Color activeColor = new Color(1f, 0f, 0f, 0.4f);
    public Color inactiveColor = new Color(0f, 1f, 0f, 0.2f);

    public bool active = false; // turn on when the move is active

    void OnDrawGizmos()
    {
        // Editor-only visualization
        Gizmos.color = active ? activeColor : inactiveColor;
        Gizmos.DrawSphere(transform.position, radius);
    }
}
