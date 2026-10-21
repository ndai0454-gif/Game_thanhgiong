using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent, DefaultExecutionOrder(140)]
public sealed class ThanhGiongSeamlessCampaign : MonoBehaviour
{
    public ThanhGiongOpenWorld world;
    public ThanhGiongCampaignController campaign;
    public Vector3 forgeAnchor;
    public Vector3 peakAnchor;
    public OpenWorldEncounter[] encounters;
    public Transform[] regionalScenery;
    public bool persistenceEnabled=true;
    public bool restoreSaveOnStart=true;
    public string checkpointFileName="ThanhGiongOpenWorld.v1.json";
    public bool GrowthComplete { get; private set; }
    public bool PreparationComplete { get; private set; }
    public bool BattleComplete { get; private set; }
    public bool CanPerformRitual => campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Preparation?Near(forgeAnchor,15):campaign.CurrentChapter!=ThanhGiongCampaignController.Chapter.Ascension||Near(peakAnchor,18);
    public string CheckpointPath => Path.Combine(Application.persistentDataPath,Path.GetFileName(checkpointFileName));
    private float saveAt=-1;
    private float nextSceneryCheck;
    private bool restoring, endingSaved;
    private Vector3 initialSpawn;
    private Vector3 lastPermittedPosition;
    private float boundaryNoticeAt;
    private Quaternion initialRotation;
    private ThanhGiongCampaignAudio audio;

    [Serializable] public sealed class EnemySave {public string path;public Vector3 position;public Quaternion rotation;public float health;public bool alive;}
    [Serializable] public sealed class ItemSave {public string path;public bool active;}
    [Serializable] public sealed class SaveData {
        public int version=1;
        public bool growth,preparation,battle;
        public Vector3 position;public Quaternion rotation;
        public ThanhGiongCampaignController.CheckpointState campaign;
        public OpenWorldEconomyState economy;
        public LegendSeedSkills.State seeds;
        public List<EnemySave> enemies=new List<EnemySave>();
        public List<ItemSave> items=new List<ItemSave>();
    }

