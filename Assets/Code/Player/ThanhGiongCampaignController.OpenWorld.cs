using UnityEngine;

public partial class ThanhGiongCampaignController
{
    public bool OpenWorldFlightInProgress => isTransitioning && CurrentChapter==Chapter.Ascension;
    public bool TryPerformRitualStep(KeyCode key)
    {
        if(Time.timeScale<=0||IsDead||isTransitioning||EquipmentBusy||openWorldCampaign!=null&&!openWorldCampaign.CanPerformRitual)return false;
        int previous=qteIndex;
        if(CurrentChapter==Chapter.Preparation)UpdateEquipQte(key);
        else if(CurrentChapter==Chapter.Ascension)UpdateAscensionQte(key);
        return qteIndex>previous;
    }
    // Chapter changes in the continuous world never teleport or load another scene.
    public void EnterOpenWorldPreparation() { isTransitioning=false; BeginPreparation(); hud?.RecallPopups(); }
    public void EnterOpenWorldBattle() { isTransitioning=false; BeginBattle(); RefreshBattleGoal(); hud?.RecallPopups(); }
    public void EnterOpenWorldAscension() { isTransitioning=false; BeginAscension(); hud?.RecallPopups(); }
}
