using UnityEngine;

public class IsometricCameraFollow : MonoBehaviour
{
    public enum Shot { Explore, Combat, Vista, Boss, Close }
    public static IsometricCameraFollow Instance { get; private set; }
    public Transform target;
    public Vector3 offset = new Vector3(-18f, 22f, -18f);
    public float followSmoothness = 7f;
    public float lookHeight = 1.2f;
    public Shot currentShot = Shot.Explore;
    public float fieldOfView = 34f;
    public float lookAhead = 2.5f;
    public float pitch = 35.26439f;
    public Rigidbody targetBody;

    [Header("Third person adventure")]
    public bool useShoulderCamera = true;
    [Range(5f, 6f)] public float shoulderDistance = 5.8f;
    [Range(75f, 85f)] public float exploreFov = 80f;
    public float aimingFov = 60f;
    public float shoulderOffset = .85f;
    public float orbitSensitivity = 2.2f;
    public float orbitPitch = 15f;
    public float collisionRadius = .22f;
    public bool IsAiming => useShoulderCamera && aim != null && aim.IsAiming;
    public ThanhGiongEnemy SoftLockTarget => aim != null ? aim.SoftLockTarget : null;
    public float ActualBoomDistance { get; private set; }
    public Vector3 ShoulderPivot { get; private set; }
    public float OrbitYaw => orbitYaw;
    public Vector3 BodyFocus { get; private set; }
    MeshyHeroVisualDriver visualDriver;
    AdventureAimController aim;
    float orbitYaw, boomDistance;
    bool orbitInitialized;
    readonly RaycastHit[] cameraHits = new RaycastHit[96];

    [Header("2.5D isometric framing")]
    public bool useOrthographicIsometric = true;
    public float isometricPitch = 35.26439f;
    public float isometricYaw = 45f;
    public float cameraDistance = 38f;
    public float exploreSize = 13f, combatSize = 11f, vistaSize = 27f, bossSize = 17f, closeSize = 8f;
    public float zoomSmoothness = 5f;
    public float leadSeconds = .3f;
    public float maximumShake = .32f;

    Camera view;
    CharacterController targetController;
    MountedHorseController targetMovement;
    Transform boundTarget;
    Vector3 smoothPosition, smoothLead;
    float shakeTimer, shakeIntensity, shakeAge;
    bool isOverview, initialized;
    Vector3 overviewPosition, overviewLookAt;

    void Awake() { Instance = this; view = GetComponent<Camera>(); ApplyProjection(); }
    void OnDestroy() { if (Instance == this) Instance = null; }
    float ShotSize => currentShot == Shot.Combat ? combatSize : currentShot == Shot.Vista ? vistaSize : currentShot == Shot.Boss ? bossSize : currentShot == Shot.Close ? closeSize : exploreSize;
    Quaternion IsometricRotation => Quaternion.Euler(isometricPitch, isometricYaw, 0f);

    void ApplyProjection()
    {
        if (view == null) view = GetComponent<Camera>();
        if (view == null) return;
        view.orthographic = !useShoulderCamera && useOrthographicIsometric;
        if (!useShoulderCamera) view.fieldOfView = fieldOfView;
        if (!initialized) view.orthographicSize = Mathf.Max(1f, ShotSize);
    }

