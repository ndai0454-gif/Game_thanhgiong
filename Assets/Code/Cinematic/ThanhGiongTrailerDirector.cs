using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cinematic Trailer & Gameplay Demo Director for "Thánh Gióng".
/// Orchestrates the 4-act narrative showcase using Unity's camera, VFX, audio, and capture pipeline.
/// Can be triggered in-game via [F10], Pause Menu, or launched via command-line arguments.
/// </summary>
[DefaultExecutionOrder(150)]
public class ThanhGiongTrailerDirector : MonoBehaviour
{
    public static ThanhGiongTrailerDirector Instance { get; private set; }

    [Header("Trailer Configuration")]
    [Tooltip("Target duration for the entire cinematic trailer in seconds.")]
    public float trailerDuration = 55f;
    [Tooltip("Frame rate to lock time during recording.")]
    public int captureFrameRate = 30;
    [Tooltip("Relative output directory for rendered trailer frames.")]
    public string frameOutputDirectory = "Builds/Videos/Frames";

    public bool IsPlayingTrailer { get; private set; }
    public bool IsRecording { get; private set; }
    public float TrailerProgress01 => trailerDuration > 0f ? Mathf.Clamp01(trailerTimer / trailerDuration) : 0f;
    public string CurrentActTitle { get; private set; } = string.Empty;
    public string CurrentActSubtitle { get; private set; } = string.Empty;
    public string CurrentMechanicTag { get; private set; } = string.Empty;

    private float trailerTimer;
    private int recordedFrameIndex;
    private Coroutine trailerCoroutine;
    private ThanhGiongCampaignController campaign;
    private MountedHorseController horse;
    private ThanhGiongCampaignVFX vfx;
    private ThanhGiongCampaignAudio audioFx;
    private IsometricCameraFollow cameraFollow;
    private float savedTimeScale = 1f;

    // Cinematic styling
    private static readonly Color Gold = new Color(0.92f, 0.78f, 0.38f);
    private static readonly Color SubtitleColor = new Color(0.96f, 0.94f, 0.88f);
    private static readonly Color TagColor = new Color(0.35f, 0.92f, 0.55f);
    private static readonly Color LetterboxColor = new Color(0.02f, 0.03f, 0.04f, 0.96f);

    private GUIStyle actTitleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle tagStyle;
    private GUIStyle bannerStyle;
    private Texture2D whitePixel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        if (Instance == null)
        {
            GameObject directorHost = new GameObject("ThanhGiong_TrailerDirector");
            directorHost.AddComponent<ThanhGiongTrailerDirector>();
            DontDestroyOnLoad(directorHost);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        whitePixel = new Texture2D(1, 1);
        whitePixel.SetPixel(0, 0, Color.white);
        whitePixel.Apply();

        CheckCommandLineArgs();
    }

