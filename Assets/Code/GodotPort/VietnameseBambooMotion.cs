using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class VietnameseBambooMotion : MonoBehaviour
{
    [Range(.2f, 3f)] public float windFrequency = 1.15f;
    [Range(.2f, 6f)] public float swayDegrees = 2.4f;
    [Range(0f, 4f)] public float gustDegrees = 1.1f;
    [Range(1f, 12f)] public float pullResponse = 7f;

    readonly List<Transform> animatedNodes = new List<Transform>();
    readonly List<Quaternion> authoredRotations = new List<Quaternion>();
    readonly List<float> phases = new List<float>();
    float pullImpulse;
    Vector2 pullDirection;

    public int AnimatedNodeCount => animatedNodes.Count;
    public float CurrentSwayDegrees { get; private set; }

    void Awake() => CacheNodes();

    void CacheNodes()
    {
        animatedNodes.Clear(); authoredRotations.Clear(); phases.Clear();
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform) continue;
            bool cluster = child.name == "MeshyModel" || child.name.StartsWith("BambooStalk_");
            if (!cluster) continue;
            animatedNodes.Add(child);
            authoredRotations.Add(child.localRotation);
            phases.Add(Mathf.Repeat(Mathf.Abs(child.GetEntityId().GetHashCode()) * .0137f, Mathf.PI * 2f));
        }

        if (animatedNodes.Count == 0)
        {
            Transform fallback = transform.childCount > 0 ? transform.GetChild(0) : transform;
            animatedNodes.Add(fallback);
            authoredRotations.Add(fallback.localRotation);
            phases.Add(Mathf.Repeat(Mathf.Abs(GetEntityId().GetHashCode()) * .0137f, Mathf.PI * 2f));
        }
    }

    public void ReactToPull(Vector3 pullerWorldPosition)
    {
        Vector3 away = transform.position - pullerWorldPosition;
        Vector3 local = transform.InverseTransformDirection(Vector3.ProjectOnPlane(away, Vector3.up).normalized);
        pullDirection = new Vector2(local.z, -local.x);
        if (pullDirection.sqrMagnitude < .01f) pullDirection = Vector2.right;
        pullDirection.Normalize();
        pullImpulse = 1f;
    }

    void LateUpdate()
    {
        if (animatedNodes.Count == 0) CacheNodes();
        float time = Time.time;
        float gust = (Mathf.PerlinNoise(time * .17f, transform.position.x * .037f) - .5f) * 2f * gustDegrees;
        pullImpulse = Mathf.MoveTowards(pullImpulse, 0f, Time.deltaTime * 1.4f);

        for (int i = 0; i < animatedNodes.Count; i++)
        {
            Transform node = animatedNodes[i];
            if (node == null) continue;
            float wave = Mathf.Sin(time * windFrequency + phases[i]);
            float secondary = Mathf.Sin(time * windFrequency * .47f + phases[i] * 1.71f);
            float x = wave * swayDegrees + gust + pullDirection.x * pullImpulse * pullResponse;
            float z = secondary * swayDegrees * .62f + pullDirection.y * pullImpulse * pullResponse;
            node.localRotation = authoredRotations[i] * Quaternion.Euler(x, 0f, z);
            CurrentSwayDegrees = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < animatedNodes.Count; i++)
            if (animatedNodes[i] != null) animatedNodes[i].localRotation = authoredRotations[i];
        pullImpulse = 0f;
    }
}
