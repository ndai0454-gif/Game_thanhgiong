using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThanhGiong.Systems
{
    /// <summary>
    /// Keeps frame rate stable in the large open world: point lights and looping particle systems that are
    /// far from the camera are switched off (and resumed when the camera comes back). Work is spread over
    /// frames so the governor itself never causes a spike. Objects spawned later are not touched.
    /// </summary>
    public sealed class ThanhGiongPerformanceGovernor : MonoBehaviour
    {
        public float lightRadius = 55f;
        public float particleRadius = 95f;
        public float hysteresis = 1.15f;
        public int itemsPerFrame = 350;

        sealed class LightItem { public Light light; public Transform t; public bool on; }
        sealed class ParticleItem { public ParticleSystem ps; public ParticleSystemRenderer renderer; public Transform t; public bool on = true; }

        readonly List<LightItem> lights = new List<LightItem>();
        readonly List<ParticleItem> particles = new List<ParticleItem>();
        int lightCursor, particleCursor;
        Transform cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<ThanhGiongPerformanceGovernor>() != null) return;
            var go = new GameObject("ThanhGiong Performance Governor");
            DontDestroyOnLoad(go);
            go.AddComponent<ThanhGiongPerformanceGovernor>();
        }

        void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; Restore(); }
        void OnSceneLoaded(Scene s, LoadSceneMode m) { Invoke(nameof(Rescan), 1.5f); }
        void Start() { Invoke(nameof(Rescan), 1.5f); Invoke(nameof(Rescan), 6f); }

        void Rescan()
        {
            Restore();
            lights.Clear(); particles.Clear();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type != LightType.Directional && l.enabled && l.gameObject.activeInHierarchy)
                    lights.Add(new LightItem { light = l, t = l.transform, on = true });
            foreach (var p in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (p.isPlaying && p.main.loop && p.gameObject.activeInHierarchy)
                    particles.Add(new ParticleItem { ps = p, renderer = p.GetComponent<ParticleSystemRenderer>(), t = p.transform });
            lightCursor = particleCursor = 0;
        }

        void Restore()
        {
            foreach (var i in lights) if (i.light != null) i.light.enabled = true;
            foreach (var i in particles) if (i.ps != null && !i.on) { i.ps.Play(false); if (i.renderer != null) i.renderer.enabled = true; }
        }

        void LateUpdate()
        {
            if (cam == null) { var c = Camera.main; if (c == null) return; cam = c.transform; }
            Vector3 origin = cam.position;
            Step(origin);
        }

        void Step(Vector3 origin)
        {
            float lr = lightRadius * lightRadius, lrOff = lr * hysteresis * hysteresis;
            float pr = particleRadius * particleRadius, prOff = pr * hysteresis * hysteresis;
            int budget = itemsPerFrame;
            for (int n = 0; n < budget && lights.Count > 0; n++)
            {
                if (lightCursor >= lights.Count) lightCursor = 0;
                var it = lights[lightCursor++];
                if (it.light == null) continue;
                float d = (it.t.position - origin).sqrMagnitude;
                bool want = it.on ? d < lrOff : d < lr;
                if (want != it.on) { it.on = want; it.light.enabled = want; }
            }
            for (int n = 0; n < budget && particles.Count > 0; n++)
            {
                if (particleCursor >= particles.Count) particleCursor = 0;
                var it = particles[particleCursor++];
                if (it.ps == null) continue;
                float d = (it.t.position - origin).sqrMagnitude;
                bool want = it.on ? d < prOff : d < pr;
                if (want == it.on) continue;
                it.on = want;
                if (want) { it.ps.Play(false); if (it.renderer != null) it.renderer.enabled = true; }
                else { it.ps.Pause(false); if (it.renderer != null) it.renderer.enabled = false; }
            }
        }
    }
}
