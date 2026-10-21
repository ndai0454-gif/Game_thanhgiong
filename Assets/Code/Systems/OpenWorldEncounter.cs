using UnityEngine;

// Sleep AI/physics without toggling OnEnable, which resets the existing enemy's health.
public sealed class OpenWorldEncounter : MonoBehaviour
{
    public string zoneId;
    public GameObject content;
    private ThanhGiongEnemy[] enemies;
    public void Apply(bool awake,OpenWorldRisk risk=OpenWorldRisk.Red)
    {
        if(enemies==null)enemies=(content!=null?content:gameObject).GetComponentsInChildren<ThanhGiongEnemy>(true);
        foreach(var enemy in enemies)if(enemy!=null){enemy.ConfigureWorldTier(risk);enemy.SetWorldDormant(!awake);}
    }
}
