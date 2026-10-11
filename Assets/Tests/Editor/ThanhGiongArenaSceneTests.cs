using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ThanhGiongArenaSceneTests
{
    const string Path = "Assets/Scenes/ThanhGiongArena/PhuDongArena.unity";
    static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

    [Test] public void ArenaIsFirstSceneAndKeepsPreviousWorlds()
    {
        Assert.AreEqual(Path, EditorBuildSettings.scenes.First(s => s.enabled).path);
        Assert.AreEqual(6, EditorBuildSettings.scenes.Count(s => s.path.Contains("ThanhGiongWorld/")));
        Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.path.EndsWith("AlbionForestMap.unity")));
    }

    [Test] public void SavedArenaHasConnectedGameplayAndOriginalFourLegHorse()
    {
        var scene = EditorSceneManager.OpenPreviewScene(Path);
        try
        {
            var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
            Component flow = components.Single(c => c != null && c.GetType() == T("ThanhGiongArenaFlow"));
            Component waves = (Component)flow.GetType().GetField("waveManager").GetValue(flow);
            Assert.NotNull(waves); Assert.NotNull(flow.GetType().GetField("bamboo").GetValue(flow));
            Assert.NotNull(waves.GetType().GetField("enemyPrefab").GetValue(waves));
            Assert.AreEqual(3, ((Array)waves.GetType().GetField("waves").GetValue(waves)).Length);
            Assert.AreEqual(6, ((Array)waves.GetType().GetField("spawnPoints").GetValue(waves)).Length);
            Assert.AreEqual(0, components.Count(c => c != null && c.GetType().Name == "LegendCheckpoint"));
            Assert.AreEqual(0, components.Count(c => c != null && c.GetType().Name == "LegendSeedSkills"));
            Component motion = flow.GetComponent(T("MountedHorseMeshMotion")); Assert.NotNull(motion);
            Assert.IsTrue((bool)motion.GetType().GetProperty("IsReady").GetValue(motion));
            Transform[] feet = (Transform[])motion.GetType().GetField("hooves").GetValue(motion);
            Assert.Less(feet.Max(f => f.position.y) - feet.Min(f => f.position.y), .01f, "All four original hooves must share the ground plane.");
            foreach (string fake in new[] { "Front Left Leg", "Front Right Leg", "Rear Left Leg", "Rear Right Leg" }) Assert.IsNull(flow.transform.Find(fake));
            var camera = components.OfType<Camera>().Single();
            Component follow = camera.GetComponent(T("IsometricCameraFollow"));
            Assert.IsTrue((bool)follow.GetType().GetField("arenaPerspective").GetValue(follow));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
