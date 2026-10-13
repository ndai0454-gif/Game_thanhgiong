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
    public void WorldMaps_HavePlayerCampaignCameraAndFourLegRig()
    {
        Type horseType = GameType("MountedHorseController");
        foreach (string path in WorldScenes)
        {
            WithScene(path, scene =>
            {
                Assert.GreaterOrEqual(Count(scene, "MountedHorseController"), 1, $"{scene.name} thiếu player");
                Assert.GreaterOrEqual(Count(scene, "ThanhGiongCampaignController"), 1, $"{scene.name} thiếu campaign");
                Assert.GreaterOrEqual(Count(scene, "IsometricCameraFollow"), 1, $"{scene.name} thiếu camera follow");
                Component horse = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren(horseType, true).Cast<Component>())
                    .First();
                Transform horseRig = horse.transform.Find("Visual/IronHorse_Rig") ?? horse.transform.Find("Visual/MountedGiong/Mount/IronHorse");
                Transform combatFocus = horse.transform.Find("CombatFocus_IronHorse");
                Assert.NotNull(horseRig, $"{scene.name}: ngựa chưa được tách thành IronHorse_Rig hoặc MountedGiong");
                Assert.NotNull(combatFocus, $"{scene.name}: thiếu điểm target riêng trên nhân vật cưỡi ngựa");
                Assert.Greater(horseRig.GetComponentsInChildren<Renderer>(true).Length, 0, $"{scene.name}: thiếu Renderer");
                Assert.NotNull(horse.GetComponent(GameType("ThanhGiongSpeedRibbon")), $"{scene.name}: thiếu vệt tốc độ Knight");
                Assert.NotNull(horse.GetComponent(GameType("ThanhGiongRibbonCape")), $"{scene.name}: thiếu dải lụa đỏ Knight");

                var godotMotion = horse.GetComponent(GameType("GodotMountedMotion"));
                if (godotMotion != null)
                {
                    Transform heroRig = horse.transform.Find("Visual/MountedGiong/Mount/ThanhGiong");
                    Assert.NotNull(heroRig, $"{scene.name}: Gióng chưa được tách thành MountedGiong/Mount/ThanhGiong");
                    Assert.Greater(heroRig.GetComponentsInChildren<Renderer>(true).Length, 0, $"{scene.name}: thiếu Renderer trên Gióng");
                }
                else
                {
                    Transform riderSeat = horseRig != null
                        ? horseRig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RiderSeat")
                        : null;
                    Transform heroRig = riderSeat != null ? riderSeat.Find("ThanhGiong_Rig") : null;
                    Assert.NotNull(heroRig, $"{scene.name}: Gióng chưa được tách thành ThanhGiong_Rig");
                    Component modularHorse = horseRig.GetComponent(GameType("HorseController"));
                    Assert.NotNull(modularHorse, $"{scene.name}: chưa tích hợp KayKit_Horse_Modular");
                    GameObject horseSource = PrefabUtility.GetCorrespondingObjectFromSource(horseRig.gameObject);
                    Assert.NotNull(horseSource, $"{scene.name}: ngựa không còn liên kết prefab nguồn");
                    Assert.AreEqual("KayKit_IronHorse_ThanhGiong", horseSource.name);
                    Assert.GreaterOrEqual(horseRig.GetComponentsInChildren<ParticleSystem>(true).Length, 3,
                        $"{scene.name}: Ngựa Sắt thiếu Aura, HoofDust hoặc FireBreath VFX");
                    Animator horseAnimator = horseRig.GetComponent<Animator>();
                    Assert.NotNull(horseAnimator, $"{scene.name}: ngựa modular thiếu Animator");
                    Assert.AreEqual("Horse_Controller", horseAnimator.runtimeAnimatorController?.name);
                    Animator heroAnimator = heroRig.GetComponentInChildren<Animator>(true);
                    Assert.NotNull(heroAnimator, $"{scene.name}: Gióng thiếu Animator riêng");
                    Assert.NotNull(heroAnimator.runtimeAnimatorController, $"{scene.name}: Gióng thiếu Animator Controller");
                    GameObject heroSource = PrefabUtility.GetCorrespondingObjectFromSource(heroRig.gameObject);
                    Assert.NotNull(heroSource, $"{scene.name}: Gióng không còn liên kết prefab nguồn");
                    Assert.AreEqual("KayKit_ThanhGiong_Hero", heroSource.name);
                    Assert.NotNull(heroRig.GetComponent(GameType("ThanhGiongCharacterController")),
                        $"{scene.name}: Gióng modular thiếu controller riêng");
                    Assert.AreEqual("ThanhGiong_Hero", heroAnimator.runtimeAnimatorController.name);
                    Assert.AreEqual("RiderSeat", heroRig.parent.name,
                        $"{scene.name}: Gióng chưa được gắn vào RiderSeat");
                    Assert.Less(new Vector2(heroRig.localPosition.x, heroRig.localPosition.z).magnitude, .15f,
                        $"{scene.name}: Gióng lệch khỏi tâm RiderSeat");
                    Assert.That(horseRig.localScale.x, Is.InRange(1.05f, 1.15f),
                        $"{scene.name}: Ngựa không còn đúng tỷ lệ với ô map");
                    Assert.That(heroRig.lossyScale.x, Is.InRange(.68f, .76f),
                        $"{scene.name}: Gióng không còn cân bằng với quân địch/map");
                    Assert.GreaterOrEqual(heroRig.GetComponentsInChildren<ParticleSystem>(true).Length, 2,
                        $"{scene.name}: Gióng thiếu Bamboo Aura hoặc Bamboo Impact VFX");
                    foreach (string fieldName in new[] { "frontLeftLeg", "frontRightLeg", "rearLeftLeg", "rearRightLeg" })
                        Assert.NotNull(horseType.GetField(fieldName)?.GetValue(horse), $"{scene.name}: thiếu {fieldName}");
                }
            });
        }
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
