using UnityEngine;

/// <summary>Grounded gait for the four original legs of the mounted mesh.</summary>
[DisallowMultipleComponent]
public sealed class MountedHorseMeshMotion : MonoBehaviour
{
    public Transform rig;
    public Transform[] upper = new Transform[4];
    public Transform[] lower = new Transform[4];
    public Transform[] hooves = new Transform[4];
    [SerializeField] Vector3[] restFeet = new Vector3[4];
    [SerializeField] Vector3[] restKnees = new Vector3[4];
    [SerializeField] float[] upperLength = new float[4];
    [SerializeField] float[] lowerLength = new float[4];
    readonly RaycastHit[] hits = new RaycastHit[16];

    public bool IsReady => rig != null && upper.Length == 4 && upper[0] != null && hooves[3] != null;

    public void Configure(Transform space, Transform[] first, Transform[] second, Transform[] feet)
    {
        rig = space; upper = first; lower = second; hooves = feet;
        for (int i = 0; i < 4; i++)
        {
            restFeet[i] = rig.InverseTransformPoint(hooves[i].position);
            restKnees[i] = rig.InverseTransformPoint(lower[i].position);
            upperLength[i] = Vector3.Distance(upper[i].position, lower[i].position) / rig.lossyScale.y;
            lowerLength[i] = Vector3.Distance(lower[i].position, hooves[i].position) / rig.lossyScale.y;
        }
    }

    public float CycleDistance(bool running)
    {
        float stride = running ? .40f : .28f;
        float duty = running ? .52f : .72f;
        return stride * Mathf.Abs(rig.lossyScale.z) / duty;
    }

    /// <summary>Called after the controller's body motion, so the feet compensate for its bounce.</summary>
    public void Pose(float phase, float amount, bool running, float attack, bool grounded, bool sampleGround = true)
    {
        if (!IsReady) return;
        float cycle = phase / (Mathf.PI * 2f);
        float duty = running ? .52f : .72f;
        float stride = running ? .40f : .28f;
        float lift = running ? .13f : .085f;
        float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(amount / .18f));
        for (int i = 0; i < 4; i++)
        {
            // Walk: four separate contacts. Trot: opposite diagonal pairs.
            float offset = running ? (i == 0 || i == 3 ? 0f : .5f)
                : (i == 0 ? 0f : i == 1 ? .5f : i == 2 ? .75f : .25f);
            float t = Mathf.Repeat(cycle + offset, 1f);
            float z, y = 0;
            if (t < duty) z = Mathf.Lerp(stride * .5f, -stride * .5f, t / duty);
            else
            {
                float swing = (t - duty) / (1f - duty);
                z = Mathf.Lerp(-stride * .5f, stride * .5f, Mathf.SmoothStep(0, 1, swing));
                y = Mathf.Sin(swing * Mathf.PI) * lift;
            }
            Vector3 foot = restFeet[i] + new Vector3(0, y, z) * blend;
            Vector3 world = rig.TransformPoint(foot);
            if (grounded)
            {
                float ground = transform.position.y;
                if (sampleGround)
                {
                    int count = Physics.RaycastNonAlloc(world + Vector3.up, Vector3.down, hits, 3f, ~0, QueryTriggerInteraction.Ignore);
                    float nearest = float.PositiveInfinity;
                    for (int h = 0; h < count; h++)
                        if (!hits[h].collider.transform.IsChildOf(transform) && hits[h].normal.y > .55f && hits[h].distance < nearest)
                        { nearest = hits[h].distance; ground = hits[h].point.y; }
                }
                world.y = ground + (.055f + y * blend) * Mathf.Abs(rig.lossyScale.y);
                foot = rig.InverseTransformPoint(world);
            }
            if (attack >= 0)
                foot += new Vector3(0, i < 2 ? .22f : .02f, i < 2 ? -.14f : 0) * Mathf.Sin(attack * Mathf.PI);
            Solve(i, foot);
        }
    }

    void Solve(int i, Vector3 foot)
    {
        // Reset the two joints before solving; both sides bend about the same sagittal plane.
        upper[i].localRotation = Quaternion.identity;
        lower[i].localRotation = Quaternion.identity;
        hooves[i].localRotation = Quaternion.identity;
        Vector3 hip = rig.InverseTransformPoint(upper[i].position);
        Vector3 offset = foot - hip;
        float a = upperLength[i], b = lowerLength[i];
        float distance = Mathf.Clamp(offset.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
        Vector3 direction = offset.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(i < 2 ? Vector3.forward : Vector3.back, direction).normalized;
        float along = (a * a - b * b + distance * distance) / (2f * distance);
        Vector3 knee = hip + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
        Vector3 kneeWorld = rig.TransformPoint(knee), footWorld = rig.TransformPoint(hip + direction * distance);
        upper[i].rotation = Quaternion.FromToRotation(lower[i].position - upper[i].position,
            kneeWorld - upper[i].position) * upper[i].rotation;
        lower[i].rotation = Quaternion.FromToRotation(hooves[i].position - lower[i].position,
            footWorld - lower[i].position) * lower[i].rotation;
        // Independent hoof bones keep both front soles facing the ground.
        hooves[i].rotation = transform.rotation;
    }
}
