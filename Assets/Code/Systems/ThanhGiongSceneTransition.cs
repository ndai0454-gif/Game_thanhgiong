using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class ThanhGiongSceneTransition : MonoBehaviour
{
    private static ThanhGiongSceneTransition instance;
    private static bool transitioning;

    [SerializeField] private float fadeOutDuration = 0.7f;
    [SerializeField] private float minimumLoadScreen = 0.45f;
    [SerializeField] private float fadeInDuration = 0.85f;
    [SerializeField] private bool demoNavigationEnabled = true;

    private float veilAlpha;
    private float displayProgress;
    private string status = string.Empty;
    private GUIStyle captionStyle;
    private GUIStyle titleStyle;
    private GUIStyle detailStyle;
    private Texture2D loadingBackground;
    private Texture2D ribbonTexture;
    private bool navigationHelpVisible;
    private static readonly Color DeepTeal = new Color(0.025f, 0.12f, 0.12f);
    private static readonly Color PaleGold = new Color(0.93f, 0.79f, 0.43f);
    private static readonly Color Cream = new Color(1f, 0.96f, 0.84f);

    public static bool IsTransitioning => transitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        transitioning = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => EnsureInstance();

    private static void EnsureInstance()
    {
        if (instance != null) return;
        GameObject host = new GameObject("Thanh Giong Scene Transition");
        instance = host.AddComponent<ThanhGiongSceneTransition>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        loadingBackground = Resources.Load<Texture2D>("ThanhGiongUI/LoadingForest");
        ribbonTexture = CreateRibbonTexture();
        veilAlpha = 1f;
        StartCoroutine(InitialReveal());
    }

    private void OnGUI()
    {
        Color previous = GUI.color;
        Matrix4x4 previousMatrix = GUI.matrix;
        int previousDepth = GUI.depth;
        GUI.depth = -100;
        if (veilAlpha > 0.001f)
        {
            if (string.IsNullOrEmpty(status)) DrawColor(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.018f, 0.025f, 0.035f), veilAlpha);
            else DrawLoadingScreen();
        }

        if (demoNavigationEnabled && navigationHelpVisible && !transitioning)
        {
            GUIStyle hint = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.height / 60, 14, 20),
                fontStyle = FontStyle.Bold
            };
            GUI.color = new Color(1f, 1f, 1f, 0.88f);
            GUI.Box(new Rect(Screen.width - 390f, Screen.height - 48f, 375f, 34f), "DEMO MAP  |  F5: Màn trước  ·  F6: Màn sau  ·  F8: Xem lại", hint);
        }
        GUI.color = previous;
        GUI.matrix = previousMatrix;
        GUI.depth = previousDepth;
    }

    private void DrawLoadingScreen()
    {
        Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
        if (loadingBackground != null)
        {
            GUI.color = new Color(1f, 1f, 1f, veilAlpha);
            GUI.DrawTexture(screen, loadingBackground, ScaleMode.ScaleAndCrop, true);
        }
        else DrawColor(screen, DeepTeal, veilAlpha);
        DrawColor(screen, new Color(0.005f, 0.06f, 0.055f), veilAlpha * 0.28f);

        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600f * scale) * 0.5f,
            (Screen.height - 900f * scale) * 0.5f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        EnsureLoadingStyles();

        DrawColor(new Rect(322f, 140f, 956f, 572f), DeepTeal, veilAlpha * 0.73f);
        DrawColor(new Rect(322f, 140f, 956f, 2f), PaleGold, veilAlpha * 0.62f);
        DrawColor(new Rect(322f, 710f, 956f, 2f), PaleGold, veilAlpha * 0.62f);
        DrawColor(new Rect(322f, 140f, 2f, 572f), PaleGold, veilAlpha * 0.5f);
        DrawColor(new Rect(1276f, 140f, 2f, 572f), PaleGold, veilAlpha * 0.5f);
        GUI.color = new Color(Cream.r, Cream.g, Cream.b, veilAlpha);
        GUI.Label(new Rect(370f, 188f, 860f, 100f), "THÁNH GIÓNG", titleStyle);
        GUI.color = new Color(PaleGold.r, PaleGold.g, PaleGold.b, veilAlpha);
        GUI.Label(new Rect(410f, 292f, 780f, 38f), "HÀNH TRÌNH TỪ PHÙ ĐỔNG ĐẾN NÚI SÓC", detailStyle);

        Rect bar = new Rect(405f, 458f, 790f, 56f);
        DrawColor(new Rect(bar.x - 5f, bar.y - 5f, bar.width + 10f, bar.height + 10f), PaleGold, veilAlpha * 0.92f);
        DrawColor(bar, new Color(0.015f, 0.12f, 0.115f), veilAlpha);
        float fillWidth = Mathf.Max(0f, (bar.width - 12f) * Mathf.Clamp01(displayProgress));
        if (fillWidth > 0f)
        {
            Rect fill = new Rect(bar.x + 6f, bar.y + 6f, fillWidth, bar.height - 12f);
            DrawColor(fill, new Color(0.32f, 0.66f, 0.16f), veilAlpha);
            DrawColor(new Rect(fill.x, fill.y, fill.width, 12f), new Color(0.76f, 0.96f, 0.34f), veilAlpha * 0.88f);
            DrawColor(new Rect(fill.x, fill.yMax - 8f, fill.width, 8f), new Color(0.08f, 0.43f, 0.28f), veilAlpha * 0.72f);
            float shimmerX = fill.x + Mathf.Repeat(Time.unscaledTime * 170f, Mathf.Max(1f, fill.width));
            DrawColor(new Rect(shimmerX, fill.y + 2f, Mathf.Min(12f, fill.xMax - shimmerX), fill.height - 4f), Cream, veilAlpha * 0.42f);
        }

        // A translucent ribbon is generated once, so its curves stay smooth at every resolution.
        if (ribbonTexture != null)
        {
            GUI.color = new Color(1f, 1f, 1f, veilAlpha * 0.83f);
            GUI.DrawTexture(new Rect(bar.x - 36f, bar.y - 49f, bar.width + 72f, 154f), ribbonTexture);
        }
        for (int i = 0; i < 24; i++)
        {
            float phase = i * 2.39996f;
            float x = bar.x + Mathf.Repeat(i * 97f + Time.unscaledTime * (9f + i % 5), bar.width);
            float y = bar.y + 28f + Mathf.Sin(phase + Time.unscaledTime * 1.3f) * (40f + i % 3 * 15f);
            float size = i % 5 == 0 ? 5f : 2f;
            DrawColor(new Rect(x, y, size, size), Cream, veilAlpha * (0.3f + 0.45f * Mathf.PingPong(Time.unscaledTime + i * 0.17f, 1f)));
        }

        GUI.color = new Color(Cream.r, Cream.g, Cream.b, veilAlpha);
        GUI.Label(new Rect(440f, 548f, 720f, 50f), Mathf.RoundToInt(displayProgress * 100f) + "%", captionStyle);
        GUI.color = new Color(PaleGold.r, PaleGold.g, PaleGold.b, veilAlpha);
        GUI.Label(new Rect(370f, 605f, 860f, 54f), status, detailStyle);
    }

    private void EnsureLoadingStyles()
    {
        if (titleStyle != null) return;
        Font bold = Resources.Load<Font>("ThanhGiongUI/OldStandard-Bold");
        Font regular = Resources.Load<Font>("ThanhGiongUI/OldStandard-Regular");
        titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, font = bold, fontSize = 78, fontStyle = bold == null ? FontStyle.Bold : FontStyle.Normal };
        captionStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, font = bold, fontSize = 35, fontStyle = bold == null ? FontStyle.Bold : FontStyle.Normal };
        detailStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, font = regular, fontSize = 26, fontStyle = FontStyle.Normal, wordWrap = true };
    }

    private static void DrawColor(Rect rect, Color color, float alpha)
    {
        GUI.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }

    private static Texture2D CreateRibbonTexture()
    {
        const int width = 1024, height = 180;
        var pixels = new Color32[width * height];
        for (int x = 0; x < width; x++)
        {
            for (int strand = 0; strand < 2; strand++)
            {
                int center = Mathf.RoundToInt(90f + Mathf.Sin(x * 0.021f + strand * 2.2f) * 34f);
                for (int offset = -6; offset <= 6; offset++)
                {
                    int y = center + offset;
                    if (y < 0 || y >= height) continue;
                    byte alpha = (byte)(Mathf.Clamp01(1f - Mathf.Abs(offset) / 7f) * (strand == 0 ? 170f : 118f));
                    int index = y * width + x;
                    if (alpha > pixels[index].a) pixels[index] = strand == 0
                        ? new Color32(255, 225, 133, alpha) : new Color32(255, 250, 211, alpha);
                }
            }
        }
        Texture2D ribbon = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Thanh Giong Loading Light Ribbon",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        ribbon.SetPixels32(pixels);
        ribbon.Apply(false, true);
        return ribbon;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) navigationHelpVisible = !navigationHelpVisible;
        if (!demoNavigationEnabled || transitioning) return;

        if (Input.GetKeyDown(KeyCode.F6))
            TryTransitionToNext("ĐANG CHUYỂN ĐẾN MÀN TIẾP THEO...");
        else if (Input.GetKeyDown(KeyCode.F5))
        {
            string previous = GetPreviousScene(SceneManager.GetActiveScene().name);
            if (!string.IsNullOrEmpty(previous)) TryTransitionTo(previous, "QUAY LẠI MÀN TRƯỚC...");
        }
        else if (Input.GetKeyDown(KeyCode.F8))
            TryTransitionTo(SceneManager.GetActiveScene().name, "ĐANG TẢI LẠI BẢN ĐỒ...");
    }

    private IEnumerator InitialReveal()
    {
        yield return null;
        yield return Fade(1f, 0f, fadeInDuration);
    }

    public static bool TryTransitionTo(string sceneName, string caption = null)
    {
        var world = FindAnyObjectByType<ThanhGiongSeamlessCampaign>();
        if (world != null) return world.HandleLegacyNavigation(sceneName);
        EnsureInstance();
        if (transitioning || string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            return false;

        string currentScene = SceneManager.GetActiveScene().name;
        if (IsForwardTransition(currentScene, sceneName))
        {
            ThanhGiongCampaignController campaign = FindAnyObjectByType<ThanhGiongCampaignController>();
            if (campaign != null && !campaign.CanAdvanceToNextScene(out string reason))
            {
                campaign.ShowMessage("CHƯA THỂ QUA MÀN · " + reason, 3f);
                return false;
            }
        }

        instance.StartCoroutine(instance.LoadScene(sceneName, caption));
        return true;
    }

    private static bool IsForwardTransition(string currentScene, string targetScene)
    {
        string cursor = currentScene;
        for (int i = 0; i < 6; i++)
        {
            cursor = GetNextScene(cursor);
            if (string.IsNullOrEmpty(cursor)) return false;
            if (cursor == targetScene) return true;
        }
        return false;
    }

    public static string GetNextScene(string currentScene)
    {
        return currentScene switch
        {
            "LangGiongTienTuyen" => "KinhThanhRenThep",
            "KinhThanhRenThep" => "PhaoDaiNgamQuanAn",
            "PhaoDaiNgamQuanAn" => "ThungLungVuotSong",
            "ThungLungVuotSong" => "TranTuyenNuiSoc",
            "TranTuyenNuiSoc" => "DinhSocHoaThanh",
            _ => null
        };
    }

    public static string GetPreviousScene(string currentScene)
    {
        return currentScene switch
        {
            "KinhThanhRenThep" => "LangGiongTienTuyen",
            "PhaoDaiNgamQuanAn" => "KinhThanhRenThep",
            "ThungLungVuotSong" => "PhaoDaiNgamQuanAn",
            "TranTuyenNuiSoc" => "ThungLungVuotSong",
            "DinhSocHoaThanh" => "TranTuyenNuiSoc",
            _ => null
        };
    }

    public static bool TryTransitionToNext(string caption = null)
    {
        string next = GetNextScene(SceneManager.GetActiveScene().name);
        return !string.IsNullOrEmpty(next) && TryTransitionTo(next, caption);
    }

    private IEnumerator LoadScene(string sceneName, string caption)
    {
        transitioning = true;
        SetPlayerInput(false);
        displayProgress = 0f;
        status = string.IsNullOrWhiteSpace(caption) ? "HÀNH TRÌNH TIẾP TỤC..." : caption;
        yield return Fade(veilAlpha, 1f, fadeOutDuration);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (load == null)
        {
            yield return Fade(1f, 0f, fadeInDuration);
            transitioning = false;
            SetPlayerInput(true);
            yield break;
        }

        load.allowSceneActivation = false;
        float hold = 0f;
        while (load.progress < 0.9f || hold < minimumLoadScreen)
        {
            hold += Time.unscaledDeltaTime;
            displayProgress = Mathf.Max(displayProgress, Mathf.Clamp01(load.progress / 0.9f));
            yield return null;
        }

        displayProgress = 1f;
        load.allowSceneActivation = true;
        while (!load.isDone) yield return null;
        yield return null;
        yield return null;

        status = string.Empty;
        SetPlayerInput(true);
        yield return Fade(1f, 0f, fadeInDuration);
        transitioning = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            veilAlpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            veilAlpha = Mathf.LerpUnclamped(from, to, t);
            yield return null;
        }
        veilAlpha = to;
    }

    private static void SetPlayerInput(bool enabled)
    {
        foreach (MountedHorseController controller in FindObjectsByType<MountedHorseController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            controller.enabled = enabled;
        foreach (AdventurerController controller in FindObjectsByType<AdventurerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            controller.enabled = enabled;
    }
}