    public void SetShot(Shot shot)
    {
        currentShot = shot;
        // Retain the perspective API for legacy consumers that explicitly disable isometric mode.
        switch (shot) {
            case Shot.Combat: offset = new Vector3(-14f, 18f, -14f); lookAhead = 2f; break;
            case Shot.Vista: offset = new Vector3(-34f, 42f, -34f); lookAhead = 0f; break;
            case Shot.Boss: offset = new Vector3(-22f, 28f, -22f); lookAhead = 0f; break;
            case Shot.Close: offset = new Vector3(-8f, 10f, -8f); lookAhead = 0f; break;
            default: offset = new Vector3(-18f, 22f, -18f); lookAhead = 2.5f; break;
        }
    }
    public void Shake(float duration, float intensity) { shakeTimer = Mathf.Max(shakeTimer, Mathf.Max(0, duration)); shakeIntensity = Mathf.Max(shakeIntensity, Mathf.Clamp01(intensity)); }
    public void SetOverview(Vector3 position, Vector3 lookAtPoint) { isOverview = true; overviewPosition = position; overviewLookAt = lookAtPoint; }
    public void ClearOverview() { isOverview = false; }
    public void SnapToTarget()
    {
        isOverview = false;
        if (target == null) return;
        ApplyProjection();
        if (useShoulderCamera) {
            orbitYaw = target.eulerAngles.y; orbitInitialized = true;
            boomDistance = shoulderDistance; initialized = false;
            UpdateShoulder(1f, true); shakeTimer = shakeIntensity = shakeAge = 0;
            return;
        }
        smoothLead = Vector3.zero; ApplyProjection();
        Quaternion rotation = useOrthographicIsometric ? IsometricRotation : Quaternion.LookRotation(Vector3.up * lookHeight - offset, Vector3.up);
        smoothPosition = useOrthographicIsometric ? target.position + Vector3.up * lookHeight - rotation * Vector3.forward * cameraDistance : target.position + offset;
        transform.SetPositionAndRotation(smoothPosition, rotation);
        if (view != null) view.orthographicSize = Mathf.Max(1f, ShotSize);
        shakeTimer = shakeIntensity = shakeAge = 0f; initialized = true;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || Time.timeScale <= 0f) return;
        ApplyProjection();
        if (!isOverview && target == null) return;
        if (useShoulderCamera && !isOverview) { UpdateShoulder(dt, false); return; }
        Vector3 focus, desiredPosition;
        Quaternion desiredRotation;
        float size = ShotSize;
        if (isOverview)
        {
            focus = overviewLookAt;
            desiredRotation = useOrthographicIsometric ? IsometricRotation : Quaternion.LookRotation(overviewLookAt - overviewPosition, Vector3.up);
            desiredPosition = useOrthographicIsometric ? focus - desiredRotation * Vector3.forward * Mathf.Max(cameraDistance, Vector3.Distance(overviewPosition, overviewLookAt)) : overviewPosition;
            size = Mathf.Max(vistaSize, Vector3.Distance(overviewPosition, overviewLookAt) * .4f);
        }
        else
        {
            if (boundTarget != target) { boundTarget = target; targetController = target.GetComponent<CharacterController>(); targetMovement = target.GetComponent<MountedHorseController>(); targetBody = target.GetComponent<Rigidbody>(); }
            Vector3 velocity = targetMovement != null && targetMovement.enabled ? targetMovement.WorldVelocity : targetController != null && targetController.enabled ? targetController.velocity : targetBody != null ? targetBody.linearVelocity : Vector3.zero;
            velocity.y = 0;
            Vector3 lead = Vector3.ClampMagnitude(velocity * leadSeconds, Mathf.Max(0, lookAhead));
            smoothLead = Vector3.Lerp(smoothLead, lead, 1f - Mathf.Exp(-5f * dt));
            focus = target.position + Vector3.up * lookHeight + smoothLead;
            desiredRotation = useOrthographicIsometric ? IsometricRotation : Quaternion.LookRotation(Vector3.up * lookHeight - offset, Vector3.up);
            desiredPosition = useOrthographicIsometric ? focus - desiredRotation * Vector3.forward * cameraDistance : target.position + smoothLead + offset;
        }
        if (!initialized) { smoothPosition = desiredPosition; transform.rotation = desiredRotation; initialized = true; }
        float blend = 1f - Mathf.Exp(-followSmoothness * (isOverview ? .5f : 1f) * dt);
        smoothPosition = Vector3.Lerp(smoothPosition, desiredPosition, blend);
        // Fixed axes never yaw with the horse or drift after a shake.
        transform.rotation = useOrthographicIsometric ? desiredRotation : Quaternion.Slerp(transform.rotation, desiredRotation, blend);
        if (view != null) view.orthographicSize = Mathf.Lerp(view.orthographicSize, Mathf.Max(1f, size), 1f - Mathf.Exp(-zoomSmoothness * dt));
        Vector3 shake = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer = Mathf.Max(0, shakeTimer - dt); shakeAge += dt;
            float amplitude = Mathf.Clamp01(shakeIntensity) * Mathf.Clamp01(shakeIntensity) * Mathf.Max(0, maximumShake);
            shake = (transform.right * Mathf.Sin(shakeAge * 67f) + transform.up * Mathf.Sin(shakeAge * 83f + .8f)) * (amplitude * .7071f);
            shakeIntensity = Mathf.MoveTowards(shakeIntensity, 0f, dt * 3.5f);
        }
        transform.position = smoothPosition + shake;
    }

    public void SetOrbit(float yaw, float elevation)
    {
        orbitYaw = yaw; orbitPitch = Mathf.Clamp(elevation, -20f, 65f); orbitInitialized = true;
    }

    void UpdateShoulder(float dt, bool snap)
    {
        if (target == null || view == null) return;
        if (aim == null || aim.transform != target) aim = target.GetComponent<AdventureAimController>();
        if (!orbitInitialized) { orbitYaw = target.eulerAngles.y; orbitInitialized = true; }
        if (!snap && (Input.GetMouseButton(1) || Input.GetMouseButton(2))) {
            orbitYaw += Input.GetAxis("Mouse X") * orbitSensitivity;
            orbitPitch = Mathf.Clamp(orbitPitch - Input.GetAxis("Mouse Y") * orbitSensitivity, -20f, 65f);
        }
        if (!snap && Input.GetKeyDown(KeyCode.V)) shoulderOffset = -shoulderOffset;
        Quaternion rotation = Quaternion.Euler(orbitPitch, orbitYaw, 0);
        // The pivot follows the rider's real seated height, while boom length stays in world metres.
        var cc = target.GetComponent<CharacterController>();
        float height = cc != null ? Mathf.Max(1.4f, (cc.center.y + cc.height * .35f) * target.lossyScale.y) : 1.8f;
        if (visualDriver == null || visualDriver.transform != target) visualDriver = target.GetComponent<MeshyHeroVisualDriver>();
        // Mounted art has an authored offset from its gameplay capsule. Frame the visible horse/rider.
        Vector3 body = visualDriver != null && visualDriver.meshyHorse != null && visualDriver.meshyHorse.gameObject.activeInHierarchy
            ? visualDriver.meshyHorse.position : target.position;
        BodyFocus = new Vector3(body.x, target.position.y + height, body.z);
        Vector3 focus = BodyFocus;
        float side = shoulderOffset * (IsAiming ? 1.35f : 1f);
        Vector3 proposedPivot = focus + rotation * Vector3.right * side;
        // Sweep the lateral shoulder offset as well as the boom, so wall hugging cannot place the pivot behind a wall.
        float lateral = ClearDistance(focus, proposedPivot - focus, Mathf.Abs(side));
        ShoulderPivot = focus + (proposedPivot - focus).normalized * lateral;
        float requested = IsAiming ? 4.3f : shoulderDistance;
        float clear = ClearDistance(ShoulderPivot, -(rotation * Vector3.forward), requested);
        if (snap || clear < boomDistance) boomDistance = clear;
        else boomDistance = Mathf.Lerp(boomDistance, clear, 1f - Mathf.Exp(-4f * dt));
        ActualBoomDistance = boomDistance;
        Vector3 desired = ShoulderPivot - rotation * Vector3.forward * boomDistance;
        // Smooth only the focus, not collision correction: walls must retract the camera immediately.
        float blend = snap || !initialized ? 1f : 1f - Mathf.Exp(-followSmoothness * dt);
        smoothPosition = Vector3.Lerp(smoothPosition, desired, blend);
        float safe = ClearDistance(ShoulderPivot, smoothPosition - ShoulderPivot, Vector3.Distance(ShoulderPivot, smoothPosition));
        smoothPosition = ShoulderPivot + (smoothPosition - ShoulderPivot).normalized * safe;
        transform.SetPositionAndRotation(smoothPosition, rotation);
        view.orthographic = false;
        view.nearClipPlane = .08f;
        view.fieldOfView = Mathf.Lerp(view.fieldOfView, IsAiming ? aimingFov : exploreFov, snap ? 1f : 1f - Mathf.Exp(-8f * dt));
        if (shakeTimer > 0) {
            shakeTimer = Mathf.Max(0, shakeTimer - dt); shakeAge += dt;
            float strength = maximumShake * shakeIntensity * shakeIntensity * .45f;
            Vector3 shaken = smoothPosition + (rotation * Vector3.right * Mathf.Sin(shakeAge * 67) + rotation * Vector3.up * Mathf.Sin(shakeAge * 83)) * strength;
            float shakeClear = ClearDistance(ShoulderPivot, shaken - ShoulderPivot, Vector3.Distance(ShoulderPivot, shaken));
            transform.position = ShoulderPivot + (shaken - ShoulderPivot).normalized * shakeClear;
            shakeIntensity = Mathf.MoveTowards(shakeIntensity, 0, dt * 3.5f);
        }
        initialized = true;
    }

    float ClearDistance(Vector3 origin, Vector3 direction, float maximum)
    {
        if (maximum <= .001f) return 0;
        int count = Physics.SphereCastNonAlloc(origin, collisionRadius, direction.normalized, cameraHits, maximum, ~0, QueryTriggerInteraction.Ignore);
        float safe = maximum;
        for (int i = 0; i < count; i++) {
            var hit = cameraHits[i];
            if (target != null && hit.transform.IsChildOf(target)) continue;
            // Enemies should not make the view pump in and out during melee.
            if (hit.transform.GetComponentInParent<ThanhGiongEnemy>() != null) continue;
            safe = Mathf.Min(safe, Mathf.Max(.05f, hit.distance - .08f));
        }
        return safe;
    }
}
