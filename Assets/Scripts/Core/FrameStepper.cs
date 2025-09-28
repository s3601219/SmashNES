using UnityEngine;
using System.Collections;

public class TrainingModeController : MonoBehaviour
{
    [Header("Keys")]
    public KeyCode pauseKey = KeyCode.P;
    public KeyCode stepKey = KeyCode.O;
    public KeyCode slowmoOnKey = KeyCode.LeftBracket;   // [
    public KeyCode slowmoOffKey = KeyCode.RightBracket;  // ]
    public KeyCode fps1ToggleKey = KeyCode.Semicolon;     // ;

    [Header("Slowmo Settings")]
    [Range(0.01f, 1f)]
    public float slowmoScale = 0.1f; // 10% speed

    [Header("1 FPS Mode")]
    public bool oneFpsMode = false;

    bool paused;

    void Update()
    {
        // Pause toggle
        if (Input.GetKeyDown(pauseKey))
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        // Step one frame while paused
        if (paused && Input.GetKeyDown(stepKey))
            StartCoroutine(StepOneFrame());

        // Slowmo controls
        if (Input.GetKeyDown(slowmoOnKey))
        {
            paused = false;
            Time.timeScale = slowmoScale;
        }
        if (Input.GetKeyDown(slowmoOffKey))
        {
            paused = false;
            Time.timeScale = 1f;
        }

        // 1 FPS toggle
        if (Input.GetKeyDown(fps1ToggleKey))
        {
            oneFpsMode = !oneFpsMode;

            if (oneFpsMode)
            {
                Application.targetFrameRate = 1;
                QualitySettings.vSyncCount = 0;
            }
            else
            {
                Application.targetFrameRate = -1; // reset to default
                QualitySettings.vSyncCount = 1;   // vsync back on
                if (!paused) Time.timeScale = 1f;
            }
        }
    }

    IEnumerator StepOneFrame()
    {
        // Run exactly one physics step
        Time.timeScale = 1f;

        // Wait until physics runs this frame
        yield return new WaitForFixedUpdate();

        // Immediately pause again BEFORE the next FixedUpdate can occur
        if (paused) Time.timeScale = 0f;

        // Let the frame render at paused state (optional but looks smoother)
        yield return null;
    }
}
