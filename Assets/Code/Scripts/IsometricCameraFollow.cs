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

    void Awake()
    {
        Instance = this;
        view = GetComponent<Camera>();
        EnsureTarget();
        ApplyProjection();
        if (target != null) SnapToTarget();
    }

    void EnsureTarget()
    {
        if (target == null)
        {
            var hero = FindAnyObjectByType<MountedHorseController>();
            if (hero != null) target = hero.transform;
            else
            {
                var adv = FindAnyObjectByType<AdventurerController>();
                if (adv != null) target = adv.transform;
            }
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureTarget();
            if (target != null) SnapToTarget();
        }
    }
#endif

    void OnDestroy() { if (Instance == this) Instance = null; }
    float ShotSize => currentShot == Shot.Combat ? combatSize : currentShot == Shot.Vista ? vistaSize : currentShot == Shot.Boss ? bossSize : currentShot == Shot.Close ? closeSize : exploreSize;
    Quaternion IsometricRotation => Quaternion.Euler(isometricPitch, isometricYaw, 0f);

    void ApplyProjection()
    {
        if (view == null) view = GetComponent<Camera>();
        if (view == null) return;
        if (view.orthographic != useOrthographicIsometric) view.orthographic = useOrthographicIsometric;
        if (!Mathf.Approximately(view.fieldOfView, fieldOfView)) view.fieldOfView = fieldOfView;
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
        if (dt <= 0f) return;
        ApplyProjection();
        if (!isOverview && target == null) return;
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
}
