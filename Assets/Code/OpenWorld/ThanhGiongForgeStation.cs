using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ThanhGiongForgeStation : MonoBehaviour
{
    public static readonly List<ThanhGiongForgeStation> ActiveStations = new List<ThanhGiongForgeStation>();
    private void OnEnable() { if (!ActiveStations.Contains(this)) ActiveStations.Add(this); }
    private void OnDisable() { ActiveStations.Remove(this); }

    public bool TryInteract(ThanhGiongOpenWorld world)
    {
        if (world == null || !world.IsWithinInteractionRange(transform.position)) return false;
        return world.TryForge(world.HasForgedSupplies ? OpenWorldForgeRecipe.RepairEquipment : OpenWorldForgeRecipe.Supplies);
    }
}
