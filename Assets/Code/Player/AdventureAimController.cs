using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared ballistic solution for the visible guide and the launched rigidbody.</summary>
[DefaultExecutionOrder(80)]
[DisallowMultipleComponent]
public sealed class AdventureAimController : MonoBehaviour
{
    public float throwSpeed = 22f, maximumRange = 40f, lockAngle = 12f;
    public bool IsAiming { get; private set; }
    public ThanhGiongEnemy SoftLockTarget { get; private set; }
    public Vector3 AimPoint { get; private set; }
    public Vector3 LaunchVelocity { get; private set; }
    public bool TrajectoryVisible => guide != null && guide.activeSelf;
    public float BambooCooldownRemaining => Mathf.Max(0, nextBamboo - Time.time);
    public Vector3 LaunchOrigin => campaign != null && campaign.CarriedWeapon != null ? campaign.CarriedWeapon.transform.position :
        launchHand != null ? launchHand.position + transform.forward * .35f : transform.position + Vector3.up * 2.3f + transform.forward * 1.3f;
    Transform launchHand;
    ThanhGiongCampaignController campaign;
    MountedHorseController movement;
    Camera view;
    GameObject guide;
    readonly List<LineRenderer> segments = new List<LineRenderer>();
    readonly RaycastHit[] hits = new RaycastHit[96];
    readonly Collider[] candidates = new Collider[128];
    Material guideMaterial;
    float nextBamboo;
    bool? overrideAim;

    void Awake() { campaign = GetComponent<ThanhGiongCampaignController>(); movement = GetComponent<MountedHorseController>(); view = Camera.main; }
    void Start()
    {
        var art=GetComponent<MeshyHeroVisualDriver>();
        if(art!=null&&art.meshyHero!=null)
            foreach(var bone in art.meshyHero.GetComponentsInChildren<Transform>())
                if(bone.name.EndsWith("RightHand")){launchHand=bone;break;}
    }
    public void SetAimOverride(bool? value) { overrideAim = value; }
    void Update()
    {
        if (view == null) view = Camera.main;
        IsAiming = campaign != null && campaign.IsBattleActive && !campaign.EquipmentBusy && Time.timeScale > 0 &&
            !movement.IsDodging && (overrideAim ?? Input.GetMouseButton(1));
        if (view == null || campaign == null) return;
        UpdateSolution();
        bool show = IsAiming && (campaign.CarriedWeapon != null || campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo);
        if (show && guide == null) BuildGuide();
        if (guide != null) { guide.SetActive(show); if (show) DrawTrajectory(); }
    }

