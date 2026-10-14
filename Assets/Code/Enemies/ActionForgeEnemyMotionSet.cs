using UnityEngine;

/// <summary>Controllers sharing the same skeleton, selected by actual combat role.</summary>
[CreateAssetMenu(menuName = "Thánh Gióng/ActionForge Enemy Motion Set")]
public sealed class ActionForgeEnemyMotionSet : ScriptableObject
{
    public RuntimeAnimatorController spearman;
    public RuntimeAnimatorController shieldBearer;
    public RuntimeAnimatorController archer;
    public RuntimeAnimatorController raider;
    public RuntimeAnimatorController boss;

    public RuntimeAnimatorController For(ThanhGiongEnemy.EnemyRole role, bool isBoss)
    {
        if (isBoss) return boss;
        switch (role)
        {
            case ThanhGiongEnemy.EnemyRole.ShieldBearer: return shieldBearer;
            case ThanhGiongEnemy.EnemyRole.Archer: return archer;
            case ThanhGiongEnemy.EnemyRole.Raider: return raider;
            default: return spearman;
        }
    }
}
