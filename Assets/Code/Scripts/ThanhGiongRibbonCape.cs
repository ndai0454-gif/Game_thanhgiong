using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hệ thống Dải Lụa Đỏ Vật Lý 3D (Verlet Ribbon Physics) & Vệt Kiếm Bay Thần Tướng cho Thánh Gióng.
/// Lấy cảm hứng trực tiếp từ cơ chế Ribbon Scarf & Weapon Trails trong "Understory".
/// </summary>
public class ThanhGiongRibbonCape : MonoBehaviour
{
    [Header("Anchor & Offset")]
    public Transform anchorTransform;
    public Vector3 anchorOffset = new Vector3(0f, 2.35f, -0.42f);
    public bool isDualRibbon = true; // Dải lụa đôi phấp phới hai bên vai

    [Header("Ribbon Geometry")]
    [Range(10, 32)] public int segmentCount = 20;
    public float segmentLength = 0.14f;
    public float ribbonStartWidth = 0.22f;
    public float ribbonMidWidth = 0.38f;
    public float ribbonEndWidth = 0.12f;

    [Header("Physics & Aerodynamics")]
    public float gravity = 4.2f;
    public float airDrag = 3.6f;
    public float windFlutter = 1.8f;
    public float gallopWaveForce = 2.4f;
    public float sprintStretchFactor = 1.35f;

    [Header("Color Palette (Crimson & Gold Trim)")]
    public Color mainRedColor = new Color(0.92f, 0.12f, 0.12f, 0.95f);
    public Color deepRedColor = new Color(0.65f, 0.05f, 0.08f, 0.85f);
    public Color goldTrimColor = new Color(1.0f, 0.82f, 0.22f, 0.95f);

    private struct RibbonNode
    {
        public Vector3 position;
        public Vector3 oldPosition;
        public Vector3 velocity;
    }

    private RibbonNode[] ribbonLeft;
    private RibbonNode[] ribbonRight;
    private LineRenderer lineLeft;
    private LineRenderer lineRight;
    private Material ribbonMaterial;

    private Vector3 lastAnchorPos;
    private Vector3 movementVelocity;
    private MountedHorseController horseController;
    private float sparkTimer;

    private void Awake()
    {
        if (ribbonLeft != null && lineLeft != null) return;
        segmentCount = Mathf.Max(2, segmentCount);
        horseController = GetComponent<MountedHorseController>();
        if (horseController == null) horseController = GetComponentInParent<MountedHorseController>();

        if (anchorTransform == null)
        {
            Transform visual = transform.Find("Visual");
            anchorTransform = visual != null ? visual : transform;
        }

        InitializeRibbonMaterial();
        SetupLineRenderers();
        InitializeNodes();
        lastAnchorPos = GetAnchorWorldPos(0);
    }

    private void InitializeRibbonMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Standard");

