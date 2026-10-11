using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only inspection of saved scenes. Preview scenes never replace the user's open scene.
public static class ScenarioAlignmentAudit
{
    public static string InspectSavedScenes()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Inspect saved scenes outside Play Mode.");
        var active = SceneManager.GetActiveScene();
        var rows = new List<object>();
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(entry.path);
            try
            {
                var roots = scene.GetRootGameObjects();
                var all = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                var components = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
                var campaigns = components.OfType<ThanhGiongCampaignController>().ToArray();
                var enemies = components.OfType<ThanhGiongEnemy>().ToArray();
                var collectibles = components.OfType<ThanhGiongCollectible>().ToArray();
                var cameras = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).ToArray();
                rows.Add(new {
                    scene = entry.path,
                    objectCount = all.Length,
                    missingScripts = roots.Sum(r => r.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))),
                    scripts = components.GroupBy(c => c.GetType().Name).ToDictionary(g => g.Key, g => g.Count()),
                    players = components.OfType<MountedHorseController>().Select(p => new {
                        p.name, active = p.gameObject.activeInHierarchy, p.enabled, p.rigVersion,
                        campaignAttached = p.GetComponent<ThanhGiongCampaignController>() != null,
                        visual = p.visual != null ? p.visual.name : null,
                        originalLegs = new[]{p.frontLeftLeg,p.frontRightLeg,p.rearLeftLeg,p.rearRightLeg}.Count(t => t != null),
                        renderers = p.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled),
                        components = p.GetComponents<MonoBehaviour>().Where(c => c != null).Select(c => c.GetType().Name).ToArray()
                    }).ToArray(),
                    campaigns = campaigns.Select(c => new {
                        c.name, active = c.gameObject.activeInHierarchy, c.enabled,
                        startingChapter = c.startingChapter.ToString(), c.swordBreakKills, c.victoryKills, c.maxHealth,
                        battleRoot = c.battleRoot != null ? c.battleRoot.name : null,
                        bambooRoot = c.bambooRoot != null ? c.bambooRoot.name : null,
                        hasHUD = c.hud != null, hasVFX = c.vfx != null, hasEndingTarget = c.ascensionTarget != null
                    }).ToArray(),
                    enemies = enemies.Length,
                    activeEnemies = enemies.Count(e => e.enabled && e.gameObject.activeInHierarchy),
                    bosses = enemies.Count(e => e.isBoss),
                    bambooPickups = collectibles.Count(c => c.kind == ThanhGiongCollectible.Kind.Bamboo),
                    foodPickups = collectibles.Count(c => c.kind <= ThanhGiongCollectible.Kind.Meat),
                    storyBeats = components.OfType<ThanhGiongStoryBeatZone>().Select(z => new { z.beatTitle, z.storyPurpose, shot = z.cameraShot.ToString() }).ToArray(),
                    cameras = cameras.Select(c => new { c.name, c.orthographic, c.fieldOfView,
                        follow = c.GetComponent<IsometricCameraFollow>() != null,
                        runtimeIsometric = c.GetComponent<IsometricCameraFollow>()?.useOrthographicIsometric,
                        target = c.GetComponent<IsometricCameraFollow>()?.target?.name }).ToArray(),
                    visibleBambooObjects = all.Count(t => t.name.IndexOf("bamboo", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.StartsWith("Tre ") || t.name.StartsWith("Khóm Tre")),
                    houseObjects = all.Count(t => t.name.IndexOf("house", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.StartsWith("Nhà")),
                    rootNames = roots.Select(r => r.name).ToArray()
                });
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        var report = new { inspectedAt = DateTime.UtcNow.ToString("o"), activeScene = active.path,
            activeSceneStillOpen = SceneManager.GetActiveScene().handle == active.handle,
            activeSceneDirty = active.isDirty, scenes = rows };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
        Directory.CreateDirectory("Temp/ScenarioAlignment");
        File.WriteAllText("Temp/ScenarioAlignment/scenes.json", json);
        return json;
    }

    public static string InspectRunningScene()
    {
        var scene = SceneManager.GetActiveScene();
        var players = UnityEngine.Object.FindObjectsByType<MountedHorseController>(FindObjectsInactive.Include);
        var campaigns = UnityEngine.Object.FindObjectsByType<ThanhGiongCampaignController>(FindObjectsInactive.Include);
        var camera = Camera.main;
        var report = new {
            playing = EditorApplication.isPlaying, scene = scene.path,
            players = players.Select(p => new { p.name, p.enabled, p.rigVersion, p.Grounded, p.PlanarSpeed,
                campaignAttached = p.GetComponent<ThanhGiongCampaignController>() != null,
                position = p.transform.position.ToString("F2") }).ToArray(),
            campaigns = campaigns.Length,
            enemies = UnityEngine.Object.FindObjectsByType<ThanhGiongEnemy>(FindObjectsInactive.Include).Length,
            bambooPickups = UnityEngine.Object.FindObjectsByType<ThanhGiongCollectible>(FindObjectsInactive.Include)
                .Count(c => c.kind == ThanhGiongCollectible.Kind.Bamboo),
            hud = UnityEngine.Object.FindObjectsByType<ThanhGiongCampaignHUD>(FindObjectsInactive.Include).Length,
            camera = camera == null ? null : new { camera.name, camera.orthographic,
                target = camera.GetComponent<IsometricCameraFollow>()?.target?.name }
        };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("Temp/ScenarioAlignment/runtime-current-scene.json", json);
        return json;
    }
}
