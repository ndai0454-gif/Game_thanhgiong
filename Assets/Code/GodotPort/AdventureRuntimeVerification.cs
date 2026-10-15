using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>Opt-in Play Mode integration probe. Never attach or save in campaign scenes.</summary>
public sealed class AdventureRuntimeVerification : MonoBehaviour
{
    public static bool Complete { get; private set; }
    readonly List<string> results = new List<string>();
    readonly Dictionary<Behaviour, bool> enabledBefore = new Dictionary<Behaviour, bool>();
    readonly List<GameObject> temporary = new List<GameObject>();
    ThanhGiongCampaignController actor;
    ThanhGiongCampaignController.CheckpointState originalState;
    MountedHorseController movement;
    AdventureAimController aim;
    AdventureStamina stamina;
    IsometricCameraFollow follow;
    ThanhGiongCampaignHUD hud;
    Camera view;
    Vector3 originalPosition;
    Quaternion originalRotation;
    float originalYaw, originalPitch, originalTimeScale;
    bool originalBattleRoot, originalBambooRoot, cleaned;
    string folder;

    IEnumerator Start()
    {
        Complete = false;
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../AdventureVerification", gameObject.scene.name));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "runtime-results.txt"), "");
        IEnumerator scenario = Run();
        while (true)
        {
            bool next;
            try { next = scenario.MoveNext(); }
            catch (Exception e) { Check(false, "probe exception: " + e); break; }
            if (!next) break;
            yield return scenario.Current;
        }
        Cleanup();
        Complete = true;
        Debug.Log("ADVENTURE_VERIFICATION_COMPLETE " + gameObject.scene.name + " checks=" + results.Count + " failures=" + results.Count(x => x.StartsWith("FAIL")));
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(.4f);
        actor = FindFirstObjectByType<ThanhGiongCampaignController>();
        view = Camera.main;
        follow = view != null ? view.GetComponent<IsometricCameraFollow>() : null;
        movement = actor != null ? actor.GetComponent<MountedHorseController>() : null;
        aim = actor != null ? actor.GetComponent<AdventureAimController>() : null;
        stamina = actor != null ? actor.GetComponent<AdventureStamina>() : null;
        hud = FindFirstObjectByType<ThanhGiongCampaignHUD>();
        Check(actor != null && view != null && follow != null && movement != null && aim != null && stamina != null && hud != null,
            "installed campaign has shoulder camera, aim, stamina and contextual HUD");
        if (actor == null || follow == null || movement == null || aim == null || stamina == null || hud == null) yield break;
        var motion = actor.GetComponent<GodotMountedMotion>();
        var pose = actor.GetComponentInChildren<MeshyMountedCombatPose>();
        Check(motion != null && motion.IsRigReady && pose != null && pose.IsReady, "actual mounted rider and horse rigs ready");
        var visuals=actor.GetComponent<AdventureVisualBootstrap>();
        yield return new WaitForSeconds(.5f);
        Check(visuals!=null&&visuals.IsBuilt&&visuals.StyledActorRenderers>0&&visuals.InteractiveFoliageRenderers>0,
            "textured cel/rim actors and interactive foliage are active in real campaign");
        Check(visuals!=null&&visuals.AmbientLeaves!=null&&visuals.AmbientLeaves.isPlaying&&visuals.ResourceGlowCount>0&&visuals.AtmosphereVolume!=null,
            "ambient bamboo leaves, resource glows and atmosphere volume are installed");
        if(visuals!=null) {
            visuals.enabled=false; yield return null;
            visuals.enabled=true; yield return new WaitForSeconds(.5f);
            Check(visuals.IsBuilt&&actor.GetComponentsInChildren<Transform>().Count(x=>x.name=="Adventure Runtime Presentation")==1,
                "presentation re-enable rebuilds effects without duplicate runtime roots");
        }
        hud.enabled=false;yield return null;hud.enabled=true;
        yield return new WaitForSeconds(.1f);
        foreach (var boss in FindObjectsByType<ThanhGiongEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => x.isBoss))
        {
            var presentation = boss.GetComponent<LegendEnemyPresentation>();
            var animation = boss.GetComponent<ThanhGiongEnemyAnimationDriver>();
            Check(presentation != null && presentation.sourceAsset.EndsWith("GiacAnGeneral_Meshy.prefab") && animation != null &&
                animation.actionForgeMotions != null && animation.actionForgeMotions.name == "GiacAnGeneral",
                "campaign boss retains Meshy general and ActionForge exact-rig motion set");
        }

