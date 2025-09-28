using UnityEngine;

public class CharacterTint : MonoBehaviour
{
    [Header("Landing Lag Tint")]
    public Color landingLagTint = new Color(1f, 0f, 0f, 0.5f); // semi-transparent red

    SpriteRenderer[] renderers;
    Color[] originalColors;

    void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;
    }

    public void SetLandingLagTint(bool enabled)
    {
        if (renderers == null) return;

        if (enabled)
        {
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].color = landingLagTint;
        }
        else
        {
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].color = originalColors[i];
        }
    }
}
