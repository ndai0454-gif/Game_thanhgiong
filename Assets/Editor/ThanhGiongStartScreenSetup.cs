using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ThanhGiongStartScreenSetup
{
    public const string ScenePath = "Assets/Scenes/ThanhGiongWorld/ManHinhCho.unity";

    [MenuItem("Tools/Thanh Giong/Tạo Màn Hình Chờ (ManHinhCho Scene)")]
    public static void CreateStartScreenScene()
    {
        Debug.Log("[ThanhGiong] Bắt đầu tạo màn hình chờ ManHinhCho.unity...");

        // Tạo scene rỗng
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Camera chính
        GameObject camGo = new GameObject("Main Camera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.015f, 0.085f, 0.08f, 1f);
        camGo.AddComponent<AudioListener>();
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0, 1.5f, -10f);
        camGo.transform.rotation = Quaternion.identity;

        // 2. Ánh sáng mặt trời
        GameObject lightGo = new GameObject("Directional Light");
        Light dirLight = lightGo.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(1f, 0.93f, 0.82f);
        dirLight.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        // 3. Start Screen Host
        GameObject startGo = new GameObject("ThanhGiong_StartScreenHost");
        startGo.AddComponent<ThanhGiongStartScreen>();

        // Đảm bảo thư mục tồn tại và lưu Scene
        string dir = Path.GetDirectoryName(ScenePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[ThanhGiong] Màn hình chờ ManHinhCho.unity đã được tạo thành công tại: " + ScenePath);
    }

    public static void CreateStartScreenSceneBatch()
    {
        CreateStartScreenScene();
        EditorApplication.Exit(0);
    }
}