        originalPosition = actor.transform.position; originalRotation = actor.transform.rotation;
        originalState = actor.CaptureCheckpointState(); originalTimeScale = Time.timeScale;
        originalYaw = follow.OrbitYaw; originalPitch = follow.orbitPitch;
        originalBattleRoot = actor.battleRoot != null && actor.battleRoot.activeSelf;
        originalBambooRoot = actor.bambooRoot != null && actor.bambooRoot.activeSelf;
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (behaviour is ThanhGiongSceneTransition || behaviour.GetType().Name.Contains("SceneGate")) Suspend(behaviour);
        var state = actor.CaptureCheckpointState();
        state.chapter = ThanhGiongCampaignController.Chapter.Battle;
        state.weapon = ThanhGiongCampaignController.Weapon.Bamboo;
        state.growth = 3; state.health = actor.maxHealth; state.kills = 0; state.qte = 3;
        Vector3 arena = new Vector3(5000, 100, 5000);
        var floor = Box("Adventure verification floor", arena + Vector3.down * .5f, new Vector3(120, 1, 120));
        Check(actor.RestoreCheckpointState(state, arena + Vector3.up * .1f, Quaternion.identity), "real campaign checkpoint enters isolated battle arena");
        foreach (var enemy in FindObjectsByType<ThanhGiongEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Suspend(enemy);
        Suspend(movement); // DriveMovement still invokes the real CharacterController path below.
        aim.SetAimOverride(false);
        yield return new WaitForSeconds(1f);
        movement.ResetMovementState();
        for (int i = 0; i < 15; i++) { movement.DriveMovement(Vector2.zero, false); yield return null; }
        follow.SnapToTarget(); follow.SetOrbit(0, 15);
        yield return new WaitForSeconds(.5f);
        Check(!view.orthographic && view.fieldOfView >= 75 && view.fieldOfView <= 85,
            "exploration uses perspective FOV 75-85 degrees actual=" + view.fieldOfView.ToString("F2"));
        Check(follow.ActualBoomDistance >= 5 && follow.ActualBoomDistance <= 6 &&
            Vector3.Distance(view.transform.position, follow.ShoulderPivot) >= 5,
            "clear-field camera boom remains 5-6 world metres actual=" + follow.ActualBoomDistance.ToString("F3"));
        Vector3 cameraBefore = view.transform.position;
        follow.SetOrbit(90, 15);
        yield return new WaitForSeconds(.7f);
        Check(Mathf.Abs(Mathf.DeltaAngle(view.transform.eulerAngles.y, 90)) < 1 && Vector3.Distance(cameraBefore, view.transform.position) > 3,
            "orbit changes real camera position and view direction");
        Vector3 start = actor.transform.position;
        int dustBefore=visuals!=null?visuals.FootstepBursts:0;
        for (float until = Time.time + 1.25f; Time.time < until;)
        { movement.DriveMovement(Vector2.up, false); yield return null; }
        Vector3 travelled = Vector3.ProjectOnPlane(actor.transform.position - start, Vector3.up);
        Check(travelled.magnitude > 1 && Vector3.Dot(travelled.normalized, Vector3.right) > .85f,
            "forward input moves actual CharacterController along orbit camera heading displacement=" + travelled);
        Check(visuals!=null&&visuals.FootstepBursts>dustBefore,"actual moving horse hoof contacts emit grounded dust");
        var art=actor.GetComponent<MeshyHeroVisualDriver>();
        Vector3 foot=art!=null&&art.meshyHorse!=null?art.meshyHorse.position:actor.transform.position;
        Vector3 trample=Shader.GetGlobalVector("_AdventurePlayerPosition");
        Check(Vector2.Distance(new Vector2(foot.x,foot.z),new Vector2(trample.x,trample.z))<.15f,
            "GPU grass trampling follows visible horse rather than offset gameplay capsule");
        for (int i = 0; i < 30; i++) { movement.DriveMovement(Vector2.zero, false); yield return null; }
        movement.ResetMovementState();
        follow.SetOrbit(90, 15);
        yield return new WaitForSeconds(.5f);
        Vector3 direction = -view.transform.forward;
        var wall = Box("Adventure verification camera wall", follow.ShoulderPivot + direction * 3.2f, new Vector3(8, 8, .35f));
        wall.transform.rotation = Quaternion.LookRotation(direction);
        Physics.SyncTransforms();
        yield return new WaitForSeconds(.2f);
        Check(follow.ActualBoomDistance > .2f && follow.ActualBoomDistance < 3.2f &&
            Vector3.Distance(view.transform.position, follow.ShoulderPivot) < 3.2f,
            "temporary wall retracts actual camera immediately boom=" + follow.ActualBoomDistance.ToString("F3"));
        wall.SetActive(false);
        yield return new WaitForSeconds(1.6f);
        Check(follow.ActualBoomDistance > 5.5f, "camera smoothly restores full boom after obstruction clears");

        stamina.Restore(); stamina.Tick(true, 1);
        Check(stamina.Current < stamina.capacity && stamina.Current > 0, "sprint consumes actual stamina resource");
        hud.TogglePause();
        Vector3 pausedCamera = view.transform.position, pausedPlayer = actor.transform.position;
        float pausedStamina = stamina.Current;
        actor.TeleportPlayer(pausedPlayer + Vector3.right * 2);
        movement.DriveMovement(Vector2.up, true);
        yield return new WaitForSecondsRealtime(.35f);
        Check(hud.IsPaused && Time.timeScale == 0 && Vector3.Distance(pausedCamera, view.transform.position) < .001f,
            "actual pause menu freezes camera even if target changes");
        Check(Mathf.Abs(stamina.Current - pausedStamina) < .001f && !aim.IsAiming,
            "pause freezes stamina and suppresses aiming");
        hud.TogglePause(); actor.TeleportPlayer(pausedPlayer);
        stamina.Tick(true, stamina.capacity / Mathf.Max(.01f, stamina.sprintCostPerSecond) + 1);
        Check(!stamina.SprintAllowed && stamina.Current <= .001f, "exhaustion blocks sprint");
        yield return new WaitForSeconds(1.8f);
        Check(stamina.SprintAllowed && stamina.Current > stamina.capacity * .2f, "exhausted stamina regenerates and restores sprint eligibility");
        stamina.Restore();

        actor.transform.rotation = Quaternion.identity;
        follow.SnapToTarget(); follow.SetOrbit(0, 5);
        aim.SetAimOverride(true);
        yield return new WaitForSeconds(.7f);
        Check(aim.IsAiming && view.fieldOfView < 65 && follow.ActualBoomDistance < 4.4f,
            "aim mode narrows FOV and brings shoulder camera closer");
        Ray centerRay = view.ViewportPointToRay(new Vector3(.5f, .5f));
        Vector3 fixturePoint = centerRay.origin + centerRay.direction * 18;
        var fixture = new GameObject("Adventure verification multicollider enemy"); temporary.Add(fixture);
        fixture.SetActive(false); fixture.transform.position = fixturePoint;
        var target = fixture.AddComponent<ThanhGiongEnemy>();
        target.maxHealth = 1000; target.role = ThanhGiongEnemy.EnemyRole.Spearman;
        var capsule = fixture.GetComponent<CapsuleCollider>(); capsule.height = 2; capsule.radius = .6f; capsule.center = Vector3.zero;
        var second = new GameObject("Second hurt collider"); second.transform.SetParent(fixture.transform, false);
        second.AddComponent<BoxCollider>().size = Vector3.one;
        fixture.SetActive(true); target.Initialize(actor, actor.transform); target.enabled = false;
        foreach (var behaviour in fixture.GetComponents<Behaviour>()) behaviour.enabled = false;
        target.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        target.Body.isKinematic = true; Physics.SyncTransforms();
        yield return new WaitForSeconds(.15f);
        aim.UpdateSolution();
        Check(aim.SoftLockTarget == target, "soft lock selects visible enemy inside camera aim cone");
        Check(aim.TrajectoryVisible && hud.AimReticleVisible && hud.AimTarget == target,
            "dotted ballistic guide and contextual target reticle appear during aiming");
        Vector3 blockPoint = Vector3.Lerp(aim.LaunchOrigin, fixturePoint, .5f);
        var blocker = Box("Adventure verification aiming obstruction", blockPoint, new Vector3(3, 4, .4f));
        blocker.transform.rotation = Quaternion.LookRotation(fixturePoint - aim.LaunchOrigin);
        Physics.SyncTransforms(); aim.UpdateSolution();
        Check(aim.SoftLockTarget == null, "soft lock rejects target behind actual solid obstruction");
        blocker.SetActive(false); Physics.SyncTransforms();
        yield return null; aim.UpdateSolution();
        Vector3 origin = aim.LaunchOrigin, velocity = aim.LaunchVelocity;
        Vector3 horizontal = Vector3.ProjectOnPlane(fixturePoint - origin, Vector3.up);
        float flightTime = horizontal.magnitude / Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
        Vector3 predicted = origin + velocity * flightTime + Physics.gravity * (.5f * flightTime * flightTime);
        Check(Vector3.Distance(predicted, fixturePoint) < .03f, "shared guide solution reaches live target under actual scene gravity error=" + Vector3.Distance(predicted, fixturePoint).ToString("F4"));
        float healthBefore = target.HealthRatio * target.maxHealth;
        Check(aim.TryFireBamboo(), "campaign accepts actual aimed bamboo projectile");
        var projectile = FindObjectsByType<ThanhGiongImprovisedWeapon>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "Tre Ngà • Phóng tre");
        Check(projectile != null && projectile.GetComponent<Rigidbody>() != null && !projectile.GetComponent<Rigidbody>().isKinematic,
            "aimed throw creates physical rigidbody projectile");
        if (projectile != null)
        {
            temporary.Add(projectile.gameObject);
            Vector3 launchedFrom = projectile.transform.position;
            Vector3 launchedVelocity = projectile.GetComponent<Rigidbody>().linearVelocity;
            float launchedAt = Time.fixedTime;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            if (projectile != null)
            {
                float elapsed = Time.fixedTime - launchedAt;
                Vector3 physicalPrediction = launchedFrom + launchedVelocity * elapsed + .5f * Physics.gravity * elapsed * elapsed;
                Check(Vector3.Distance(projectile.transform.position, physicalPrediction) < .35f &&
                    Vector3.Distance(projectile.transform.position, launchedFrom) > 1,
                    "actual rigidbody advances along displayed ballistic solution error=" + Vector3.Distance(projectile.transform.position, physicalPrediction).ToString("F3"));
            }
            else Check(false, "projectile survives initial clear ballistic flight");
        }
        float timeout = Time.time + 2.5f;
        while (Time.time < timeout && Mathf.Abs(target.HealthRatio * target.maxHealth - healthBefore) < .01f) yield return null;
        float damage = healthBefore - target.HealthRatio * target.maxHealth;
        Check(Mathf.Abs(damage - 38) < .1f, "real projectile impact damages enemy once despite multiple hurt colliders actual=" + damage.ToString("F2"));
        yield return new WaitForSeconds(.35f);
        Check(Mathf.Abs(healthBefore - target.HealthRatio * target.maxHealth - 38) < .1f,
            "collision and sweep callbacks cannot duplicate impact damage");
        aim.SetAimOverride(false);
        yield return new WaitForSeconds(.3f);
        Check(!aim.TrajectoryVisible && !hud.AimReticleVisible, "aim guide and reticle hide when aiming ends");
        var minimap = typeof(ThanhGiongCampaignHUD).GetField("mapCamera", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(hud) as Camera;
        Check(minimap != null && minimap.targetTexture != null && minimap.targetTexture.IsCreated() && !minimap.CompareTag("MainCamera"),
            "HUD minimap renders real terrain through separate render-texture camera");
        yield return new WaitForEndOfFrame();
        Texture2D frame=ScreenCapture.CaptureScreenshotAsTexture();
        bool hasWorld=false;
        for(int x=1;x<8;x++)for(int y=1;y<8;y++)if(frame.GetPixel(frame.width*x/8,frame.height*y/8).grayscale>.04f)hasWorld=true;
        Destroy(frame);
        Check(hasWorld,"minimap mask preserves Game View backbuffer and full HUD frame is not black");
        Check(actor.Kills == 0 && actor.CurrentChapter == ThanhGiongCampaignController.Chapter.Battle,
            "verification damage does not award kills or trigger stage progression");
    }

