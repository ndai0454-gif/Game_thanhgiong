using UnityEngine;

// Four narrative chapters share the existing six-scene campaign and actual game state.
public partial class ThanhGiongCampaignHUD
{
    private GUIStyle chapterHeading, chapterKicker, chapterCentered, chapterAction, chapterSubtitle;
    private static readonly string[] NarrativeTitles = {
        "TIẾNG GỌI PHÙ ĐỔNG", "LỬA RÈN VÀNG SON", "TRE NGÀ GIỮ NƯỚC", "DẤU CHÂN BẤT TỬ"
    };
    private static readonly string[] EquipmentKeys = { "E", "Q", "E", "F" };
    private static readonly string[] EquipmentNames = { "Mặc giáp", "Đội nón", "Lên ngựa", "Thử lửa" };
    private static readonly string[] AscensionKeys = { "E", "Q", "E" };
    private static readonly string[] AscensionNames = { "Đặt giáp", "Đặt nón", "Về trời" };

    public int NarrativeChapterIndex => campaign == null ? 0 : Mathf.Clamp((int)campaign.CurrentChapter,0,3);
    public string NarrativeChapterTitle => NarrativeTitles[NarrativeChapterIndex];
    public bool ChapterRitualVisible => campaign != null && !campaign.IsDead &&
        (campaign.CurrentChapter == ThanhGiongCampaignController.Chapter.Preparation ||
         campaign.CurrentChapter == ThanhGiongCampaignController.Chapter.Ascension);

    private Color ChapterAccent => NarrativeChapterIndex switch {
        0 => new Color(.62f,.67f,.47f),
        1 => new Color(.74f,.61f,.41f),
        2 => new Color(.70f,.43f,.34f),
        _ => new Color(.61f,.69f,.65f)
    };

    private void EnsureChapterStyles()
    {
        if(chapterHeading != null) return;
        chapterHeading=Style(42,Paper); chapterHeading.font=ThanhGiongUITheme.Light; chapterHeading.alignment=TextAnchor.MiddleCenter;
        chapterKicker=Style(16,Gold); chapterKicker.alignment=TextAnchor.MiddleCenter;
        chapterCentered=Style(18,Muted); chapterCentered.alignment=TextAnchor.MiddleCenter;
        chapterAction=Style(20,Paper,true); chapterAction.alignment=TextAnchor.MiddleCenter;
        chapterSubtitle=Style(15,AncientPaper); chapterSubtitle.alignment=TextAnchor.UpperCenter; chapterSubtitle.wordWrap=true;
    }

    private void DrawChapterHUD(float w,float h)
    {
        EnsureChapterStyles();
        Color saved=GUI.color;
        Color accent=ChapterAccent;
        // Right anchor leaves the actor and the existing upper-left vitality/skills clear.
        Rect quest=new Rect(w-314,104,294,MissionVisible?152:91);
        DrawMysticPanel(quest, Obsidian, MutedBronze, true);
        GUI.color=saved;

        ShadowLabel(new Rect(quest.x+14,quest.y+9,265,20),$"❖ HỒI {NarrativeChapterIndex+1:00} · {NarrativeChapterTitle} ❖",caption);
        for(int i=0;i<4;i++) {
            bool active = i == NarrativeChapterIndex;
            bool passed = i < NarrativeChapterIndex;
            Color mark = active ? Gold : (passed ? accent : new Color(MutedBronze.r, MutedBronze.g, MutedBronze.b, 0.4f));
            float thickness = active ? 2.5f : 1.5f;
            DrawHudLine(new Vector2(quest.x+15+i*66,quest.y+35),new Vector2(quest.x+71+i*66,quest.y+35),mark,thickness);
        }
        ShadowLabel(new Rect(quest.x+14,quest.y+43,265,24),ChapterProgressLabel(),hudBody);
        ThinBar(new Rect(quest.x+15,quest.y+73,264,4),campaign.StageCompletion01,accent);
        if(MissionVisible) {
            ShadowLabel(new Rect(quest.x+14,quest.y+85,265,42),ShortObjective(),hudBody);
            ShadowLabel(new Rect(quest.x+14,quest.y+130,265,18),"TAB nhắc nhiệm vụ   ·   ESC tạm dừng",small);
        }
        if(RegionIntroVisible) {
            float remaining = regionUntil - Time.time;
            float elapsed = regionPopupSeconds - remaining;
            float a = Mathf.Clamp01(elapsed / 0.6f) * Mathf.Clamp01(remaining / 0.6f);
            a = a * a * (3f - 2f * a);
            if (a > 0.001f) {
                string sub = RegionSubtitle();
                float subWidth = 780f;
                float subHeight = Mathf.Max(26f, chapterSubtitle.CalcHeight(new GUIContent(sub), subWidth));
                float bannerWidth = 840f;
                float bannerHeight = 132f + subHeight;
                float startY = h * 0.38f - 40f;

                // Black Myth cinematic chapter banner
                Rect bannerRect = new Rect((w - bannerWidth)/2, startY - 14, bannerWidth, bannerHeight);
                DrawMysticPanel(bannerRect, new Color(Obsidian.r, Obsidian.g, Obsidian.b, a * 0.92f), new Color(MutedBronze.r, MutedBronze.g, MutedBronze.b, a * 0.70f), true);

                GUI.color = new Color(Gold.r, Gold.g, Gold.b, a);
                ShadowLabel(new Rect((w-620)/2, startY, 620, 22), $"❖   HỒI {NarrativeChapterIndex+1:00}   ·   {NarrativeChapterTitle}   ❖", chapterKicker);

                GUI.color = new Color(Paper.r, Paper.g, Paper.b, a);
                ShadowLabel(new Rect((w-780)/2, startY + 24, 780, 56), SceneTitle(), chapterHeading);

                float lineY = startY + 86f;
                Color lineCol = new Color(accent.r, accent.g, accent.b, a * 0.85f);
                Color goldCol = new Color(Gold.r, Gold.g, Gold.b, a);
                DrawHudLine(new Vector2(w*.5f-240, lineY), new Vector2(w*.5f-18, lineY), lineCol, 1.5f);
                DrawHudLine(new Vector2(w*.5f+18, lineY), new Vector2(w*.5f+240, lineY), lineCol, 1.5f);
                // Calligraphic diamond center crest
                DrawHudLine(new Vector2(w*.5f, lineY-5), new Vector2(w*.5f+6, lineY), goldCol, 1.8f);
                DrawHudLine(new Vector2(w*.5f+6, lineY), new Vector2(w*.5f, lineY+5), goldCol, 1.8f);
                DrawHudLine(new Vector2(w*.5f, lineY+5), new Vector2(w*.5f-6, lineY), goldCol, 1.8f);
                DrawHudLine(new Vector2(w*.5f-6, lineY), new Vector2(w*.5f, lineY-5), goldCol, 1.8f);

                GUI.color = new Color(AncientPaper.r, AncientPaper.g, AncientPaper.b, a * 0.95f);
                ShadowLabel(new Rect((w-subWidth)/2, lineY + 10, subWidth, subHeight + 4), sub, chapterSubtitle);
            }
        }
        GUI.color=saved;
        if(ChapterRitualVisible) DrawChapterRitual(w,h);
    }

