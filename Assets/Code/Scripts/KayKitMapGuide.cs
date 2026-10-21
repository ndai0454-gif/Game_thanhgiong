using UnityEngine;

public class KayKitMapGuide : MonoBehaviour
{
    public string mapTitle;
    public string description;
    public string nextScene;
    [Header("Kenney Fantasy UI")]
    public Texture2D panelFrame;
    public Texture2D dividerTexture;
    [Tooltip("Bật khi cần xem bảng thông tin map demo cũ.")]
    public bool showLegacyPanel;

    private GUIStyle title;
    private GUIStyle body;
    private GUIStyle frame;
    private bool legacyPanelVisible;

    private void Update()
    {
        if (showLegacyPanel && Input.GetKeyDown(KeyCode.F1)) legacyPanelVisible = !legacyPanelVisible;
        if (!string.IsNullOrEmpty(nextScene) && Input.GetKeyDown(KeyCode.N))
            ThanhGiongSceneTransition.TryTransitionTo(nextScene, "ĐANG CHUYỂN ĐẾN MÀN TIẾP THEO...");
    }

    private void OnGUI()
    {
        if (!showLegacyPanel || !legacyPanelVisible) return;
        if (title == null)
        {
            Font bold = ThanhGiongUITheme.Medium;
            Font regular = ThanhGiongUITheme.Regular;
            title = new GUIStyle(GUI.skin.label) { font = bold, fontSize = 24, fontStyle = bold == null ? FontStyle.Bold : FontStyle.Normal };
            title.normal.textColor = new Color(1f, .78f, .18f);
            body = new GUIStyle(GUI.skin.label) { font = regular, fontSize = 16, wordWrap = true };
            body.normal.textColor = Color.white;
            frame = new GUIStyle(GUI.skin.box);
            frame.normal.background = panelFrame;
            frame.border = new RectOffset(8, 8, 8, 8);
        }
        Rect panel = new Rect(18f, 18f, Mathf.Min(440f, Screen.width - 36f), 120f);
        GUI.color = new Color(.04f, .055f, .08f, .92f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(1f, .8f, .3f, .9f);
        GUI.Box(panel, GUIContent.none, frame);
        GUI.color = Color.white;
        GUI.Label(new Rect(32f, 26f, 410f, 34f), mapTitle, title);
        GUI.Label(new Rect(32f, 60f, 410f, 48f), description, body);
        if (dividerTexture != null)
        {
            GUI.color = new Color(1f, .82f, .35f, .65f);
            GUI.DrawTexture(new Rect(32f, 101f, panel.width - 28f, 3f), dividerTexture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
        }
        GUI.Label(new Rect(32f, 106f, panel.width - 28f, 22f), "WASD: di chuyển · Shift: chạy · Chuột trái: đánh · Space: nhảy · N: map tiếp theo", body);
    }
}
