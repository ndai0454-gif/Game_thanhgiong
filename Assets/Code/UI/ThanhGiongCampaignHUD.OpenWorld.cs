using UnityEngine;

public partial class ThanhGiongCampaignHUD
{
    public void NotifyWorldZone()
    {
        regionUntil=Time.time+3.8f;
        RecallPopups();
    }
}