    GameObject Box(string label, Vector3 position, Vector3 scale)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = label;
        box.transform.position = position; box.transform.localScale = scale;
        temporary.Add(box); return box;
    }
    void Suspend(Behaviour behaviour)
    {
        if (!enabledBefore.ContainsKey(behaviour)) enabledBefore.Add(behaviour, behaviour.enabled);
        behaviour.enabled = false;
    }
    void Check(bool ok, string message)
    {
        string result = (ok ? "PASS " : "FAIL ") + message;
        results.Add(result); Debug.Log("ADVENTURE_CHECK " + result);
        File.WriteAllLines(Path.Combine(folder, "runtime-results.txt"), results);
    }
    void Cleanup()
    {
        if (cleaned) return; cleaned = true;
        if (hud != null && hud.IsPaused) hud.TogglePause();
        if (aim != null) aim.SetAimOverride(null);
        foreach (var go in temporary) if (go != null) { go.SetActive(false); Destroy(go); }
        if (actor != null && originalState != null)
        {
            actor.RestoreCheckpointState(originalState, originalPosition, originalRotation);
            if (actor.battleRoot != null) actor.battleRoot.SetActive(originalBattleRoot);
            if (actor.bambooRoot != null) actor.bambooRoot.SetActive(originalBambooRoot);
        }
        foreach (var pair in enabledBefore) if (pair.Key != null) pair.Key.enabled = pair.Value;
        if (stamina != null) stamina.Restore();
        if (follow != null) { follow.SnapToTarget(); follow.SetOrbit(originalYaw, originalPitch); }
        if (originalState != null) Time.timeScale = originalTimeScale;
    }
    void OnDestroy() { Cleanup(); }
}
