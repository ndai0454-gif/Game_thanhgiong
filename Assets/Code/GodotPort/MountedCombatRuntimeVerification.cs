using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Opt-in integration check for the FBX rig actually installed in campaign maps.</summary>
public sealed class MountedCombatRuntimeVerification : MonoBehaviour
{
    readonly List<string> results = new List<string>();
    int failures;
    string folder;
    MeshyHeroVisualDriver driver;
    ThanhGiongCampaignController actor;
    MountedHorseController movement;
    GodotMountedMotion motion;
    MeshyMountedCombatPose pose;
    void Check(bool ok, string text) { results.Add((ok ? "PASS " : "FAIL ") + text); if (!ok) failures++; Debug.Log("MOUNTED_CHECK " + results.Last()); }

    IEnumerator Start()
    {
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../MountedCombatVerification"));
        Directory.CreateDirectory(folder);
        yield return new WaitForSeconds(.3f);
        actor = FindAnyObjectByType<ThanhGiongCampaignController>();
        driver = actor.GetComponent<MeshyHeroVisualDriver>();
        movement = actor.GetComponent<MountedHorseController>();
        motion = actor.GetComponent<GodotMountedMotion>();
        pose = actor.GetComponentInChildren<MeshyMountedCombatPose>();
        Check(driver != null && pose != null && pose.IsReady && motion.IsRigReady, "active FBX rider and horse ready");
        if (driver == null || pose == null || !pose.IsReady) { Finish(); yield break; }
        var state = actor.CaptureCheckpointState();
        state.chapter = ThanhGiongCampaignController.Chapter.Battle; state.weapon = ThanhGiongCampaignController.Weapon.IronSword;
        state.growth = 3; state.kills = actor.swordBreakKills - 1; state.health = actor.maxHealth;
        state.bamboo = 0;
        Check(actor.RestoreCheckpointState(state, actor.transform.position, actor.transform.rotation), "battle setup restores checkpoint");
        movement.enabled = true;
        if (actor.battleRoot != null) actor.battleRoot.SetActive(false);
        yield return new WaitForSeconds(.6f);
        var transforms = driver.meshyRoot.GetComponentsInChildren<Transform>(true);
        var iron = transforms.First(x => x.name == "Weapon_IronSword");
        var bamboo = transforms.First(x => x.name == "Weapon_GoldenBamboo");
        Check(iron.gameObject.activeInHierarchy && !bamboo.gameObject.activeSelf, "iron spear visible; bamboo hidden before break");
        Check(pose.ReinGripError < .09f * Mathf.Abs(driver.meshyRoot.lossyScale.x), "left hand reaches leather reins error=" + pose.ReinGripError.ToString("F3"));
        Check(transforms.Count(x => x.name.EndsWith("Leather Rein")) == 2, "two bridle reins exist");
        Capture("01-riding-spear");
        Check(actor.TryMeleeAttack(), "spear attack accepted by campaign");
        yield return new WaitForSeconds(.12f);
        Capture("02-spear-thrust");
        Check(iron.GetComponentInChildren<TrailRenderer>().emitting, "spear trail active during strike");
        yield return new WaitForSeconds(.65f);
        var deadObject = new GameObject("Verification defeated enemy"); deadObject.SetActive(false);
        var enemy = deadObject.AddComponent<ThanhGiongEnemy>();
        actor.NotifyEnemyDefeated(enemy); Destroy(deadObject);
        yield return new WaitForSeconds(.10f);
        Check(actor.CurrentWeapon == ThanhGiongCampaignController.Weapon.None && !iron.gameObject.activeSelf && !bamboo.gameObject.activeSelf, "kill threshold breaks spear and hides both weapons");
        Check(pose.IsBreaking && GameObject.Find("BrokenSpear_Fragments") != null, "break recoil and detached fragments play");
        Capture("03-broken-spear");
        yield return new WaitForSeconds(.7f);
        var sceneGroves = FindObjectsByType<ThanhGiongCollectible>(FindObjectsSortMode.None)
            .Where(x => x.kind == ThanhGiongCollectible.Kind.Bamboo && x.gameObject.activeInHierarchy && x.transform.root != actor.transform.root).ToArray();
        Check(sceneGroves.Length >= 4, "actual scene bamboo groves registered count=" + sceneGroves.Length);
        if (sceneGroves.Length == 0) { Finish(); yield break; }
        var item = sceneGroves.OrderBy(x => Vector3.Distance(x.transform.position, actor.transform.position)).First();
        var controller = actor.GetComponent<CharacterController>();
        controller.enabled = false;
        actor.transform.position = item.transform.position + Vector3.right * 4f;
        controller.enabled = true; movement.ResetMovementState();
        item = sceneGroves.OrderBy(x => Vector3.ProjectOnPlane(x.transform.position - actor.transform.position, Vector3.up).sqrMagnitude).First();
        // The same method is called by E in UpdateBattle, including range/discovery.
        item.SendMessage("OnTriggerEnter", controller, SendMessageOptions.RequireReceiver);
        Check(actor.CaptureCheckpointState().bamboo == 0, "entering bamboo trigger does not silently consume E tap");
        for (int tap = 0; tap < 3; tap++)
        {
            Check(actor.TryPullNearestBamboo(), "E interaction finds scene grove on tap " + (tap + 1));
            Check(actor.CaptureCheckpointState().bamboo == tap + 1, "E tap advances pull exactly once");
            yield return new WaitForSeconds(.16f);
            Check(pose.IsPulling, "pull pose active on tap " + (tap + 1));
            if (tap == 0) Capture("04-pulling-bamboo");
        }
        yield return new WaitForSeconds(.7f);
        Check(actor.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo && bamboo.gameObject.activeInHierarchy && !iron.gameObject.activeSelf, "third pull equips bamboo in visible hand");
        var pulled = sceneGroves.Where(x => x.IsCollected).ToArray();
        Check(pulled.Length == 1 && !pulled[0].gameObject.activeInHierarchy, "scene grove collapses and is removed after third E");
        Check(sceneGroves.Any(x => x != item && x.gameObject.activeInHierarchy), "pulling one grove preserves other bamboo");
        Check(actor.TryMeleeAttack(), "bamboo attack accepted by campaign");
        yield return new WaitForSeconds(.18f); Capture("05-bamboo-sweep");
        var current = driver.heroAnimator.GetCurrentAnimatorStateInfo(0); var next = driver.heroAnimator.GetNextAnimatorStateInfo(0);
        Check(current.IsName("BambooSweep360") || current.IsName("BambooHeavySlam") || next.IsName("BambooSweep360") || next.IsName("BambooHeavySlam"), "bamboo attack enters fitted sweep or slam animation");
        yield return new WaitForSeconds(.9f);
        Check(GameObject.Find("BrokenSpear_Fragments") == null, "broken spear debris cleaned up");
        // Exercise the real CharacterController on an isolated floor, avoiding map obstacles.
        movement.enabled = false;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Mounted verification floor";
        floor.transform.position = new Vector3(0f, 99f, 0f); floor.transform.localScale = new Vector3(100f, 2f, 100f);
        controller.enabled = false; actor.transform.position = new Vector3(0f, 101f, 0f); controller.enabled = true;
        movement.ResetMovementState(); Physics.SyncTransforms();
        float elapsed = 0f;
        while (elapsed < 1f) { movement.DriveMovement(Vector2.zero, false); elapsed += Time.deltaTime; yield return null; }
        elapsed = 0f; float maxSideSlip = 0f;
        while (elapsed < 1.5f) { movement.DriveMovement(Vector2.up, true); elapsed += Time.deltaTime; yield return null; }
        Check(movement.PlanarSpeed > movement.runSpeed * .8f, "real movement accelerates to gallop speed=" + movement.PlanarSpeed.ToString("F2"));
        Capture("08-riding-gallop");
        var sourceEnemy = FindObjectsByType<ThanhGiongEnemy>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(x=>!x.isBoss);
        Check(sourceEnemy != null, "actual enemy model available for mounted collision");
        if(sourceEnemy != null)
        {
            var contactEnemy = Instantiate(sourceEnemy.gameObject).GetComponent<ThanhGiongEnemy>();
            contactEnemy.name = "Mounted Collision Verification Enemy";contactEnemy.gameObject.SetActive(true);
            contactEnemy.Initialize(actor,actor.transform);contactEnemy.enabled=false;
            contactEnemy.transform.position=actor.transform.position+actor.transform.forward*1.5f;
            var body=contactEnemy.GetComponent<Rigidbody>();if(body!=null){body.useGravity=false;body.constraints=RigidbodyConstraints.FreezeAll;}
            var extra=new GameObject("Duplicate enemy contact collider");extra.transform.SetParent(contactEnemy.transform,false);extra.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            var feedback=actor.GetComponent<GodotCombatFeedback>();
            int bursts=feedback.BloodBurstCount;float health=contactEnemy.HealthRatio;
            Check(actor.ProcessChargeContacts(true)==1 && contactEnemy.HealthRatio<health,"real charge overlap damages enemy exactly once despite multiple colliders");
            Check(feedback.BloodBurstCount==bursts+1 && feedback.ActiveBloodDropletCount==16 && feedback.LastBloodTargetName==contactEnemy.name,"mounted enemy collision emits red blood spray at enemy");
            float afterHealth=contactEnemy.HealthRatio;
            Check(actor.ProcessChargeContacts(true)==0 && contactEnemy.HealthRatio==afterHealth && feedback.BloodBurstCount==bursts+1,"charge cooldown prevents duplicate damage and blood");
            var legacy=actor.GetComponent<MountedHorsePhysicsTrample>();
            if(legacy==null)legacy=actor.gameObject.AddComponent<MountedHorsePhysicsTrample>();
            Check(legacy.CampaignOwnsCharge && legacy.ProcessTrampleContacts()==0 && contactEnemy.HealthRatio==afterHealth,"legacy trample yields to campaign charge");
            Check(!feedback.PlayMountedImpact(actor.transform,actor.transform.position,actor.transform.forward),"collision blood never targets rider or horse");
            yield return new WaitForSeconds(.15f);
            CaptureImpact("10-mounted-blood",contactEnemy.transform);
            yield return new WaitForSeconds(1.1f);
            Check(feedback.ActiveBloodBurstCount==0 && feedback.ActiveBloodDropletCount==0,"blood spray cleans up after its lifetime");
            Destroy(contactEnemy.gameObject);
            yield return null; // let contact colliders leave the world before measuring unobstructed turning
        }
        elapsed = 0f;
        while (elapsed < 1f)
        {
            movement.DriveMovement(Vector2.right, true);
            maxSideSlip = Mathf.Max(maxSideSlip, Mathf.Abs(Vector3.Dot(movement.WorldVelocity, actor.transform.right)));
            elapsed += Time.deltaTime; yield return null;
        }
        Check(maxSideSlip < .05f, "turning travels along horse heading sideSlip=" + maxSideSlip.ToString("F3"));
        Capture("09-riding-turn");
        elapsed = 0f;
        while (elapsed < 1.5f) { movement.DriveMovement(Vector2.zero, false); elapsed += Time.deltaTime; yield return null; }
        Check(movement.PlanarSpeed < .08f, "releasing movement settles horse speed=" + movement.PlanarSpeed.ToString("F3"));
        float idlePhase = motion.GaitPhase;
        yield return new WaitForSeconds(.25f);
        Check(Mathf.Abs(Mathf.DeltaAngle(idlePhase * 360f, motion.GaitPhase * 360f)) < 1f, "stationary horse stops stepping");
        Check(pose.ReinGripError < .12f * Mathf.Abs(driver.meshyRoot.lossyScale.x), "rider reins stay attached through real movement and turn");
        Destroy(floor);
        motion.enabled = false; movement.enabled = false;
        var skin = driver.meshyHorse.GetComponentInChildren<SkinnedMeshRenderer>();
        var before = new Mesh(); var after = new Mesh(); skin.BakeMesh(before);
        for (int i = 0; i < 40; i++) { motion.Drive(1f / 60f, 5f, .55f, 1f, true, 0, 0, 0, -1, false); yield return null; }
        skin.BakeMesh(after);
        var a = before.vertices; var b = after.vertices;
        Check(a.Zip(b, (x, y) => Vector3.Distance(x, y)).Max() > .03f, "horse skinned geometry moves during walk"); Capture("06-horse-walk");
        for (int i = 0; i < 40; i++) { motion.Drive(1f / 60f, 8.5f, 1f, 1f, true, 0, 0, 0, -1, false); yield return null; }
        Capture("07-horse-gallop");
        Check(pose.ReinGripError < .12f * Mathf.Abs(driver.meshyRoot.lossyScale.x), "rider keeps reins during gallop");
        Destroy(before); Destroy(after);
        Time.timeScale = 0f;
        yield return null; // settle the final already-scheduled LateUpdate before measuring pause
        Vector3 hand = transforms.First(x => x.name == "LeftHand").position;
        float clock = motion.MotionTime;
        yield return new WaitForSecondsRealtime(.2f);
        Check(Vector3.Distance(hand, transforms.First(x => x.name == "LeftHand").position) < .001f && Mathf.Approximately(clock, motion.MotionTime), "pause freezes mounted pose and horse");
        Time.timeScale = 1f; motion.enabled = true; movement.enabled = true;
        Finish();
    }

