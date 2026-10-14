using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MountedHorseController))]
public class MountedHorsePhysicsTrample : MonoBehaviour
{
    public float trampleRadius = 2.2f;
    public float trampleDamage = 35f;
    public float trampleForce = 14f;
    public float trampleCooldown = 0.35f;

    private MountedHorseController movement;
    private ThanhGiongCampaignAudio audioFx;
    private ThanhGiongCampaignController campaign;
    private GodotCombatFeedback combatFeedback;
    private readonly Collider[] contacts = new Collider[48];
    private readonly HashSet<ThanhGiongEnemy> hitEnemies = new HashSet<ThanhGiongEnemy>();
    private readonly Dictionary<ThanhGiongEnemy, float> enemyCooldowns = new Dictionary<ThanhGiongEnemy, float>();
    private readonly List<ThanhGiongEnemy> expiredEnemies = new List<ThanhGiongEnemy>();
    private float nextTrampleTime;
    public int TrampleHitCount { get; private set; }
    public bool CampaignOwnsCharge => campaign != null && campaign.isActiveAndEnabled;

    private void Awake()
    {
        movement = GetComponent<MountedHorseController>();
        audioFx = GetComponent<ThanhGiongCampaignAudio>();
        campaign = GetComponent<ThanhGiongCampaignController>();
        combatFeedback = GetComponent<GodotCombatFeedback>();
        if (combatFeedback == null) combatFeedback = gameObject.AddComponent<GodotCombatFeedback>();
    }

    private void Update()
    {
        // The campaign already applies charge damage and feedback. This component
        // remains a fallback for standalone horses, never a second damage path.
        if (CampaignOwnsCharge || !Input.GetKey(KeyCode.LeftShift)) return;
        ProcessTrampleContacts();
    }

    public int ProcessTrampleContacts()
    {
        if (!isActiveAndEnabled || CampaignOwnsCharge || Time.timeScale <= 0f || movement == null ||
            movement.IsDodging || movement.PlanarSpeed < 5f || Time.time < nextTrampleTime) return 0;

        Vector3 chestPos = transform.position + Vector3.up * 1.0f + transform.forward * 1.5f;
        int count = Physics.OverlapSphereNonAlloc(chestPos, trampleRadius, contacts, ~0, QueryTriggerInteraction.Ignore);
        int hitsApplied = 0;
        hitEnemies.Clear();
        expiredEnemies.Clear();
        foreach (var entry in enemyCooldowns)
            if (entry.Key == null || Time.time >= entry.Value) expiredEnemies.Add(entry.Key);
        foreach (ThanhGiongEnemy expiredEnemy in expiredEnemies) enemyCooldowns.Remove(expiredEnemy);

        for (int i = 0; i < count; i++)
        {
            Collider col = contacts[i];
            if (col.transform.IsChildOf(transform)) continue;

            ThanhGiongEnemy enemy = col.GetComponentInParent<ThanhGiongEnemy>();
            if (enemy != null && enemy.gameObject.activeInHierarchy && enemy.HealthRatio > 0f && hitEnemies.Add(enemy))
            {
                if (enemyCooldowns.TryGetValue(enemy, out float nextHit) && Time.time < nextHit) continue;
                enemyCooldowns[enemy] = Time.time + Mathf.Max(.35f, trampleCooldown);
                float healthBefore = enemy.HealthRatio;
                // TakeDamage already performs ragdoll knockback. A second impulse
                // here used to overwrite it and double the apparent impact.
                enemy.TakeDamage(trampleDamage, 1.2f, transform.position, Mathf.Clamp(trampleForce / 14f, .75f, 1.6f), false);
                if (enemy.HealthRatio >= healthBefore) continue;
                combatFeedback?.PlayMountedImpact(enemy.transform, transform.position, transform.forward);

                TrampleHitCount++;
                hitsApplied++;
            }
        }

        if (hitsApplied > 0)
        {
            nextTrampleTime = Time.time + trampleCooldown;
            audioFx?.PlayImpact(false);
            IsometricCameraFollow.Instance?.Shake(0.3f, 0.5f);
        }
        return hitsApplied;
    }

    private void OnDisable()
    {
        hitEnemies.Clear(); enemyCooldowns.Clear(); expiredEnemies.Clear();
        nextTrampleTime = 0f;
    }
}
