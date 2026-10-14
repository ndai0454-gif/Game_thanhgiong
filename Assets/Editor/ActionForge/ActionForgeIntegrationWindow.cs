using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThanhGiong.EditorTools
{
    public class ActionForgeIntegrationWindow : EditorWindow
    {
        const string KnightGlb = "Assets/Plugins/KayKit_Adventurers_2.0_FREE/KayKit_Adventurers_2.0_FREE/Characters/gltf/Knight.glb";
        const string MeshyGlb = "Assets/Art/Meshy/ThanhGiong_Meshy.glb";
        string report;
        Vector2 scroll;

        [MenuItem("Tools/Thánh Gióng/ActionForge Animation Studio", false, 10)]
        public static void Open()
        {
            var window = GetWindow<ActionForgeIntegrationWindow>("ActionForge Studio");
            window.minSize = new Vector2(560, 480);
            window.Show();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            bool meshy = File.Exists(MeshyGlb);
            EditorGUILayout.LabelField("ActionForge → Thánh Gióng", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Tạo/chỉnh animation trên web, xuất FBX trên bộ xương của nhân vật đã chọn, nhập và chuyển vào Unity.", MessageType.Info);
            if (GUILayout.Button("1. Mở Sword Attack trên ActionForge", GUILayout.Height(32)))
                Application.OpenURL("https://actionforge.app/#clip=sword_attack");
            if (GUILayout.Button(meshy ? "2. Hiển thị Thánh Gióng Meshy để chọn trên web" : "2. Hiển thị Knight.glb để chọn trên web", GUILayout.Height(28)))
                EditorUtility.RevealInFinder(Path.GetFullPath(meshy ? MeshyGlb : KnightGlb));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Thiết lập trên ActionForge", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Character → + Rigged character → chọn model vừa hiển thị.\n" +
                "Keep its skeleton → kiểm tra mapping → Done.\n" +
                "Chọn Sword Attack; Format = FBX; Units = Unity; In place = bật.\n" +
                "Xuất trên Own skeleton của nhân vật. File mannequin mặc định có xương khác.\n" +
                "ActionForge xử lý nhân vật cục bộ trong trình duyệt.", MessageType.None);
            if (GUILayout.Button("3. Nhập FBX vừa xuất…", GUILayout.Height(32)))
            {
                string file = EditorUtility.OpenFilePanel("Chọn FBX từ ActionForge", System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) + "/Downloads", "fbx");
                if (!string.IsNullOrEmpty(file))
                {
                    Directory.CreateDirectory(ActionForgeAnimationBridge.Folder);
                    string destination = ActionForgeAnimationBridge.Folder + "/" + (meshy ? "Meshy_ActionForge.fbx" : Path.GetFileName(file));
                    if (Path.GetFullPath(file) != Path.GetFullPath(destination))
                    {
                        if (File.Exists(destination))
                        {
                            Directory.CreateDirectory("MeshyVerification/AnimationBackups");
                            File.Copy(destination, "MeshyVerification/AnimationBackups/" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Path.GetFileName(destination));
                        }
                        File.Copy(file, destination, true);
                    }
                    AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                    report = "Đã nhập " + destination;
                }
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                if (GUILayout.Button(meshy ? "4. Cập nhật animation và dựng prefab Meshy" : "4. Tích hợp Sword Attack vào game", GUILayout.Height(34)))
                {
                    try { if (meshy) { MeshyMountedInstaller.BuildPrefab(); report = "Đã dựng prefab Meshy với bộ animation hiện tại."; } else report = ActionForgeAnimationBridge.IntegrateSwordAttack(); }
                    catch (Exception e) { report = e.Message; Debug.LogWarning("[ActionForge] " + e.Message); }
                }
            if (meshy && GUILayout.Button("Áp dụng prefab Meshy vào sáu màn chơi", GUILayout.Height(28)))
            {
                try { MeshyMountedInstaller.InstallAll(); report = "Đã cập nhật sáu màn. Bản trước được giữ dưới Previous Visual - Before Meshy và có backup scene."; }
                catch (Exception e) { report = e.Message; Debug.LogWarning("[Meshy] " + e.Message); }
            }
            if (GUILayout.Button("Mở thư mục Animation"))
            {
                Directory.CreateDirectory(ActionForgeAnimationBridge.Folder);
                EditorUtility.RevealInFinder(Path.GetFullPath(ActionForgeAnimationBridge.Folder));
            }
            var mounted = AssetDatabase.LoadAssetAtPath<ActionForgeMountedClip>(ActionForgeAnimationBridge.MountedClipPath);
            if (meshy)
                EditorGUILayout.HelpBox("Bộ Meshy dùng 9 clip ActionForge: Idle Loop, Sword Regular A/B, Sword Attack, Hit Chest, Interact, Pickup Table, Spell Simple Shoot và Spell Simple Idle Loop. Chọn đủ các clip trước khi xuất một FBX.", MessageType.Info);
            else
            EditorGUILayout.HelpBox(mounted != null
                ? "Sword Attack đã tích hợp: KayKit Animator + thân trên người cưỡi ngựa. Chân/ngựa vẫn dùng chuyển động cưỡi; đòn tre giữ animation riêng."
                : "Chưa có dữ liệu Sword Attack cho người cưỡi ngựa. Nhập FBX rồi bấm Tích hợp.", mounted != null ? MessageType.Info : MessageType.Warning);
            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }
}
