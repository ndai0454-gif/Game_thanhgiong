using UnityEngine;
using UnityEngine.SceneManagement;

// Compact red/bronze Kenney HUD. Scaled game time keeps popup durations frozen during pause.
public partial class ThanhGiongCampaignHUD : MonoBehaviour
{
    public ThanhGiongCampaignController campaign;
    public Texture2D panelFrame, bannerFrame, dividerTexture, combatCursor;
    [Range(1, 20)] public float missionPopupSeconds = 6f;
    [Range(1, 20)] public float controlsPopupSeconds = 4f;
    [Range(.05f, 1)] public float popupFadeSeconds = .3f;
    public bool MissionVisible => Time.time < missionUntil;
    public bool ControlsVisible => Time.time < controlsUntil;
    public bool GuideVisible => guidesVisible;
    public bool IsPaused => paused;
    public int ChapterCount => ChapterScenes.Length;
    public bool RegionIntroVisible => Time.time < regionUntil;
    public int PauseSelection { get; private set; }
    public static float CanvasScale(Rect safeArea) => Mathf.Max(.1f,Mathf.Min(safeArea.width/1280f,safeArea.height/720f));
    private static readonly string[] ChapterScenes = {
        "LangGiongTienTuyen", "KinhThanhRenThep", "PhaoDaiNgamQuanAn", "ThungLungVuotSong",
        "TranTuyenNuiSoc", "DinhSocHoaThanh"
    };
    private static readonly string[] ChapterLabels = {
        "1 · LÀNG PHÙ ĐỔNG", "2 · KINH THÀNH RÈN THÉP", "3 · PHÁO ĐÀI QUÂN ÂN", "4 · THUNG LŨNG VƯỢT SÔNG",
        "5 · TRẬN TUYẾN NÚI SÓC", "6 · ĐỈNH SÓC HÓA THÁNH"
    };
    private string pauseNotice = string.Empty;
    private float missionUntil, controlsUntil, nextBossCheck, regionUntil;
    private bool guidesVisible, paused, haveState;
    private ThanhGiongCampaignController.Chapter previousChapter;
    private string previousZoneId = string.Empty;
    private ThanhGiongCampaignController.Weapon previousWeapon;
    private int previousStep, previousGrowth;
    private bool previousHeatReady, previousCarrying, nearInteraction;
    private ThanhGiongEnemy cachedBoss;
    private LegendSeedSkills seeds;
    private LegendCheckpoint checkpoint;
    private GUIStyle title, body, caption, small, centered, frame, button, hudTitle, hudBody, tile;
    private Font serifRegular, serifBold;
    private readonly Collider[] nearby = new Collider[32];
    private float nextNearScan;
    private static readonly Color Paper = ThanhGiongUITheme.Paper;
    private static readonly Color Gold = ThanhGiongUITheme.Brass;
    private static readonly Color Muted = ThanhGiongUITheme.Muted;
    private static readonly Color PanelTeal = ThanhGiongUITheme.Ink;
    private static readonly Color Forest = new Color(.13f, .05f, .04f);
    private static readonly Color Spring = new Color(.55f, .88f, .27f);

    private void Start()
    {
        if (campaign == null) campaign = FindAnyObjectByType<ThanhGiongCampaignController>();
        if(campaign!=null){seeds=campaign.GetComponent<LegendSeedSkills>();checkpoint=campaign.GetComponent<LegendCheckpoint>();}
        RecallPopups();
        regionUntil=Time.time+3.8f;
        if (combatCursor != null) Cursor.SetCursor(combatCursor,
            new Vector2(combatCursor.width * .18f, combatCursor.height * .18f), CursorMode.Auto);
    }

    public void RecallPopups()
    {
        missionUntil = Time.time + missionPopupSeconds;
        controlsUntil = Time.time + controlsPopupSeconds;
    }

    public void TogglePause()
    {
        if (ThanhGiongSceneTransition.IsTransitioning) return;
        paused = !paused;
        Time.timeScale = paused ? 0 : 1;
        pauseNotice = string.Empty;
        if(paused)PauseSelection=0;
    }

    public bool SelectChapter(int index)
    {
        if (index < 0 || index >= ChapterScenes.Length) return false;
        return LoadFromPause(ChapterScenes[index], "ĐANG ĐẾN " + ChapterLabels[index]);
    }

