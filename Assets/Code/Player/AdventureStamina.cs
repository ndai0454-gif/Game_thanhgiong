using UnityEngine;

[DefaultExecutionOrder(-30)]
[DisallowMultipleComponent]
public sealed class AdventureStamina : MonoBehaviour
{
    public float capacity = 180f, sprintCostPerSecond = 12f, regenerationPerSecond = 34f;
    public float Current { get; private set; } = 100f;
    public float Ratio => Mathf.Clamp01(Current / Mathf.Max(1f, capacity));
    public bool SprintAllowed => !exhausted && Current > 0;
    public bool IsConsuming { get; private set; }
    MountedHorseController movement;
    ThanhGiongCampaignController campaign;
    bool exhausted;
    float recoverAfter;
    void Awake() { movement = GetComponent<MountedHorseController>(); campaign = GetComponent<ThanhGiongCampaignController>(); Current = capacity; }
    void Update()
    {
        if (Time.timeScale <= 0 || campaign != null && campaign.IsDead) return;
        Tick(Input.GetKey(KeyCode.LeftShift) && movement != null && movement.enabled && movement.PlanarSpeed > movement.walkSpeed * 1.05f, Time.deltaTime);
    }
    public void Tick(bool sprinting, float dt)
    {
        IsConsuming = sprinting && SprintAllowed;
        if (IsConsuming) { Current = Mathf.Max(0, Current - sprintCostPerSecond * dt); recoverAfter = Time.time + .8f; if (Current <= 0) exhausted = true; }
        else if (Time.time >= recoverAfter) Current = Mathf.Min(capacity, Current + regenerationPerSecond * dt);
        if (exhausted && Ratio >= .20f) exhausted = false;
    }
    public void Restore() { Current = capacity; exhausted = false; recoverAfter = 0; }
}
