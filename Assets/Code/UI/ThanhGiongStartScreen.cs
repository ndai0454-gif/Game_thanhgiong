using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Màn hình chờ & Menu chính của "Thánh Gióng: Hành Trình Từ Phù Đổng Đến Núi Sóc".
/// Hiển thị trước khi vào game và bắt đầu chơi, mang phong cách huyền sử Đông Sơn rực rỡ.
/// Cho phép người chơi chuẩn bị, đọc cốt truyện/binh pháp, chỉnh âm thanh, chọn màn chơi hoặc vào game ngay.
/// </summary>
[DefaultExecutionOrder(-500)]
public class ThanhGiongStartScreen : MonoBehaviour
{
    public static ThanhGiongStartScreen Instance { get; private set; }

    public enum Tab
    {
        Main,
        ChapterSelect,
        Controls,
        Settings,
        Story
    }

    [Header("Cấu hình màn hình chờ")]
    [Tooltip("Tự động hiển thị màn hình chờ khi vào màn chơi đầu tiên")]
    public bool showOnStart = true;
    [Tooltip("Thời gian mờ dần khi chuyển từ màn hình chờ vào trận")]
    public float fadeDuration = 0.65f;

    public static bool IsWaitingActive { get; private set; } = false;

    private Tab currentTab = Tab.Main;
    private float veilAlpha = 1f;
    private bool isTransitioningToGame = false;
    private AudioSource menuAudioSource;
    private AudioSource sfxAudioSource;
    private AudioClip menuMusic;
    private AudioClip startSfx;
    private AudioClip clickSfx;
    private Texture2D bgTexture;
    private Texture2D drumMedallion;
    private Texture2D ribbonTexture;
    private Texture2D whitePixel;
    private Font fontBold;
    private Font fontRegular;

    // Styles
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle headerStyle;
    private GUIStyle bodyStyle;
    private GUIStyle noteStyle;
    private GUIStyle mainButtonStyle;
    private GUIStyle secondaryButtonStyle;
    private GUIStyle backButtonStyle;
    private GUIStyle panelBoxStyle;

    // Particles (Linh khí vàng kim bay lượn)
    private struct Ember
    {
        public float x;
        public float y;
        public float speed;
        public float size;
        public float seed;
    }
    private readonly Ember[] embers = new Ember[36];

    // Chapters info
    public static readonly string[] ChapterScenes = {
        "LangGiongTienTuyen", "KinhThanhRenThep", "PhaoDaiNgamQuanAn",
        "ThungLungVuotSong", "TranTuyenNuiSoc", "DinhSocHoaThanh"
    };

    public static readonly string[] ChapterTitles = {
        "MÀN 1 · LÀNG PHÙ ĐỔNG",
        "MÀN 2 · KINH THÀNH RÈN THÉP",
        "MÀN 3 · PHÁO ĐÀI QUÂN ÂN",
        "MÀN 4 · THUNG LŨNG VƯỢT SÔNG",
        "MÀN 5 · TRẬN TUYẾN NÚI SÓC",
        "MÀN 6 · ĐỈNH SÓC HÓA THÁNH"
    };

    public static readonly string[] ChapterSubtitles = {
        "Tiếng rao sứ giả · Gom đủ lương thực vươn vai thành tráng sĩ khổng lồ",
        "Lò rèn triều đình · Rèn gươm sắt, giáp sắt và cưỡi Ngựa Sắt xuất quân",
        "Tiền tuyến hiểm trở · Đột phá căn cứ giặc Ân, quét sạch tiền đồn",
        "Bình nguyên sông dài · Hành quân thần tốc qua thác ghềnh và phòng tuyến",
        "Quyết chiến đỉnh cao · Gươm gãy nhổ bụi tre ngà càn quét Tướng giặc Ân",
        "Non sông thái bình · Cởi giáp sắt để lại đỉnh Sóc, cưỡi ngựa bay về trời"
    };

    // Color Palette Đông Sơn
    private static readonly Color DeepTeal = new Color(0.015f, 0.085f, 0.08f);
    private static readonly Color PaleGold = new Color(0.93f, 0.79f, 0.43f);
    private static readonly Color BrightGold = new Color(1f, 0.88f, 0.52f);
    private static readonly Color Cream = new Color(1f, 0.96f, 0.88f);
    private static readonly Color Crimson = new Color(0.72f, 0.16f, 0.14f);
    private static readonly Color MutedText = new Color(0.76f, 0.72f, 0.62f);
    private static readonly Color PanelBackground = new Color(0.018f, 0.11f, 0.10f, 0.88f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        if (Instance != null) return;
        string activeScene = SceneManager.GetActiveScene().name;
        // Chỉ tự động gắn trên các màn chơi chính nếu chưa có
        GameObject host = new GameObject("ThanhGiong_StartScreenHost");
        host.AddComponent<ThanhGiongStartScreen>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Khởi tạo hạt linh khí
        for (int i = 0; i < embers.Length; i++)
        {
            embers[i] = new Ember
            {
                x = Random.Range(40f, 1560f),
                y = Random.Range(60f, 840f),
                speed = Random.Range(18f, 42f),
                size = Random.Range(2.5f, 6.5f),
                seed = Random.Range(0f, 100f)
            };
        }

        // Tải tài nguyên
        bgTexture = Resources.Load<Texture2D>("ThanhGiongUI/LoadingForest");
        fontBold = Resources.Load<Font>("ThanhGiongUI/OldStandard-Bold");
        fontRegular = Resources.Load<Font>("ThanhGiongUI/OldStandard-Regular");
        menuMusic = Resources.Load<AudioClip>("ThanhGiongAudio/music_camp");
        if (menuMusic == null) menuMusic = Resources.Load<AudioClip>("ThanhGiongAudio/music_sacred");
        startSfx = Resources.Load<AudioClip>("ThanhGiongAudio/arrival");
        if (startSfx == null) startSfx = Resources.Load<AudioClip>("ThanhGiongAudio/metal_equip");
        clickSfx = Resources.Load<AudioClip>("ThanhGiongAudio/hit_metal");

        drumMedallion = CreateDongSonDrumTexture();
        ribbonTexture = CreateRibbonTexture();

        // Cài đặt âm thanh
        menuAudioSource = gameObject.AddComponent<AudioSource>();
        menuAudioSource.loop = true;
        menuAudioSource.playOnAwake = false;
        menuAudioSource.volume = 0.75f * LegendAudioMix.Music * LegendAudioMix.Master;

        sfxAudioSource = gameObject.AddComponent<AudioSource>();
        sfxAudioSource.loop = false;
        sfxAudioSource.playOnAwake = false;

        // Bật màn hình chờ ban đầu nếu ở màn 1 hoặc màn hình sảnh
        string sceneName = SceneManager.GetActiveScene().name;
        if (showOnStart && (sceneName == "LangGiongTienTuyen" || sceneName == "ManHinhCho" || sceneName.Contains("Start")))
        {
            OpenWaitingScreen();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "ManHinhCho")
        {
            OpenWaitingScreen();
        }
        else
        {
            CloseWaitingScreenImmediate();
        }
    }

