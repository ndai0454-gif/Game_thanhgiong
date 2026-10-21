using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Records a gameplay demo reel of the seamless open world as numbered PNG frames. The rider is driven through the real
/// MountedHorseController (DriveMovement, as AdventureRuntimeVerification does), so gait, camera and HUD are the game's own.
/// Time is locked with Time.captureFramerate, so playback is smooth however slowly frames are written.
/// Assemble the frames with Scripts/make_demo_video.py. Add this component to a scene object in Play mode to start.
/// </summary>
public sealed class ThanhGiongDemoRecorder : MonoBehaviour
{
    public static bool Complete { get; private set; }
    public static int Frames { get; private set; }
    public static string Progress { get; private set; } = "idle";

    public string outputDirectory = "Builds/Videos/DemoFrames";
    public int frameRate = 30;
    [Tooltip("Re-record a single shot (0-based) instead of the whole reel; frames overwrite from startFrameIndex.")]
    public int onlyShot = -1;
    public int startFrameIndex;

    struct Shot
    {
        public string zone;      // zone id to start in
        public string toward;    // zone id to face; null = face the sun
        public float seconds;
        public bool ride;        // true: gallop forward; false: stand and sweep the camera
        public float pitch;
        public Shot(string zone, string toward, float seconds, bool ride, float pitch)
        { this.zone = zone; this.toward = toward; this.seconds = seconds; this.ride = ride; this.pitch = pitch; }
    }

    IEnumerator Start()
    {
        Complete = false; Frames = onlyShot >= 0 ? startFrameIndex : 0; Progress = "starting";
        var actor = FindAnyObjectByType<ThanhGiongCampaignController>();
        var movement = actor != null ? actor.GetComponent<MountedHorseController>() : null;
        var cam = IsometricCameraFollow.Instance;
        if (actor == null || actor.openWorldCampaign == null || movement == null || cam == null) { Progress = "missing references"; yield break; }
        var world = actor.openWorldCampaign.world;

        string dir = Path.Combine(Application.dataPath, "..", outputDirectory);
        if (onlyShot < 0 && Directory.Exists(dir)) foreach (var old in Directory.GetFiles(dir, "frame_*.png")) File.Delete(old);
        Directory.CreateDirectory(dir);

        cam.mouseLook = false;                         // never grab the cursor while recording
        actor.openWorldCampaign.persistenceEnabled = false;
        movement.enabled = false;                      // DriveMovement below replaces its Update
        Time.captureFramerate = frameRate;

        var shots = new List<Shot>
        {
            new Shot("hub", "blue_forest", 7f, true, 14f),
            new Shot("hub", null, 5f, false, -20f),
            new Shot("blue_swamp", "yellow_steppe", 5f, true, 16f),
            new Shot("blue_highland", "yellow_mountain", 5f, true, 18f),
            new Shot("yellow_mountain", "blue_mountain", 5f, true, 14f),
            new Shot("red_fort", "red_river", 5f, true, 16f),
            new Shot("black_battle", "soc_peak", 5f, true, 14f),
            new Shot("soc_peak", null, 5f, false, 6f),
        };

        int shotIndex = 0;
        foreach (var shot in shots)
        {
            if (onlyShot >= 0 && shots.IndexOf(shot) != onlyShot) continue;
            Progress = "shot " + (++shotIndex) + "/" + shots.Count + " " + shot.zone;
            Vector3 start = world.zones.Find(z => z.id == shot.zone).center;
            float yaw = shot.toward != null ? YawTowards(start, world.zones.Find(z => z.id == shot.toward).center) : SunYaw();
            float startYaw = shot.toward != null ? yaw : yaw - 70f;

            actor.TeleportPlayer(start + Vector3.up * .4f);
            Physics.SyncTransforms();
            world.RefreshCurrentZone();
            cam.SnapToTarget();
            cam.SetOrbit(startYaw, shot.pitch);
            for (int i = 0; i < 6; i++) { movement.DriveMovement(Vector2.zero, false); yield return null; }   // settle

            int frames = Mathf.RoundToInt(shot.seconds * frameRate);
            for (int f = 0; f < frames; f++)
            {
                float t = f / (float)Mathf.Max(1, frames - 1);
                // Camera: gentle sweep (+/- 12 deg) while riding, a wide pan towards the sun while standing.
                float camYaw = shot.ride ? yaw + Mathf.Sin(t * Mathf.PI) * 12f : Mathf.Lerp(startYaw, yaw, Mathf.SmoothStep(0f, 1f, t));
                cam.SetOrbit(camYaw, shot.pitch);
                movement.DriveMovement(shot.ride ? Vector2.up : Vector2.zero, shot.ride);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "frame_" + Frames.ToString("D5") + ".png"));
                Frames++;
                yield return null;
            }
        }

        Time.captureFramerate = 0;
        movement.enabled = true;
        cam.mouseLook = true;
        Progress = "complete";
        Complete = true;
        Debug.Log("DEMO_RECORDER complete frames=" + Frames + " dir=" + dir);
    }

    static float YawTowards(Vector3 from, Vector3 to)
    {
        Vector3 d = Vector3.ProjectOnPlane(to - from, Vector3.up);
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    static float SunYaw()
    {
        var sun = RenderSettings.sun;
        if (sun == null) return 0f;
        Vector3 toSun = -sun.transform.forward;
        return Mathf.Atan2(toSun.x, toSun.z) * Mathf.Rad2Deg;
    }
}
