using UnityEngine;
using TMPro;

public class PercentUI : MonoBehaviour
{
    [Header("Binding")]
    public PlatformFighterActor target;      // leave empty to auto-find CPU
    public TextMeshProUGUI label;            // auto-grab from this GO if null

    [Header("Display")]
    public string prefix = "";               // e.g., "CPU "
    public bool showPercentSign = true;

    void Reset()
    {
        label = GetComponent<TextMeshProUGUI>();
        AutoFindTargetIfMissing();
    }

    void Awake()
    {
        if (!label) label = GetComponent<TextMeshProUGUI>();
        AutoFindTargetIfMissing();
    }

    void AutoFindTargetIfMissing()
    {
        if (target) return;
        var cpu = FindObjectOfType<CpuLevel>();
        if (cpu) target = cpu.GetComponent<PlatformFighterActor>();
    }

    void Update()
    {
        if (!label) return;
        float p = target ? target.percent : 0f;
        int r = Mathf.RoundToInt(p);
        label.text = (string.IsNullOrEmpty(prefix) ? "" : prefix) + r + (showPercentSign ? "%" : "");
    }
}
