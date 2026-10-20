using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Dev tool: moves the mounted hero to an isolated floor, drives it through seat, gallop,
/// attacks, bamboo pull and equipment, and saves 4-view contact sheets to Captures/PoseAudit.
/// Add it to any GameObject in Play mode; it removes itself and its floor when finished.
/// </summary>
public sealed class PoseAuditDirector : MonoBehaviour
{
    public string prefix = "audit";
    [Tooltip("Capture the fully grown rider (growth stage 3) instead of the current stage.")]
    public bool grown = true;
    public bool Done { get; private set; }
    public string Report { get; private set; } = "";

    ThanhGiongCampaignController campaign;
    MountedHorseController movement;
    GodotMountedMotion motion;
    MeshyHeroVisualDriver driver;
    MeshyMountedCombatPose pose;
    GameObject floor;
    Vector2 input;
    bool running;

    IEnumerator Start()
    {
        campaign = FindFirstObjectByType<ThanhGiongCampaignController>();
        if (campaign == null) { Report = "no campaign"; Done = true; yield break; }
        movement = campaign.GetComponent<MountedHorseController>();
        motion = campaign.GetComponent<GodotMountedMotion>();
        driver = campaign.GetComponent<MeshyHeroVisualDriver>();
        pose = campaign.GetComponentInChildren<MeshyMountedCombatPose>(true);
        foreach (var enemy in FindObjectsByType<ThanhGiongEnemy>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);

        floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "PoseAudit Floor";
        floor.transform.position = new Vector3(0f, 99f, 0f);
        floor.transform.localScale = new Vector3(400f, 2f, 400f);
        floor.GetComponent<Renderer>().enabled = false;
        DontDestroyOnLoad(gameObject);
        // Teleport only: changing chapter can start a scene transition mid-audit.
        campaign.TeleportPlayer(new Vector3(0f, 101f, 0f));
        campaign.transform.rotation = Quaternion.identity;
        movement.ResetMovementState();
        Physics.SyncTransforms();
        movement.enabled = false;
        motion.SyncEquipment(2, 0);
        if (grown)
        {
            typeof(ThanhGiongCampaignController).GetProperty("GrowthPhase")?.GetSetMethod(true)?.Invoke(campaign, new object[] { 3 });
            typeof(ThanhGiongCampaignController).GetMethod("ApplyGrowthScale", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(campaign, new object[] { false });
        }

        SetWeapon(ThanhGiongCampaignController.Weapon.IronSword);
        yield return Hold(Vector2.zero, false, 1.6f);
        yield return Shot("01_seat_idle");

        yield return Hold(Vector2.up, true, 2.2f);
        yield return Shot("02_gallop_a");
        yield return Hold(Vector2.up, true, .12f);
        yield return Shot("02_gallop_b");
        yield return Hold(Vector2.zero, false, 2.0f);

        yield return Attack("03_attack_iron");
        SetWeapon(ThanhGiongCampaignController.Weapon.Bamboo);
        yield return Hold(Vector2.zero, false, .8f);
        yield return Attack("04_attack_bamboo");

        SetWeapon(ThanhGiongCampaignController.Weapon.None);
        yield return Hold(Vector2.zero, false, .8f);
        if (pose != null)
        {
            Transform actor = campaign.transform;
            Time.timeScale = .25f;
            pose.PlayBambooPull(actor.position + actor.right * 2.4f + actor.forward * .8f);
            yield return Hold(Vector2.zero, false, .12f);
            yield return Shot("05_pull_a");
            yield return Hold(Vector2.zero, false, .2f);
            yield return Shot("05_pull_b");
            yield return Hold(Vector2.zero, false, .12f);
            yield return Shot("05_pull_c");
            Time.timeScale = 1f;
        }
        yield return Hold(Vector2.zero, false, 1f);

        motion.SyncEquipment(1, 0);
        yield return Hold(Vector2.zero, false, .4f);
        yield return Shot("06_unequipped");
        yield return Equip("equip_armor", "07_equip_armor");
        yield return Equip("equip_helmet", "08_equip_helmet");
        yield return Shot("09_fully_equipped");
        yield return Equip("remove_helmet", "10_remove_helmet");
        yield return Equip("remove_armor", "11_remove_armor");
        yield return Shot("12_removed");

        movement.enabled = true;
        Destroy(floor);
        Done = true;
        Debug.Log("POSE_AUDIT_DONE " + prefix + "\n" + Report);
        Destroy(gameObject, .5f);
    }

    void Update()
    {
        if (movement != null && !movement.enabled) movement.DriveMovement(input, running);
    }

    IEnumerator Hold(Vector2 value, bool run, float seconds)
    {
        input = value; running = run;
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
        input = Vector2.zero; running = false;
    }

    IEnumerator Attack(string name)
    {
        // Slow motion so each phase is sampled even when the editor renders few frames.
        Time.timeScale = .2f;
        // Same durations the campaign uses for iron (.32s) and bamboo (.58s) strikes.
        bool bamboo = campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo;
        movement.TriggerCampaignAttack(bamboo ? .58f : .32f);
        float[] marks = { .18f, .42f, .72f };
        for (int i = 0; i < marks.Length; i++)
        {
            float guard = Time.time + 2f;
            while (movement.AttackProgress >= 0f && movement.AttackProgress < marks[i] && Time.time < guard) yield return null;
            yield return Shot(name + "_" + (char)('a' + i));
        }
        Time.timeScale = 1f;
        yield return Hold(Vector2.zero, false, 1.2f);
    }

    IEnumerator Equip(string action, string name)
    {
        Time.timeScale = .35f;
        motion.PlayEquipment(action);
        yield return null;
        float[] marks = { .3f, .62f };
        for (int i = 0; i < marks.Length; i++)
        {
            float guard = Time.time + 4f;
            while (motion.IsBusy && motion.EquipmentProgress < marks[i] && Time.time < guard) yield return null;
            yield return Shot(name + "_" + (char)('a' + i));
        }
        Time.timeScale = 1f;
        float wait = Time.time + 4f;
        while (motion.IsBusy && Time.time < wait) yield return null;
        yield return Hold(Vector2.zero, false, .3f);
    }

    void SetWeapon(ThanhGiongCampaignController.Weapon weapon)
    {
        PropertyInfo property = typeof(ThanhGiongCampaignController).GetProperty("CurrentWeapon");
        property?.GetSetMethod(true)?.Invoke(campaign, new object[] { weapon });
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        if (driver == null || driver.meshyRoot == null || driver.meshyHero == null) yield break;
        Transform root = driver.meshyRoot, hero = driver.meshyHero, mount = driver.meshyMount != null ? driver.meshyMount : root;
        Transform chest = null;
        foreach (Transform t in hero.GetComponentsInChildren<Transform>(true)) if (t.name == "Spine01") { chest = t; break; }
        Vector3 center = (chest != null ? chest.position : hero.position) - mount.up * .9f;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        int[] layers = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) { layers[i] = renderers[i].gameObject.layer; renderers[i].gameObject.layer = 31; }
        var go = new GameObject("PoseAudit Camera");
        var cam = go.AddComponent<Camera>();
        cam.cullingMask = 1 << 31; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.78f, .8f, .84f); cam.fieldOfView = 38f; cam.nearClipPlane = .05f; cam.farClipPlane = 200f;
        const int size = 640;
        var rt = new RenderTexture(size, size, 24);
        cam.targetTexture = rt;
        var sheet = new Texture2D(size * 2, size * 2, TextureFormat.RGB24, false);
        float[] azimuth = { -90f, 35f, 150f, 90f };
        float distance = 6.2f * Mathf.Max(.5f, root.lossyScale.x / 1.65f);
        for (int k = 0; k < 4; k++)
        {
            Vector3 dir = Quaternion.AngleAxis(azimuth[k], mount.up) * Vector3.ProjectOnPlane(mount.forward, Vector3.up).normalized;
            go.transform.position = center + dir * distance + Vector3.up * distance * .16f;
            go.transform.LookAt(center, Vector3.up);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0); tex.Apply();
            sheet.SetPixels((k % 2) * size, (1 - k / 2) * size, size, size, tex.GetPixels());
            Destroy(tex);
        }
        RenderTexture.active = null; cam.targetTexture = null;
        rt.Release(); Destroy(rt); Destroy(go);
        for (int i = 0; i < renderers.Length; i++) renderers[i].gameObject.layer = layers[i];
        sheet.Apply();
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Captures/PoseAudit"));
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, prefix + "_" + name + ".png"), sheet.EncodeToPNG());
        Destroy(sheet);
        Report += name + " " + Metrics(hero, mount) + "\n";
    }

    static string Metrics(Transform hero, Transform mount)
    {
        string text = "";
        foreach (string bone in new[] { "Hips", "LeftLeg", "LeftFoot", "RightFoot", "LeftHand", "RightHand", "Head" })
            foreach (Transform t in hero.GetComponentsInChildren<Transform>(true))
                if (t.name == bone) { text += bone + mount.InverseTransformPoint(t.position).ToString("F2") + " "; break; }
        return text;
    }
}