    public void UpdateSolution()
    {
        if (view == null) view = Camera.main;
        if (view == null) return;
        Ray ray = view.ViewportPointToRay(new Vector3(.5f, .5f));
        AimPoint = ray.origin + ray.direction * maximumRange;
        float closest = maximumRange;
        int count = Physics.RaycastNonAlloc(ray, hits, maximumRange, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++) {
            if (IsSelf(hits[i].transform) || hits[i].distance >= closest) continue;
            closest = hits[i].distance; AimPoint = hits[i].point;
        }
        SoftLockTarget = null;
        if (IsAiming) {
            float best = float.MaxValue;
            count = Physics.OverlapSphereNonAlloc(transform.position, maximumRange, candidates, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) {
                var enemy = candidates[i].GetComponentInParent<ThanhGiongEnemy>();
                if (enemy == null || enemy.HealthRatio <= 0 || !enemy.gameObject.activeInHierarchy) continue;
                Vector3 point = TargetPoint(enemy), delta = point - ray.origin;
                float angle = Vector3.Angle(ray.direction, delta);
                Vector3 screen = view.WorldToViewportPoint(point);
                if (angle > lockAngle || screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) continue;
                float score = angle + delta.magnitude * .015f;
                if (score >= best || !ClearLine(LaunchOrigin, point, enemy)) continue;
                best = score; SoftLockTarget = enemy;
            }
            if (SoftLockTarget != null) AimPoint = TargetPoint(SoftLockTarget);
        }
        Vector3 origin = LaunchOrigin;
        if (!SolveBallistic(origin, AimPoint, throwSpeed, out Vector3 velocity)) {
            velocity = (AimPoint - origin).normalized * throwSpeed;
            velocity.y += 4.5f;
        }
        LaunchVelocity = velocity;
    }
    public static Vector3 TargetPoint(ThanhGiongEnemy enemy)
    {
        var capsule = enemy.GetComponent<CapsuleCollider>();
        return capsule != null ? capsule.bounds.center : enemy.transform.position + Vector3.up * 1.4f;
    }
    bool IsSelf(Transform value) => value.IsChildOf(transform) || campaign != null && campaign.CarriedWeapon != null && value.IsChildOf(campaign.CarriedWeapon.transform);
    bool ClearLine(Vector3 start, Vector3 end, ThanhGiongEnemy targetEnemy)
    {
        Vector3 delta = end - start;
        int n = Physics.RaycastNonAlloc(start, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) {
            if (IsSelf(hits[i].transform) || hits[i].transform.GetComponentInParent<ThanhGiongEnemy>() == targetEnemy) continue;
            return false;
        }
        return true;
    }
    public static bool SolveBallistic(Vector3 origin, Vector3 target, float speed, out Vector3 velocity)
    {
        Vector3 delta = target - origin, flat = Vector3.ProjectOnPlane(delta, Vector3.up);
        float distance = flat.magnitude, gravity = -Physics.gravity.y, square = speed * speed;
        float determinant = square * square - gravity * (gravity * distance * distance + 2 * delta.y * square);
        if (distance < .1f || gravity <= 0 || determinant < 0) { velocity = Vector3.zero; return false; }
        float tangent = (square - Mathf.Sqrt(determinant)) / (gravity * distance);
        float cosine = 1f / Mathf.Sqrt(1 + tangent * tangent);
        velocity = flat.normalized * (speed * cosine) + Vector3.up * (speed * cosine * tangent);
        return true;
    }
    public bool TryFireBamboo()
    {
        if (!IsAiming || !campaign.IsBattleActive || campaign.CurrentWeapon != ThanhGiongCampaignController.Weapon.Bamboo || campaign.EquipmentBusy ||
            movement.IsDodging || Time.timeScale <= 0 || Time.time < nextBamboo) return false;
        nextBamboo = Time.time + 1.2f;
        UpdateSolution();
        var projectile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        projectile.name = "Tre Ngà • Phóng tre";
        projectile.transform.position = LaunchOrigin;
        projectile.transform.localScale = new Vector3(.12f, .9f, .12f);
        projectile.transform.rotation = Quaternion.FromToRotation(Vector3.up, LaunchVelocity.normalized);
        var weapon = projectile.AddComponent<ThanhGiongImprovisedWeapon>();
        weapon.throwDamage = 38f; weapon.stunDuration = .45f; weapon.aoeRadius = 1.5f;
        var renderer = projectile.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(.42f, .61f, .16f); renderer.material = mat;
        projectile.AddComponent<AdventureProjectileMaterial>().owned = mat;
        weapon.Launch(LaunchVelocity, transform);
        movement.TriggerCampaignAttack(.45f);
        IsometricCameraFollow.Instance?.Shake(.12f, .18f);
        return true;
    }
    void BuildGuide()
    {
        guide = new GameObject("Dotted ballistic aiming guide"); guide.transform.SetParent(transform, false);
        guideMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        for (int i = 0; i < 28; i++) {
            var go = new GameObject("Arc dash"); go.transform.SetParent(guide.transform, false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = guideMaterial;
            line.positionCount = 2; line.useWorldSpace = true; line.startWidth = line.endWidth = .035f;
            line.startColor = line.endColor = new Color(.74f, .94f, 1f, .8f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            segments.Add(line);
        }
    }
    void DrawTrajectory()
    {
        Vector3 origin = LaunchOrigin, previous = origin;
        bool blocked = false;
        for (int i = 0; i < segments.Count; i++) {
            float t = (i + 1) * .055f;
            Vector3 point = origin + LaunchVelocity * t + .5f * Physics.gravity * t * t;
            Vector3 delta = point - previous;
            if (!blocked) {
                int n = Physics.SphereCastNonAlloc(previous, .1f, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < n; j++) if (!IsSelf(hits[j].transform)) { blocked = true; break; }
            }
            segments[i].enabled = !blocked;
            segments[i].SetPosition(0, previous); segments[i].SetPosition(1, Vector3.Lerp(previous, point, .60f));
            previous = point;
        }
    }
    void OnDisable() { IsAiming = false; SoftLockTarget = null; if (guide != null) guide.SetActive(false); }
    void OnDestroy() { if (guideMaterial != null) Destroy(guideMaterial); if (guide != null) Destroy(guide); }
}

public sealed class AdventureProjectileMaterial : MonoBehaviour
{
    public Material owned;
    void OnDestroy() { if (owned != null) Destroy(owned); }
}
