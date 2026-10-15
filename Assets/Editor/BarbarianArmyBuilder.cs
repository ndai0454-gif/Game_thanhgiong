using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Builds a reusable KayKit Barbarian enemy and deploys a deterministic army.</summary>
public static class BarbarianArmyBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Enemies/KayKit_Barbarian_Soldier.prefab";
    private const string TargetScene = "Assets/Scenes/ThanhGiongWorld/PhaoDaiNgamQuanAn.unity";
    private const string ArmyRootName = "DOI QUAN TAN (QIN ARMY)";
    private const string AutoBuildKey = "ThanhGiong.BarbarianArmy.2026-10-02-v1";

    [InitializeOnLoadMethod]
    private static void BuildRequestedArmyOnce()
    {
        if (EditorPrefs.GetBool(AutoBuildKey, false)) return;
        EditorApplication.delayCall += TryAutoBuild;
    }

    private static void TryAutoBuild()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoBuild;
            return;
        }

        if (SceneManager.GetActiveScene().path != TargetScene) return;
        EditorPrefs.SetBool(AutoBuildKey, true);
        BuildAndDeploy();
    }

    [MenuItem("Tools/Thanh Giong/Enemies/Build and Deploy Barbarian Army")]
    public static void BuildAndDeploy()
    {
        GameObject prefab = BuildPrefab();
        DeployArmy(prefab, SceneManager.GetActiveScene());
    }

    private static GameObject BuildPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ThanhGiong.EditorTools.QinSoldierBuilder.VisualPrefabPath) == null)
        {
            ThanhGiong.EditorTools.QinSoldierBuilder.Build();
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null) return prefab;
        return AssetDatabase.LoadAssetAtPath<GameObject>(ThanhGiong.EditorTools.QinSoldierBuilder.GameplayPrefabPath);
    }

    private static void DeployArmy(GameObject prefab, Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("No loaded scene for Qin army.");

        GameObject oldRoot = scene.GetRootGameObjects().FirstOrDefault(item => item.name == ArmyRootName || item.name == "DOI QUAN BARBARIAN");
        if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot);

        GameObject army = new GameObject(ArmyRootName);
        SceneManager.MoveGameObjectToScene(army, scene);

        Vector3 center = FindDeploymentCenter(scene);
        const int rows = 4;
        const int columns = 6;
        const float xSpacing = 2.2f;
        const float zSpacing = 2.4f;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                GameObject soldier = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (soldier == null) continue;
                soldier.name = $"QinSoldier_{row + 1:00}_{column + 1:00}";
                soldier.transform.SetParent(army.transform, true);
                float x = (column - (columns - 1) * .5f) * xSpacing;
                float z = row * zSpacing;
                soldier.transform.position = GroundPoint(center + new Vector3(x, 0f, z));
                soldier.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                ThanhGiongEnemy soldierEnemy = soldier.GetComponent<ThanhGiongEnemy>();
                if (soldierEnemy != null) ThanhGiong.EditorTools.QinSoldierCampaignInstaller.ApplySoldier(soldierEnemy);
            }
        }

        // A larger commander anchors the rear rank without changing the reusable prefab.
        GameObject commander = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (commander != null)
        {
            commander.name = "Qin Army Commander";
            commander.transform.SetParent(army.transform, true);
            commander.transform.position = GroundPoint(center + new Vector3(0f, 0f, rows * zSpacing + 1.8f));
            commander.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            commander.transform.localScale = Vector3.one * 1.35f;
            ThanhGiongEnemy commanderEnemy = commander.GetComponent<ThanhGiongEnemy>();
            commanderEnemy.enemyName = "Đội Trưởng Quân Tần";
            commanderEnemy.maxHealth = 180f;
            commanderEnemy.moveSpeed = 2.9f;
            ThanhGiong.EditorTools.QinSoldierCampaignInstaller.ApplySoldier(commanderEnemy);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = army;
        Debug.Log($"[Thanh Giong] Deployed 24 Qin soldiers and 1 commander in {scene.name}.");
    }

    private static Vector3 FindDeploymentCenter(Scene scene)
    {
        Transform player = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.CompareTag("Player") || item.name.Contains("Player Spawn"));
        return player != null ? player.position + new Vector3(0f, 0f, 18f) : new Vector3(0f, 0f, 12f);
    }

    private static Vector3 GroundPoint(Vector3 point)
    {
        if (Physics.Raycast(point + Vector3.up * 80f, Vector3.down, out RaycastHit hit, 180f, ~0, QueryTriggerInteraction.Ignore))
            point.y = hit.point.y + .03f;
        return point;
    }
}
