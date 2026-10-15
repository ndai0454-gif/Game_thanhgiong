using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ThanhGiongResourceNode : MonoBehaviour
{
    public OpenWorldResource resource;
    [Tooltip("0 uses the biome's primary / secondary / rare ratio 6 : 3 : 1.")]
    [Min(0)] public int yieldOverride;
    [Min(1f)] public float respawnSeconds = 45f;
    public static readonly List<ThanhGiongResourceNode> ActiveNodes = new List<ThanhGiongResourceNode>();
    public bool IsReady => Time.time >= availableAt;
    public float RemainingSeconds => Mathf.Max(0f, availableAt - Time.time);
    private float availableAt;
    private Renderer[] visuals;
    private Color[] originalColors;
    private MaterialPropertyBlock propertyBlock;
    private bool lastReady = true;

    private void Awake()
    {
        visuals = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        originalColors = new Color[visuals.Length];
        for (int i = 0; i < visuals.Length; i++) if (visuals[i].sharedMaterial != null && visuals[i].sharedMaterial.HasProperty("_BaseColor")) originalColors[i] = visuals[i].sharedMaterial.GetColor("_BaseColor");
    }
    private void OnEnable() { if (!ActiveNodes.Contains(this)) ActiveNodes.Add(this); }
    private void OnDisable() { ActiveNodes.Remove(this); }
    public void RestoreCooldown(float remainingSeconds) { availableAt = Time.time + Mathf.Max(0f, remainingSeconds); }

    public bool TryHarvest(ThanhGiongOpenWorld world)
    {
        if (world == null || !IsReady || !world.IsWithinInteractionRange(transform.position)) return false;
        var zone = world.FindZone(transform.position);
        if (zone == null || zone != world.CurrentZone) return false;
        int baseYield = ThanhGiongOpenWorld.BaseGatherYield(zone.biome, resource);
        if (baseYield == 0) return false;
        int tierBonus = zone.risk == OpenWorldRisk.Black ? 4 : zone.risk == OpenWorldRisk.Red ? 3 : zone.risk == OpenWorldRisk.Yellow ? 2 : 1;
        int amount = yieldOverride > 0 ? yieldOverride : baseYield * tierBonus;
        if (!world.TryGather(resource, amount)) return false;
        availableAt = Time.time + respawnSeconds;
        return true;
    }

    private void Update()
    {
        if (visuals == null || lastReady == IsReady) return;
        lastReady = IsReady;
        for (int i = 0; i < visuals.Length; i++) {
            if (visuals[i] == null || visuals[i].sharedMaterial == null || !visuals[i].sharedMaterial.HasProperty("_BaseColor")) continue;
            visuals[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", IsReady ? originalColors[i] : originalColors[i] * .38f);
            visuals[i].SetPropertyBlock(propertyBlock);
        }
    }
}
