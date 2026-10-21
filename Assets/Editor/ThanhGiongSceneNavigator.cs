using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ThanhGiongSceneNavigator
{
    private const string Root = "Assets/Scenes/ThanhGiongWorld/";
    private const string AutoOpenKey = "ThanhGiong.SceneNavigator.Opened.DinhSoc.2026-10-02-v4";

    [InitializeOnLoadMethod]
    private static void OpenRequestedSceneOnce()
    {
        if (EditorPrefs.GetBool(AutoOpenKey, false)) return;
        EditorApplication.delayCall += TryOpenRequestedScene;
    }

    private static void TryOpenRequestedScene()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
        {
            EditorApplication.delayCall += TryOpenRequestedScene;
            return;
        }

        EditorPrefs.SetBool(AutoOpenKey, true);
        OpenScene("DinhSocHoaThanh");
    }

    [MenuItem("Tools/Thanh Giong/Open Map/0 - Màn Hình Chờ (Lobby)")]
    private static void OpenStartScreen() => OpenScene("ManHinhCho");

    [MenuItem("Tools/Thanh Giong/Open Map/1 - Làng Gióng")]
    private static void OpenVillage() => OpenScene("LangGiongTienTuyen");

    [MenuItem("Tools/Thanh Giong/Open Map/2 - Kinh Thành Rèn Thép")]
    private static void OpenForge() => OpenScene("KinhThanhRenThep");

    [MenuItem("Tools/Thanh Giong/Open Map/3 - Pháo Đài Ngầm")]
    private static void OpenDungeon() => OpenScene("PhaoDaiNgamQuanAn");

    [MenuItem("Tools/Thanh Giong/Open Map/4 - Thung Lũng Vượt Sông")]
    private static void OpenRiver() => OpenScene("ThungLungVuotSong");

    [MenuItem("Tools/Thanh Giong/Open Map/5 - Trận Tuyến Núi Sóc")]
    private static void OpenBattlefield() => OpenScene("TranTuyenNuiSoc");

    [MenuItem("Tools/Thanh Giong/Open Map/6 - Đỉnh Sóc Hóa Thánh")]
    private static void OpenPeak() => OpenScene("DinhSocHoaThanh");

    private static void OpenScene(string sceneName)
    {
        string path = Root + sceneName + ".unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            Debug.LogError("[Thanh Giong] Scene not found: " + path);
            return;
        }

        if (!EditorSceneManager.SaveOpenScenes())
        {
            Debug.LogWarning("[Thanh Giong] Scene switch cancelled because the current scene could not be saved.");
            return;
        }

        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Debug.Log("[Thanh Giong] Opened scene: " + sceneName);
    }
}
