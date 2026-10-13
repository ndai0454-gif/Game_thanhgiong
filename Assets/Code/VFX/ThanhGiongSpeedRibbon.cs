using UnityEngine;

/// <summary>Red silk and sword-light ribbons that wake only at iron-horse speed.</summary>
public class ThanhGiongSpeedRibbon : MonoBehaviour
{
    public Transform motionSource;
    public Transform ribbonAnchor;
    public float activationSpeed = 1.2f;
    public float fullSpeed = 6.0f;

    private TrailRenderer silk;
    private TrailRenderer swordLight;
    private Vector3 previousPosition;

    private void Awake()
    {
        if (motionSource == null) motionSource = transform;
        if (ribbonAnchor == null) ribbonAnchor = transform;
        previousPosition = motionSource.position;
        silk = CreateTrail("Dải Lụa Đỏ", new Color(.9f,.035f,.018f,1f), .38f, .72f, -0.34f);
        swordLight = CreateTrail("Vệt Kiếm Vàng", new Color(1f,.72f,.12f,.9f), .12f, .34f, 0.24f);
    }

    private TrailRenderer CreateTrail(string trailName, Color color, float width, float time, float side)
    {
        GameObject go = new GameObject(trailName);
        go.transform.SetParent(ribbonAnchor, false);
        go.transform.localPosition = new Vector3(side, 1.35f, -1.05f);
        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.time = time;
        trail.minVertexDistance = .1f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(.72f, width*.55f), new Keyframe(1f, 0f));
        trail.colorGradient = Gradient(color);
        Shader shader = ResolveTrailShader();
        if (shader != null)
        {
            trail.material = new Material(shader);
            trail.material.color = color;
        }
        trail.emitting = false;
        trail.numCornerVertices = 3;
        trail.numCapVertices = 2;
        return trail;
    }

    private static Shader ResolveTrailShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        return shader;
    }

    private static Gradient Gradient(Color head)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(head,0f), new GradientColorKey(new Color(head.r*.55f,head.g*.4f,head.b*.25f),1f) },
            new[] { new GradientAlphaKey(head.a,0f), new GradientAlphaKey(0f,1f) });
        return gradient;
    }

    private void LateUpdate()
    {
        float speed = Vector3.Distance(motionSource.position, previousPosition) / Mathf.Max(Time.deltaTime, .0001f);
        previousPosition = motionSource.position;
        float intensity = Mathf.InverseLerp(activationSpeed, fullSpeed, speed);
        bool active = intensity > .02f;
        silk.emitting = active;
        swordLight.emitting = active;
        silk.widthMultiplier = Mathf.Lerp(.25f, 1f, intensity);
        swordLight.widthMultiplier = Mathf.Lerp(.2f, 1f, intensity);
    }

    private void OnDisable()
    {
        if (silk != null) { silk.emitting=false; silk.Clear(); }
        if (swordLight != null) { swordLight.emitting=false; swordLight.Clear(); }
    }
}
