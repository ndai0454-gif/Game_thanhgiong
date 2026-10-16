using UnityEngine;
using UnityEngine.Rendering;

namespace ThanhGiong.Environment
{
    /// <summary>
    /// Low-poly drifting clouds built from the KayKit cloud meshes. A fixed pool is created once in Start and
    /// recycled around the follow target (the main camera by default), so nothing is instantiated or destroyed at runtime.
    /// </summary>
    public class ThanhGiongSkyClouds : MonoBehaviour
    {
        [Header("Assets")]
        public Mesh[] cloudMeshes;
        public Material cloudMaterial;

        [Header("Layout")]
        [Min(1)] public int cloudCount = 26;
        public float minRadius = 140f;
        public float maxRadius = 520f;
        public Vector2 altitudeRange = new Vector2(85f, 150f);
        [Tooltip("Cloud width in metres (the source meshes are ~3 m wide).")]
        public Vector2 widthRange = new Vector2(70f, 160f);

        [Header("Motion")]
        public Vector2 windDirection = new Vector2(1f, 0.35f);
        public float windSpeed = 2.2f;
        public int seed = 7;

        private Transform[] clouds;
        private float[] speedScale;
        private Transform follow;
        private Vector3 wind;

        private void Start()
        {
            if (cloudMeshes == null || cloudMeshes.Length == 0) { enabled = false; return; }
            follow = Camera.main != null ? Camera.main.transform : transform;
            wind = new Vector3(windDirection.x, 0f, windDirection.y).normalized * windSpeed;

            var rng = new System.Random(seed);
            clouds = new Transform[cloudCount];
            speedScale = new float[cloudCount];
            for (int i = 0; i < cloudCount; i++)
            {
                Mesh mesh = cloudMeshes[i % cloudMeshes.Length];
                var go = new GameObject("Cloud " + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = cloudMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                float width = Mathf.Lerp(widthRange.x, widthRange.y, (float)rng.NextDouble());
                float scale = width / Mathf.Max(0.01f, mesh.bounds.size.x);
                go.transform.localScale = new Vector3(scale, scale * 0.7f, scale);
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(minRadius, maxRadius, Mathf.Sqrt((float)rng.NextDouble()));
                float altitude = Mathf.Lerp(altitudeRange.x, altitudeRange.y, (float)rng.NextDouble());
                go.transform.position = follow.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                go.transform.position = new Vector3(go.transform.position.x, altitude, go.transform.position.z);

                clouds[i] = go.transform;
                speedScale[i] = Mathf.Lerp(0.6f, 1.4f, (float)rng.NextDouble());
            }
        }

        private void Update()
        {
            if (clouds == null) return;
            if (follow == null) { if (Camera.main == null) return; follow = Camera.main.transform; }
            Vector3 center = follow.position;
            float dt = Time.deltaTime;
            for (int i = 0; i < clouds.Length; i++)
            {
                Transform c = clouds[i];
                Vector3 p = c.position + wind * (speedScale[i] * dt);
                Vector3 flat = new Vector3(p.x - center.x, 0f, p.z - center.z);
                // Wrap to the opposite side of the ring so the sky stays populated while the player moves.
                if (flat.sqrMagnitude > maxRadius * maxRadius)
                {
                    Vector3 wrapped = center - flat.normalized * (maxRadius * 0.98f);
                    p.x = wrapped.x; p.z = wrapped.z;
                }
                c.position = p;
            }
        }
    }
}
