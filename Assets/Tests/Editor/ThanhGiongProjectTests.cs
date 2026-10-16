using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ThanhGiongProjectTests
{
    private static readonly string[] WorldScenes =
    {
        "Assets/Scenes/ThanhGiongWorld/LangGiongTienTuyen.unity",
        "Assets/Scenes/ThanhGiongWorld/KinhThanhRenThep.unity",
        "Assets/Scenes/ThanhGiongWorld/PhaoDaiNgamQuanAn.unity",
        "Assets/Scenes/ThanhGiongWorld/ThungLungVuotSong.unity",
        "Assets/Scenes/ThanhGiongWorld/TranTuyenNuiSoc.unity",
        "Assets/Scenes/ThanhGiongWorld/DinhSocHoaThanh.unity"
    };

    private const string OpenWorldScene = "Assets/Scenes/ThanhGiongWorld/ThanhGiongDaiViet.unity";

    private static Type GameType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.Name == name);
    }

    private static int Count(Scene scene, string typeName)
    {
        Type type = GameType(typeName);
        Assert.NotNull(type, $"Không tìm thấy type {typeName}");
        return scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren(type, true).Length);
    }

    private static void WithScene(string path, Action<Scene> check)
    {
        Scene active = EditorSceneManager.GetActiveScene();
        bool alreadyLoaded = active.path == path;
        Scene scene = alreadyLoaded ? active : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try { check(scene); }
        finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void BuildSettings_ContainsOnlyExistingScenes()
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path), $"Scene bị thiếu: {scene.path}");
    }

    [Test]
    public void SceneTransitions_FollowNarrativeOrder()
    {
        Type transition = GameType("ThanhGiongSceneTransition");
        Assert.NotNull(transition);
        var next = transition.GetMethod("GetNextScene");
        Assert.NotNull(next);
        Assert.AreEqual("KinhThanhRenThep", next.Invoke(null, new object[] { "LangGiongTienTuyen" }));
        Assert.AreEqual("PhaoDaiNgamQuanAn", next.Invoke(null, new object[] { "KinhThanhRenThep" }));
        Assert.AreEqual("ThungLungVuotSong", next.Invoke(null, new object[] { "PhaoDaiNgamQuanAn" }));
        Assert.AreEqual("TranTuyenNuiSoc", next.Invoke(null, new object[] { "ThungLungVuotSong" }));
        Assert.AreEqual("DinhSocHoaThanh", next.Invoke(null, new object[] { "TranTuyenNuiSoc" }));
        Assert.IsNull(next.Invoke(null, new object[] { "DinhSocHoaThanh" }));

        var previous = transition.GetMethod("GetPreviousScene");
        Assert.NotNull(previous);
        Assert.AreEqual("KinhThanhRenThep", previous.Invoke(null, new object[] { "PhaoDaiNgamQuanAn" }));
        Assert.AreEqual("PhaoDaiNgamQuanAn", previous.Invoke(null, new object[] { "ThungLungVuotSong" }));
        Assert.IsNull(previous.Invoke(null, new object[] { "LangGiongTienTuyen" }));
    }

    [Test]
    public void OpenWorld_HasMeshyMountedPlayerCampaignAndCamera()
    {
        // The shipped game is the seamless open world; the six legacy scenes are kept only for reference.
        WithScene(OpenWorldScene, scene =>
        {
            Assert.GreaterOrEqual(Count(scene, "MountedHorseController"), 1, $"{scene.name} thiếu player");
            Assert.GreaterOrEqual(Count(scene, "ThanhGiongCampaignController"), 1, $"{scene.name} thiếu campaign");
            Assert.GreaterOrEqual(Count(scene, "ThanhGiongOpenWorld"), 1, $"{scene.name} thiếu hệ thống open world");
            Assert.GreaterOrEqual(Count(scene, "ThanhGiongSeamlessCampaign"), 1, $"{scene.name} thiếu seamless campaign");
            Assert.GreaterOrEqual(Count(scene, "IsometricCameraFollow"), 1, $"{scene.name} thiếu camera follow");
            Assert.GreaterOrEqual(Count(scene, "ThanhGiongCampaignHUD"), 1, $"{scene.name} thiếu HUD");

            Type horseType = GameType("MountedHorseController");
            Component horse = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren(horseType, true).Cast<Component>())
                .First();
            Transform rig = horse.transform.Find("Visual/Meshy_Mounted_ThanhGiong");
            Assert.NotNull(rig, $"{scene.name}: thiếu Visual/Meshy_Mounted_ThanhGiong trên nhân vật cưỡi ngựa");
            Assert.Greater(rig.GetComponentsInChildren<Renderer>(true).Length, 0, $"{scene.name}: thiếu Renderer trên rig Meshy");
            Assert.NotNull(horse.transform.Find("CombatFocus_IronHorse"), $"{scene.name}: thiếu điểm target riêng trên nhân vật cưỡi ngựa");
            Assert.NotNull(horse.GetComponent(GameType("ThanhGiongSpeedRibbon")), $"{scene.name}: thiếu vệt tốc độ");
            Assert.NotNull(horse.GetComponent(GameType("ThanhGiongRibbonCape")), $"{scene.name}: thiếu dải lụa đỏ");
            Assert.NotNull(horse.GetComponent(GameType("MeshyMountedCombatPose")), $"{scene.name}: thiếu MeshyMountedCombatPose");
        });
    }

    [Test]
    public void BattleMaps_HaveInvadersAndRequiredBosses()
    {
        WithScene(WorldScenes[2], scene =>
        {
            Assert.GreaterOrEqual(Count(scene, "ThanhGiongEnemy"), 20);
            Assert.GreaterOrEqual(Count(scene, "ThanhGiongEnemyAnimationDriver"), 20);
            Assert.GreaterOrEqual(Count(scene, "ThanhGiongGoldenDissolve"), 20);
        });
        WithScene(WorldScenes[3], scene => Assert.GreaterOrEqual(Count(scene, "ThanhGiongEnemy"), 6));
        WithScene(WorldScenes[4], scene => Assert.GreaterOrEqual(Count(scene, "ThanhGiongEnemy"), 12));
    }

    [Test]
    public void CombatContracts_ArePresent()
    {
        Type enemy = GameType("ThanhGiongEnemy");
        Assert.NotNull(enemy.GetMethod("TakeDamage", new[] { typeof(float), typeof(float), typeof(Vector3) }));
        Assert.NotNull(enemy.GetField("stuckDuration"));
        Assert.NotNull(enemy.GetField("vulnerabilityMultiplier"));
        Assert.NotNull(enemy.GetField("surroundRadius"));
        Assert.NotNull(enemy.GetField("separationStrength"));
        Assert.NotNull(enemy.GetProperty("FocusTarget"));
        Assert.NotNull(Shader.Find("ThanhGiong/Golden Noise Dissolve"));
        Assert.NotNull(GameType("ThanhGiongSpeedRibbon"));
        Material magma = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Horse/Mat_IronHorse_MagmaCore.mat");
        Assert.NotNull(magma);
        Assert.IsTrue(magma.IsKeywordEnabled("_EMISSION"), "Lõi dung nham chưa bật HDR emission");
    }
}