    void Finish()
    {
        results.Add("MOUNTED_COMBAT " + (failures == 0 ? "PASS" : "FAIL") + " failures=" + failures);
        File.WriteAllLines(Path.Combine(folder, "results.txt"), results); Debug.Log(results.Last());
    }

    void Capture(string label)
    {
        var cameraObject = new GameObject("Mounted Verification Camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        float scale = Mathf.Abs(driver.meshyRoot.lossyScale.x);
        Vector3 center = actor.transform.position + Vector3.up * (1.45f * scale);
        camera.transform.position = center + actor.transform.TransformDirection(new Vector3(3.8f, 1.1f, 4.8f)) * scale;
        camera.transform.LookAt(center); camera.orthographic = true; camera.orthographicSize = 2.05f * scale;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f,.08f,.075f); camera.cullingMask = 1 << 31;
        var rendered = driver.meshyRoot.GetComponentsInChildren<Transform>(true);
        var layers = rendered.Select(x => x.gameObject.layer).ToArray();
        var target = RenderTexture.GetTemporary(1000, 1000, 24); var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            foreach (var t in rendered) t.gameObject.layer = 31;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image = new Texture2D(1000,1000,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1000,1000),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(folder,label+".png"), image.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < rendered.Length; i++) if (rendered[i] != null) rendered[i].gameObject.layer = layers[i];
            RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(target);
            if (image != null) Destroy(image); Destroy(cameraObject);
        }
    }

    void CaptureImpact(string label,Transform enemy)
    {
        var go=new GameObject("Mounted blood verification camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
        Vector3 center=Vector3.Lerp(actor.transform.position,enemy.position,.5f)+Vector3.up*1.7f;
        camera.transform.position=center+actor.transform.right*6f-actor.transform.forward*4f+Vector3.up*3f;camera.transform.LookAt(center);
        camera.orthographic=true;camera.orthographicSize=4.4f;
        var target=RenderTexture.GetTemporary(1200,900,24);var previous=RenderTexture.active;Texture2D image=null;
        try
        {
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image=new Texture2D(1200,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,900),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(folder,label+".png"),image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);
            if(image!=null)Destroy(image);Destroy(go);
        }
    }
}