    private void CheckCommandLineArgs()
    {
        string[] args = Environment.GetCommandLineArgs();
        foreach (string arg in args)
        {
            if (arg.Equals("--play-trailer", StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(DeferredStartTrailer(false));
                break;
            }
            if (arg.Equals("--record-trailer", StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(DeferredStartTrailer(true));
                break;
            }
        }
    }

    private IEnumerator DeferredStartTrailer(bool record)
    {
        yield return new WaitForSeconds(1.0f);
        PlayTrailer(record);
    }

    private void Update()
    {
        // Toggle trailer with F10
        if (Input.GetKeyDown(KeyCode.F10))
        {
            if (IsPlayingTrailer)
            {
                StopTrailer();
            }
            else
            {
                PlayTrailer(false);
            }
        }

        if (IsPlayingTrailer)
        {
            trailerTimer += Time.deltaTime;

            if (IsRecording)
            {
                CaptureFrame();
            }
        }
    }

    private void FindSceneReferences()
    {
        campaign = FindAnyObjectByType<ThanhGiongCampaignController>();
        if (campaign != null)
        {
            horse = campaign.GetComponent<MountedHorseController>();
            vfx = campaign.vfx ?? campaign.GetComponent<ThanhGiongCampaignVFX>();
            audioFx = campaign.GetComponent<ThanhGiongCampaignAudio>();
        }
        cameraFollow = IsometricCameraFollow.Instance;
    }

    public void PlayTrailer(bool record = false)
    {
        if (IsPlayingTrailer) return;

        FindSceneReferences();
        IsPlayingTrailer = true;
        IsRecording = record;
        trailerTimer = 0f;
        recordedFrameIndex = 0;

        if (IsRecording)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", frameOutputDirectory);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
            Time.captureFramerate = captureFrameRate;
        }

        savedTimeScale = Time.timeScale;
        Time.timeScale = 1f;

        if (trailerCoroutine != null) StopCoroutine(trailerCoroutine);
        trailerCoroutine = StartCoroutine(TrailerSequenceRoutine());
    }

    public void StopTrailer()
    {
        if (!IsPlayingTrailer) return;

        if (trailerCoroutine != null)
        {
            StopCoroutine(trailerCoroutine);
            trailerCoroutine = null;
        }

        if (IsRecording)
        {
            Time.captureFramerate = 0;
            Debug.Log($"[ThanhGiongTrailerDirector] Hoàn tất ghi hình trailer: {recordedFrameIndex} frames lưu tại {frameOutputDirectory}");
        }

        IsPlayingTrailer = false;
        IsRecording = false;
        Time.timeScale = savedTimeScale;
        CurrentActTitle = string.Empty;
        CurrentActSubtitle = string.Empty;
        CurrentMechanicTag = string.Empty;

        if (cameraFollow != null)
        {
            cameraFollow.SetShot(IsometricCameraFollow.Shot.Explore);
        }
    }

    private void CaptureFrame()
    {
        string fullDir = Path.Combine(Application.dataPath, "..", frameOutputDirectory);
        string filename = Path.Combine(fullDir, $"frame_{recordedFrameIndex:D5}.png");
        ScreenCapture.CaptureScreenshot(filename);
        recordedFrameIndex++;
    }

    private IEnumerator TrailerSequenceRoutine()
    {
        FindSceneReferences();

        // ==========================================
        // KHÚC DẠO ĐẦU: LOGO & GIỚI THIỆU HUYỀN THOẠI (0s - 7s)
        // ==========================================
        CurrentActTitle = "THÁNH GIÓNG: BẢN HÙNG CA ĐẤT VIỆT";
        CurrentActSubtitle = "Huyền thoại tráng sĩ làng Phù Đổng dẹp giặc Ân cứu nước";
        CurrentMechanicTag = "UNITY 6 URP · 3D DÂN GIAN ACTION-ADVENTURE";
        if (cameraFollow != null) cameraFollow.SetShot(IsometricCameraFollow.Shot.Vista);

        yield return new WaitForSeconds(7.0f);

        // ==========================================
        // HỒI I: TIẾNG RAO DƯỚI MÁI TRANH (7s - 18s)
        // ==========================================
        CurrentActTitle = "HỒI I · TIẾNG RAO DƯỚI MÁI TRANH";
        CurrentActSubtitle = "Giặc Ân áp sát biên cương. Gióng gom lương thực, vươn vai hóa đại tráng sĩ.";
        CurrentMechanicTag = "CƠ CHẾ: TĂNG TRƯỞNG KÍCH THƯỚC THỰC TẾ (DYNAMIC SCALE)";
        if (cameraFollow != null) cameraFollow.SetShot(IsometricCameraFollow.Shot.Close);

        if (campaign != null)
        {
            // Trình diễn hiệu ứng ăn lương thực và vươn vai
            for (int i = 0; i < 3; i++)
            {
                if (vfx != null) vfx.PlayFoodBurst(campaign.transform.position, Color.yellow);
                yield return new WaitForSeconds(1.2f);
            }

            if (vfx != null) vfx.PlayGrowth(campaign.transform);
        }

        yield return new WaitForSeconds(5.0f);

        // ==========================================
        // HỒI II: RÈN THÉP & XUẤT QUÂN (18s - 29s)
        // ==========================================
        CurrentActTitle = "HỒI II · RÈN THÉP & XUẤT QUÂN";
        CurrentActSubtitle = "Sứ giả mang Giáp Sắt, Gươm Sắt & Thần Mã. Lửa thiêng bùng cháy mở cổng thành.";
        CurrentMechanicTag = "CƠ CHẾ: TRANG BỊ VUA BAN & THẦN MÃ PHUN LỬA";
        if (cameraFollow != null) cameraFollow.SetShot(IsometricCameraFollow.Shot.Combat);

        if (campaign != null && vfx != null)
        {
            // Hiệu ứng tiếng vó ngựa và phun ngọn lửa mở màn
            vfx.PlayFireLine(campaign.transform, 18f);
        }

        yield return new WaitForSeconds(8.0f);

        // ==========================================
        // HỒI III: TRẬN TUYẾN NÚI SÓC (29s - 45s)
        // ==========================================
        CurrentActTitle = "HỒI III · TRẬN TUYẾN NÚI SÓC";
        CurrentActSubtitle = "Chiến trường rực lửa. Gươm gãy trước tướng giặc -> Nhổ tre ngà quạt bão tố 360°!";
        CurrentMechanicTag = "CƠ CHẾ: DYNASTY WARRIORS COMBAT & VŨ KHÍ ỨNG BIẾN (TRE NGÀ)";
        if (cameraFollow != null) cameraFollow.SetShot(IsometricCameraFollow.Shot.Boss);

        if (campaign != null && vfx != null)
        {
            // Pha 1: Chém gươm nét thư pháp
            vfx.PlayCalligraphySlash(campaign.transform, 9f);
            yield return new WaitForSeconds(2.0f);

            // Pha 2: Gươm gãy kịch tính
            vfx.PlaySwordBreak(campaign.transform);
            yield return new WaitForSeconds(2.5f);

            // Pha 3: Quét tre ngà 360 độ
            vfx.PlayBambooSweep(campaign.transform, 12f);
            yield return new WaitForSeconds(2.5f);

            // Pha 4: Thần mã xả Hỏa Tuyến cực đại
            vfx.PlayFireLine(campaign.transform, 24f);
        }

        yield return new WaitForSeconds(6.0f);

        // ==========================================
        // HỒI IV: HÓA THÁNH VỀ TRỜI (45s - 55s)
        // ==========================================
        CurrentActTitle = "HỒI IV · HÓA THÁNH VỀ TRỜI";
        CurrentActSubtitle = "Để lại giáp sắt trên đỉnh Sóc Sơn. Cưỡi thần mã bay vút về trời, khai sinh Làng Cháy & Ao Hồ.";
        CurrentMechanicTag = "DI TÍCH LỊCH SỬ BẤT TỬ · PHI THĂNG THẦN THOẠI";
        if (cameraFollow != null) cameraFollow.SetShot(IsometricCameraFollow.Shot.Vista);

        if (campaign != null && vfx != null)
        {
            vfx.SpawnLegendaryRemains(campaign.transform.position + Vector3.up * 18f, campaign.transform.position);
        }

        yield return new WaitForSeconds(8.0f);

        // Outro
        CurrentActTitle = "THÁNH GIÓNG · BẢN HÙNG CA HOÀN TẤT";
        CurrentActSubtitle = "Nhấn F10 để xem lại Trailer Demo | Sẵn sàng xuất trận!";
        CurrentMechanicTag = "BẢN PHÁT HÀNH WINDOWS STANDALONE X64";

        yield return new WaitForSeconds(3.0f);

        StopTrailer();
    }

    private void EnsureStyles()
    {
        if (actTitleStyle == null)
        {
            actTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.height / 28, 22, 38),
                fontStyle = FontStyle.Bold
            };
            actTitleStyle.normal.textColor = Gold;
        }

