using UnityEngine;

public class GameSetup : MonoBehaviour
{
    void Awake()
    {
        Application.targetFrameRate = 60;   // lock rendering to 60 fps
        QualitySettings.vSyncCount = 0;     // disable vsync so targetFrameRate works
    }
}
