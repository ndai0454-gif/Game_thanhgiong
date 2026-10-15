using UnityEngine;

/// <summary>Standalone controls for the generated general's exact-skeleton ActionForge clips.</summary>
public sealed class GiacAnGeneralShowcase : MonoBehaviour
{
    public static readonly string[] StateNames = {
        "sword_idle", "sword_attack", "sword_block", "sword_heavy_combo",
        "sword_regular_a", "sword_regular_b", "walk_loop", "jog_fwd_loop",
        "sprint_loop", "hit_chest", "death01"
    };

    [SerializeField] private Animator targetAnimator;
    [SerializeField] private bool autoCycle = true;
    [SerializeField, Min(1f)] private float minimumPreviewSeconds = 3f;
    private int selectedState;
    private float nextChangeTime;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;

    public Animator TargetAnimator { get => targetAnimator; set => targetAnimator = value; }
    public string CurrentState => StateNames[selectedState];

    private void Awake()
    {
        if (targetAnimator == null) targetAnimator = GetComponentInChildren<Animator>();
        if (targetAnimator != null) targetAnimator.applyRootMotion = false;
    }

    private void Start() => SelectState(0);

    private void Update()
    {
        if (autoCycle && targetAnimator != null && Time.time >= nextChangeTime)
            SelectState((selectedState + 1) % StateNames.Length);
    }

    public void SelectState(int index)
    {
        if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null) return;
        int candidate = Mathf.Clamp(index, 0, StateNames.Length - 1);
        int hash = Animator.StringToHash("Base Layer." + StateNames[candidate]);
        if (!targetAnimator.HasState(0, hash)) return;
        selectedState = candidate;
        targetAnimator.Play(hash, 0, 0f);
        targetAnimator.Update(0f);
        nextChangeTime = Time.time + Mathf.Max(minimumPreviewSeconds,
            targetAnimator.GetCurrentAnimatorStateInfo(0).length + 0.8f);
    }

    private void OnGUI()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        }
        float scale = Mathf.Max(0.6f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        GUILayout.BeginArea(new Rect(20, 18, 272, 650), GUI.skin.box);
        GUILayout.Label("GIAC AN GENERAL", titleStyle);
        GUILayout.Label("Meshy model / ActionForge motion", labelStyle);
        GUILayout.Space(8);
        GUILayout.Label("Current: " + CurrentState, labelStyle);
        autoCycle = GUILayout.Toggle(autoCycle, "Automatically preview all 11 clips");
        if (GUILayout.Button("Restart current clip", GUILayout.Height(26))) SelectState(selectedState);
        GUILayout.Space(6);
        for (int i = 0; i < StateNames.Length; i++)
            if (GUILayout.Button((selectedState == i ? "> " : "") + StateNames[i], GUILayout.Height(28)))
            {
                autoCycle = false;
                SelectState(i);
            }
        GUILayout.EndArea();
        GUI.matrix = previousMatrix;
    }
}
