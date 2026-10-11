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
    private readonly HashSet<ThanhGiongEnemy> hitEnemies = new HashSet<ThanhGiongEnemy>();
    private float nextTrampleTime;

    private void Awake()
    {
        movement = GetComponent<MountedHorseController>();
        audioFx = GetComponent<ThanhGiongCampaignAudio>();
    }

    private void Update()
    {
        bool arenaRunning = ThanhGiongArenaInput.Running && ThanhGiongArenaInput.Move.sqrMagnitude > 0.01f;
        bool legacyRunning = Input.GetKey(KeyCode.LeftShift) && (Input.GetAxisRaw("Horizontal") != 0f || Input.GetAxisRaw("Vertical") != 0f);
        bool isSprinting = arenaRunning || legacyRunning;
        if (!isSprinting || Time.time < nextTrampleTime) return;

        Vector3 chestPos = transform.position + Vector3.up * 1.0f + transform.forward * 1.5f;
        Collider[] hits = Physics.OverlapSphere(chestPos, trampleRadius, ~0, QueryTriggerInteraction.Ignore);
        bool trampledAny = false;

        foreach (var col in hits)
        {
            if (col.transform.IsChildOf(transform)) continue;

            ThanhGiongEnemy enemy = col.GetComponentInParent<ThanhGiongEnemy>();
            if (enemy != null && enemy.gameObject.activeSelf && !hitEnemies.Contains(enemy))
            {
                hitEnemies.Add(enemy);
                Vector3 knockbackDir = (enemy.transform.position - transform.position).normalized + transform.forward * 0.4f + Vector3.up * 0.6f;
                enemy.TakeDamage(trampleDamage, 1.2f, transform.position);

                var ragdoll = enemy.GetComponent<ThanhGiongRagdollPhysics>();
                if (ragdoll != null)
                {
                    ragdoll.ApplyKnockback(transform.position, knockbackDir, trampleForce, 2.8f);
                }

                trampledAny = true;
            }
        }

        if (trampledAny)
        {
            nextTrampleTime = Time.time + trampleCooldown;
            audioFx?.PlayStoneSmash();
            IsometricCameraFollow.Instance?.Shake(0.3f, 0.5f);
            hitEnemies.Clear();
        }
    }
}