    public static void CloseWaitingScreenImmediate()
    {
        IsWaitingActive = false;
        if (Instance != null)
        {
            Instance.veilAlpha = 0f;
            Instance.isTransitioningToGame = false;
            if (Instance.menuAudioSource != null && Instance.menuAudioSource.isPlaying)
            {
                Instance.menuAudioSource.Stop();
            }
        }
        Time.timeScale = 1f;
        SetPlayerInput(true);
    }

    public static void OpenWaitingScreen()
    {
        if (Instance == null)
        {
            GameObject host = new GameObject("ThanhGiong_StartScreenHost");
            Instance = host.AddComponent<ThanhGiongStartScreen>();
        }

        Instance.currentTab = Tab.Main;
        Instance.veilAlpha = 1f;
        Instance.isTransitioningToGame = false;
        IsWaitingActive = true;
        Time.timeScale = 0f;
        SetPlayerInput(false);

        if (Instance.menuMusic != null && Instance.menuAudioSource != null && !Instance.menuAudioSource.isPlaying)
        {
            Instance.menuAudioSource.clip = Instance.menuMusic;
            Instance.menuAudioSource.volume = 0.75f * LegendAudioMix.Music * LegendAudioMix.Master;
            Instance.menuAudioSource.Play();
        }
    }

    public void BeginGame()
    {
        if (isTransitioningToGame) return;

        PlaySfx(startSfx);
        string currentScene = SceneManager.GetActiveScene().name;

        // Nếu đang ở màn sảnh riêng "ManHinhCho", nạp màn chơi
        if (currentScene == "ManHinhCho")
        {
            isTransitioningToGame = true;
            IsWaitingActive = false;
            veilAlpha = 0f;
            Time.timeScale = 1f;
            if (menuAudioSource != null) menuAudioSource.Stop();

            string targetScene = "LangGiongTienTuyen";
            if (!Application.CanStreamedLevelBeLoaded(targetScene))
            {
                if (Application.CanStreamedLevelBeLoaded("ThanhGiongDaiViet"))
                    targetScene = "ThanhGiongDaiViet";
            }

            Debug.Log($"[ThanhGiongStartScreen] Bắt đầu vào game -> Đang chuyển sang scene: {targetScene}");

            if (!ThanhGiongSceneTransition.TryTransitionTo(targetScene, "ĐANG TIẾN VÀO LÀNG PHÙ ĐỔNG..."))
            {
                SceneManager.LoadScene(targetScene);
            }
            return;
        }

        // Nếu đã ở trong màn chơi, mở màn hình chờ mờ dần vào game
        StartCoroutine(TransitionIntoGameplay());
    }

    private IEnumerator TransitionIntoGameplay()
    {
        isTransitioningToGame = true;

        // Giảm dần âm nhạc menu
        float startVol = menuAudioSource != null ? menuAudioSource.volume : 0f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            veilAlpha = 1f - t;
            if (menuAudioSource != null) menuAudioSource.volume = Mathf.Lerp(startVol, 0f, t);
            yield return null;
        }

        veilAlpha = 0f;
        if (menuAudioSource != null) menuAudioSource.Stop();

        IsWaitingActive = false;
        isTransitioningToGame = false;
        Time.timeScale = 1f;
        SetPlayerInput(true);

