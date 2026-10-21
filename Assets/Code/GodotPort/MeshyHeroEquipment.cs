using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Iron helmet (nón sắt) and golden iron armour for the Meshy rider. Both meshes are built at
/// runtime from the hero's own skin weights, so they fit the head and chest at every growth
/// stage. They are parented to the Head and Spine bones; while GodotMountedMotion plays an
/// equip/remove action, MeshyMountedCombatPose calls <see cref="Animate"/> to move the piece
/// along its path and to get the two hand grips.
/// </summary>
public sealed class MeshyHeroEquipment : MonoBehaviour
{
    public Transform Helmet { get; private set; }
    /// <summary>Golden armour worn on the body: a skinned shell grown from the hero's own torso.</summary>
    public Transform Armor { get; private set; }
    /// <summary>Rigid breastplate carried in both hands while the armour goes on or comes off.</summary>
    public Transform ArmorBundle { get; private set; }
    public bool IsReady => Helmet != null && Armor != null && ArmorBundle != null;
    public float ArmorGlow { get; private set; }

    Transform hero;
    float helmetRadius, armorHalfWidth;          // bone-local units
    Vector3 helmetUp, helmetForward, armorUp, armorForward;
    Material ironMaterial, goldMaterial, shellMaterial;
    Mesh helmetMesh, bundleMesh, shellMesh;
    static readonly Color ShellTint = new Color(1f, .84f, .42f);
    static readonly Color ShellSheen = new Color(.42f, .27f, .06f);
    static readonly Color GlowColor = new Color(1f, .62f, .16f);

    public void Build(Transform heroRoot)
    {
        if (IsReady || heroRoot == null) return;
        var skin = heroRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (skin == null || skin.sharedMesh == null || skin.bones == null) return;
        Transform[] bones = skin.bones;
        int head = Index(bones, "Head"), chest = Index(bones, "Spine");
        if (head < 0 || chest < 0) return;
        hero = heroRoot;

        Mesh source = skin.sharedMesh;
        if (!source.isReadable) return;
        Vector3[] vertices = source.vertices;
        BoneWeight[] weights = source.boneWeights;
        Matrix4x4[] bind = source.bindposes;
        if (weights == null || weights.Length != vertices.Length || bind.Length != bones.Length) return;

        // The auto-rig weights most of the torso to the thighs and shoulders, so the chest is
        // found geometrically: skin between the hips and the neck that is not arm, leg or head.
        var limbs = new HashSet<int>();
        foreach (string limb in new[] { "LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand",
                                        "LeftLeg", "LeftFoot", "LeftToeBase", "RightLeg", "RightFoot", "RightToeBase", "Head", "head_end" })
        { int index = Index(bones, limb); if (index >= 0) limbs.Add(index); }
        int hips = Index(bones, "Hips"), neckIndex = Index(bones, "neck");
        Vector3 hipsBind = BindPosition(bind, hips >= 0 ? hips : chest), neckBind = BindPosition(bind, neckIndex >= 0 ? neckIndex : head);
        Vector3 axis = neckBind - hipsBind;
        float axisLength = Mathf.Max(axis.magnitude, 1e-5f);
        axis /= axisLength;
        var headPoints = new List<Vector3>();
        var torsoPoints = new List<Vector3>();
        var torsoVertex = new bool[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            BoneWeight w = weights[i];
            if (w.weight0 >= .5f && w.boneIndex0 == head) { headPoints.Add(bind[head].MultiplyPoint3x4(vertices[i])); continue; }
            if (limbs.Contains(w.boneIndex0)) continue;
            float along = Vector3.Dot(vertices[i] - hipsBind, axis) / axisLength;
            if (along < .12f || along > .97f) continue;
            torsoVertex[i] = true;
            torsoPoints.Add(bind[chest].MultiplyPoint3x4(vertices[i]));
        }
        if (headPoints.Count < 50 || torsoPoints.Count < 50) return;

        // Same cel shader as the hero so the new pieces share its rim light and outline look.
        Material heroMaterial = skin.sharedMaterial;
        ironMaterial = NewMaterial(heroMaterial, "Non Sat Iron", new Color(.26f, .28f, .32f), .8f, .5f);
        goldMaterial = NewMaterial(heroMaterial, "Non Sat Gold Trim", new Color(1f, .76f, .3f), .95f, .7f);
        shellMaterial = heroMaterial != null ? new Material(heroMaterial) { name = "Giap Sat Vang Shell" } : NewMaterial(null, "Giap Sat Vang Shell", ShellTint, .85f, .6f);
        SetColor(shellMaterial, "_BaseColor", ShellTint);
        SetFloat(shellMaterial, "_Metallic", .85f);
        SetFloat(shellMaterial, "_Smoothness", .62f);
        shellMaterial.EnableKeyword("_EMISSION");

        Transform headBone = bones[head], chestBone = bones[chest];
        Transform headTip = FindChild(headBone, "head_end");
        Transform neck = FindChild(chestBone, "neck");
        helmetUp = headTip != null && headTip.localPosition.sqrMagnitude > 1e-8f ? headTip.localPosition.normalized : Vector3.up;
        armorUp = neck != null && neck.localPosition.sqrMagnitude > 1e-8f ? neck.localPosition.normalized : Vector3.up;
        helmetForward = Flat(headBone.InverseTransformDirection(heroRoot.forward), helmetUp);
        armorForward = Flat(chestBone.InverseTransformDirection(heroRoot.forward), armorUp);

        helmetMesh = BuildHelmet(headPoints, out helmetRadius);
        shellMesh = BuildShell(source, torsoVertex, axisLength * .014f);
        Helmet = NewPiece("IronHelmet_NonSat", headBone, helmetMesh, new[] { ironMaterial, goldMaterial });
        Armor = NewShell("IronArmor_GiapVang", skin, shellMesh, shellMaterial);
        // The carried armour is a rigid snapshot of the shell, re-baked at the start of each
        // action so it lands exactly where the worn shell appears.
        bundleMesh = new Mesh { name = "GiapSatVang_Carried" };
        ArmorBundle = NewPiece("IronArmor_Carried", Armor.parent, bundleMesh, new[] { shellMaterial });
        ArmorBundle.localPosition = Armor.localPosition; ArmorBundle.localRotation = Armor.localRotation; ArmorBundle.localScale = Armor.localScale;
        SetWorn(false, false);
    }