    private string ChapterProgressLabel() => campaign.CurrentChapter switch {
        ThanhGiongCampaignController.Chapter.Prologue => $"Lương thực {campaign.Food:0} / {campaign.foodPerGrowth*3:0}  ·  Lớn {campaign.GrowthPhase+1}/4",
        ThanhGiongCampaignController.Chapter.Preparation => $"Trang bị vua ban  ·  {Mathf.Min(campaign.EquipmentStep,4)}/4",
        ThanhGiongCampaignController.Chapter.Battle when campaign.CurrentWeapon==ThanhGiongCampaignController.Weapon.None => $"THƯƠNG GÃY  ·  Nhổ tre {campaign.BambooPullStep}/3",
        ThanhGiongCampaignController.Chapter.Battle => $"Dẹp quân Ân  ·  {campaign.Kills}/{campaign.RequiredBattleKills}",
        ThanhGiongCampaignController.Chapter.Ascension => $"Nghi thức đỉnh Sóc  ·  {Mathf.Min(campaign.EquipmentStep,3)}/3",
        _ => "NON SÔNG THÁI BÌNH"
    };

    private void DrawChapterRitual(float w,float h)
    {
        bool preparation=campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Preparation;
        string[] keys=preparation?EquipmentKeys:AscensionKeys;
        string[] labels=preparation?EquipmentNames:AscensionNames;
        int step=Mathf.Clamp(campaign.EquipmentStep,0,keys.Length);
        float width=keys.Length*114f+28;
        Rect panel=new Rect((w-width)/2,h-151,width,119);
        Color saved=GUI.color;
        DrawMysticPanel(panel, Obsidian, MutedBronze, true);
        GUI.color=saved;
        string ritualTitle = campaign.EquipmentBusy ? "❖  CHỜ ĐỘNG TÁC HOÀN TẤT  ❖" : preparation ? "❖  TRANG BỊ VUA BAN  ❖" : "❖  NGHI THỨC HÓA THÁNH  ❖";
        ShadowLabel(new Rect(panel.x+10,panel.y+7,width-20,22),ritualTitle,chapterKicker);
        for(int i=0;i<keys.Length;i++) {
            float x=panel.x+14+i*114;
            Color color=i==step?ChapterAccent:i<step?Gold:Muted;
            DrawHudArc(new Vector2(x+50,panel.y+55),19,1,color,i==step?2:1);
            if(i<keys.Length-1) DrawHudLine(new Vector2(x+74,panel.y+55),new Vector2(x+140,panel.y+55),MutedBronze,1);
            ShadowLabel(new Rect(x+31,panel.y+38,38,32),i<step?"❖":keys[i],chapterAction);
            ShadowLabel(new Rect(x,panel.y+82,100,24),labels[i],chapterCentered);
        }
        GUI.color=saved;
    }
}
