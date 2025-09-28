using UnityEngine;

[RequireComponent(typeof(FighterInput))]
public class CpuLevel : MonoBehaviour
{
    [Tooltip("0 = dummy (no inputs). Higher values for future AI.")]
    public int level = 0;

    void Awake()
    {
        var fi = GetComponent<FighterInput>();
        // Level 0: completely idle dummy for testing
        if (level <= 0) fi.enabled = false;
        // (For future AI levels, leave FighterInput enabled and drive it via an AI script.)
    }
}
