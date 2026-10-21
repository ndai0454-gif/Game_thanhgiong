using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>Opt-in evidence capture; never saved in campaign scenes.</summary>
public sealed class AdventureFrameCapture : MonoBehaviour
{
    public string fileName = "gameplay-hud.png";
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.3f);
        yield return new WaitForEndOfFrame();
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../AdventureVerification", gameObject.scene.name));
        Directory.CreateDirectory(folder);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, fileName));
        yield return null;
        Destroy(gameObject);
    }
}