    public bool RestartCurrentMap()
    {
        if(paused&&campaign.openWorldCampaign==null&&!ThanhGiongSceneTransition.IsTransitioning)LegendCheckpoint.ForgetScene(SceneManager.GetActiveScene().name);
        return LoadFromPause(SceneManager.GetActiveScene().name, "ĐANG CHƠI LẠI MÀN…");
    }

    public bool RestartCheckpoint()
    {
        if(campaign!=null&&campaign.openWorldCampaign!=null)return campaign.openWorldCampaign.SaveCheckpoint();
        if(!paused||checkpoint==null||!checkpoint.HasCheckpoint||ThanhGiongSceneTransition.IsTransitioning)return false;
        paused=false;Time.timeScale=1;
        if(checkpoint.RestartCheckpoint()){guidesVisible=false;return true;}
        paused=true;Time.timeScale=0;pauseNotice="Chờ động tác hiện tại kết thúc rồi thử lại.";return false;
    }

    private float quitWithoutSaveUntil = -1f;

    /// <summary>Saves the journey, then closes the game. If saving is not possible right now,
    /// a second press within a few seconds quits anyway.</summary>
    public void SaveAndQuit()
    {
        bool openWorld = campaign != null && campaign.openWorldCampaign != null;
        bool saved = !openWorld || campaign.openWorldCampaign.SaveCheckpoint();
        if (!saved && Time.unscaledTime > quitWithoutSaveUntil)
        {
            quitWithoutSaveUntil = Time.unscaledTime + 4f;
            pauseNotice = "Chưa lưu được lúc này (đang chiến đấu, trang bị hoặc bay). Nhấn THOÁT lần nữa để thoát không lưu.";
            return;
        }
        PlayerPrefs.Save();
        pauseNotice = saved ? "Đã lưu hành trình. Đang thoát…" : "Đang thoát…";
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private bool LoadFromPause(string sceneName, string message)
    {
        if (!paused || ThanhGiongSceneTransition.IsTransitioning) return false;
        // Scene fades use scaled time, so release pause before starting their coroutine.
        paused = false; Time.timeScale = 1;
        if (ThanhGiongSceneTransition.TryTransitionTo(sceneName, message)) {
            guidesVisible = false; pauseNotice = string.Empty; return true;
        }
        paused = true; Time.timeScale = 0;
        pauseNotice = "Màn này chưa sẵn sàng để mở. Chọn màn khác hoặc tiếp tục chơi.";
        return false;
    }

    private void Update()
    {
        if (campaign != null && campaign.openWorldCampaign != null && campaign.openWorldCampaign.world.MapOpen) {
            if (Input.GetKeyDown(KeyCode.Escape)) campaign.openWorldCampaign.world.SetMapOpen(false);
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
        if (Input.GetKeyDown(KeyCode.F1)) guidesVisible = !guidesVisible;
        if (Input.GetKeyDown(KeyCode.Tab)) RecallPopups();
        if(paused)UpdatePauseNavigation();
        if(seeds==null&&campaign!=null)seeds=campaign.GetComponent<LegendSeedSkills>();
        if(checkpoint==null&&campaign!=null)checkpoint=campaign.GetComponent<LegendCheckpoint>();
        if (campaign == null || paused) return;
        UpdateAdventureHUD();
        bool ready = campaign.Heat01 >= 1;
        bool carrying = campaign.CarriedWeapon != null;
        if (!haveState || previousChapter != campaign.CurrentChapter || previousStep != campaign.EquipmentStep) RecallPopups();
        else if (previousWeapon != campaign.CurrentWeapon || previousGrowth != campaign.GrowthPhase ||
            (ready && !previousHeatReady) || (carrying && !previousCarrying)) controlsUntil = Time.time + controlsPopupSeconds;
        if(haveState&&(previousGrowth!=campaign.GrowthPhase||previousWeapon!=campaign.CurrentWeapon))missionUntil=Time.time+missionPopupSeconds;
        if (campaign.openWorldCampaign != null && campaign.openWorldCampaign.world != null)
        {
            var currentZone = campaign.openWorldCampaign.world.CurrentZone;
            string currentZoneId = currentZone != null ? currentZone.id : string.Empty;
            if (haveState && !string.IsNullOrEmpty(currentZoneId) && currentZoneId != previousZoneId)
            {
                regionUntil = Time.time + 4.5f;
            }
            previousZoneId = currentZoneId;
        }
        previousChapter = campaign.CurrentChapter; previousWeapon = campaign.CurrentWeapon;
        previousStep = campaign.EquipmentStep; previousGrowth = campaign.GrowthPhase;
        previousHeatReady = ready; previousCarrying = carrying; haveState = true;
        if (Time.unscaledTime < nextNearScan) return;
        nextNearScan = Time.unscaledTime + 0.1f; // proximity hint does not need per-frame physics queries
        bool nowNear = false;
        int count = Physics.OverlapSphereNonAlloc(campaign.transform.position, 4.5f, nearby, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++) {
            if (nearby[i].GetComponentInParent<ThanhGiongCollectible>() != null ||
                nearby[i].GetComponentInParent<ThanhGiongImprovisedWeapon>() != null) { nowNear = true; break; }
        }
        if (nowNear && !nearInteraction) controlsUntil = Time.time + controlsPopupSeconds;
        nearInteraction = nowNear;
    }

    private void UpdatePauseNavigation()
    {
        int choices = 7 + ChapterScenes.Length;
        if(Input.GetKeyDown(KeyCode.DownArrow))PauseSelection=(PauseSelection+1)%choices;
        if(Input.GetKeyDown(KeyCode.UpArrow))PauseSelection=(PauseSelection+choices-1)%choices;
        float step=Input.GetKeyDown(KeyCode.RightArrow)?.05f:Input.GetKeyDown(KeyCode.LeftArrow)?-.05f:0;
        if(step!=0&&PauseSelection>=3&&PauseSelection<=5) {
            float master=LegendAudioMix.Master,music=LegendAudioMix.Music,effects=LegendAudioMix.Effects;
            if(PauseSelection==3)master+=step;else if(PauseSelection==4)music+=step;else effects+=step;
            LegendAudioMix.SetMix(master,music,effects);
        }
        if(!Input.GetKeyDown(KeyCode.Return)&&!Input.GetKeyDown(KeyCode.KeypadEnter))return;
        if(PauseSelection==0)TogglePause();else if(PauseSelection==1)RestartCheckpoint();
        else if(PauseSelection==2)RestartCurrentMap();else if(PauseSelection==6+ChapterScenes.Length)SaveAndQuit();else if(PauseSelection>=6)SelectChapter(PauseSelection-6);
    }

    private void EnsureStyles()
    {
        if (title != null) return;
        serifRegular = ThanhGiongUITheme.Regular;
        serifBold = ThanhGiongUITheme.Medium;
        title = Style(30, Paper); title.font = ThanhGiongUITheme.Light;
        body = Style(17, Muted); caption = Style(15, Gold);
        small = Style(14, Muted, true); centered = Style(24, Paper); centered.alignment = TextAnchor.MiddleCenter;
        hudTitle = Style(21, Paper); hudBody = Style(16, Paper);
        tile = Style(13, Paper); tile.alignment = TextAnchor.MiddleCenter;
        frame = new GUIStyle(GUIStyle.none);
        button = ThanhGiongUITheme.Button();
    }

    private GUIStyle Style(int size, Color color, bool bold = false)
    {
        GUIStyle result = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(size * ThanhGiongUITheme.FontScale), wordWrap = true,
            font = bold ? serifBold : serifRegular,
            fontStyle = bold && serifBold == null ? FontStyle.Bold : FontStyle.Normal };
        result.normal.textColor = color; return result;
    }

    private void OnGUI()
    {
        if (campaign != null && campaign.openWorldCampaign != null && campaign.openWorldCampaign.world.MapOpen) return;
        if (campaign == null || (ThanhGiongTrailerDirector.Instance != null && ThanhGiongTrailerDirector.Instance.IsPlayingTrailer) || ThanhGiongStartScreen.IsMenuVisible) return;
        EnsureStyles();
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        // A fixed logical canvas avoids overlapping boxes on narrow Game views.
        Rect safe=Screen.safeArea;
        if(safe.width<=0||safe.height<=0)safe=new Rect(0,0,Screen.width,Screen.height);
        float scale = CanvasScale(safe);
        float w = safe.width / scale, h = safe.height / scale;
        GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x,Screen.height-safe.yMax,0),Quaternion.identity,new Vector3(scale,scale,1));
        if (!paused && !guidesVisible) DrawChapterHUD(w,h);
        GUI.color = Color.white;
        if (!paused && !guidesVisible) DrawAdventureHUD(w,h,safe,scale);
        if (ControlsVisible && !paused && !guidesVisible && !ChapterRitualVisible) {
            GUI.color = new Color(1, 1, 1, PopupAlpha(controlsUntil,controlsPopupSeconds));
            Rect actions = new Rect((w - 500) / 2, h - 112, 500, 26);
            ShadowLabel(new Rect(actions.x + 10, actions.y + 4, 480, 18), ControlHint(), small);
        }
        GUI.color = Color.white;
        DrawBoss(w);
        if (campaign.IsDead || !string.IsNullOrEmpty(campaign.CenterMessage)) {
            string notice=campaign.IsDead ? "GIÓNG ĐÃ GỤC NGÃ · R: VỀ ĐIỂM DỪNG CHÂN" : ShortNotice(campaign.CenterMessage);
            float noticeWidth=Mathf.Min(w-80,700);
            var noticeStyle=new GUIStyle(hudBody){alignment=TextAnchor.MiddleCenter,wordWrap=true};
            float noticeHeight=Mathf.Max(40,noticeStyle.CalcHeight(new GUIContent(notice),noticeWidth-24)+12);
            Rect message = new Rect((w - noticeWidth) / 2, h - (ChapterRitualVisible ? 168 : 126) - noticeHeight, noticeWidth, noticeHeight);
            Box(message); ShadowLabel(new Rect(message.x + 12, message.y + 6, noticeWidth-24, noticeHeight-12), notice, noticeStyle);
        }
        if (guidesVisible || paused) {
            float guideHeight = paused ? 640 : 350;
            Rect guide = new Rect((w - 560) / 2, (h - guideHeight) / 2, 560, guideHeight);
            ShadowLabel(new Rect(guide.x + 24, guide.y + 20, 512, 40), paused ? "Tạm dừng" : "Binh pháp Phù Đổng", title);
            string help=paused ? "WASD đi · Shift phi ngựa · Space nhảy · C/Alt né\nChuột phải ngắm · Chuột giữa xoay · V đổi vai\n1–5 chọn hạt · Q gieo · R về điểm dừng chân\nMenu: ↑↓ chọn · ←→ âm lượng · Enter xác nhận" :
                "WASD: di chuyển · Shift: phi ngựa · Space: nhảy · C / Alt: lăn né\nChuột trái: đánh / ném đá · Giữ chuột phải + trái: phóng tre\nChuột giữa: xoay máy · V: đổi vai · Chuột phải: ngắm khóa mềm\n1 Quang: trói, phá khiên · 2 Tre: gai chậm · 3 Sen: hồi máu, hộ thể\n4 Lửa: thiêu, lộ giáp · 5 Gió: ngắt đòn, đẩy lùi · Q: gieo hạt\nE / L: nhổ tre · E: vác đá / nhặt lương · F: Hỏa Tuyến\nE → Q → E → F: trang bị · E → Q → E: đặt giáp, hóa thánh\nNé vòng đỏ của tướng giặc; phản công khi đao mắc kẹt.\nR: về điểm dừng chân khi gục ngã · Tab: nhiệm vụ · F1: trợ giúp";
            ShadowLabel(new Rect(guide.x+24,guide.y+(paused?66:76),512,paused?90:246),help,body);
            if (paused) DrawPauseButtons(guide);
        }
        GUI.color = oldColor; GUI.matrix = oldMatrix;
    }

    // Text-only panels: no surrounding box, a soft dark outline keeps the text readable on any background.
    private static void ShadowLabel(Rect rect, string text, GUIStyle style)
    {
        Color keep = GUI.color;
        GUI.color = new Color(.05f, .02f, .02f, keep.a * .85f);
        foreach (Vector2 o in new[] { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1), new Vector2(1, 1) })
            GUI.Label(new Rect(rect.x + o.x, rect.y + o.y, rect.width, rect.height), text, style);
        GUI.color = keep;
        GUI.Label(rect, text, style);
    }

    private float PopupAlpha(float until,float duration) => Mathf.Clamp01((until-Time.time)/popupFadeSeconds)*Mathf.Clamp01((duration-until+Time.time)/popupFadeSeconds);

    private void DrawSeedBar(float w,float h)
    {
        float x=(w-420)/2, y=h-91;
        string[] names={"LUMEN", "TRE", "SEN", "LỬA", "GIÓ"};
        Color previous=GUI.color;
        for(int i=0;i<5;i++) {
            bool selected=(int)seeds.SelectedSeed==i;
            GUI.color=selected?Color.white:new Color(1,1,1,seeds.ReadyFor(i)?.72f:.40f);
            Rect slot=new Rect(x+4+i*84,y,76,34);Box(slot);
            GUI.color=selected?Gold:new Color(Paper.r,Paper.g,Paper.b,.75f);
            ShadowLabel(new Rect(slot.x+2,slot.y+2,72,28),$"{i+1} {names[i]}",tile);
            if(seeds.CooldownFor(i)>0)ThinBar(new Rect(slot.x+5,slot.yMax-4,66,2),Mathf.Clamp01(seeds.CooldownFor(i)/11),Muted);
            if(selected)GUI.DrawTexture(new Rect(slot.x+8,slot.yMax-2,60,1),Texture2D.whiteTexture);
        }
        GUI.color=previous;
        string cooldown=seeds.CooldownRemaining>0?$"{seeds.CooldownRemaining:0.0}s":"Q gieo";
        string[] effects={"Trói chân + phá khiên: đánh mục tiêu phát sáng", "Gai tre gây thương + làm chậm đường tiến", "Gieo dưới chân: hồi máu + giảm 32% sát thương", "Thiêu đốt + mở giáp cho đòn kế tiếp", "Ngắt đòn báo hiệu + đẩy lùi vòng vây"};
        ShadowLabel(new Rect(x,y+38,420,18),effects[(int)seeds.SelectedSeed],small);
        string ward=campaign.LotusWardRemaining>0?$" · Hộ thể {campaign.LotusWardRemaining:0.0}s":"";
        ShadowLabel(new Rect(x,y+57,420,18),$"Linh lực {seeds.Mana:0}/{seeds.MaxMana:0} · giá {seeds.ManaCost:0} · {seeds.ChargesRemaining} hạt · {cooldown}{ward}",small);
    }

    private void DrawVitals(float h)
    {
        Rect rect=new Rect(16,h-108,278,92);Box(rect);
        ShadowLabel(new Rect(rect.x+12,rect.y+5,245,23),$"SINH LỰC  {Mathf.RoundToInt(campaign.Health01*100)}%",hudTitle);
        DrawEnchantedHealthBar(new Rect(rect.x+12,rect.y+31,254,23),campaign.Health01,false);
        string resource=campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Prologue
            ? $"Lương {Mathf.RoundToInt(campaign.FoodProgress*100)}% · lớn {campaign.GrowthPhase+1}/4"
            : campaign.IsBattleActive ? $"Hạ {campaign.Kills}/{campaign.RequiredBattleKills} · hoàn thành {Mathf.RoundToInt(campaign.StageCompletion01*100)}% · hỏa {Mathf.RoundToInt(campaign.Heat01*100)}%"
            : campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Preparation ? $"Trang bị {Mathf.Min(campaign.EquipmentStep,4)}/4" : "Phù Đổng Thiên Vương";
        ShadowLabel(new Rect(rect.x+12,rect.y+59,254,17),resource,small);
        if (campaign.CurrentChapter == ThanhGiongCampaignController.Chapter.Battle && campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo)
            ShadowLabel(new Rect(rect.x+12,rect.y+75,254,15),campaign.BambooVariantName,small);
    }

    private string ControlHint()
    {
        if(campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Preparation||campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Ascension)
            return Sequence()+" · WASD di chuyển · Tab nhiệm vụ · F1 trợ giúp";
        if(campaign.CarriedWeapon!=null||campaign.CurrentWeapon==ThanhGiongCampaignController.Weapon.Bamboo) return "Chuột phải ngắm · Trái phóng · Giữa xoay · V đổi vai · F1 trợ giúp";
        if(campaign.Heat01>=1||nearInteraction) return CompactStatus()+" · C/Alt né · F1 trợ giúp";
        return "WASD đi · Phải ngắm · Giữa xoay · V đổi vai · E/L tương tác · F1 trợ giúp";
    }

    private static string ShortNotice(string text)
    {
        return text.Replace('\n',' ').Replace('\r',' ').Trim(); // never cut text off: the notice box wraps instead
    }

    private void DrawPauseButtons(Rect guide)
    {
        bool wasEnabled = GUI.enabled;
        GUI.enabled = !ThanhGiongSceneTransition.IsTransitioning;
        if (GUI.Button(new Rect(guide.x + 24, guide.y + 158, 250, 32), "TIẾP TỤC  [ESC]", button)) TogglePause();
        if (GUI.Button(new Rect(guide.x + 286, guide.y + 158, 250, 32), "XEM ĐOẠN GIỚI THIỆU [F10]", button)) { TogglePause(); ThanhGiongTrailerDirector.Instance?.PlayTrailer(false); }
        if (GUI.Button(new Rect(guide.x + 24, guide.y + 200, 250, 32), "🏛️ MÀN HÌNH CHỜ", button)) { TogglePause(); ThanhGiongStartScreen.OpenWaitingScreen(); }
        if (GUI.Button(new Rect(guide.x + 286, guide.y + 200, 250, 32), campaign.openWorldCampaign!=null?"VỀ LÀNG AN TOÀN":"CHƠI LẠI TOÀN MÀN", button)) RestartCurrentMap();
        ShadowLabel(new Rect(guide.x+24,guide.y+251,512,22),"ÂM THANH",caption);
        float master=MixSlider(guide,282,"TỔNG",LegendAudioMix.Master);
        float music=MixSlider(guide,311,"NHẠC",LegendAudioMix.Music);
        float effects=MixSlider(guide,340,"HIỆU ỨNG",LegendAudioMix.Effects);
        if(!Mathf.Approximately(master,LegendAudioMix.Master)||!Mathf.Approximately(music,LegendAudioMix.Music)||!Mathf.Approximately(effects,LegendAudioMix.Effects))LegendAudioMix.SetMix(master,music,effects);
        ShadowLabel(new Rect(guide.x + 24, guide.y + 383, 512, 24), campaign.openWorldCampaign!=null?"ĐỊA DANH · MỞ BẢN ĐỒ, CƯỠI NGỰA ĐẾN VÙNG":"CHỌN CHƯƠNG", caption);
        for (int i = 0; i < ChapterScenes.Length; i++) {
            int row = i / 2, column = i % 2;
            Rect rect = new Rect(guide.x + 24 + column * 262, guide.y + 414 + row * 40, 250, 34);
            if (GUI.Button(rect, ChapterLabels[i], button)) SelectChapter(i);
        }
        int chapterRows = (ChapterScenes.Length + 1) / 2;
        Rect quitRect = new Rect(guide.x + 24, guide.y + 414 + chapterRows * 40 + 10, 512, 34);
        GUI.enabled = true; // closing the game must stay possible even during a scene fade
        if (GUI.Button(quitRect, campaign.openWorldCampaign != null ? "LƯU & THOÁT GAME" : "THOÁT GAME", button)) SaveAndQuit();
        GUI.enabled = wasEnabled;
        if (!string.IsNullOrEmpty(pauseNotice))
            ShadowLabel(new Rect(guide.x + 24, quitRect.yMax + 6, 512, 38), pauseNotice, small);
        Rect focus;
        if(PauseSelection==0)focus=new Rect(guide.x+24,guide.y+158,512,32);
        else if(PauseSelection<=2)focus=new Rect(guide.x+24+(PauseSelection-1)*262,guide.y+200,250,32);
        else if(PauseSelection<=5)focus=new Rect(guide.x+20,guide.y+273+(PauseSelection-3)*29,520,25);
        else if(PauseSelection==6+ChapterScenes.Length)focus=quitRect;
        else {int i=PauseSelection-6;focus=new Rect(guide.x+24+i%2*262,guide.y+414+i/2*40,250,34);}
        Color saved=GUI.color;GUI.color=Gold;
        GUI.DrawTexture(new Rect(focus.x-3,focus.y-2,focus.width+6,2),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(focus.x-3,focus.yMax,focus.width+6,2),Texture2D.whiteTexture);
        GUI.color=saved;
    }

    private float MixSlider(Rect guide,float y,string label,float value)
    {
        ShadowLabel(new Rect(guide.x+24,guide.y+y-3,130,22),label,small);
        float result=ThanhGiongUITheme.Slider(new Rect(guide.x+156,guide.y+y+3,328,18),value);
        ShadowLabel(new Rect(guide.x+490,guide.y+y-3,46,22),Mathf.RoundToInt(result*100)+"%",small);return result;
    }

    private string SceneTitle() => campaign.openWorldCampaign != null && campaign.openWorldCampaign.world.CurrentZone != null ? campaign.openWorldCampaign.world.CurrentZone.displayName : SceneManager.GetActiveScene().name switch {
        "LangGiongTienTuyen" => "LÀNG PHÙ ĐỔNG", "KinhThanhRenThep" => "KINH THÀNH RÈN THÉP",
        "PhaoDaiNgamQuanAn" => "PHÁO ĐÀI QUÂN ÂN", "ThungLungVuotSong" => "THUNG LŨNG VƯỢT SÔNG",
        "TranTuyenNuiSoc" => "TRẬN TUYẾN NÚI SÓC", "DinhSocHoaThanh" => "ĐỈNH SÓC HÓA THÁNH", _ => "THÁNH GIÓNG"
    };
    private string RegionSubtitle() => campaign.openWorldCampaign != null && campaign.openWorldCampaign.world.CurrentZone != null ? ThanhGiongOpenWorld.RiskDescription(campaign.openWorldCampaign.world.CurrentZone.risk) : SceneManager.GetActiveScene().name switch {
        "LangGiongTienTuyen" => "Lũy tre xanh giữ lời thề cứu nước.",
        "KinhThanhRenThep" => "Từ lửa rèn, người anh hùng lên đường.",
        "PhaoDaiNgamQuanAn" => "Phá vòng vây, mở đường về Núi Sóc.",
        "ThungLungVuotSong" => "Qua cầu gỗ, theo tiếng nước về phía trước.",
        "TranTuyenNuiSoc" => "Tre ngà quét giặc, non sông chờ bình yên.",
        "DinhSocHoaThanh" => "Gác lại giáp sắt, cưỡi ngựa về trời.",
        _ => "Theo con đường giữa những tán rừng."
    };

    private string ShortObjective() => campaign.openWorldCampaign != null ? campaign.openWorldCampaign.CurrentObjective : campaign.CurrentChapter switch {
        ThanhGiongCampaignController.Chapter.Prologue => $"Gom lương thực để Gióng lớn thành tráng sĩ. {Mathf.RoundToInt(campaign.Food)}/{campaign.foodPerGrowth * 3f}.",
        ThanhGiongCampaignController.Chapter.Preparation => $"Nhận giáp, nón và ngựa vua ban. Bước {Mathf.Min(campaign.EquipmentStep + 1, 4)}/4.",
        ThanhGiongCampaignController.Chapter.Battle when campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.None => "Thương đã gãy. Đến khóm tre sáng, nhấn E ba lần để nhổ tre.",
        ThanhGiongCampaignController.Chapter.Battle => $"Dẹp quân Ân. Áp lực {Mathf.RoundToInt(campaign.BattlePressure01*100)}%; dùng đúng loại tre để phá vòng vây.",
        ThanhGiongCampaignController.Chapter.Ascension => "Đặt giáp, nón trên đỉnh Sóc. Cưỡi ngựa về trời.",
        _ => "Hoàn thành đại nghiệp cứu quốc. Non sông thái bình."
    };

    private string Sequence() => campaign.CurrentChapter switch {
        ThanhGiongCampaignController.Chapter.Preparation => campaign.EquipmentBusy ? "ĐANG TRANG BỊ…" : "E  →  Q  →  E  →  F",
        ThanhGiongCampaignController.Chapter.Ascension => campaign.EquipmentBusy ? "ĐANG ĐẶT TRANG BỊ…" : "E  →  Q  →  E",
        _ => "TAB: XEM LẠI NHIỆM VỤ"
    };

    private string CompactStatus()
    {
        if (campaign.CarriedWeapon != null) return "ĐÁ / GỐC CÂY  ·  CHUỘT TRÁI ĐỂ NÉM";
        if (campaign.Heat01 >= 1) return "HỎA KHÍ ĐẦY  ·  NHẤN F PHUN LỬA";
        return campaign.CurrentWeapon switch {
            ThanhGiongCampaignController.Weapon.IronSword => "THƯƠNG SẮT  ·  QUÉT SẠCH QUÂN ÂN",
            ThanhGiongCampaignController.Weapon.Bamboo => campaign.BambooVariantName.ToUpperInvariant()+"  ·  QUÉT 360°",
            _ => "E: NHẶT / TƯƠNG TÁC"
        };
    }

    private void DrawBoss(float w)
    {
        if (!campaign.IsBattleActive) return;
        if (Time.time >= nextBossCheck) {
            nextBossCheck = Time.time + 1;
            if (cachedBoss == null || !cachedBoss.gameObject.activeInHierarchy || cachedBoss.HealthRatio <= 0) {
                cachedBoss = null;
                foreach (ThanhGiongEnemy enemy in FindObjectsByType<ThanhGiongEnemy>())
                    if (enemy.isBoss && enemy.HealthRatio > 0) { cachedBoss = enemy; break; }
            }
        }
        if (cachedBoss == null || !cachedBoss.gameObject.activeInHierarchy || cachedBoss.HealthRatio <= 0) return;
        Rect rect = new Rect((w - 560) / 2, 14, 560, 64);
        var bossName = new GUIStyle(hudTitle) { alignment = TextAnchor.MiddleCenter };
        ShadowLabel(new Rect(rect.x, rect.y, rect.width, 26), cachedBoss.IsStuckInGround ? "TƯỚNG ÂN · ĐAO MẮC KẸT!" : "TƯỚNG GIẶC ÂN", bossName);
        DrawEnchantedHealthBar(new Rect(rect.x, rect.y + 30, rect.width, 22), cachedBoss.HealthRatio, true,
            Mathf.CeilToInt(cachedBoss.HealthRatio * cachedBoss.maxHealth) + " / " + Mathf.CeilToInt(cachedBoss.maxHealth));
    }

    private void Box(Rect rect,bool expanded=false)
    {
        // Text-only HUD: panels draw no background box or border rules.
    }

    private void ThinBar(Rect rect,float value,Color fill)
    {
        Color previous=GUI.color;
        GUI.color=new Color(.09f,.045f,.035f,.6f);GUI.DrawTexture(rect,Texture2D.whiteTexture);
        GUI.color=fill;GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width*Mathf.Clamp01(value),rect.height),Texture2D.whiteTexture);
        GUI.color=previous;
    }

    private void DrawEnchantedHealthBar(Rect rect, float value, bool boss, string label = null)
    {
        // Lacquer health bar: dark track, gold hairline frame, fill that shifts green -> amber -> red,
        // a soft top highlight and the exact numbers centred on the bar.
        value = Mathf.Clamp01(value);
        Color saved = GUI.color; float a = saved.a;
        GUI.color = new Color(.04f, .02f, .02f, .88f * a);
        GUI.DrawTexture(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Texture2D.whiteTexture);
        GUI.color = new Color(.24f, .17f, .10f, a);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        Color fill = boss ? Color.Lerp(new Color(.62f, .10f, .08f), new Color(.92f, .28f, .14f), value)
            : value > .5f ? Color.Lerp(new Color(.95f, .72f, .22f), new Color(.40f, .74f, .34f), (value - .5f) * 2f)
            : Color.Lerp(new Color(.82f, .16f, .12f), new Color(.95f, .72f, .22f), value * 2f);
        Rect fillRect = new Rect(rect.x, rect.y, rect.width * value, rect.height);
        GUI.color = new Color(fill.r, fill.g, fill.b, a);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, .18f * a);
        GUI.DrawTexture(new Rect(fillRect.x, fillRect.y, fillRect.width, rect.height * .35f), Texture2D.whiteTexture);
        GUI.color = new Color(Gold.r, Gold.g, Gold.b, .85f * a);
        GUI.DrawTexture(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x - 2, rect.yMax + 1, rect.width + 4, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x - 2, rect.y - 2, 1, rect.height + 4), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax + 1, rect.y - 2, 1, rect.height + 4), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, a);
        if (!string.IsNullOrEmpty(label)) {
            var numbers = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, normal = { textColor = Paper } };
            ShadowLabel(rect, label, numbers);
        }
        GUI.color = saved;
    }

    private void Bar(Rect rect, float value, Color fill, string label)
    {
        Color previous = GUI.color;
        GUI.color = new Color(.09f, .045f, .035f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = fill; GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * Mathf.Clamp01(value), rect.height - 4), Texture2D.whiteTexture);
        GUI.color = previous;
        ShadowLabel(new Rect(rect.x + 8, rect.y + 2, rect.width - 16, rect.height), label + "  " + Mathf.RoundToInt(value * 100) + "%", small);
    }

    private void OnDisable()
    {
        ReleaseAdventureHUD();
        if (paused) { Time.timeScale = 1; paused = false; }
    }
}
