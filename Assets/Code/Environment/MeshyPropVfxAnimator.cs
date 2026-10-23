using UnityEngine;

namespace ThanhGiong.Environment
{
    /// <summary>
    /// Runtime animator for Meshy 3D environment blocks & props:
    /// - Flickers forge/lantern/brazier/altar point lights
    /// - Adds subtle wind sway to bamboo groves & war banners
    /// - Adds gentle hover/pulse to resource feast trays & sacred altars
    /// </summary>
    public class MeshyPropVfxAnimator : MonoBehaviour
    {
        public enum PropEffectKind
        {
            None = 0,
            HearthLantern = 1,
            SacredDrumShrine = 2,
            MoltenForge = 3,
            GoldenBambooWind = 4,
            WarBannerFlutter = 5,
            EnemyWatchtowerFire = 6,
            RiverBridgeMist = 7,
            ResourceFeastHover = 8,
            CelestialAscensionAltar = 9,
            SacredStatueAura = 10,
            CrimsonWarlordGaze = 11
        }

        public PropEffectKind effectKind = PropEffectKind.HearthLantern;
        public Light propLight;
        public Transform animatedMeshRoot;
        public float baseLightIntensity = 2.0f;
        public float phaseOffset;

        private Vector3 _initialLocalPos;
        private Quaternion _initialLocalRot;
        private Vector3 _initialLocalScale;
        private float _initialLightRange;
        private bool _capturedLight;
        private bool _initialized;

        private void Awake()
        {
            CaptureInitialTransform();
            phaseOffset = Mathf.Abs(transform.position.x * 0.73f + transform.position.z * 1.19f) % 10f;
        }

        private void CaptureInitialTransform()
        {
            if (animatedMeshRoot == null)
            {
                animatedMeshRoot = transform;
            }
            _initialLocalPos = animatedMeshRoot.localPosition;
            _initialLocalRot = animatedMeshRoot.localRotation;
            _initialLocalScale = animatedMeshRoot.localScale;
            _initialized = true;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (!_initialized) CaptureInitialTransform();

            float t = Time.time + phaseOffset;

            if (propLight != null)
            {
                if (!_capturedLight)
                {
                    _initialLightRange = propLight.range;
                    _capturedLight = true;
                }

                switch (effectKind)
                {
                    case PropEffectKind.MoltenForge:
                    {
                        // Nhịp thở nhiệt nóng kết hợp bập bùng ngọn lửa lò rèn thần
                        float heatPulse = Mathf.Sin(t * 1.8f) * 0.12f;
                        float flicker = 0.22f * Mathf.Sin(t * 7.5f) + 0.11f * Mathf.Sin(t * 16.3f) + 0.05f * Mathf.Cos(t * 27.1f);
                        propLight.intensity = baseLightIntensity * (1f + heatPulse + flicker);
                        if (_initialLightRange > 0.01f)
                        {
                            propLight.range = _initialLightRange * (1f + 0.07f * Mathf.Sin(t * 2.8f));
                        }
                        break;
                    }
                    case PropEffectKind.EnemyWatchtowerFire:
                    {
                        // Lửa tháp canh giặc bập bùng dữ dội trong gió
                        float windFlicker = 0.20f * Mathf.Sin(t * 9.2f) + 0.12f * Mathf.Cos(t * 14.1f) + 0.06f * Mathf.Sin(t * 23.5f);
                        propLight.intensity = baseLightIntensity * (1f + windFlicker);
                        break;
                    }
                    case PropEffectKind.SacredDrumShrine:
                    case PropEffectKind.CelestialAscensionAltar:
                    case PropEffectKind.SacredStatueAura:
                        propLight.intensity = baseLightIntensity * (1f + 0.20f * Mathf.Sin(t * 2.4f));
                        break;
                    case PropEffectKind.CrimsonWarlordGaze:
                        propLight.intensity = baseLightIntensity * (1f + 0.32f * Mathf.Sin(t * 5.8f));
                        break;
                    case PropEffectKind.HearthLantern:
                    default:
                    {
                        // Đèn lồng hiên nhà bập bùng êm dịu, ấm áp
                        float gentleFlicker = 0.10f * Mathf.Sin(t * 3.8f) + 0.05f * Mathf.Sin(t * 8.6f);
                        propLight.intensity = baseLightIntensity * (1f + gentleFlicker);
                        break;
                    }
                }
            }

            if (animatedMeshRoot != null && animatedMeshRoot != transform)
            {
                switch (effectKind)
                {
                    case PropEffectKind.GoldenBambooWind:
                    {
                        float swayX = Mathf.Sin(t * 1.8f) * 2.2f;
                        float swayZ = Mathf.Cos(t * 1.35f) * 1.6f;
                        animatedMeshRoot.localRotation = _initialLocalRot * Quaternion.Euler(swayX, 0f, swayZ);
                        break;
                    }
                    case PropEffectKind.WarBannerFlutter:
                    {
                        float swayZ = Mathf.Sin(t * 2.6f) * 1.8f;
                        animatedMeshRoot.localRotation = _initialLocalRot * Quaternion.Euler(0f, 0f, swayZ);
                        break;
                    }
                    case PropEffectKind.ResourceFeastHover:
                    {
                        float bob = Mathf.Sin(t * 2.5f) * 0.08f;
                        animatedMeshRoot.localPosition = _initialLocalPos + new Vector3(0f, bob, 0f);
                        animatedMeshRoot.localRotation = _initialLocalRot * Quaternion.Euler(0f, (Time.time * 22f) % 360f, 0f);
                        break;
                    }
                    case PropEffectKind.CelestialAscensionAltar:
                    {
                        float pulse = 1f + Mathf.Sin(t * 2.0f) * 0.015f;
                        animatedMeshRoot.localScale = _initialLocalScale * pulse;
                        break;
                    }
                }
            }
        }
    }
}