    /// <summary>Rest pose on the bones; called every frame by GodotMountedMotion.</summary>
    public void SetWorn(bool helmet, bool armor)
    {
        if (!IsReady) return;
        Rest(Helmet, helmet);
        if (ArmorBundle.gameObject.activeSelf) ArmorBundle.gameObject.SetActive(false);
        if (Armor.gameObject.activeSelf != armor) Armor.gameObject.SetActive(armor);
        bakedAction = null;
    }

    string bakedAction;
    Vector3 bundleCenterLocal;

    void BakeCarried(string action)
    {
        if (bakedAction == action) return;
        bakedAction = action;
        var skin = Armor.GetComponent<SkinnedMeshRenderer>();
        bool wasActive = Armor.gameObject.activeSelf;
        if (!wasActive) Armor.gameObject.SetActive(true);
        skin.BakeMesh(bundleMesh, true);
        if (!wasActive) Armor.gameObject.SetActive(false);
        bundleMesh.RecalculateBounds();
        bundleCenterLocal = bundleMesh.bounds.center;
        armorHalfWidth = bundleMesh.bounds.extents.x;
    }

    /// <summary>
    /// Moves the piece for an equip/remove action at normalized time t and returns world-space
    /// hand grips plus how strongly the hands should follow them.
    /// </summary>
    public bool Animate(string action, float t, Transform mount, out Vector3 leftGrip, out Vector3 rightGrip, out float reach)
    {
        leftGrip = rightGrip = Vector3.zero; reach = 0f;
        if (!IsReady || mount == null || string.IsNullOrEmpty(action)) return false;
        bool removing = action.StartsWith("remove");
        bool isHelmet = action.EndsWith("helmet");
        float phase = removing ? 1f - t : t;               // 0 = in the hands, 1 = worn
        Vector3 up = mount.up, forward = mount.forward, right = mount.right;
        reach = Smooth(0f, .14f, t) * (1f - Smooth(.86f, 1f, t));
        if (isHelmet)
        {
            Transform bone = Helmet.parent;
            Vector3 settled = bone.position;
            float size = bone.TransformVector(helmetUp * helmetRadius).magnitude;
            // Lifted from in front of the chest, raised clear of the topknot, then lowered on.
            Vector3 held = settled + forward * (2.6f * size) - up * (4.2f * size);
            Vector3 raised = settled + up * (1.9f * size) + forward * (.3f * size);
            Vector3 position = phase < .55f
                ? Vector3.Lerp(held, raised, Smooth(0f, .55f, phase))
                : Vector3.Lerp(raised, settled, Smooth(.55f, .9f, phase));
            float tilt = 1f - Smooth(.55f, .92f, phase);
            Helmet.gameObject.SetActive(true);
            Helmet.SetPositionAndRotation(position, Quaternion.AngleAxis(-18f * tilt, right) * Quaternion.AngleAxis(9f * tilt, forward) * bone.rotation);
            Helmet.localScale = Vector3.one * (removing ? 1f - Smooth(.85f, 1f, t) * .4f : 1f);
            leftGrip = position - right * (1.15f * size) - up * (.2f * size);
            rightGrip = position + right * (1.15f * size) - up * (.2f * size);
            return true;
        }

        // Armour: the golden plate is carried in and pressed to the chest; the moment it lands
        // the skinned shell takes over with a flare (reversed when it is taken off).
        BakeCarried(action);
        ArmorBundle.localPosition = Armor.localPosition; ArmorBundle.localRotation = Armor.localRotation;
        ArmorBundle.localScale = Armor.localScale;
        Vector3 center = Armor.TransformPoint(bundleCenterLocal);
        float width = Armor.TransformVector(Vector3.right * armorHalfWidth).magnitude;
        Vector3 offset = forward * (1.6f * width) - up * (.4f * width);
        float travel = 1f - Smooth(.08f, .8f, phase);
        Quaternion lean = Quaternion.AngleAxis(16f * travel, right);
        bool shellOn = phase >= .8f;
        Armor.gameObject.SetActive(shellOn);
        ArmorBundle.gameObject.SetActive(!shellOn);
        float shrink = removing ? 1f - Smooth(.85f, 1f, t) * .4f : 1f;
        // Rotate and scale about the plate's own centre, not the hero's feet.
        Vector3 pivotToOrigin = ArmorBundle.position - center;
        ArmorBundle.position = center + offset * travel + lean * pivotToOrigin * shrink;
        ArmorBundle.rotation = lean * ArmorBundle.rotation;
        ArmorBundle.localScale = Armor.localScale * shrink;
        Vector3 at = center + offset * travel;
        ArmorGlow = Mathf.Max(ArmorGlow, Smooth(.62f, .8f, phase) * (1f - Smooth(.8f, 1f, phase) * .5f));
        leftGrip = at - right * (1.0f * width) + forward * (.35f * width);
        rightGrip = at + right * (1.0f * width) + forward * (.35f * width);
        return true;
    }

