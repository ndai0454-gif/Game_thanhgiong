using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Explicit smoke check; never attached to shipped scenes or started automatically.</summary>
public sealed class MeshyRuntimeVerification : MonoBehaviour
{
    readonly List<string> results = new List<string>();
    string folder;
    ThanhGiongCampaignController actor;
    MeshyMountedRig rig;
    GodotMountedMotion motion;
    MountedHorseController movement;
    int failures;
    public bool Finished { get; private set; }

    void Check(bool passed, string caption)
    {
        string text = (passed ? "PASS " : "FAIL ") + caption;
        results.Add(text); Debug.Log("MESHY_CHECK " + text); if (!passed) failures++;
    }

    IEnumerator Start()
    {
        yield return null; // let the campaign choose its chapter before the checks
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../MeshyVerification"));
        Directory.CreateDirectory(folder);
        actor = FindAnyObjectByType<ThanhGiongCampaignController>();
        rig = actor.GetComponentInChildren<MeshyMountedRig>();
        motion = actor.GetComponent<GodotMountedMotion>(); movement = actor.GetComponent<MountedHorseController>();
        if (actor.IsDead) actor.RestartBattle();
        var initial = actor.CaptureCheckpointState();
        initial.weapon = ThanhGiongCampaignController.Weapon.IronSword;
        actor.RestoreCheckpointState(initial, actor.transform.position, actor.transform.rotation);
        actor.enabled = false; movement.enabled = false;
        if (actor.battleRoot != null) actor.battleRoot.SetActive(false);
        Check(rig != null && rig.IsReady && motion.IsRigReady, "new mounted prefab and Animator ready");
        Check(!rig.heroAnimator.applyRootMotion, "Animator root motion disabled");
        var bones = rig.heroAnimator.GetComponentsInChildren<Transform>(true);
        var hand = bones.First(x => x.name == "mixamorig:RightHand");
        var leg = bones.First(x => x.name == "mixamorig:RightUpLeg");
        var spear = bones.First(x => x.name == "Weapon_GoldenBamboo");
        Check(spear.IsChildOf(hand), "new spear attached to corrected right hand");
        Quaternion legRest = leg.localRotation;
        WeaponVisibility();
        yield return new WaitForSeconds(1.2f);
        float readyUntil = Time.time + 10f;
        while (motion.IsBusy && Time.time < readyUntil) yield return null;
        Check(!motion.IsBusy, "equipment motion completes before combat");
        Capture("game_idle");
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "gameplay_full.png"));
        for (int i = 0; i < 3; i++)
        {
            rig.heroAnimator.enabled = false;
            rig.heroAnimator.Play("Idle", 0, 0f); rig.heroAnimator.Update(0f);
            Quaternion before = hand.rotation;
            Check(actor.TryMeleeAttack(), "campaign accepts sword combo " + (i + 1));
            rig.heroAnimator.Update(0f); rig.heroAnimator.Update(.1f);
            string role = i == 0 ? "AttackA" : i == 1 ? "AttackB" : "AttackC";
            Check(State(role), "Animator enters " + role);
            Check(Quaternion.Angle(before, hand.rotation) > 1f, role + " moves the skinned weapon hand");
            if (i == 0) Capture("game_attack");
            for (int step = 0; step < 100; step++) rig.heroAnimator.Update(.01f);
            yield return new WaitForSeconds(.65f);
            Check(State("Idle"), role + " returns to idle");
        }
        rig.heroAnimator.enabled = true;
        var state = actor.CaptureCheckpointState(); state.weapon = ThanhGiongCampaignController.Weapon.Bamboo;
        actor.RestoreCheckpointState(state, actor.transform.position, actor.transform.rotation);
        movement.enabled = false; if (actor.battleRoot != null) actor.battleRoot.SetActive(false);
        WeaponVisibility();
        Check(actor.TryMeleeAttack(), "campaign accepts new spear attack");
        yield return new WaitForSeconds(.10f);
        Check(State("BambooAttack"), "Animator enters spear sweep"); Capture("game_spear");
        yield return new WaitForSeconds(.9f);
        Check(Quaternion.Angle(legRest, leg.localRotation) < .01f, "authored actions preserve seated leg bones");

        rig.heroAnimator.SetFloat("ActionSpeed", 1f);
        foreach (string role in new[]{"Pickup","Hit","Fire","Interact"})
        {
            rig.heroAnimator.SetTrigger(role);
            yield return new WaitForSeconds(.10f);
            Check(State(role), "Animator enters " + role);
            yield return new WaitForSeconds(role == "Interact" ? 2.2f : role == "Pickup" ? 1.0f : .65f);
            Check(State("Idle"), role + " recovers to idle");
        }
        motion.SetFlying(true);
        yield return new WaitForSeconds(.18f);
        Check(State("Ascension"), "flying activates ascension pose"); Capture("game_ascension");
        motion.SetFlying(false);
        yield return new WaitForSeconds(.18f);
        Check(State("Idle"), "landing exits ascension pose");

        motion.enabled = false;
        var horse = motion.visual.GetComponentsInChildren<SkinnedMeshRenderer>().First(x => x.name == "Meshy Horse Skin");
        var startMesh = new Mesh(); var endMesh = new Mesh();
        horse.BakeMesh(startMesh);
        float phase = motion.GaitPhase;
        for (int i = 0; i < 45; i++) { motion.Drive(1f / 60f, 5f, .55f, 1f, true, 0, 0, 0, -1, false); yield return null; }
        horse.BakeMesh(endMesh);
        Vector3[] start = startMesh.vertices, end = endMesh.vertices;
        float maxChange = 0f;
        for (int i = 0; i < start.Length; i++) maxChange = Mathf.Max(maxChange, Vector3.Distance(start[i], end[i]));
        Check(maxChange > .03f && Mathf.Abs(motion.GaitPhase - phase) > .01f, "horse skin deforms during walk (delta=" + maxChange.ToString("F3") + "m)");
        Capture("game_walk");
        for (int i = 0; i < 45; i++) { motion.Drive(1f / 60f, 10f, 1f, 1f, true, 0, 0, 0, -1, false); yield return null; }
        Capture("game_gallop");
        Check(horse.bones.Length == 17 && horse.sharedMesh.vertexCount < 70000, "horse has 17 joints and optimized geometry");
        Check(actor.GetComponentsInChildren<Renderer>().All(x => x.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported)), "new active materials supported by render pipeline");
        Destroy(startMesh); Destroy(endMesh);
        results.Add("MESHY_RUNTIME " + (failures == 0 ? "PASS" : "FAIL") + " failures=" + failures);
        File.WriteAllLines(Path.Combine(folder, "runtime-results.txt"), results);
        Debug.Log(results.Last()); Finished = true;
        motion.enabled = true; movement.enabled = true; actor.enabled = true;
    }

    bool State(string name)
    {
        return rig.heroAnimator.GetCurrentAnimatorStateInfo(0).IsName(name) ||
            rig.heroAnimator.IsInTransition(0) && rig.heroAnimator.GetNextAnimatorStateInfo(0).IsName(name);
    }

    void WeaponVisibility()
    {
        foreach (var t in rig.heroAnimator.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Weapon_IronSword") t.gameObject.SetActive(actor.CurrentWeapon == ThanhGiongCampaignController.Weapon.IronSword);
            if (t.name == "Weapon_GoldenBamboo") t.gameObject.SetActive(actor.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo);
        }
    }

    void Capture(string label)
    {
        var go = new GameObject("Meshy Verification Camera");
        var camera = go.AddComponent<Camera>();
        Transform focus = rig.heroAnimator.transform;
        Vector3 center = actor.transform.position + Vector3.up * (1.15f * Mathf.Abs(motion.visual.lossyScale.x));
        float scale = Mathf.Abs(motion.visual.lossyScale.x);
        camera.transform.position = center + actor.transform.TransformDirection(new Vector3(3.8f, 1.1f, 4.8f)) * scale;
        camera.transform.LookAt(center);
        camera.orthographic = true; camera.orthographicSize = 1.65f * scale;
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .07f, .065f);
        camera.cullingMask = 1 << 31;
        var rendered = rig.GetComponentsInChildren<Transform>(true);
        int[] layers = rendered.Select(x => x.gameObject.layer).ToArray();
        foreach (var t in rendered) t.gameObject.layer = 31;
        var key = new GameObject("Meshy Verification Light");
        var light = key.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.5f;
        light.cullingMask = 1 << 31;
        key.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
        var target = new RenderTexture(1200, 1000, 24);
        camera.targetTexture = target; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(1200, 1000, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1200, 1000), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(folder, label + ".png"), image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        for (int i = 0; i < rendered.Length; i++) rendered[i].gameObject.layer = layers[i];
        Destroy(key);
        target.Release(); Destroy(target); Destroy(image); Destroy(go);
    }
}
