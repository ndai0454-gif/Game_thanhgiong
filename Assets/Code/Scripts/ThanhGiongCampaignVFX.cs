using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThanhGiongCampaignVFX : MonoBehaviour
{
    private Material lineMaterial;
    private readonly List<GameObject> spearDebris = new List<GameObject>();

    private void Awake()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) lineMaterial = new Material(shader);
    }

    public void PlayGrowth(Transform target)
    {
        IsometricCameraFollow.Instance?.Shake(0.5f, 0.4f);
        StartCoroutine(Ring(target.position, 1.2f, 6.5f, new Color(1f, .78f, .12f, .95f), .75f));
        StartCoroutine(DongSonSunBurst(target.position, 12, 5.5f, new Color(1f, .85f, .2f, .85f), .6f));
    }

    public void PlayCalligraphySlash(Transform target, float range)
    {
        StartCoroutine(CalligraphyStroke(target.position + Vector3.up * 1.2f, target.forward, range));
    }

    public void PlayBambooSweep(Transform target, float radius)
    {
        IsometricCameraFollow.Instance?.Shake(0.35f, 0.5f);
        StartCoroutine(Ring(target.position + Vector3.up * .25f, .8f, radius, new Color(.2f, 1f, .35f, .95f), .42f));
        StartCoroutine(Ring(target.position + Vector3.up * .55f, .4f, radius * 1.15f, new Color(.85f, 1f, .3f, .85f), .5f));
    }

    public void PlayFireLine(Transform target, float length)
    {
        IsometricCameraFollow.Instance?.Shake(0.85f, 0.95f);
        StartCoroutine(FireLine(target.position + Vector3.up * .3f, target.forward, length));
    }

    public void PlaySwordBreak(Transform target)
    {
        if (target == null) return;
        // Keep the legacy method/object names so existing scenes and callers still work.
        Transform weapon = null;
        foreach (Transform child in target.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "Weapon_IronSword") continue;
            if (weapon == null || child.gameObject.activeInHierarchy) weapon = child;
            if (child.gameObject.activeInHierarchy) break;
        }
        Vector3 origin = weapon != null ? weapon.position : target.position + Vector3.up * 1.6f;
        IsometricCameraFollow.Instance?.Shake(0.7f, 1.1f);
        StartCoroutine(SpearBreakFragments(target, weapon, origin));
        for (int i = 0; i < 9; i++)
        {
            StartCoroutine(Ring(origin + Vector3.up * (i * .06f), .2f, 1.5f + i * .22f, new Color(.7f, .85f, 1f, .9f), .28f + i * .03f));
        }
    }

    private IEnumerator SpearBreakFragments(Transform target, Transform weapon, Vector3 origin)
    {
        const int count = 5;
        const float lifetime = 2.1f;
        GameObject debris = new GameObject("BrokenSpear_Fragments");
        spearDebris.Add(debris);
        // A delayed destroy also covers an interrupted coroutine or disabled effect host.
        Destroy(debris, lifetime + .15f);
        Transform[] pieces = new Transform[count];
        Vector3[] velocities = new Vector3[count];
        Vector3[] spins = new Vector3[count];
        Vector3[] scales = new Vector3[count];
        Renderer source = null;
        Renderer spearhead = null;
        if (weapon != null)
            foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || renderer is TrailRenderer || renderer is LineRenderer) continue;
                if (source == null || renderer.name == "Iron Spear Shaft") source = renderer;
                if (renderer.name == "Iron Spearhead") spearhead = renderer;
            }
        Material sourceMaterial = source != null && source.sharedMaterial != null ? source.sharedMaterial : lineMaterial;
        MeshFilter headMesh = spearhead != null ? spearhead.GetComponent<MeshFilter>() : null;
        Quaternion rotation = weapon != null ? weapon.rotation : target.rotation;
        Vector3 shaftAxis = rotation * Vector3.up;
        Vector3 right = target.right;
        Vector3 forward = target.forward;
        float floorY = target.position.y + .08f;
        float length = source != null ? Mathf.Clamp(source.bounds.size.magnitude, 1.2f, 3.6f) : 2f;
        for (int i = 0; i < count; i++)
        {
            bool tip = i == count - 1;
            GameObject fragment;
            if (tip && headMesh != null && headMesh.sharedMesh != null)
            {
                fragment = new GameObject("SpearHead_Fragment", typeof(MeshFilter), typeof(MeshRenderer));
                fragment.GetComponent<MeshFilter>().sharedMesh = headMesh.sharedMesh;
            }
            else fragment = GameObject.CreatePrimitive(tip ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            fragment.name = tip ? "SpearHead_Fragment" : "SpearShaft_Fragment_" + i;
            fragment.transform.SetParent(debris.transform, false);
            fragment.transform.position = origin + shaftAxis * (i * length / count);
            fragment.transform.rotation = rotation;
            fragment.transform.localScale = tip
                ? (headMesh != null ? headMesh.transform.lossyScale : new Vector3(.15f, .28f, .055f))
                : new Vector3(.075f, length / (count * 2f), .075f);
            if (tip && spearhead != null) fragment.transform.position = spearhead.transform.position;
            Collider collider = fragment.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            fragment.GetComponent<Renderer>().sharedMaterial = tip && spearhead != null ? spearhead.sharedMaterial : sourceMaterial;
            pieces[i] = fragment.transform;
            scales[i] = fragment.transform.localScale;
            float fan = (i - (count - 1) * .5f) * .65f;
            velocities[i] = forward * (1.4f + i * .25f) + right * fan + Vector3.up * (2.1f + i * .23f);
            spins[i] = new Vector3(210f + i * 55f, 110f - i * 45f, 80f + i * 60f);
        }
        float elapsed = 0f;
        while (elapsed < lifetime && debris != null)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float shrink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lifetime - .45f, lifetime, elapsed));
            for (int i = 0; i < count; i++)
            {
                velocities[i] += Vector3.down * (9.81f * dt);
                Vector3 position = pieces[i].position + velocities[i] * dt;
                if (position.y < floorY)
                {
                    position.y = floorY;
                    velocities[i] = new Vector3(velocities[i].x * .68f, Mathf.Abs(velocities[i].y) * .24f, velocities[i].z * .68f);
                    spins[i] *= .65f;
                }
                pieces[i].position = position;
                pieces[i].Rotate(spins[i] * dt, Space.World);
                pieces[i].localScale = scales[i] * shrink;
            }
            yield return null;
        }
        spearDebris.Remove(debris);
        if (debris != null) Destroy(debris);
    }

    private void OnDisable()
    {
        foreach (GameObject debris in spearDebris)
            if (debris != null) Destroy(debris);
        spearDebris.Clear();
    }

    public void PlayFoodBurst(Vector3 pos, Color color)
    {
        StartCoroutine(Ring(pos + Vector3.up * 0.35f, 0.3f, 2.4f, color, 0.42f));
        StartCoroutine(Ring(pos + Vector3.up * 0.75f, 0.15f, 1.8f, new Color(1f, 1f, 1f, 0.9f), 0.35f));
    }

    public void SpawnLegendaryRemains(Vector3 skyPos, Vector3 groundPos)
    {
        // Dropping fiery sparks creating Lang Chay & hoof prints creating lakes
        StartCoroutine(DropFireSpark(skyPos, groundPos));
    }

    private IEnumerator DropFireSpark(Vector3 from, Vector3 to)
    {
        GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.name = "FireDrop_LangChay";
        spark.transform.position = from;
        spark.transform.localScale = Vector3.one * 0.75f;
        Collider c = spark.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer r = spark.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = lineMaterial;
            r.material.color = new Color(1f, 0.35f, 0.05f, 1f);
        }

        float elapsed = 0f;
        float duration = 1.1f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            spark.transform.position = Vector3.Lerp(from, to, t * t);
            yield return null;
        }

        // On landing: spawn miniature Lake / crater
        CreateAoHoCrater(to);
        Destroy(spark);
    }

    private void CreateAoHoCrater(Vector3 pos)
    {
        GameObject lake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lake.name = "AoHo_DiTich";
        lake.transform.position = pos + Vector3.up * 0.02f;
        lake.transform.localScale = new Vector3(3.8f, 0.04f, 3.8f);
        Collider c = lake.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer r = lake.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = lineMaterial;
            r.material.color = new Color(0.12f, 0.45f, 0.78f, 0.85f);
        }
    }

    private IEnumerator CalligraphyStroke(Vector3 origin, Vector3 forward, float range)
    {
        GameObject go = new GameObject("Calligraphy Brush Stroke");
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.positionCount = 24;
        line.widthMultiplier = 0.28f;

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        for (int i = 0; i < 24; i++)
        {
            float t = i / 23f;
            float angle = Mathf.Lerp(-65f, 65f, t) * Mathf.Deg2Rad;
            Vector3 pos = origin + (forward * Mathf.Cos(angle) + right * Mathf.Sin(angle)) * range;
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.4f;
            line.SetPosition(i, pos);
        }

        float elapsed = 0f;
        const float dur = 0.32f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            line.startColor = new Color(1f, 0.82f, 0.15f, 1f - t);
            line.endColor = new Color(0.9f, 0.25f, 0.02f, (1f - t) * 0.7f);
            line.widthMultiplier = Mathf.Lerp(0.35f, 0.02f, t);
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator DongSonSunBurst(Vector3 center, int rays, float length, Color color, float duration)
    {
        GameObject go = new GameObject("Dong Son Sun Rays");
        go.transform.position = center;
        LineRenderer[] lines = new LineRenderer[rays];
        for (int i = 0; i < rays; i++)
        {
            GameObject rayGo = new GameObject("Ray_" + i);
            rayGo.transform.SetParent(go.transform, false);
            LineRenderer lr = rayGo.AddComponent<LineRenderer>();
            lr.sharedMaterial = lineMaterial;
            lr.positionCount = 2;
            lr.widthMultiplier = 0.18f;
            lines[i] = lr;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curLen = Mathf.Lerp(0.5f, length, t);
            Color c = color; c.a *= (1f - t);

            for (int i = 0; i < rays; i++)
            {
                float rad = (i / (float)rays) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(rad), 0.05f, Mathf.Sin(rad));
                lines[i].SetPosition(0, dir * 0.8f);
                lines[i].SetPosition(1, dir * curLen);
                lines[i].startColor = c;
                lines[i].endColor = c;
                lines[i].widthMultiplier = 0.2f * (1f - t);
            }
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator Ring(Vector3 position, float start, float end, Color color, float duration)
    {
        GameObject go = new GameObject("Dong Son Brush Ring");
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 48;
        line.widthMultiplier = .18f;
        go.transform.position = position;
        for (int i = 0; i < 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a), .04f + Mathf.Sin(a * 6f) * .025f, Mathf.Sin(a)));
        }
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            float scale = Mathf.Lerp(start, end, 1f - (1f - t) * (1f - t));
            go.transform.localScale = Vector3.one * scale;
            Color faded = color; faded.a *= 1f - t;
            line.startColor = faded; line.endColor = faded;
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator FireLine(Vector3 origin, Vector3 forward, float length)
    {
        GameObject go = new GameObject("Hoa Tuyen Core");
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.positionCount = 2;
        line.SetPosition(0, origin);
        line.SetPosition(1, origin + forward * length);
        line.widthMultiplier = 3.2f;
        line.numCapVertices = 8;

        float time = 0f;
        const float dur = 0.85f;
        while (time < dur)
        {
            time += Time.deltaTime;
            float t = time / dur;
            float pulse = 1f + Mathf.Sin(time * 35f) * .18f;
            line.widthMultiplier = Mathf.Lerp(3.2f, .15f, t) * pulse;
            line.startColor = new Color(1f, .95f, .75f, 1f - t); // High white-yellow heat core
            line.endColor = new Color(1f, .15f, 0f, 1f - t);
            yield return null;
        }
        Destroy(go);
    }

    public void PlayAscensionDivineLight(Vector3 center)
    {
        IsometricCameraFollow.Instance?.Shake(1.1f, 1.4f);
        StartCoroutine(DongSonSunBurst(center, 24, 20f, new Color(1f, 0.88f, 0.22f, 1f), 2.8f));
        StartCoroutine(Ring(center, 1f, 16f, new Color(1f, 0.85f, 0.2f, 0.95f), 2.0f));
        StartCoroutine(Ring(center, 2.5f, 24f, new Color(1f, 0.95f, 0.55f, 0.85f), 2.5f));
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
