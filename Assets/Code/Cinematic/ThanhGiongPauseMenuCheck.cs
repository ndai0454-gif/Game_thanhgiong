using System;
using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Diagnostic: when the player is started with "-tgPauseCheck &lt;folder&gt;", opens the pause menu,
/// screenshots it, then presses Save &amp; Quit. Does nothing in a normal run.
/// </summary>
public sealed class ThanhGiongPauseMenuCheck : MonoBehaviour
{
    string dir;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-tgPauseCheck");
        if (i < 0 || i + 1 >= args.Length) return;
        var go = new GameObject("PauseMenuCheck");
        DontDestroyOnLoad(go);
        go.AddComponent<ThanhGiongPauseMenuCheck>().dir = args[i + 1];
    }

    IEnumerator Start()
    {
        Directory.CreateDirectory(dir);
        ThanhGiongCampaignController actor = null;
        for (int t = 0; t < 600 && actor == null; t++) { actor = FindAnyObjectByType<ThanhGiongCampaignController>(); yield return null; }
        for (int t = 0; t < 120; t++) yield return null;
        var hud = actor != null ? actor.hud : null;
        if (hud == null) { Debug.LogError("[PauseCheck] no HUD"); Application.Quit(2); yield break; }
        hud.TogglePause();
        for (int t = 0; t < 10; t++) yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, "pause_menu.png"));
        for (int t = 0; t < 10; t++) yield return new WaitForEndOfFrame();
        var world = actor.openWorldCampaign;
        string path = world != null ? world.CheckpointPath : "(no open world)";
        DateTime before = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        Debug.Log($"[PauseCheck] save file {path} before={before:o}");
        hud.SaveAndQuit();
        Debug.Log($"[PauseCheck] after SaveAndQuit: written={(File.Exists(path) && File.GetLastWriteTimeUtc(path) > before)}");
    }
}
