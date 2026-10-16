using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ThanhGiongPlayModeTests
{
    private static Type GameType(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.Name == name);
    }

    [UnityTest]
    public IEnumerator OpenWorld_StartsAsPlayableCampaign()
    {
        yield return SceneManager.LoadSceneAsync("ThanhGiongDaiViet", LoadSceneMode.Single);
        for (int i = 0; i < 8; i++) yield return null;

        Type campaignType = GameType("ThanhGiongCampaignController");
        Type horseType = GameType("MountedHorseController");
        Type enemyType = GameType("ThanhGiongEnemy");
        Assert.NotNull(campaignType);
        Assert.NotNull(horseType);
        Assert.NotNull(enemyType);

        Component campaign = UnityEngine.Object.FindFirstObjectByType(campaignType) as Component;
        Component horse = UnityEngine.Object.FindFirstObjectByType(horseType) as Component;
        Assert.NotNull(campaign, "Campaign chưa khởi tạo trong thế giới Đại Việt");
        Assert.NotNull(horse, "Player chưa khởi tạo trong thế giới Đại Việt");
        Assert.IsTrue(horse.gameObject.activeInHierarchy && ((Behaviour)horse).enabled);

        Transform meshyRig = horse.transform.Find("Visual/Meshy_Mounted_ThanhGiong");
        Assert.NotNull(meshyRig, "Thiếu rig Meshy_Mounted_ThanhGiong");
        Assert.Greater(meshyRig.GetComponentsInChildren<Renderer>(true).Length, 0);

        FieldInfo gait = horseType.GetField("gaitTime", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo frontLeg = horseType.GetField("frontLeftLeg", BindingFlags.Instance | BindingFlags.Public);
        MethodInfo animateLegs = horseType.GetMethod("AnimateLegs", BindingFlags.Instance | BindingFlags.NonPublic);
        Transform leg = frontLeg.GetValue(horse) as Transform;
        if (leg == null) {
            leg = horse.transform.Find("Visual/MountedGiong/Mount/IronHorse/model/body/fl_upper");
            frontLeg.SetValue(horse, leg);
        }
        if (leg != null) {
            Quaternion before = leg.localRotation;
            gait.SetValue(horse, 1.1f);
            animateLegs.Invoke(horse, new object[] { 1f, false });
            Assert.Greater(Quaternion.Angle(before, leg.localRotation), 0.001f, "Animation bốn chân ngựa không chuyển động");
        }

        horseType.GetMethod("BeginAttack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(horse, null);
        Assert.Greater((float)horseType.GetField("attackTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(horse), 0f);

        PropertyInfo chapter = campaignType.GetProperty("CurrentChapter");
        Assert.NotNull(chapter.GetValue(campaign), "Campaign chưa có chương hiện tại");
        Assert.GreaterOrEqual(UnityEngine.Object.FindObjectsByType(enemyType, FindObjectsInactive.Include, FindObjectsSortMode.None).Length, 20);
        Assert.NotNull(Shader.Find("ThanhGiong/Golden Noise Dissolve"));
    }
}