        if (subtitleStyle == null)
        {
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.height / 46, 14, 22),
                fontStyle = FontStyle.Normal
            };
            subtitleStyle.normal.textColor = SubtitleColor;
        }

        if (tagStyle == null)
        {
            tagStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.height / 52, 12, 18),
                fontStyle = FontStyle.Bold
            };
            tagStyle.normal.textColor = TagColor;
        }

        if (bannerStyle == null)
        {
            bannerStyle = new GUIStyle(GUI.skin.box);
        }
    }

    private void OnGUI()
    {
        if (!IsPlayingTrailer) return;

        EnsureStyles();

        float screenW = Screen.width;
        float screenH = Screen.height;
        float barH = screenH * 0.12f;

        // Cinematic 2.39:1 Letterbox Bars (Top & Bottom)
        DrawRect(new Rect(0, 0, screenW, barH), LetterboxColor);
        DrawRect(new Rect(0, screenH - barH, screenW, barH), LetterboxColor);

        // Gold decorative accent lines
        DrawRect(new Rect(0, barH - 2, screenW, 2), Gold * 0.6f);
        DrawRect(new Rect(0, screenH - barH, screenW, 2), Gold * 0.6f);

        // Top bar content: Act title and Game branding
        GUI.Label(new Rect(20, 10, screenW - 40, barH * 0.5f), "GAME THÁNH GIÓNG 3D  ·  OFFICIAL GAMEPLAY TRAILER", tagStyle);
        GUI.Label(new Rect(20, barH * 0.45f, screenW - 40, barH * 0.5f), CurrentActTitle, actTitleStyle);

        // Bottom bar content: Subtitle context and mechanic callout
        GUI.Label(new Rect(40, screenH - barH + 12, screenW - 80, barH * 0.45f), CurrentActSubtitle, subtitleStyle);
        GUI.Label(new Rect(40, screenH - barH + barH * 0.52f, screenW - 80, barH * 0.4f), CurrentMechanicTag, tagStyle);

        // Progress bar at the bottom edge
        float progW = screenW * TrailerProgress01;
        DrawRect(new Rect(0, screenH - 4, progW, 4), Gold);

        // Trailer hint indicator
        GUI.Label(new Rect(screenW - 220, 12, 200, 24), "[F10: Thoát Trailer]", subtitleStyle);
    }

    private void DrawRect(Rect rect, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, whitePixel);
        GUI.color = prev;
    }
}
