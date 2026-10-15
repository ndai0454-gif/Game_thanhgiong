using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThanhGiongImprovisedWeapon : MonoBehaviour
{
    public enum WeaponType { Boulder, TreeLog }
    public WeaponType type = WeaponType.Boulder;
    public float throwDamage = 120f;
    public float stunDuration = 2.5f;
    public float aoeRadius = 5.5f;

    private bool isCarried;
    public bool IsCarried => isCarried;
    private Transform carrier;
    private Rigidbody rb;
    private Collider col;
    private bool launched, impacted;
    private Vector3 previousPosition;
    private float launchedAt;
    readonly RaycastHit[] flightHits = new RaycastHit[48];

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        col = GetComponent<Collider>();
    }

    public void PickUp(Transform player)
    {
        isCarried = true;
        carrier = player;
        rb.isKinematic = true;
        if (col != null) col.enabled = false;
        transform.SetParent(player);
        transform.localPosition = new Vector3(0f, 3.2f, 0.4f);
        transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
    }

    public void Throw(Vector3 direction, float force = 18f)
    {
        Launch(direction.normalized * force + Vector3.up * 4.5f, carrier);
    }

    public void Launch(Vector3 velocity, Transform owner)
    {
        carrier = owner;
        isCarried = false;
        transform.SetParent(null);
        if (col != null) col.enabled = true;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = 0;
        rb.linearVelocity = velocity;
        rb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);
        if (col != null && carrier != null)
            foreach (var ownedCollider in carrier.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, ownedCollider);
        previousPosition = transform.position;
        launchedAt = Time.time; launched = true; impacted = false;
    }

    void FixedUpdate()
    {
        if (!launched || impacted || Time.timeScale <= 0) return;
        Vector3 delta = transform.position - previousPosition;
        int n = delta.sqrMagnitude > .00001f ? Physics.SphereCastNonAlloc(previousPosition, .12f, delta.normalized, flightHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore) : 0;
        float closest = float.MaxValue; int nearest = -1;
        for (int i = 0; i < n; i++) {
            Transform hit = flightHits[i].transform;
            if (hit.IsChildOf(transform) || carrier != null && hit.IsChildOf(carrier)) continue;
            if (flightHits[i].distance < closest) { closest = flightHits[i].distance; nearest = i; }
        }
        if (nearest >= 0) { transform.position = flightHits[nearest].point; ExplodeImpact(); }
        else if (Time.time - launchedAt > 4f) ExplodeImpact();
        previousPosition = transform.position;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!launched || impacted || carrier != null && collision.transform.IsChildOf(carrier)) return;
        ExplodeImpact();
    }

    private void ExplodeImpact()
    {
        if (impacted) return;
        impacted = true;
        // Camera shake
        IsometricCameraFollow.Instance?.Shake(0.4f, 0.8f);

        // AOE Damage & Stun
        Collider[] hits = Physics.OverlapSphere(transform.position, aoeRadius);
        var affected = new HashSet<ThanhGiongEnemy>();
        foreach (var h in hits)
        {
            ThanhGiongEnemy enemy = h.GetComponentInParent<ThanhGiongEnemy>();
            if (enemy != null && enemy.gameObject.activeSelf && affected.Add(enemy))
            {
                enemy.TakeDamage(throwDamage, stunDuration);
            }
        }

        // VFX Ring
        ThanhGiongCampaignVFX vfx = FindAnyObjectByType<ThanhGiongCampaignVFX>();
        vfx?.PlayGrowth(transform);

        Destroy(gameObject, 0.2f);
    }
}
