using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Title menu shown when the game starts: Tiếp tục / Chơi mới / Cài đặt / Thoát.
/// The world stays frozen behind it. In the Unity editor it is off unless PlayerPrefs "TG.MainMenu.Editor" = 1,
/// so verification and recording harnesses keep running unattended.
/// </summary>
public sealed class ThanhGiongMainMenu : MonoBehaviour
{
    enum Page { Main, Settings }

    static bool skipOnce;
    Page page = Page.Main;
    bool open = true, hasSave;
    float previousTimeScale = 1f;
    bool previousCursorVisible;
    CursorLockMode previousLock;
    string checkpointPath;
    GUIStyle title, subtitle, button, label, small;
    float notice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Application.isBatchMode || FindFirstObjectByType<ThanhGiongMainMenu>() != null) return;
        if (Application.isEditor && PlayerPrefs.GetInt("TG.MainMenu.Editor", 0) != 1) return;
        // Nếu bản build mở bằng màn hình chờ (ManHinhCho) thì dùng màn hình đó, không hiện menu thứ hai.
        if (UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/ThanhGiongWorld/ManHinhCho.unity") == 0) return;
        if (skipOnce) { skipOnce = false; return; }
        if (FindFirstObjectByType<ThanhGiongCampaignController>() == null) return;
        new GameObject("ThanhGiong Main Menu").AddComponent<ThanhGiongMainMenu>();
    }

    void Start()
    {
        var seamless = FindFirstObjectByType<ThanhGiongSeamlessCampaign>();
        checkpointPath = seamless != null ? seamless.CheckpointPath : null;
        hasSave = !string.IsNullOrEmpty(checkpointPath) && File.Exists(checkpointPath);
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        previousCursorVisible = Cursor.visible; previousLock = Cursor.lockState;
        Freeze();
    }

    void Freeze() { Time.timeScale = 0f; Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }

    void Update() { if (open) Freeze(); }

    void Close()
    {
        open = false;
        Time.timeScale = previousTimeScale;
        Cursor.visible = previousCursorVisible; Cursor.lockState = previousLock;
        Destroy(gameObject);
    }

    void NewGame()
    {
        var seamless = FindFirstObjectByType<ThanhGiongSeamlessCampaign>();
        if (seamless != null) { seamless.persistenceEnabled = false; seamless.restoreSaveOnStart = false; }
        try { if (!string.IsNullOrEmpty(checkpointPath) && File.Exists(checkpointPath)) File.Delete(checkpointPath); } catch { }
        ThanhGiongVillagerProgress.ResetAll();
        skipOnce = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void EnsureStyles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { font = ThanhGiongUITheme.Light, fontSize = 74, alignment = TextAnchor.MiddleCenter, normal = { textColor = ThanhGiongUITheme.Brass } };
        subtitle = new GUIStyle(title) { font = ThanhGiongUITheme.Regular, fontSize = 22, normal = { textColor = ThanhGiongUITheme.Muted } };
        label = new GUIStyle(subtitle) { fontSize = 20, alignment = TextAnchor.MiddleLeft, normal = { textColor = ThanhGiongUITheme.Paper } };
        small = new GUIStyle(subtitle) { fontSize = 15 };
        button = ThanhGiongUITheme.Button(24);
    }

    static void Fill(Rect r, Color c) { Color old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old; }

    void OnGUI()
    {
        if (!open) return;
        GUI.depth = -10000;
        EnsureStyles();
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        Matrix4x4 oldMatrix = GUI.matrix;
        // Full-screen lacquer background (not scaled) with a warm vignette band.
        GUI.matrix = Matrix4x4.identity;
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.075f, .035f, .028f, 1f));
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280f * scale) * .5f, (Screen.height - 720f * scale) * .5f, 0f), Quaternion.identity, Vector3.one * scale);

        Fill(new Rect(440, 212, 400, 1), new Color(.96f, .74f, .24f, .55f));
        Fill(new Rect(440, 664, 400, 1), new Color(.96f, .74f, .24f, .35f));
        GUI.Label(new Rect(240, 70, 800, 100), "THÁNH GIÓNG", title);
        GUI.Label(new Rect(240, 160, 800, 34), "Phù Đổng Thiên Vương · Truyền thuyết đánh giặc Ân", subtitle);

        if (page == Page.Main) DrawMain(); else DrawSettings();
        GUI.Label(new Rect(240, 676, 800, 24), "Dùng chuột để chọn", small);
        GUI.matrix = oldMatrix;
    }

    void DrawMain()
    {
        float y = 250f; const float w = 360f, h = 54f, gap = 16f;
        float x = (1280f - w) * .5f;
        if (hasSave) {
            if (GUI.Button(new Rect(x, y, w, h), "TIẾP TỤC", button)) Close();
            y += h + gap;
            if (GUI.Button(new Rect(x, y, w, h), "CHƠI MỚI", button)) NewGame();
        } else {
            if (GUI.Button(new Rect(x, y, w, h), "BẮT ĐẦU", button)) Close();
        }
        y += h + gap;
        if (GUI.Button(new Rect(x, y, w, h), "CÀI ĐẶT", button)) page = Page.Settings;
        y += h + gap;
        if (GUI.Button(new Rect(x, y, w, h), "THOÁT GAME", button)) Quit();
        if (hasSave) GUI.Label(new Rect(240, y + h + 18f, 800, 24), "Chơi mới sẽ xóa hành trình đã lưu.", small);
    }

    void DrawSettings()
    {
        float x = 420f, y = 240f;
        float master = Slider(x, y, "TỔNG ÂM LƯỢNG", LegendAudioMix.Master);
        float music = Slider(x, y + 56f, "NHẠC NỀN", LegendAudioMix.Music);
        float effects = Slider(x, y + 112f, "HIỆU ỨNG", LegendAudioMix.Effects);
        if (!Mathf.Approximately(master, LegendAudioMix.Master) || !Mathf.Approximately(music, LegendAudioMix.Music) || !Mathf.Approximately(effects, LegendAudioMix.Effects))
            LegendAudioMix.SetMix(master, music, effects);

        GUI.Label(new Rect(x, y + 176f, 260, 36), "TOÀN MÀN HÌNH", label);
        if (GUI.Button(new Rect(x + 270f, y + 174f, 190, 40), Screen.fullScreen ? "BẬT" : "TẮT", ThanhGiongUITheme.Button(20)))
            Screen.fullScreen = !Screen.fullScreen;

        GUI.Label(new Rect(x, y + 228f, 260, 36), "CHẤT LƯỢNG ĐỒ HỌA", label);
        string[] names = QualitySettings.names;
        int level = QualitySettings.GetQualityLevel();
        string shown = names.Length > 0 ? names[Mathf.Clamp(level, 0, names.Length - 1)] : "-";
        if (GUI.Button(new Rect(x + 270f, y + 226f, 40, 40), "<", ThanhGiongUITheme.Button(20))) QualitySettings.SetQualityLevel(Mathf.Max(0, level - 1), true);
        GUI.Label(new Rect(x + 316f, y + 226f, 150, 40), shown, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
        if (GUI.Button(new Rect(x + 470f, y + 226f, 40, 40), ">", ThanhGiongUITheme.Button(20))) QualitySettings.SetQualityLevel(Mathf.Min(names.Length - 1, level + 1), true);

        if (GUI.Button(new Rect((1280f - 360f) * .5f, y + 310f, 360f, 54f), "QUAY LẠI", button)) { page = Page.Main; PlayerPrefs.Save(); }
    }

    float Slider(float x, float y, string text, float value)
    {
        GUI.Label(new Rect(x, y, 260, 36), text, label);
        float v = ThanhGiongUITheme.Slider(new Rect(x + 270f, y + 12f, 230, 20), value);
        GUI.Label(new Rect(x + 510f, y, 70, 36), Mathf.RoundToInt(v * 100f) + "%", label);
        return v;
    }
}
