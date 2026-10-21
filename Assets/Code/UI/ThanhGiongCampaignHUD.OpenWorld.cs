using UnityEngine;

public partial class ThanhGiongCampaignHUD
{
    public void NotifyWorldZone()
    {
        regionUntil=Time.time+regionPopupSeconds;
        RecallPopups();
    }
}
