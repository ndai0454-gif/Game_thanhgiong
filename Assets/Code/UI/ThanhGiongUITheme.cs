using UnityEngine;

/// <summary>Đông Hồ / sơn mài typography and palette shared by the HUD, map and scene overlays.</summary>
public static class ThanhGiongUITheme
{
    // Bảng màu Đông Hồ: nền sơn mài nâu đen, giấy điệp, vàng kim, đỏ son, xanh ngọc.
    public static readonly Color Ink = new Color(.105f, .055f, .045f, .92f);
    public static readonly Color Paper = new Color(.99f, .94f, .80f);
    public static readonly Color Muted = new Color(.84f, .75f, .60f);
    public static readonly Color Brass = new Color(.96f, .74f, .24f);
    public static readonly Color Moss = new Color(.27f, .62f, .52f);
    public static readonly Color Vermilion = new Color(.80f, .17f, .12f);
    /// <summary>Be Vietnam Pro có x-height lớn hơn Cormorant, nên thu cỡ chữ để giữ bố cục cũ.</summary>
    public const float FontScale = .86f;
    private static Font regular, medium, light;
    private static Texture2D normal, hover, pressed;
    private static GUIStyle slider, sliderThumb;
    // Regular/Medium = chữ thân (Be Vietnam Pro, đủ dấu tiếng Việt); Light = chữ tiêu đề (Old Standard Bold).
    public static Font Regular => regular != null ? regular : regular = Load("BeVietnamPro-Medium", "CormorantGaramond-Regular");
    public static Font Medium => medium != null ? medium : medium = Load("BeVietnamPro-Bold", "CormorantGaramond-Medium");
    public static Font Light => light != null ? light : light = Load("OldStandard-Bold", "CormorantGaramond-Light");
    private static Font Load(string preferred, string fallback)
    {
        var f = Resources.Load<Font>("ThanhGiongUI/" + preferred);
        return f != null ? f : Resources.Load<Font>("ThanhGiongUI/" + fallback);
    }

    public static float Slider(Rect rect, float value)
    {
        if (slider == null)
        {
            slider = new GUIStyle(GUIStyle.none) { fixedHeight = 2, margin = new RectOffset(0, 0, 7, 0) };
            slider.normal.background = Flat(new Color(.36f, .20f, .12f));
            sliderThumb = new GUIStyle(GUIStyle.none) { fixedWidth = 7, fixedHeight = 16 };
            sliderThumb.normal.background = Flat(Brass);
            sliderThumb.hover.background = sliderThumb.normal.background;
            sliderThumb.active.background = sliderThumb.normal.background;
        }
        return GUI.HorizontalSlider(rect, value, 0, 1, slider, sliderThumb);
    }

    private static Texture2D Flat(Color color)
    {
        var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color);
        texture.Apply(false, true);
        return texture;
    }

    public static GUIStyle Button(int size = 17)
    {
        if (normal == null) normal = Flat(new Color(.20f, .07f, .05f, .92f));
        if (hover == null) hover = Flat(new Color(.55f, .14f, .09f, .98f));
        if (pressed == null) pressed = Flat(new Color(.12f, .04f, .03f, 1f));
        var result = new GUIStyle(GUI.skin.button) {
            font = Medium, fontSize = Mathf.RoundToInt(size * FontScale), fontStyle = FontStyle.Normal,
            wordWrap = true, border = new RectOffset(), padding = new RectOffset(10, 10, 3, 3)
        };
        result.normal.background = normal; result.normal.textColor = Paper;
        result.hover.background = hover; result.hover.textColor = Paper;
        result.active.background = pressed; result.active.textColor = Brass;
        result.focused.background = hover; result.focused.textColor = Paper;
        result.onNormal = result.normal; result.onHover = result.hover;
        result.onActive = result.active; result.onFocused = result.focused;
        return result;
    }

        /// <summary>Text-only HUD helper: dark outline keeps text readable without a panel behind it.</summary>
        public static void ShadowLabel(Rect rect, string text, GUIStyle style)
        {
            Color keep = GUI.color;
            GUI.color = new Color(.05f, .02f, .02f, keep.a * .85f);
            GUI.Label(new Rect(rect.x - 1, rect.y, rect.width, rect.height), text, style);
            GUI.Label(new Rect(rect.x + 1, rect.y, rect.width, rect.height), text, style);
            GUI.Label(new Rect(rect.x, rect.y - 1, rect.width, rect.height), text, style);
            GUI.Label(new Rect(rect.x, rect.y + 1, rect.width, rect.height), text, style);
            GUI.color = keep;
            GUI.Label(rect, text, style);
        }
    }
