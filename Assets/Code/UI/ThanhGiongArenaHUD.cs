using UnityEngine;

public sealed class ThanhGiongArenaHUD : MonoBehaviour
{
    public ThanhGiongArenaFlow arena;
    ThanhGiongCampaignController campaign;
    GUIStyle title, text, small, button;

    void Awake() { if (arena != null) campaign = arena.GetComponent<ThanhGiongCampaignController>(); }
    void OnGUI()
    {
        if (arena == null || campaign == null) return;
        if (title == null)
        {
            Font font = Resources.Load<Font>("ThanhGiongUI/OldStandard-Regular");
            title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
            text = new GUIStyle(title) { fontSize = 22, fontStyle = FontStyle.Normal };
            small = new GUIStyle(text) { fontSize = 17, alignment = TextAnchor.MiddleLeft };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 24 };
        }
        GUIStyle instruction = new GUIStyle(text) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        Matrix4x4 previous = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2f, (Screen.height - 720 * scale) / 2f), Quaternion.identity, new Vector3(scale, scale, 1));
        Panel(new Rect(24, 22, 360, 105));
        GUI.Label(new Rect(40, 28, 310, 30), "THÁNH GIÓNG · PHÙ ĐỔNG", small);
        Fill(new Rect(40, 66, 316, 22), new Color(.15f, .08f, .06f));
        Fill(new Rect(40, 66, 316 * campaign.Health01, 22), new Color(.68f, .2f, .15f));
        GUI.Label(new Rect(40, 90, 320, 26), $"Máu {Mathf.CeilToInt(campaign.Health)}/{campaign.maxHealth:0}   ·   {Mathf.FloorToInt(arena.ElapsedSeconds / 60):00}:{Mathf.FloorToInt(arena.ElapsedSeconds % 60):00}", small);
        Panel(new Rect(420, 22, 830, 72));
        GUI.Label(new Rect(435, 25, 800, 60), arena.Objective, text);
        if (arena.CanFight)
        {
            var wave = arena.waveManager;
            GUI.Label(new Rect(435, 96, 800, 30), $"Đợt {wave.WaveIndex + 1}/3   ·   Đã xuất hiện {wave.Spawned}/{wave.waves[wave.WaveIndex].count}   ·   Còn {wave.AliveCount} giặc", small);
        }
        Panel(new Rect(100, 664, 1080, 38));
        GUI.Label(new Rect(110, 666, 1060, 32), "WASD: đi   Shift: chạy   Chuột trái: đánh   C: né   E: nhổ tre   Chuột phải: xoay góc nhìn   Esc: tạm dừng", instruction);
        if (arena.CurrentStage == ThanhGiongArenaFlow.Stage.SeekingBamboo && !arena.NearBamboo)
        {
            Vector3 p = Camera.main != null ? Camera.main.WorldToScreenPoint(arena.bamboo.transform.position + Vector3.up * 4) : Vector3.zero;
            if (p.z > 0) GUI.Label(new Rect((p.x - (Screen.width - 1280 * scale) / 2) / scale - 75, (Screen.height - p.y - (Screen.height - 720 * scale) / 2) / scale, 150, 40), "KHÓM TRE · E", text);
            GUI.Label(new Rect(400, 580, 700, 35), $"Cách khóm tre {Vector3.Distance(arena.transform.position, arena.bamboo.transform.position):0} m", text);
        }
        bool overlay = arena.Paused || arena.CurrentStage == ThanhGiongArenaFlow.Stage.Intro || arena.CurrentStage == ThanhGiongArenaFlow.Stage.Victory || arena.CurrentStage == ThanhGiongArenaFlow.Stage.Defeat;
        if (overlay)
        {
            Panel(new Rect(320, 190, 640, 340));
            string heading = arena.Paused ? "TẠM DỪNG" : arena.CurrentStage == ThanhGiongArenaFlow.Stage.Intro ? "THÁNH GIÓNG XUẤT TRẬN" : arena.CurrentStage == ThanhGiongArenaFlow.Stage.Victory ? "CHIẾN THẮNG" : "THỬ LẠI TRẬN CHIẾN";
            GUI.Label(new Rect(345, 210, 590, 55), heading, title);
            string detail = arena.CurrentStage == ThanhGiongArenaFlow.Stage.Intro ? "Đánh ba đợt giặc Ân. Khi roi sắt gãy, đến khóm tre và nhấn E ba lần để tiếp chiến." : arena.Objective;
            GUI.Label(new Rect(360, 278, 560, 95), detail, text);
            if (arena.CurrentStage == ThanhGiongArenaFlow.Stage.Intro)
            {
                if (GUI.Button(new Rect(440, 405, 400, 60), "XUẤT TRẬN · ENTER", button)) arena.BeginCombat();
            }
            else if (arena.Paused)
            {
                if (GUI.Button(new Rect(365, 405, 260, 60), "TIẾP TỤC", button)) arena.SetPaused(false);
                if (GUI.Button(new Rect(650, 405, 260, 60), "CHƠI LẠI · R", button)) arena.RestartRound();
            }
            else if (GUI.Button(new Rect(440, 405, 400, 60), "CHƠI LẠI · R", button)) arena.RestartRound();
        }
        GUI.matrix = previous;
    }
    static void Panel(Rect rect) => Fill(rect, new Color(.025f, .09f, .075f, .91f));
    static void Fill(Rect rect, Color color) { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
}
