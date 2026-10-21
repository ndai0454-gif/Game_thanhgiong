using UnityEngine;

/// <summary>Dân làng: đi dạo quanh vị trí gốc, quay mặt về hướng đi; animation procedural trên bộ xương
/// (Hips/LegL/LegR/Chest/ArmL/ArmR/Head) do ThanhGiongVillagerRigger dựng: đi bộ, thở khi đứng, gật đầu.</summary>
public class ThanhGiongVillager : MonoBehaviour
{
    [Tooltip("Mã NPC trong ThanhGiongNpcLore (để trống = không nói chuyện)")] public string npcId;
    public static readonly System.Collections.Generic.List<ThanhGiongVillager> All = new System.Collections.Generic.List<ThanhGiongVillager>();
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public float wanderRadius = 5f;
    public float walkSpeed = 0.9f;
    public Vector2 idleTime = new Vector2(2f, 6f);
    [Tooltip("Hệ số nhịp bước (trẻ em nhanh, cụ già chậm)")] public float stride = 1f;
    [Tooltip("Độ khom lưng (cụ già)")] public float hunch = 0f;

    const float CullDistance = 60f;

    Vector3 home, target;
    float wait, phase, idlePhase, cullTimer, moveBlend;
    Transform visual, hips, legL, legR, chest, armL, armR, head;
    Quaternion qHips, qLegL, qLegR, qChest, qArmL, qArmR, qHead;
    float visualBaseY;
    Renderer[] renderers;
    bool visible = true;

    static Transform FindDeep(Transform r, string n)
    {
        if (r.name == n) return r;
        for (int i = 0; i < r.childCount; i++) { var f = FindDeep(r.GetChild(i), n); if (f != null) return f; }
        return null;
    }

    void Start()
    {
        home = transform.position; target = home;
        visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
        visualBaseY = visual.localPosition.y;
        hips = FindDeep(visual, "Hips"); legL = FindDeep(visual, "LegL"); legR = FindDeep(visual, "LegR");
        chest = FindDeep(visual, "Chest"); armL = FindDeep(visual, "ArmL"); armR = FindDeep(visual, "ArmR"); head = FindDeep(visual, "Head");
        if (hips) qHips = hips.localRotation; if (legL) qLegL = legL.localRotation; if (legR) qLegR = legR.localRotation;
        if (chest) qChest = chest.localRotation; if (armL) qArmL = armL.localRotation; if (armR) qArmR = armR.localRotation; if (head) qHead = head.localRotation;
        wait = Random.Range(0f, idleTime.y);
        phase = Random.value * 6.28f; idlePhase = Random.value * 6.28f;
    }

    void Update()
    {
        cullTimer -= Time.deltaTime;
        if (cullTimer <= 0f)
        {
            cullTimer = 0.5f;
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>();
            Camera cam = Camera.main;
            visible = cam == null || (cam.transform.position - transform.position).sqrMagnitude < CullDistance * CullDistance;
            foreach (var r in renderers) if (r != null && r.enabled != visible) r.enabled = visible;
        }

        Vector3 flat = target - transform.position; flat.y = 0f;
        bool moving = flat.sqrMagnitude > 0.04f;
        if (moving)
        {
            Vector3 dir = flat.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 6f * Time.deltaTime);
            Vector3 p = transform.position + dir * walkSpeed * Time.deltaTime;
            if (Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            transform.position = p;
            phase += Time.deltaTime * walkSpeed * 5.5f * stride;
        }
        else
        {
            wait -= Time.deltaTime;
            if (wait <= 0f)
            {
                Vector2 r = Random.insideUnitCircle * wanderRadius;
                target = home + new Vector3(r.x, 0f, r.y);
                wait = Random.Range(idleTime.x, idleTime.y);
            }
        }
        idlePhase += Time.deltaTime * 1.4f;
        moveBlend = Mathf.MoveTowards(moveBlend, moving ? 1f : 0f, Time.deltaTime * 4f);
        if (!visible) return;

        float sw = Mathf.Sin(phase) * moveBlend;
        float breathe = Mathf.Sin(idlePhase) * (1f - moveBlend);
        if (legL) legL.localRotation = qLegL * Quaternion.Euler(sw * 32f, 0f, 0f);
        if (legR) legR.localRotation = qLegR * Quaternion.Euler(-sw * 32f, 0f, 0f);
        if (armL) armL.localRotation = qArmL * Quaternion.Euler(-sw * 24f, 0f, breathe * 1.5f);
        if (armR) armR.localRotation = qArmR * Quaternion.Euler(sw * 24f, 0f, -breathe * 1.5f);
        if (chest) chest.localRotation = qChest * Quaternion.Euler(hunch + breathe * 1.2f + moveBlend * 3f, Mathf.Sin(phase) * 4f * moveBlend, 0f);
        if (head) head.localRotation = qHead * Quaternion.Euler(Mathf.Sin(idlePhase * 0.7f) * 3f * (1f - moveBlend), Mathf.Sin(idlePhase * 0.45f) * 14f * (1f - moveBlend), 0f);
        if (hips) hips.localRotation = qHips * Quaternion.Euler(0f, Mathf.Sin(phase) * 5f * moveBlend, 0f);
        Vector3 lp = visual.localPosition; lp.y = visualBaseY + Mathf.Abs(Mathf.Cos(phase)) * 0.035f * moveBlend; visual.localPosition = lp;
    }
}
