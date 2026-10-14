using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MountedCombatVerificationMenu
{
    const string Flag = "ThanhGiong.VerifyMountedCombat";
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.playModeStateChanged -= OnPlay;
        EditorApplication.playModeStateChanged += OnPlay;
    }
    [MenuItem("Tools/Thánh Gióng/Meshy/Verify Mounted Combat")]
    static void Verify()
    {
        if (EditorApplication.isPlaying) { new GameObject("Mounted Combat Verification").AddComponent<MountedCombatRuntimeVerification>(); return; }
        EditorSceneManager.SaveOpenScenes();
        SessionState.SetBool(Flag, true);
        // Retest the current campaign map so the same scenario can cover its real groves.
        if (!EditorSceneManager.GetActiveScene().path.StartsWith("Assets/Scenes/ThanhGiongWorld/"))
            EditorSceneManager.OpenScene("Assets/Scenes/ThanhGiongWorld/LangGiongTienTuyen.unity");
        EditorApplication.isPlaying = true;
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
        SessionState.SetBool(Flag, false);
        new GameObject("Mounted Combat Verification").AddComponent<MountedCombatRuntimeVerification>();
    }
}
