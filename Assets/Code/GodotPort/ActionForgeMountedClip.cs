using System;
using UnityEngine;

/// <summary>Editor-baked upper-body rotations for the mounted rider's own skeleton.</summary>
public sealed class ActionForgeMountedClip : ScriptableObject
{
    [Serializable]
    public sealed class Track
    {
        public string bone;
        public Quaternion[] rotations;

        public Quaternion Evaluate(float normalizedTime)
        {
            if (rotations == null || rotations.Length == 0) return Quaternion.identity;
            float frame = Mathf.Clamp01(normalizedTime) * (rotations.Length - 1);
            int a = Mathf.FloorToInt(frame), b = Mathf.Min(a + 1, rotations.Length - 1);
            return Quaternion.Slerp(rotations[a], rotations[b], frame - a);
        }
    }

    public string sourceAsset;
    public float sourceDuration;
    [Range(.05f, .95f)] public float impactNormalizedTime = .4f;
    public Track[] tracks;

    // The campaign resolves its hit at 0.12 seconds. Place the clip's strike there,
    // then play the remaining follow-through over the accepted attack duration.
    public float SourceTime(float attackProgress, float attackDuration)
    {
        float hit = Mathf.Clamp(.12f / Mathf.Max(attackDuration, .1f), .05f, .85f);
        float t = Mathf.Clamp01(attackProgress);
        return t < hit
            ? Mathf.Lerp(0f, impactNormalizedTime, t / hit)
            : Mathf.Lerp(impactNormalizedTime, 1f, (t - hit) / (1f - hit));
    }
}