        // Nhắc lại nhiệm vụ cho người chơi
        ThanhGiongCampaignHUD hud = FindAnyObjectByType<ThanhGiongCampaignHUD>();
        if (hud != null) hud.RecallPopups();
    }

    public void SelectAndLoadChapter(int index)
    {
        if (index < 0 || index >= ChapterScenes.Length) return;

        PlaySfx(clickSfx);
        isTransitioningToGame = true;
        IsWaitingActive = false;
        veilAlpha = 0f;
        Time.timeScale = 1f;
        if (menuAudioSource != null) menuAudioSource.Stop();

        string scene = ChapterScenes[index];
        Debug.Log($"[ThanhGiongStartScreen] Chọn chương -> Đang chuyển sang scene: {scene}");

        if (!ThanhGiongSceneTransition.TryTransitionTo(scene, "ĐANG ĐẾN " + ChapterTitles[index]))
        {
            SceneManager.LoadScene(scene);
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(clip, 0.9f * LegendAudioMix.Effects * LegendAudioMix.Master);
        }
    }

    private static void SetPlayerInput(bool enabled)
    {
        foreach (MountedHorseController controller in FindObjectsByType<MountedHorseController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            controller.enabled = enabled;
        foreach (AdventurerController controller in FindObjectsByType<AdventurerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            controller.enabled = enabled;
    }

    private void Update()
    {
        if (!IsWaitingActive) return;

        // Phím tắt bắt đầu nhanh
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (currentTab == Tab.Main) BeginGame();
            else currentTab = Tab.Main;
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentTab != Tab.Main)
            {
                currentTab = Tab.Main;
                PlaySfx(clickSfx);
            }
            else
            {
                // Nếu đang trong màn chơi và mở lại màn hình chờ, Escape sẽ tiếp tục chơi
                string currentScene = SceneManager.GetActiveScene().name;
                if (currentScene != "ManHinhCho") BeginGame();
            }
        }
        else if (Input.GetKeyDown(KeyCode.F1))
        {
            currentTab = currentTab == Tab.Controls ? Tab.Main : Tab.Controls;
            PlaySfx(clickSfx);
        }
        else if (Input.GetKeyDown(KeyCode.F10))
        {
            // Bật Trailer demo
            IsWaitingActive = false;
            Time.timeScale = 1f;
            if (menuAudioSource != null) menuAudioSource.Stop();
            ThanhGiongTrailerDirector.Instance?.PlayTrailer(false);
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            font = fontBold,
            fontSize = 68,
            fontStyle = fontBold == null ? FontStyle.Bold : FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            font = fontBold,
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter
        };

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            font = fontBold,
            fontSize = 26,
            fontStyle = fontBold == null ? FontStyle.Bold : FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            font = fontRegular,
            fontSize = 17,
            wordWrap = true
        };

        noteStyle = new GUIStyle(GUI.skin.label)
        {
            font = fontRegular,
            fontSize = 14,
            wordWrap = true
        };

        mainButtonStyle = new GUIStyle(GUI.skin.button)
        {
            font = fontBold,
            fontSize = 21,
            alignment = TextAnchor.MiddleCenter
        };
        mainButtonStyle.normal.textColor = Cream;
        mainButtonStyle.hover.textColor = BrightGold;
        mainButtonStyle.active.textColor = BrightGold;

        secondaryButtonStyle = new GUIStyle(GUI.skin.button)
        {
            font = fontBold,
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        secondaryButtonStyle.normal.textColor = PaleGold;
        secondaryButtonStyle.hover.textColor = Cream;
        secondaryButtonStyle.active.textColor = Cream;

        backButtonStyle = new GUIStyle(GUI.skin.button)
        {
            font = fontBold,
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        backButtonStyle.normal.textColor = PaleGold;
        backButtonStyle.hover.textColor = Cream;

        panelBoxStyle = new GUIStyle(GUI.skin.box);
    }

    private void OnGUI()
    {
        if (!IsWaitingActive && veilAlpha <= 0.001f) return;

        EnsureStyles();
        Color oldColor = GUI.color;
        Matrix4x4 oldMatrix = GUI.matrix;
        int oldDepth = GUI.depth;
        GUI.depth = -200;

        // Tọa độ canvas chuẩn 1600x900 giữ tỉ lệ và căn giữa màn hình
        Rect safe = Screen.safeArea;
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
        float scale = Mathf.Min(safe.width / 1600f, safe.height / 900f);
        float canvasX = (safe.width - 1600f * scale) * 0.5f + safe.x;
        float canvasY = (safe.height - 900f * scale) * 0.5f + (Screen.height - safe.yMax);
        GUI.matrix = Matrix4x4.TRS(new Vector3(canvasX, canvasY, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

        // 1. NỀN CHÍNH
        DrawBackground();

        // 2. CÁC HẠT LINH KHÍ VÀNG KIM
        DrawDivineEmbers();

        // 3. TIÊU ĐỀ SỬ THI
        DrawHeader();

        // 4. NỘI DUNG TỪNG TAB
        switch (currentTab)
        {
            case Tab.Main:
                DrawMainTab();
                break;
            case Tab.ChapterSelect:
                DrawChapterSelectTab();
                break;
            case Tab.Controls:
                DrawControlsTab();
                break;
            case Tab.Settings:
                DrawSettingsTab();
                break;
            case Tab.Story:
                DrawStoryTab();
                break;
        }

        // 5. THANH THÔNG TIN DƯỚI ĐÁY
        DrawFooter();

        GUI.color = oldColor;
        GUI.matrix = oldMatrix;
        GUI.depth = oldDepth;
    }

    private void DrawBackground()
    {
        Rect full = new Rect(0, 0, 1600, 900);

        if (bgTexture != null)
        {
            GUI.color = new Color(0.35f, 0.45f, 0.45f, veilAlpha);
            GUI.DrawTexture(full, bgTexture, ScaleMode.ScaleAndCrop, true);
        }
        else
        {
            DrawSolidColor(full, DeepTeal, veilAlpha);
        }

        // Lớp phủ màu ngọc thẫm tăng độ tương phản và chiều sâu
        DrawSolidColor(full, new Color(0.012f, 0.06f, 0.055f), veilAlpha * 0.76f);

        // Trống Đồng Đông Sơn mờ ảo quay chậm ở tâm màn hình
        if (drumMedallion != null)
        {
            float drumSize = 780f;
            Rect drumRect = new Rect(800f - drumSize * 0.5f, 470f - drumSize * 0.5f, drumSize, drumSize);
            GUI.color = new Color(PaleGold.r, PaleGold.g, PaleGold.b, veilAlpha * 0.16f);
            GUI.DrawTexture(drumRect, drumMedallion, ScaleMode.ScaleToFit, true);
        }

        // Dải ruy băng ánh sáng uốn lượn
        if (ribbonTexture != null)
        {
            GUI.color = new Color(1f, 0.95f, 0.7f, veilAlpha * 0.38f);
            GUI.DrawTexture(new Rect(100f, 150f, 1400f, 180f), ribbonTexture);
        }

        // Khung viền kép hoàng gia quanh màn hình
        DrawOrnateFrame(new Rect(30, 20, 1540, 860), PaleGold, veilAlpha * 0.65f);
    }

    private void DrawDivineEmbers()
    {
        GUI.color = new Color(PaleGold.r, PaleGold.g, PaleGold.b, veilAlpha * 0.55f);
        float time = Time.unscaledTime;

        for (int i = 0; i < embers.Length; i++)
        {
            float y = Mathf.Repeat(embers[i].y - time * embers[i].speed, 880f);
            float x = embers[i].x + Mathf.Sin(time * 1.5f + embers[i].seed) * 26f;
            float pulse = 0.4f + 0.6f * Mathf.PingPong(time * 1.2f + embers[i].seed, 1f);
            float s = embers[i].size * pulse;

            Color emberColor = (i % 3 == 0)
                ? new Color(BrightGold.r, BrightGold.g, BrightGold.b, veilAlpha * pulse * 0.7f)
                : new Color(Cream.r, Cream.g, Cream.b, veilAlpha * pulse * 0.45f);

            DrawSolidColor(new Rect(x, y, s, s), emberColor, emberColor.a);
        }
    }

    private void DrawHeader()
    {
        // Nhãn triều đại
        GUI.color = new Color(PaleGold.r, PaleGold.g, PaleGold.b, veilAlpha * 0.85f);
        GUI.Label(new Rect(200, 36, 1200, 26), "★ TRIỀU ĐẠI HÙNG VƯƠNG THỨ SÁU · BẢO VỆ XÃ TẮC VĂN LANG ★", subtitleStyle);

        // Tiêu đề THÁNH GIÓNG lớn
        float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 2.8f);

        // Hiệu ứng đổ bóng vàng rực
        GUI.color = new Color(0.05f, 0.02f, 0f, veilAlpha * 0.9f);
        GUI.Label(new Rect(202, 64, 1200, 84), "THÁNH GIÓNG", titleStyle);
        GUI.color = new Color(PaleGold.r, PaleGold.g, PaleGold.b, veilAlpha * pulse);
        GUI.Label(new Rect(200, 62, 1200, 84), "THÁNH GIÓNG", titleStyle);

        // Phụ đề sử thi
        GUI.color = new Color(Cream.r, Cream.g, Cream.b, veilAlpha * 0.92f);
        GUI.Label(new Rect(200, 142, 1200, 32), "HÀNH TRÌNH TỪ PHÙ ĐỔNG ĐẾN NÚI SÓC · HUYỀN THOẠI BẤT TỬ", subtitleStyle);

        // Đường kẻ phân cách vàng kim
        DrawSolidColor(new Rect(450, 178, 700, 2), PaleGold, veilAlpha * 0.75f);
        DrawSolidColor(new Rect(785, 174, 30, 10), PaleGold, veilAlpha * 0.9f);
    }

    private void DrawMainTab()
    {
        // 1. BẢNG TRÁI: CỐT TRUYỆN & LỜI DẪN NHẬP (X: 120, Y: 210, W: 760, H: 540)
        Rect storyBox = new Rect(110, 204, 750, 546);
        DrawPanelBox(storyBox, PanelBackground, PaleGold, veilAlpha);

        GUI.color = new Color(BrightGold.r, BrightGold.g, BrightGold.b, veilAlpha);
        GUI.Label(new Rect(storyBox.x + 36, storyBox.y + 26, 680, 36), "🏛️ HUYỀN TÍCH PHÙ ĐỔNG THIÊN VƯƠNG", headerStyle);

        DrawSolidColor(new Rect(storyBox.x + 36, storyBox.y + 64, 678, 1), PaleGold, veilAlpha * 0.4f);

        string lore =
            "Thuở xưa, giặc Ân sang xâm lấn nước ta, thế trận vô cùng nguy cấp. Vua Hùng sai sứ giả đi khắp bốn phương tìm bậc hiền tài cứu nước.\n\n" +
            "Tại làng Phù Đổng, có cậu bé lên ba chưa từng biết nói biết cười, nghe tiếng loa truyền bỗng ngồi dậy bảo mẹ: 'Mẹ mời sứ giả vào đây!'. Gióng yêu cầu nhà vua đúc một con ngựa sắt, một bộ giáp sắt, một chiếc nón sắt và một cây thương sắt.\n\n" +
            "Cả làng gom gạo nuôi Gióng. Ăn hết bảy nong cơm ba nong cà, Gióng vươn vai thành một tráng sĩ khổng lồ, oai phong lẫm liệt, nhảy lên ngựa sắt phi thẳng ra tiền tuyến đánh đuổi giặc ngoại xâm bảo vệ non sông.";

        GUI.color = new Color(Cream.r, Cream.g, Cream.b, veilAlpha * 0.95f);
        GUI.Label(new Rect(storyBox.x + 36, storyBox.y + 78, 678, 230), lore, bodyStyle);

        // Hộp mẹo chiến thuật nổi bật (Battle Tip)
        Rect tipBox = new Rect(storyBox.x + 30, storyBox.y + 322, 690, 194);
        DrawPanelBox(tipBox, new Color(0.04f, 0.16f, 0.14f, 0.9f), PaleGold, veilAlpha * 0.8f);

        GUI.color = new Color(BrightGold.r, BrightGold.g, BrightGold.b, veilAlpha);
        GUI.Label(new Rect(tipBox.x + 20, tipBox.y + 16, 650, 28), "🎋 BINH PHÁP CHIẾN TRẬN ĐẶC BIỆT", headerStyle);

        string tipText =
            "• GƯƠM GÃY NHỔ TRE: Khi thương sắt gãy ở Màn 3, chạy đến khóm tre ngà phát sáng, nhấn [E] để nhổ tre quét sạch vòng vây!\n" +
            "• HỎA KHÍ NGỰA SẮT: Đánh hạ kẻ địch tích đủ 100% Nhiệt, nhấn [F] để phun luồng Hỏa Tuyến quét sạch đội hình giặc.\n" +
            "• NGŨ LINH HẠT: Nhấn [1–5] chọn hạt thần (Trói, Gai, Hồi máu, Thiêu đốt, Đẩy lùi) và nhấn [Q] để gieo trận pháp cứu nguy.";

        GUI.color = new Color(Cream.r, Cream.g, Cream.b, veilAlpha * 0.9f);
        GUI.Label(new Rect(tipBox.x + 20, tipBox.y + 54, 650, 126), tipText, noteStyle);

        // 2. BẢNG PHẢI: CÁC NÚT ĐIỀU HƯỚNG VÀ VÀO GAME (X: 890, Y: 204, W: 600, H: 546)
        Rect menuBox = new Rect(890, 204, 600, 546);
        DrawPanelBox(menuBox, PanelBackground, PaleGold, veilAlpha);

        GUI.color = new Color(BrightGold.r, BrightGold.g, BrightGold.b, veilAlpha);
        GUI.Label(new Rect(menuBox.x + 36, menuBox.y + 26, 528, 36), "⚔️ XUẤT TRẬN CỨU NƯỚC", headerStyle);

        DrawSolidColor(new Rect(menuBox.x + 36, menuBox.y + 64, 528, 1), PaleGold, veilAlpha * 0.4f);

        // Nút BẮT ĐẦU CHƠI (Nút lớn nhất, phát sáng nhấp nháy, viền kép)
        float glowPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.5f);
        Color btnBorderColor = Color.Lerp(PaleGold, BrightGold, glowPulse);

        Rect playBtnRect = new Rect(menuBox.x + 36, menuBox.y + 86, 528, 76);
        DrawPanelBox(playBtnRect, new Color(Crimson.r, Crimson.g, Crimson.b, 0.88f), btnBorderColor, veilAlpha);

        GUI.color = new Color(1f, 1f, 1f, veilAlpha);
        if (GUI.Button(playBtnRect, "⚔️  BẮT ĐẦU CHƠI NGAY  ⚔️\n[ Nhấn SPACE hoặc ENTER ]", mainButtonStyle))
        {
            BeginGame();
        }

        // Nút 2: CHỌN MÀN CHƠI (6 MÀN)
        Rect chBtnRect = new Rect(menuBox.x + 36, menuBox.y + 178, 528, 56);
        if (GUI.Button(chBtnRect, "🗺️  CHỌN MÀN CHƠI  (6 MÀN CHIẾN DỊCH)", secondaryButtonStyle))
        {
            PlaySfx(clickSfx);
            currentTab = Tab.ChapterSelect;
        }

        // Nút 3: BINH PHÁP & ĐIỀU KHIỂN
        Rect ctrlBtnRect = new Rect(menuBox.x + 36, menuBox.y + 248, 528, 56);
        if (GUI.Button(ctrlBtnRect, "📜  BINH PHÁP & ĐIỀU KHIỂN  [F1]", secondaryButtonStyle))
        {
            PlaySfx(clickSfx);
            currentTab = Tab.Controls;
        }

        // Nút 4: CÀI ĐẶT ÂM THANH
        Rect setBtnRect = new Rect(menuBox.x + 36, menuBox.y + 318, 528, 56);
        if (GUI.Button(setBtnRect, "🔊  CÀI ĐẶT ÂM THANH & HIỆU ỨNG", secondaryButtonStyle))
        {
            PlaySfx(clickSfx);
            currentTab = Tab.Settings;
        }

        // Nút 5: XEM TRAILER DEMO ĐIỆN ẢNH
        Rect trlBtnRect = new Rect(menuBox.x + 36, menuBox.y + 388, 528, 56);
        if (GUI.Button(trlBtnRect, "🎬  XEM TRAILER DEMO CHIẾN DỊCH  [F10]", secondaryButtonStyle))
        {
            PlaySfx(clickSfx);
            IsWaitingActive = false;
            Time.timeScale = 1f;
            if (menuAudioSource != null) menuAudioSource.Stop();
            ThanhGiongTrailerDirector.Instance?.PlayTrailer(false);
        }

        // Nút 6: THOÁT GAME
        Rect quitBtnRect = new Rect(menuBox.x + 36, menuBox.y + 458, 528, 54);
        if (GUI.Button(quitBtnRect, "🚪  THOÁT TRÒ CHƠI", secondaryButtonStyle))
        {
            PlaySfx(clickSfx);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private void DrawChapterSelectTab()
    {
        Rect box = new Rect(110, 204, 1380, 546);
        DrawPanelBox(box, PanelBackground, PaleGold, veilAlpha);

        GUI.color = new Color(BrightGold.r, BrightGold.g, BrightGold.b, veilAlpha);
        GUI.Label(new Rect(box.x + 36, box.y + 24, 1000, 36), "🗺️ CHỌN MÀN CHƠI CHIẾN DỊCH", headerStyle);

        DrawSolidColor(new Rect(box.x + 36, box.y + 60, 1308, 1), PaleGold, veilAlpha * 0.4f);

        // 6 Màn chơi bố trí dạng lưới 2 cột x 3 hàng
        for (int i = 0; i < ChapterScenes.Length; i++)
        {
            int col = i % 2;
            int row = i / 2;
            float cx = box.x + 36 + col * 664;
            float cy = box.y + 74 + row * 128;

            Rect cardRect = new Rect(cx, cy, 644, 114);
            DrawPanelBox(cardRect, new Color(0.025f, 0.14f, 0.13f, 0.85f), PaleGold, veilAlpha * 0.7f);

            GUI.color = new Color(BrightGold.r, BrightGold.g, BrightGold.b, veilAlpha);
            GUI.Label(new Rect(cardRect.x + 18, cardRect.y + 12, 430, 26), ChapterTitles[i], headerStyle);

            GUI.color = new Color(Cream.r, Cream.g, Cream.b, veilAlpha * 0.85f);
            GUI.Label(new Rect(cardRect.x + 18, cardRect.y + 42, 430, 60), ChapterSubtitles[i], noteStyle);

            Rect playMapBtn = new Rect(cardRect.xMax - 170, cardRect.y + 32, 150, 50);
            if (GUI.Button(playMapBtn, "VÀO MÀN ▶", mainButtonStyle))
            {
                SelectAndLoadChapter(i);
            }
        }

        // Nút quay lại
        Rect backBtn = new Rect(box.x + 36, box.yMax - 48, 220, 36);
        if (GUI.Button(backBtn, "◀ QUAY LẠI [ESC]", backButtonStyle))
        {
            PlaySfx(clickSfx);
            currentTab = Tab.Main;
        }
    }

    private void DrawControlsTab()
    {
        Rect box = new Rect(110, 204, 1380, 546);
        DrawPanelBox(box, PanelBackground, PaleGold, veilAlpha);

        GUI.color = new Color(BrightGold.r, BrightGold.g, BrightGold.b, veilAlpha);
        GUI.Label(new Rect(box.x + 36, box.y + 24, 1000, 36), "📜 BINH PHÁP & HƯỚNG DẪN ĐIỀU KHIỂN CHIẾN ĐẤU", headerStyle);

        DrawSolidColor(new Rect(box.x + 36, box.y + 60, 1308, 1), PaleGold, veilAlpha * 0.4f);

        // Cột 1: Di chuyển & Hành động cơ bản
        Rect col1 = new Rect(box.x + 36, box.y + 74, 640, 400);
        DrawPanelBox(col1, new Color(0.025f, 0.14f, 0.13f, 0.75f), PaleGold, veilAlpha * 0.6f);

        GUI.color = new Color(BrightGold.r, BrightGold.g, BrightGold.b, veilAlpha);
        GUI.Label(new Rect(col1.x + 18, col1.y + 14, 600, 28), "🏇 DI CHUYỂN & CHIẾN ĐẤU CƠ BẢN", headerStyle);

        string moves =
            "• W, A, S, D: Điều khiển tráng sĩ & phi ngựa vượt địa hình\n" +
            "• SHIFT (Giữ): Phi nước đại tốc độ cao càn lướt kẻ địch\n" +
            "• SPACE: Nhảy cao vượt chướng ngại vật & hào lũy\n" +
            "• C hoặc CTRL: Lăn né tránh đòn hiểm độc của tướng giặc\n" +
            "• CHUỘT TRÁI: Đánh liên hoàn (Chém gươm sắt / Quét tre ngà / Ném đá)\n" +
            "• GIỮ CHUỘT PHẢI: Xoay tự do góc nhìn camera chiến trận\n" +
            "• PHÍM E: Tương tác nhặt vật phẩm, cởi giáp, và NHỔ KHÓM TRE NGÀ\n" +
            "• PHÍM F: Kích hoạt Hỏa Tuyến Ngựa Sắt thiêu rụi toàn bộ kẻ thù";

        GUI.color = new Color(Cream.r, Cream.g, Cream.b, veilAlpha * 0.95f);
        GUI.Label(new Rect(col1.x + 18, col1.y + 50, 600, 330), moves, bodyStyle);

        // Cột 2: Tuyệt kỹ Ngũ Linh Hạt & Phím chức năng
        Rect col2 = new Rect(box.x + 704, box.y + 74, 640, 400);
        DrawPanelBox(col2, new Color(0.025f, 0.14f, 0.13f, 0.75f), PaleGold, veilAlpha * 0.6f);

        GUI.color = new Color(BrightGold.r, BrightGold.g, BrightGold.b, veilAlpha);
        GUI.Label(new Rect(col2.x + 18, col2.y + 14, 600, 28), "🌿 NGŨ LINH HẠT & PHÍM TẮT TIỆN ÍCH", headerStyle);

        string seedsAndHotkeys =
            "• PHÍM 1 (LUMEN): Khóa chân kẻ địch & phá khiên phát sáng\n" +
            "• PHÍM 2 (TRE): Bẫy gai tre ngà gây sát thương & làm chậm bước tiến\n" +
            "• PHÍM 3 (SEN): Trận hoa sen hộ thể dưới chân, hồi máu & giảm 32% sát thương\n" +
            "• PHÍM 4 (LỬA): Thiêu đốt hỏa diệm, mở giáp giặc cho đòn kế tiếp\n" +
            "• PHÍM 5 (GIÓ): Cơn gió lốc ngắt đòn đối phương & đẩy lùi vòng vây\n" +
            "• PHÍM Q: Gieo loại hạt đã chọn xuống trận địa\n" +
            "• TAB: Xem bảng nhiệm vụ & tiến độ  ·  F1: Bật/Tắt hướng dẫn\n" +
            "• F2: Đổi chế độ camera  ·  F10: Xem trailer điện ảnh";

        GUI.color = new Color(Cream.r, Cream.g, Cream.b, veilAlpha * 0.95f);
        GUI.Label(new Rect(col2.x + 18, col2.y + 50, 600, 330), seedsAndHotkeys, bodyStyle);

        // Nút quay lại
        Rect backBtn = new Rect(box.x + 36, box.yMax - 48, 220, 36);
        if (GUI.Button(backBtn, "◀ QUAY LẠI [ESC]", backButtonStyle))
        {
            PlaySfx(clickSfx);
            currentTab = Tab.Main;
        }
    }

    private void DrawSettingsTab()
    {
        Rect box = new Rect(110, 204, 1380, 546);
        DrawPanelBox(box, PanelBackground, PaleGold, veilAlpha);

        GUI.color = new Color(BrightGold.r, BrightGold.g, BrightGold.b, veilAlpha);
        GUI.Label(new Rect(box.x + 36, box.y + 24, 1000, 36), "🔊 CÀI ĐẶT ÂM THANH & TRẢI NGHIỆM", headerStyle);

        DrawSolidColor(new Rect(box.x + 36, box.y + 60, 1308, 1), PaleGold, veilAlpha * 0.4f);

        float master = LegendAudioMix.Master;
        float music = LegendAudioMix.Music;
        float effects = LegendAudioMix.Effects;

        // Thanh trượt 1: ÂM LƯỢNG TỔNG
        GUI.color = new Color(Cream.r, Cream.g, Cream.b, veilAlpha);
        GUI.Label(new Rect(box.x + 60, box.y + 100, 300, 30), "ÂM LƯỢNG TỔNG (MASTER)", headerStyle);
        float newMaster = GUI.HorizontalSlider(new Rect(box.x + 380, box.y + 110, 500, 24), master, 0f, 1f);
        GUI.Label(new Rect(box.x + 910, box.y + 104, 80, 30), Mathf.RoundToInt(newMaster * 100f) + "%", headerStyle);

        // Thanh trượt 2: NHẠC NỀN SỬ THI
        GUI.Label(new Rect(box.x + 60, box.y + 170, 300, 30), "NHẠC NỀN (MUSIC)", headerStyle);
        float newMusic = GUI.HorizontalSlider(new Rect(box.x + 380, box.y + 180, 500, 24), music, 0f, 1f);
        GUI.Label(new Rect(box.x + 910, box.y + 174, 80, 30), Mathf.RoundToInt(newMusic * 100f) + "%", headerStyle);

        // Thanh trượt 3: HIỆU ỨNG CHIẾN ĐẤU
        GUI.Label(new Rect(box.x + 60, box.y + 240, 300, 30), "HIỆU ỨNG CHIẾN ĐẤU (SFX)", headerStyle);
        float newEffects = GUI.HorizontalSlider(new Rect(box.x + 380, box.y + 250, 500, 24), effects, 0f, 1f);
        GUI.Label(new Rect(box.x + 910, box.y + 244, 80, 30), Mathf.RoundToInt(newEffects * 100f) + "%", headerStyle);

        if (!Mathf.Approximately(master, newMaster) || !Mathf.Approximately(music, newMusic) || !Mathf.Approximately(effects, newEffects))
        {
            LegendAudioMix.SetMix(newMaster, newMusic, newEffects);
            if (menuAudioSource != null) menuAudioSource.volume = 0.75f * newMusic * newMaster;
        }

        // Khung thông tin phím nhanh
        Rect hotkeyInfoBox = new Rect(box.x + 60, box.y + 310, 930, 160);
        DrawPanelBox(hotkeyInfoBox, new Color(0.02f, 0.12f, 0.11f, 0.8f), PaleGold, veilAlpha * 0.6f);

        GUI.color = new Color(BrightGold.r, BrightGold.g, BrightGold.b, veilAlpha);
        GUI.Label(new Rect(hotkeyInfoBox.x + 20, hotkeyInfoBox.y + 16, 600, 28), "🎮 PHÍM TẮT ĐIỀU HƯỚNG NHANH TRONG GAME", headerStyle);

        string hint =
            "• F5 / F6: Chuyển nhanh giữa màn chơi trước / màn chơi sau (Demo Navigation)\n" +
            "• F8: Tải lại tức thì bản đồ hiện tại  ·  F10: Phát Trailer điện ảnh 4 cảnh\n" +
            "• ESC: Mở bảng Tạm Dừng / Quay lại màn hình chờ bất kỳ lúc nào";

        GUI.color = new Color(Cream.r, Cream.g, Cream.b, veilAlpha * 0.9f);
        GUI.Label(new Rect(hotkeyInfoBox.x + 20, hotkeyInfoBox.y + 54, 880, 90), hint, bodyStyle);

        // Nút quay lại
        Rect backBtn = new Rect(box.x + 36, box.yMax - 48, 220, 36);
        if (GUI.Button(backBtn, "◀ QUAY LẠI [ESC]", backButtonStyle))
        {
            PlaySfx(clickSfx);
            currentTab = Tab.Main;
        }
    }

    private void DrawStoryTab()
    {
        // Có thể mở rộng tab cốt truyện mở rộng nếu cần
        DrawMainTab();
    }

    private void DrawFooter()
    {
        Rect footer = new Rect(110, 762, 1380, 52);
        DrawSolidColor(new Rect(footer.x, footer.y, footer.width, 1), PaleGold, veilAlpha * 0.35f);

        GUI.color = new Color(PaleGold.r, PaleGold.g, PaleGold.b, veilAlpha * 0.85f);
        GUI.Label(new Rect(footer.x, footer.y + 12, footer.width, 26), "★ NON SÔNG GẤM VÓC NGÀN NĂM VỮNG BỀN · TINH THẦN PHÙ ĐỔNG ĐỜI ĐỜI BẤT DIỆT ★", subtitleStyle);

        GUI.color = new Color(MutedText.r, MutedText.g, MutedText.b, veilAlpha * 0.65f);
        GUI.Label(new Rect(footer.x, footer.y + 30, footer.width, 20), "PRU213 - Unity 6 3D Action RPG · Dự Án Truyền Thuyết Thánh Gióng", noteStyle);
    }

    private static void DrawPanelBox(Rect rect, Color bg, Color border, float alpha)
    {
        DrawSolidColor(rect, bg, alpha * bg.a);
        DrawSolidColor(new Rect(rect.x, rect.y, rect.width, 2), border, alpha * 0.85f);
        DrawSolidColor(new Rect(rect.x, rect.yMax - 2, rect.width, 2), border, alpha * 0.85f);
        DrawSolidColor(new Rect(rect.x, rect.y, 2, rect.height), border, alpha * 0.85f);
        DrawSolidColor(new Rect(rect.xMax - 2, rect.y, 2, rect.height), border, alpha * 0.85f);

        // Góc trang trí vàng đồng 6x6
        float cSize = 6f;
        DrawSolidColor(new Rect(rect.x - 2, rect.y - 2, cSize, cSize), border, alpha);
        DrawSolidColor(new Rect(rect.xMax - cSize + 2, rect.y - 2, cSize, cSize), border, alpha);
        DrawSolidColor(new Rect(rect.x - 2, rect.yMax - cSize + 2, cSize, cSize), border, alpha);
        DrawSolidColor(new Rect(rect.xMax - cSize + 2, rect.yMax - cSize + 2, cSize, cSize), border, alpha);
    }

    private static void DrawOrnateFrame(Rect rect, Color color, float alpha)
    {
        // Khung ngoài
        DrawSolidColor(new Rect(rect.x, rect.y, rect.width, 2), color, alpha);
        DrawSolidColor(new Rect(rect.x, rect.yMax - 2, rect.width, 2), color, alpha);
        DrawSolidColor(new Rect(rect.x, rect.y, 2, rect.height), color, alpha);
        DrawSolidColor(new Rect(rect.xMax - 2, rect.y, 2, rect.height), color, alpha);

        // Khung trong cách 6px
        float inset = 6f;
        Rect inner = new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2, rect.height - inset * 2);
        DrawSolidColor(new Rect(inner.x, inner.y, inner.width, 1), color, alpha * 0.45f);
        DrawSolidColor(new Rect(inner.x, inner.yMax - 1, inner.width, 1), color, alpha * 0.45f);
        DrawSolidColor(new Rect(inner.x, inner.y, 1, inner.height), color, alpha * 0.45f);
        DrawSolidColor(new Rect(inner.xMax - 1, inner.y, 1, inner.height), color, alpha * 0.45f);
    }

    private static void DrawSolidColor(Rect rect, Color color, float alpha)
    {
        Color previous = GUI.color;
        GUI.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static Texture2D CreateDongSonDrumTexture()
    {
        const int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ThanhGiong_DongSonDrumProcedural",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxRadius = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > maxRadius)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float angle = Mathf.Atan2(dy, dx);
                float normDist = dist / maxRadius;

                // Tâm mặt trời 14 tia sáng Đông Sơn
                byte alpha = 0;
                if (normDist < 0.22f)
                {
                    float sunRay = Mathf.Cos(angle * 14f);
                    float intensity = Mathf.Clamp01(sunRay * 0.6f + 0.4f);
                    alpha = (byte)(intensity * (1f - normDist / 0.22f) * 160f);
                }
                else
                {
                    // Các vành hoa văn đồng tâm
                    float ring1 = Mathf.Abs(Mathf.Sin(normDist * 34f));
                    float birdRays = Mathf.Cos(angle * 8f);
                    if (ring1 > 0.82f) alpha = 110;
                    else if (normDist > 0.4f && normDist < 0.65f && birdRays > 0.6f) alpha = 85;
                    else if (normDist > 0.8f && Mathf.Cos(angle * 28f) > 0.7f) alpha = 70;
                }

                pixels[y * size + x] = new Color32(238, 202, 110, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateRibbonTexture()
    {
        const int width = 1024, height = 120;
        var pixels = new Color32[width * height];
        for (int x = 0; x < width; x++)
        {
            int center = Mathf.RoundToInt(60f + Mathf.Sin(x * 0.02f) * 22f);
            for (int offset = -5; offset <= 5; offset++)
            {
                int y = center + offset;
                if (y < 0 || y >= height) continue;
                byte alpha = (byte)(Mathf.Clamp01(1f - Mathf.Abs(offset) / 6f) * 110f);
                pixels[y * width + x] = new Color32(255, 230, 140, alpha);
            }
        }
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "ThanhGiong_RibbonProcedural",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }
}