    void LateUpdate()
    {
        if (shellMaterial == null) return;
        ArmorGlow = Mathf.MoveTowards(ArmorGlow, 0f, Time.deltaTime * .8f);
        Color emission = ShellSheen + GlowColor * (2.4f * ArmorGlow);
        SetColor(shellMaterial, "_EmissionColor", emission);
    }

    void Rest(Transform item, bool visible)
    {
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;
        item.localScale = Vector3.one;
        if (item.gameObject.activeSelf != visible) item.gameObject.SetActive(visible);
    }

    // ---------------------------------------------------------------- mesh building

    Mesh BuildHelmet(List<Vector3> points, out float radius)
    {
        Vector3 up = helmetUp, fwd = helmetForward, right = Vector3.Cross(up, fwd).normalized;
        var heights = new List<float>(points.Count);
        Vector3 center = Vector3.zero;
        foreach (Vector3 p in points) { heights.Add(Vector3.Dot(p, up)); center += p; }
        center /= points.Count;
        heights.Sort();
        // Ignore the topknot: the skull top is well below the highest hair vertices.
        float bottom = heights[(int)(heights.Count * .03f)], top = heights[(int)(heights.Count * .85f)];
        float height = Mathf.Max(top - bottom, 1e-4f);
        Vector3 flatCenter = center - up * Vector3.Dot(center, up);
        var radii = new List<float>();
        foreach (Vector3 p in points)
        {
            float h = Vector3.Dot(p, up);
            if (h < top - .6f * height || h > top - .2f * height) continue;
            radii.Add((p - up * h - flatCenter).magnitude);
        }
        radii.Sort();
        radius = radii.Count > 10 ? radii[(int)(radii.Count * .9f)] : height * .45f;
        float r = radius * 1.05f, bandHeight = .15f * height;
        Vector3 origin = flatCenter + up * (top - .5f * height);

        var builder = new MeshBuilder(2);
        // Iron bowl rising to a pointed crown (Đông Sơn style conical helmet).
        builder.Lathe(0, origin, right, up, fwd, new[] {
            new Vector2(r * 1.03f, bandHeight), new Vector2(r * 1.0f, bandHeight + .2f * height),
            new Vector2(r * .86f, bandHeight + .4f * height), new Vector2(r * .6f, bandHeight + .58f * height),
            new Vector2(r * .3f, bandHeight + .72f * height), new Vector2(r * .07f, bandHeight + .8f * height) }, 36);
        // Gold headband with a short brim.
        builder.Lathe(1, origin, right, up, fwd, new[] {
            new Vector2(r * 1.2f, -.03f * height), new Vector2(r * 1.17f, -.005f * height), new Vector2(r * 1.06f, 0f),
            new Vector2(r * 1.06f, bandHeight), new Vector2(r * 1.02f, bandHeight + .015f * height) }, 36);
        // Gold spike.
        float crown = bandHeight + .78f * height;
        builder.Lathe(1, origin, right, up, fwd, new[] {
            new Vector2(r * .12f, crown), new Vector2(r * .14f, crown + .05f * height), new Vector2(r * .07f, crown + .09f * height),
            new Vector2(r * .05f, crown + .2f * height), new Vector2(r * .004f, crown + .32f * height) }, 16);
        // Gold ridges down the bowl.
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * .5f;
            builder.Ridge(1, origin, right * Mathf.Sin(a) + fwd * Mathf.Cos(a), up, r, bandHeight, height * 1.15f);
        }
        return builder.ToMesh("NonSat_Helmet");
    }

    // Copies the torso triangles of the hero skin, pushed out along their normals, keeping
    // the hero's own bone weights so the armour follows every deformation of the body.
    static Mesh BuildShell(Mesh source, bool[] keep, float offset)
    {
        Vector3[] vertices = source.vertices, normals = source.normals;
        Vector2[] uv = source.uv;
        BoneWeight[] weights = source.boneWeights;
        int[] triangles = source.triangles;
        var remap = new Dictionary<int, int>();
        var outVertices = new List<Vector3>(); var outNormals = new List<Vector3>(); var outUv = new List<Vector2>();
        var outWeights = new List<BoneWeight>(); var outTriangles = new List<int>();
        bool hasNormals = normals != null && normals.Length == vertices.Length, hasUv = uv != null && uv.Length == vertices.Length;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            if (!keep[a] || !keep[b] || !keep[c]) continue;
            foreach (int v in new[] { a, b, c })
            {
                if (!remap.TryGetValue(v, out int index))
                {
                    index = outVertices.Count; remap[v] = index;
                    Vector3 n = hasNormals ? normals[v] : Vector3.zero;
                    outVertices.Add(vertices[v] + n * offset); outNormals.Add(n);
                    outUv.Add(hasUv ? uv[v] : Vector2.zero); outWeights.Add(weights[v]);
                }
                outTriangles.Add(index);
            }
        }
        var mesh = new Mesh { name = "GiapSatVang_Shell" };
        if (outVertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(outVertices); mesh.SetNormals(outNormals); mesh.SetUVs(0, outUv);
        mesh.SetTriangles(outTriangles, 0);
        mesh.boneWeights = outWeights.ToArray();
        mesh.bindposes = source.bindposes;
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------------------------------------------------------- helpers

    static int Index(Transform[] bones, string name)
    {
        for (int i = 0; i < bones.Length; i++) if (bones[i] != null && bones[i].name == name) return i;
        return -1;
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static Vector3 BindPosition(Matrix4x4[] bind, int bone) => bind[bone].inverse.MultiplyPoint3x4(Vector3.zero);

    static Vector3 Flat(Vector3 v, Vector3 up)
    {
        Vector3 f = Vector3.ProjectOnPlane(v, up);
        if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(Vector3.forward, up);
        if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(Vector3.right, up);
        return f.normalized;
    }

    static Material NewMaterial(Material template, string name, Color color, float metallic, float smoothness)
    {
        Shader fallback = Shader.Find("Universal Render Pipeline/Lit");
        var m = template != null ? new Material(template) : new Material(fallback != null ? fallback : Shader.Find("Standard"));
        m.name = name;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", null);
        if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
        m.DisableKeyword("_NORMALMAP");
        SetColor(m, "_BaseColor", color); SetColor(m, "_Color", color);
        SetFloat(m, "_Metallic", metallic); SetFloat(m, "_Smoothness", smoothness);
        // Thin generated shells: render both faces so winding never hides a piece.
        SetFloat(m, "_Cull", 0f);
        m.EnableKeyword("_EMISSION");
        return m;
    }

    static void SetColor(Material m, string property, Color value) { if (m != null && m.HasProperty(property)) m.SetColor(property, value); }
    static void SetFloat(Material m, string property, float value) { if (m != null && m.HasProperty(property)) m.SetFloat(property, value); }

    Transform NewShell(string name, SkinnedMeshRenderer heroSkin, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(heroSkin.transform.parent, false);
        go.transform.localPosition = heroSkin.transform.localPosition;
        go.transform.localRotation = heroSkin.transform.localRotation;
        go.transform.localScale = heroSkin.transform.localScale;
        go.layer = heroSkin.gameObject.layer;
        var renderer = go.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        renderer.bones = heroSkin.bones;
        renderer.rootBone = heroSkin.rootBone;
        renderer.sharedMaterial = material;
        renderer.updateWhenOffscreen = true;
        return go.transform;
    }

    Transform NewPiece(string name, Transform bone, Mesh mesh, Material[] materials)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(bone, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        go.layer = bone.gameObject.layer;
        return go.transform;
    }

    static float Smooth(float a, float b, float v)
    {
        float t = Mathf.Clamp01((v - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    void OnDestroy()
    {
        foreach (Object o in new Object[] { ironMaterial, goldMaterial, shellMaterial, helmetMesh, bundleMesh, shellMesh })
            if (o != null) Destroy(o);
    }

    sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<int>[] triangles;
        public MeshBuilder(int submeshes)
        {
            triangles = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) triangles[i] = new List<int>();
        }

        // Surface of revolution; profile x = radius, y = height along `up`.
        public void Lathe(int sub, Vector3 origin, Vector3 right, Vector3 up, Vector3 fwd, Vector2[] profile, int segments)
        {
            int start = vertices.Count;
            for (int j = 0; j < profile.Length; j++)
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    vertices.Add(origin + (right * Mathf.Sin(a) + fwd * Mathf.Cos(a)) * profile[j].x + up * profile[j].y);
                }
            Grid(sub, start, profile.Length, segments + 1, true);
        }

        // Elliptical band segment between two heights and two angles (degrees, 0 = forward).
        public void Shell(int sub, Vector3 center, Vector3 right, Vector3 up, Vector3 fwd, float rx, float rz,
                          float y0, float y1, float a0, float a1, int columns, int rows)
        {
            int start = vertices.Count;
            for (int j = 0; j <= rows; j++)
            {
                float v = j / (float)rows, y = Mathf.Lerp(y0, y1, v);
                float bulge = 1f + .06f * Mathf.Sin(v * Mathf.PI) + .05f * v;   // chest swells above the waist
                for (int i = 0; i <= columns; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / (float)columns) * Mathf.Deg2Rad;
                    vertices.Add(center + right * (Mathf.Sin(a) * rx * bulge) + fwd * (Mathf.Cos(a) * rz * bulge) + up * y);
                }
            }
            Grid(sub, start, rows + 1, columns + 1, true);
        }

        // Upper half ellipsoid.
        public void Cap(int sub, Vector3 center, Vector3 right, Vector3 up, Vector3 fwd, float sx, float sy, float sz, int segments, int rings)
        {
            int start = vertices.Count;
            for (int j = 0; j <= rings; j++)
            {
                float phi = (1f - j / (float)rings) * Mathf.PI * .5f;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    vertices.Add(center + right * (Mathf.Sin(a) * Mathf.Cos(phi) * sx) + fwd * (Mathf.Cos(a) * Mathf.Cos(phi) * sz) + up * (Mathf.Sin(phi) * sy));
                }
            }
            Grid(sub, start, rings + 1, segments + 1, false);
        }

        // Narrow raised strip following the dome from the band to the crown.
        public void Ridge(int sub, Vector3 origin, Vector3 dir, Vector3 up, float r, float band, float height)
        {
            Vector3 side = Vector3.Cross(up, dir).normalized * (r * .05f);
            float[] h = { band, band + .18f * height, band + .36f * height, band + .52f * height, band + .62f * height };
            float[] k = { 1.04f, .97f, .80f, .54f, .30f };
            int start = vertices.Count;
            for (int j = 0; j < h.Length; j++)
            {
                Vector3 c = origin + dir * (r * k[j] + r * .015f) + up * h[j];
                vertices.Add(c - side); vertices.Add(c + dir * (r * .03f)); vertices.Add(c + side);
            }
            Grid(sub, start, h.Length, 3, true);
        }

        void Grid(int sub, int start, int rows, int columns, bool outward)
        {
            for (int j = 0; j < rows - 1; j++)
                for (int i = 0; i < columns - 1; i++)
                {
                    int a = start + j * columns + i, b = a + 1, c = a + columns, d = c + 1;
                    if (outward) { triangles[sub].AddRange(new[] { a, c, b, b, c, d }); }
                    else { triangles[sub].AddRange(new[] { a, b, c, b, d, c }); }
                }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