        ribbonMaterial = new Material(shader)
        {
            name = "ThanhGiong_RibbonSilk_Mat"
        };
    }

    private void SetupLineRenderers()
    {
        // Line Left (Dải lụa trái)
        Transform existingLeft = transform.Find("RibbonSilk_Left");
        GameObject leftGo = existingLeft != null ? existingLeft.gameObject : new GameObject("RibbonSilk_Left");
        leftGo.transform.SetParent(transform, false);
        lineLeft = SetupSingleLine(leftGo);

        if (isDualRibbon)
        {
            // Line Right (Dải lụa phải)
            Transform existingRight = transform.Find("RibbonSilk_Right");
            GameObject rightGo = existingRight != null ? existingRight.gameObject : new GameObject("RibbonSilk_Right");
            rightGo.transform.SetParent(transform, false);
            lineRight = SetupSingleLine(rightGo);
        }
    }

    private LineRenderer SetupSingleLine(GameObject go)
    {
        LineRenderer lr = go.GetComponent<LineRenderer>();
        if (lr == null) lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = ribbonMaterial;
        lr.useWorldSpace = true;
        lr.positionCount = segmentCount;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        // Custom width curve: thắt ở cổ, phồng ở giữa, vuốt nhọn ở đuôi
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, ribbonStartWidth);
        widthCurve.AddKey(0.45f, ribbonMidWidth);
        widthCurve.AddKey(1f, ribbonEndWidth);
        lr.widthCurve = widthCurve;

        // Gradient color: Đỏ son pha viền vàng kim
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(goldTrimColor, 0.0f),
                new GradientColorKey(mainRedColor, 0.15f),
                new GradientColorKey(deepRedColor, 0.75f),
                new GradientColorKey(goldTrimColor, 1.0f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.98f, 0.0f),
                new GradientAlphaKey(0.95f, 0.7f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        lr.colorGradient = gradient;

        return lr;
    }

    private void InitializeNodes()
    {
        ribbonLeft = new RibbonNode[segmentCount];
        ribbonRight = new RibbonNode[segmentCount];

        Vector3 startLeft = GetAnchorWorldPos(-0.16f);
        Vector3 startRight = GetAnchorWorldPos(0.16f);
        Vector3 backDir = -transform.forward;

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 posL = startLeft + backDir * (i * segmentLength);
            ribbonLeft[i].position = posL;
            ribbonLeft[i].oldPosition = posL;

            Vector3 posR = startRight + backDir * (i * segmentLength);
            ribbonRight[i].position = posR;
            ribbonRight[i].oldPosition = posR;
        }
    }

    private Vector3 GetAnchorWorldPos(float sideOffset)
    {
        if (anchorTransform == null) return transform.position + anchorOffset;

        Vector3 offset = anchorOffset;
        offset.x += sideOffset;
        return anchorTransform.TransformPoint(offset);
    }

    private void Update()
    {
        if (ribbonLeft == null || lineLeft == null) Awake();
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        dt = Mathf.Min(dt, .05f);

        Vector3 currentAnchorPos = GetAnchorWorldPos(0);
        movementVelocity = (currentAnchorPos - lastAnchorPos) / dt;
        lastAnchorPos = currentAnchorPos;

        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && movementVelocity.sqrMagnitude > 4f;
        float currentLength = isSprinting ? segmentLength * sprintStretchFactor : segmentLength;

        // Verlet physics simulation for both ribbons
        SimulateRibbon(ribbonLeft, GetAnchorWorldPos(-0.16f), currentLength, dt, -1f);
        if (isDualRibbon && ribbonRight != null)
        {
            SimulateRibbon(ribbonRight, GetAnchorWorldPos(0.16f), currentLength, dt, 1f);
        }

        // Apply positions to LineRenderers
        for (int i = 0; i < segmentCount; i++)
        {
            lineLeft.SetPosition(i, ribbonLeft[i].position);
            if (isDualRibbon && lineRight != null)
                lineRight.SetPosition(i, ribbonRight[i].position);
        }

        // Star sparks & golden embers when galloping (Understory scarf star dust effect)
        if (isSprinting)
        {
            sparkTimer += dt;
            if (sparkTimer >= 0.08f)
            {
                sparkTimer = 0f;
                SpawnStarSpark(ribbonLeft[segmentCount - 1].position);
                if (isDualRibbon)
                    SpawnStarSpark(ribbonRight[segmentCount - 1].position);
            }
        }
    }

    private void SimulateRibbon(RibbonNode[] nodes, Vector3 anchorPos, float segLen, float dt, float sideSign)
    {
        nodes[0].position = anchorPos;
        nodes[0].oldPosition = anchorPos;

        float speed = movementVelocity.magnitude;
        Vector3 backFlow = -movementVelocity.normalized * (speed * airDrag);

        // Wind & gallop wave
        float time = Time.time;
        float wave = Mathf.Sin(time * 12f) * gallopWaveForce;
        float sideWave = Mathf.Cos(time * 7f + sideSign) * windFlutter;

        Vector3 totalWind = backFlow + Vector3.up * wave + transform.right * (sideWave * sideSign);
        Vector3 gravityVector = Vector3.down * gravity;

        // 1. Verlet Integration Step
        for (int i = 1; i < segmentCount; i++)
        {
            Vector3 vel = (nodes[i].position - nodes[i].oldPosition) * 0.92f;
            nodes[i].oldPosition = nodes[i].position;

            float nodeWeight = (i / (float)segmentCount);
            Vector3 acceleration = gravityVector + totalWind * (0.4f + nodeWeight * 0.6f);

            nodes[i].position += vel + acceleration * (dt * dt);
        }

        // 2. Relaxation Constraints (Duy trì độ dài dải lụa không bị giãn vô hạn)
        const int iterations = 3;
        for (int iter = 0; iter < iterations; iter++)
        {
            nodes[0].position = anchorPos;
            for (int i = 0; i < segmentCount - 1; i++)
            {
                Vector3 delta = nodes[i + 1].position - nodes[i].position;
                float currentDist = delta.magnitude;
                if (currentDist > 0.0001f)
                {
                    float diff = (currentDist - segLen) / currentDist;
                    if (i == 0)
                    {
                        nodes[i + 1].position -= delta * diff;
                    }
                    else
                    {
                        nodes[i].position += delta * (0.5f * diff);
                        nodes[i + 1].position -= delta * (0.5f * diff);
                    }
                }
            }
        }
    }

    private void SpawnStarSpark(Vector3 position)
    {
        GameObject spark = new GameObject("ScarfStarDust");
        spark.transform.position = position + Random.insideUnitSphere * 0.12f;

        LineRenderer lr = spark.AddComponent<LineRenderer>();
        lr.sharedMaterial = ribbonMaterial;
        lr.useWorldSpace = true;
        lr.positionCount = 4;
        lr.startWidth = 0.14f;
        lr.endWidth = 0.01f;
        lr.startColor = goldTrimColor;
        lr.endColor = new Color(1f, 0.35f, 0.05f, 0f);

        Vector3 p = spark.transform.position;
        lr.SetPosition(0, p + Vector3.up * 0.15f);
        lr.SetPosition(1, p + Vector3.right * 0.15f);
        lr.SetPosition(2, p - Vector3.up * 0.15f);
        lr.SetPosition(3, p - Vector3.right * 0.15f);

        StartCoroutine(FadeAndDestroySpark(spark, lr, 0.32f));
    }

    private IEnumerator FadeAndDestroySpark(GameObject obj, LineRenderer lr, float duration)
    {
        float elapsed = 0f;
        Vector3 drift = -transform.forward * 1.5f + Vector3.up * 0.8f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            obj.transform.position += drift * Time.deltaTime;
            lr.startWidth = Mathf.Lerp(0.14f, 0.01f, t);
            yield return null;
        }
        Destroy(obj);
    }

    private void OnDisable()
    {
        if (lineLeft != null) lineLeft.enabled = false;
        if (lineRight != null) lineRight.enabled = false;
    }

    private void OnDestroy()
    {
        if (ribbonMaterial != null) Destroy(ribbonMaterial);
        if (lineLeft != null && lineLeft.gameObject != null) Destroy(lineLeft.gameObject);
        if (lineRight != null && lineRight.gameObject != null) Destroy(lineRight.gameObject);
    }
}