    private void Awake()
    {
        if(campaign==null)campaign=GetComponent<ThanhGiongCampaignController>();
        if(world==null)world=GetComponent<ThanhGiongOpenWorld>();
        if(campaign!=null)campaign.openWorldCampaign=this;
        audio=GetComponent<ThanhGiongCampaignAudio>();
        initialSpawn=transform.position;lastPermittedPosition=initialSpawn; initialRotation=transform.rotation;
    }
    private void Start()
    {
        if(campaign==null||world==null){enabled=false;return;}
        if(encounters==null||encounters.Length==0)encounters=campaign.battleRoot.GetComponentsInChildren<OpenWorldEncounter>(true);
        world.ZoneChanged+=OnZoneChanged;
        world.EconomyChanged+=ScheduleSave;
        if(restoreSaveOnStart&&persistenceEnabled)LoadCheckpoint();
        OnZoneChanged(world.CurrentZone);
    }
    private void OnDestroy()
    {
        if(world!=null){world.ZoneChanged-=OnZoneChanged;world.EconomyChanged-=ScheduleSave;}
    }
    private void Update()
    {
        if(campaign==null||world==null)return;
        if(Time.timeScale>0&&!campaign.IsDead&&!campaign.EquipmentBusy&&!world.IsRecovering) {
            var zone=world.CurrentZone;
            bool forbidden=zone!=null&&(!PreparationComplete&&zone.risk>=OpenWorldRisk.Yellow || zone.id=="soc_peak"&&!BattleComplete);
            if(forbidden){
                campaign.TeleportPlayer(lastPermittedPosition);world.RefreshCurrentZone();
                if(Time.unscaledTime>=boundaryNoticeAt){boundaryNoticeAt=Time.unscaledTime+2;campaign.ShowMessage(!PreparationComplete?"Đường chiến tuyến: hoàn tất trang bị tại lò rèn trước khi xuất quân.":"Đường Đỉnh Sóc: dẹp hết quân Ân trước khi làm nghi thức hóa thánh.",2);}
            }else lastPermittedPosition=transform.position;
            if(GrowthComplete&&campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Prologue&&world.HasForgedSupplies&&Near(forgeAnchor,13))campaign.EnterOpenWorldPreparation();
            if(BattleComplete&&campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Battle&&Near(peakAnchor,15))campaign.EnterOpenWorldAscension();
            if(Input.GetKeyDown(KeyCode.F9))SaveCheckpoint();
            if(transform.position.y < -12 || transform.position.x < -435 || transform.position.x > 305 || transform.position.z < -425 || transform.position.z > 305)ReturnToHub();
        }
        if(campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Complete&&!endingSaved){endingSaved=true;ScheduleSave();}
        if(saveAt>=0&&Time.unscaledTime>=saveAt&&!campaign.EquipmentBusy&&!world.IsRecovering){saveAt=-1;SaveCheckpoint();}
    }
    private void LateUpdate()
    {
        if(world==null||campaign==null)return;
        string zone=world.CurrentZone!=null?world.CurrentZone.id:string.Empty;
        if(encounters!=null)foreach(var group in encounters)if(group!=null){var definition=world.zones.Find(z=>z.id==group.zoneId);group.Apply(campaign.IsBattleActive&&!world.IsSafe&&zone==group.zoneId,definition!=null?definition.risk:OpenWorldRisk.Red);}
        // All terrain and routes stay resident. Only distant decorative authored clusters sleep.
        if(regionalScenery!=null&&Time.unscaledTime>=nextSceneryCheck){nextSceneryCheck=Time.unscaledTime+.5f;
            foreach(var root in regionalScenery)if(root!=null){bool visible=(root.position-transform.position).sqrMagnitude<240f*240f;if(root.gameObject.activeSelf!=visible)root.gameObject.SetActive(visible);}}
    }
    private bool Near(Vector3 point,float radius){Vector3 d=transform.position-point;d.y=0;return d.sqrMagnitude<radius*radius;}
    private void OnZoneChanged(OpenWorldZone zone)
    {
        campaign.hud?.NotifyWorldZone();
        if(zone!=null&&audio!=null)audio.SetOpenWorldRegion(zone.id=="soc_peak"?"sacred":zone.id=="red_river"?"river":zone.id=="red_fort"?"camp":zone.risk>=OpenWorldRisk.Yellow?"battle":Near(forgeAnchor,45)?"forge":"village");
        ScheduleSave();
    }
    private void ScheduleSave(){if(!restoring&&persistenceEnabled)saveAt=Time.unscaledTime+1f;}
    public string CurrentObjective {
        get {
            if(campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Prologue)
                return !GrowthComplete?$"Gom lương thực trong làng: {campaign.Food:0}/{campaign.foodPerGrowth*3:0}. M mở bản đồ.":!world.HasForgedSupplies?"Hỏi Thợ rèn Cả (quặng) và Cụ Ba (tre/gỗ) bằng phím F, rồi lấy 6 quặng ở núi + 3 tre/gỗ trong rừng. Về lò rèn, nhấn G đúc vật tư.":"Đi đến lò rèn trong làng để nhận trang bị vua ban.";
            if(campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Preparation)return "Tại lò rèn: E mặc giáp → Q đội nón → E lên ngựa → F thử lửa.";
            if(campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Battle)return BattleComplete?"Quân Ân đã tan. Theo đường đến Đỉnh Sóc trên bản đồ M để hóa thánh.":campaign.CurrentWeapon==ThanhGiongCampaignController.Weapon.None?$"Thương gãy! Nhổ tre E × 3. Đã hạ {campaign.Kills}/{campaign.RequiredBattleKills} giặc.":$"Dẹp trại vùng vàng, pháo đài đỏ và đại bản doanh đen. {campaign.Kills}/{campaign.RequiredBattleKills} giặc.";
            if(campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Ascension)return "Trên đỉnh Sóc: E đặt giáp → Q đặt nón → E cưỡi ngựa về trời.";
            return "Non sông thái bình. Hành trình bốn hồi hoàn tất trong một thế giới.";
        }
    }
    public void CompleteGrowth()
    {
        if(GrowthComplete)return;GrowthComplete=true;
        campaign.ShowMessage("GIÓNG ĐÃ TRƯỞNG THÀNH · Đi lấy quặng và tre, trở về lò rèn. M: bản đồ lục địa.",6);
        campaign.hud?.RecallPopups();ScheduleSave();
    }
    public void CompletePreparation()
    {
        if(PreparationComplete)return;PreparationComplete=true;
        campaign.EnterOpenWorldBattle();
        campaign.ShowMessage("XUẤT QUÂN · Cưỡi ngựa theo các con đường đến vùng vàng, đỏ và đen. Chiến tuyến đã mở!",5);
        ScheduleSave();
    }
    public void CompleteBattle()
    {
        if(BattleComplete)return;BattleComplete=true;
        campaign.ShowMessage("QUÂN ÂN ĐẠI BẠI · Tiếp tục cưỡi ngựa đến Đỉnh Sóc để làm nghi thức hóa thánh.",6);
        ScheduleSave();
    }
    public bool HandleLegacyNavigation(string destination)
    {
        if(destination==SceneManager.GetActiveScene().name)return ReturnToHub();
        world.SetMapOpen(true);
        world.ShowNotice("Các địa danh nằm trên cùng lục địa. Chọn vùng xem đường rồi cưỡi ngựa đến đó.",6);
        return true;
    }
    public bool ReturnToHub()
    {
        if(campaign.OpenWorldFlightInProgress||world.IsRecovering)return false;
        var state=campaign.CaptureCheckpointState();state.health=campaign.maxHealth;
        if(!campaign.RestoreCheckpointState(state,world.safeSpawn,initialRotation))return false;
        world.RefreshCurrentZone();ScheduleSave();return true;
    }
    // Closing the window keeps the journey, the same as Save & Quit from the pause menu.
    private void OnApplicationQuit(){if(persistenceEnabled&&!restoring)SaveCheckpoint();}
    public bool SaveCheckpoint()
    {
        if(!persistenceEnabled||restoring||campaign==null||campaign.IsDead||campaign.EquipmentBusy||campaign.OpenWorldFlightInProgress)return false;
        var data=new SaveData {growth=GrowthComplete,preparation=PreparationComplete,battle=BattleComplete,position=transform.position,rotation=transform.rotation,campaign=campaign.CaptureCheckpointState(),economy=world.CaptureEconomy()};
        var seeds=GetComponent<LegendSeedSkills>();if(seeds!=null)data.seeds=seeds.CaptureState();
        bool beforeBattle=(int)campaign.CurrentChapter<(int)ThanhGiongCampaignController.Chapter.Battle;
        foreach(var enemy in campaign.battleRoot.GetComponentsInChildren<ThanhGiongEnemy>(true))data.enemies.Add(new EnemySave {path=NamedPath(enemy.transform),position=enemy.transform.position,rotation=enemy.transform.rotation,health=beforeBattle?enemy.maxHealth:enemy.HealthRatio*enemy.maxHealth,alive=beforeBattle||enemy.HealthRatio>0});
        foreach(var item in FindObjectsByType<ThanhGiongCollectible>(FindObjectsInactive.Include))data.items.Add(new ItemSave {path=NamedPath(item.transform),active=item.gameObject.activeSelf&&!item.IsCollected});
        try {
            string json=JsonUtility.ToJson(data,true);Directory.CreateDirectory(Path.GetDirectoryName(CheckpointPath));
            File.WriteAllText(CheckpointPath+".tmp",json);
            if(File.Exists(CheckpointPath))File.Copy(CheckpointPath,CheckpointPath+".bak",true);
            File.Copy(CheckpointPath+".tmp",CheckpointPath,true);
            return true;
        }catch(Exception e){Debug.LogWarning("Open world save failed: "+e.Message);return false;}
    }
    public bool LoadCheckpoint()
    {
        if(!persistenceEnabled||!File.Exists(CheckpointPath)||campaign.EquipmentBusy)return false;
        SaveData data;
        try{data=JsonUtility.FromJson<SaveData>(File.ReadAllText(CheckpointPath));}catch(Exception e){Debug.LogWarning("Open world save unreadable: "+e.Message);return false;}
        if(data==null||data.version!=1||data.campaign==null||data.economy==null||data.campaign.growth<0||data.campaign.growth>3)return false;
        // Recover older interrupted-flight checkpoints at the final ritual step, on solid ground.
        if(data.campaign.chapter==ThanhGiongCampaignController.Chapter.Ascension&&data.campaign.qte>=3){data.campaign.qte=2;data.position=peakAnchor;}
        restoring=true;
        try {
            if(!world.RestoreEconomy(data.economy)||!campaign.RestoreCheckpointState(data.campaign,data.position,data.rotation))return false;
            GrowthComplete=data.growth;PreparationComplete=data.preparation;BattleComplete=data.battle;
            var seeds=GetComponent<LegendSeedSkills>();if(seeds!=null&&data.seeds!=null)seeds.RestoreState(data.seeds);
            // Pre-battle actors have not entered Awake/OnEnable yet; their normal first activation initializes health.
            if(data.campaign.chapter>=ThanhGiongCampaignController.Chapter.Battle)
                foreach(var saved in data.enemies){var node=ResolveNamed(saved.path);if(node==null)continue;var enemy=node.GetComponent<ThanhGiongEnemy>();if(enemy!=null){var zone=world.FindZone(saved.position);if(zone!=null)enemy.ConfigureWorldTier(zone.risk);enemy.RestoreCheckpoint(campaign,transform,saved.position,saved.rotation,saved.health,saved.alive);}}
            foreach(var saved in data.items){var node=ResolveNamed(saved.path);if(node!=null&&node.GetComponent<ThanhGiongCollectible>()!=null)node.gameObject.SetActive(saved.active);}
            world.RefreshCurrentZone();return true;
        }finally{restoring=false;}
    }
    private static string NamedPath(Transform node){string path=node.name;while(node.parent!=null){node=node.parent;path=node.name+"/"+path;}return path;}
    private static Transform ResolveNamed(string path){string[] parts=path.Split('/');Transform node=null;foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())if(root.name==parts[0]){node=root.transform;break;}for(int i=1;i<parts.Length&&node!=null;i++)node=node.Find(parts[i]);return node;}
}
