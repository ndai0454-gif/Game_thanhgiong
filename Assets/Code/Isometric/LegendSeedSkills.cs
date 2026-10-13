using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(120)]
public sealed class LegendSeedSkills : MonoBehaviour
{
    public enum Seed { Lumen, Bamboo, Lotus, Ember, Wind }
    public float maxMana = 100, manaPerSecond = 4, plantRange = 8;
    public int chargesPerSeed = 3;
    public Seed SelectedSeed { get; private set; }
    public string SelectedName => Names[(int)SelectedSeed];
    public float Mana { get; private set; }
    public float MaxMana => maxMana;
    public int PlantedCount { get; private set; }
    public int ActivePlantCount => plants.Count;
    public int ActiveMaterialCount => materials.Count;
    public int LastPulseAffected { get; private set; }
    public float VisualPhase { get; private set; }
    public float CooldownRemaining => Mathf.Max(0, cooldowns[(int)SelectedSeed] - Time.time);
    public int ChargesRemaining => charges[(int)SelectedSeed];
    public float ManaCost => Costs[(int)SelectedSeed];
    public int ChargesFor(int index) => index>=0&&index<5?charges[index]:0;
    public float CooldownFor(int index) => index>=0&&index<5?Mathf.Max(0,cooldowns[index]-Time.time):0;
    public bool ReadyFor(int index) => index>=0&&index<5&&charges[index]>0&&CooldownFor(index)<=0&&Mana>=Costs[index];
    public string LastFailure { get; private set; } = string.Empty;
    public bool IsPlantingContext => campaign != null && !campaign.IsDead && !campaign.EquipmentBusy &&
        (campaign.CurrentChapter == ThanhGiongCampaignController.Chapter.Prologue || campaign.IsBattleActive);
    public static readonly string[] Names = { "QUANG CĂN (LUMEN)", "MẦM TRE", "SEN HỒI PHỤC", "HẠT LỬA", "HẠT GIÓ" };
    static readonly float[] Costs = { 20, 30, 25, 35, 20 }, Cooldowns = { 8, 9, 11, 10, 8 };
    static readonly Color[] Colors = { new Color(1,.83f,.34f), new Color(.48f,.74f,.27f), new Color(.86f,.59f,.78f), new Color(1,.34f,.13f), new Color(.50f,.84f,.93f) };
    readonly float[] cooldowns = new float[5]; readonly int[] charges = new int[5];
    readonly RaycastHit[] groundHits = new RaycastHit[32]; readonly Collider[] overlaps = new Collider[64];
    readonly List<Plant> plants = new List<Plant>(); readonly HashSet<ThanhGiongEnemy> pulseHits = new HashSet<ThanhGiongEnemy>();
    ThanhGiongCampaignController campaign; MountedHorseController movement; ThanhGiongCampaignAudio audioFx;
    GameObject root; readonly List<Material> materials = new List<Material>();
    sealed class Plant { public Seed seed; public Vector3 position; public GameObject visual; public Material material; public LineRenderer ring; public float created, expires, nextPulse, pulseAt; public bool bloomed; public readonly HashSet<ThanhGiongEnemy> rooted = new HashSet<ThanhGiongEnemy>(); }
    [Serializable] public sealed class State { public int selection; public float mana; public int planted; public int[] charges; public float[] cooldownRemaining; }

    void Awake() { campaign=GetComponent<ThanhGiongCampaignController>(); movement=GetComponent<MountedHorseController>(); audioFx=GetComponent<ThanhGiongCampaignAudio>(); Mana=maxMana; for(int i=0;i<5;i++) charges[i]=chargesPerSeed; }
    void Update()
    {
        if (Time.timeScale<=0) return;
        VisualPhase += Time.deltaTime;
        if (campaign != null && !campaign.IsDead) Mana=Mathf.Min(maxMana,Mana+manaPerSecond*Time.deltaTime);
        if (IsPlantingContext) {
            for(int i=0;i<5;i++) if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))) SelectSeed(i);
            if(Input.GetKeyDown(KeyCode.Q)) TryPlantAt(SelectedSeed==Seed.Lotus?transform.position:transform.position+transform.forward*3f);
        }
        for(int i=plants.Count-1;i>=0;i--) {
            Plant p=plants[i];
            if(Time.time>=p.expires) { Release(p); plants.RemoveAt(i); continue; }
            float age = Time.time - p.created;
            if (!p.bloomed && age >= .32f) { p.bloomed = true; audioFx?.PlaySeed((int)p.seed,true); }
            if(p.visual) {
                float grow = Mathf.Clamp01(age / .42f);
                float eased = 1 - Mathf.Pow(1 - grow,3);
                float fade = Mathf.Clamp01((p.expires - Time.time) / .6f);
                float pulse = Mathf.Exp(-Mathf.Max(0,Time.time-p.pulseAt)*9);
                p.visual.transform.localScale=Vector3.one*(eased*fade*(1+Mathf.Sin(VisualPhase*3f)*.025f+pulse*.06f));
                if (p.seed == Seed.Wind) p.visual.transform.localRotation=Quaternion.Euler(0,VisualPhase*35,0);
                if (p.ring != null) p.ring.widthMultiplier=.04f+pulse*.035f;
            }
            if(Time.time>=p.nextPulse) { p.nextPulse=Time.time+.5f; Pulse(p); }
        }
    }
    public void SelectSeed(int index) { if(index>=0&&index<5) SelectedSeed=(Seed)index; }
    public bool TryPlantAt(Vector3 position)
    {
        int seed=(int)SelectedSeed;
        if(!IsPlantingContext||Time.timeScale<=0||movement!=null&&movement.IsDodging) return Fail("Chưa thể gieo hạt lúc này.");
        if(CooldownRemaining>0) return Fail("Hạt đang hồi phục.");
        if(charges[seed]<=0) return Fail("Đã dùng hết hạt này tại điểm dừng chân.");
        if(Mana<Costs[seed]) return Fail("Không đủ linh lực.");
        Vector3 delta=position-transform.position;delta.y=0;
        if(delta.sqrMagnitude>plantRange*plantRange) return Fail("Vị trí quá xa.");
        if(!TryGround(position,out Vector3 grounded)) return Fail("Cần đất bằng để gieo hạt.");
        if(Mathf.Abs(grounded.y-transform.position.y)>1.8f||!ClearPlacementLine(grounded))return Fail("Vị trí bị che khuất hoặc quá cao.");
        if(plants.Count>=12) return Fail("Đang có quá nhiều hạt đang nở.");
        Mana-=Costs[seed]; charges[seed]--; cooldowns[seed]=Time.time+Cooldowns[seed]; PlantedCount++;
        float duration = SelectedSeed switch { Seed.Lumen => 6f, Seed.Bamboo => 7f, Seed.Lotus => 6f, Seed.Ember => 5f, _ => 3f };
        Plant p=new Plant {seed=SelectedSeed,position=grounded,created=Time.time,expires=Time.time+duration,nextPulse=Time.time,pulseAt=Time.time};
        p.visual=BuildVisual(p); plants.Add(p); audioFx?.PlaySeed(seed);
        if (SelectedSeed == Seed.Lotus && (transform.position-grounded).sqrMagnitude < 3.3f*3.3f)
        {
            campaign.HealPlayer(18f);
            campaign.ApplyLotusWard(3.5f);
        }
        LastFailure=string.Empty; return true;
    }
    bool Fail(string message) { LastFailure=message; campaign?.ShowMessage(message,1.6f); return false; }
    bool ClearPlacementLine(Vector3 grounded)
    {
        Vector3 from=transform.position+Vector3.up*.8f,to=grounded+Vector3.up*.3f,direction=to-from;
        int count=Physics.RaycastNonAlloc(from,direction.normalized,groundHits,direction.magnitude,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++) {
            var hit=groundHits[i];
            if(hit.transform.IsChildOf(transform)||hit.transform.GetComponentInParent<ThanhGiongEnemy>()!=null||hit.collider.attachedRigidbody!=null&&!hit.collider.attachedRigidbody.isKinematic)continue;
            if(hit.distance<direction.magnitude-.15f)return false;
        }
        return true;
    }
    public bool TryGround(Vector3 requested,out Vector3 supported)
    {
        supported=requested;
        int count=Physics.RaycastNonAlloc(requested+Vector3.up*3f,Vector3.down,groundHits,7f,~0,QueryTriggerInteraction.Ignore);
        float nearest=float.MaxValue;bool found=false;
        for(int i=0;i<count;i++) { var h=groundHits[i]; if(h.transform.IsChildOf(transform)||h.transform.GetComponentInParent<ThanhGiongEnemy>()!=null||h.normal.y<.65f||h.distance>=nearest)continue; nearest=h.distance;supported=h.point+Vector3.up*.06f;found=true; }
        return found;
    }
    void Pulse(Plant p)
    {
        LastPulseAffected=0;
        if(campaign==null||campaign.IsDead) return;
        float radius=RadiusFor(p.seed);
        if(p.seed==Seed.Lotus) { if((transform.position-p.position).sqrMagnitude<radius*radius) { campaign.HealPlayer(3f); campaign.ApplyLotusWard(1.1f); LastPulseAffected=1; p.pulseAt=Time.time; } return; }
        if(!campaign.IsBattleActive) return;
        int count=Physics.OverlapSphereNonAlloc(p.position+Vector3.up*.6f,radius,overlaps,~0,QueryTriggerInteraction.Ignore);pulseHits.Clear();
        for(int i=0;i<count;i++) {
            var enemy=overlaps[i].GetComponentInParent<ThanhGiongEnemy>();
            if(enemy==null||!enemy.gameObject.activeInHierarchy||enemy.HealthRatio<=0||!pulseHits.Add(enemy))continue;
            bool affected=false;
            if(p.seed==Seed.Lumen) { if(p.rooted.Add(enemy)) { enemy.ApplyRoot(enemy.isBoss?.85f:2.6f); enemy.ApplyExposure(4f,enemy.isBoss?1.12f:1.25f); affected=true; var feedback = GetComponent<GodotCombatFeedback>(); if (feedback != null) feedback.PlayImpact(enemy.transform, false); } }
            else if(p.seed==Seed.Bamboo) { enemy.TakeDamage(5f,.05f,p.position); enemy.ApplySlow(.75f,enemy.isBoss?.8f:.55f); affected=true; }
            else if(p.seed==Seed.Ember) { enemy.TakeDamage(8f,0f,p.position); enemy.ApplyExposure(2.8f,enemy.isBoss?1.1f:1.18f); affected=true; }
            else if(p.seed==Seed.Wind && p.rooted.Add(enemy)) { enemy.TakeDamage(10f,.15f,p.position); enemy.ApplyWindGust(p.position,enemy.isBoss?3f:9f,enemy.isBoss?.5f:1.25f); affected=true; }
            if(affected)LastPulseAffected++;
        }
        if(LastPulseAffected>0)p.pulseAt=Time.time;
    }
    static float RadiusFor(Seed seed) => seed==Seed.Wind?3.5f:seed==Seed.Lotus?3.3f:2.6f;
    GameObject BuildVisual(Plant p)
    {
        if(root==null)root=new GameObject("Legend Seed Effects");
        var visual=new GameObject(Names[(int)p.seed]);visual.transform.SetParent(root.transform,false);visual.transform.position=p.position;
        Shader shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)shader=Shader.Find("Sprites/Default");
        Material material=new Material(shader);material.SetColor("_BaseColor",Colors[(int)p.seed]);material.SetColor("_Color",Colors[(int)p.seed]);materials.Add(material);p.material=material;
        var ring=visual.AddComponent<LineRenderer>();ring.sharedMaterial=material;ring.useWorldSpace=false;ring.loop=true;ring.positionCount=32;ring.widthMultiplier=.06f;ring.shadowCastingMode=ShadowCastingMode.Off;
        p.ring=ring;
        float visualRadius=RadiusFor(p.seed);
        for(int i=0;i<32;i++){float a=i*Mathf.PI*2/32;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*visualRadius,.02f,Mathf.Sin(a)*visualRadius));}
        int petals=p.seed==Seed.Bamboo?5:6;
        for(int i=0;i<petals;i++) {
            var piece=GameObject.CreatePrimitive(p.seed==Seed.Bamboo?PrimitiveType.Cylinder:PrimitiveType.Sphere);piece.transform.SetParent(visual.transform,false);Collider collider=piece.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            float a=i*Mathf.PI*2/petals;
            piece.transform.localPosition=new Vector3(Mathf.Cos(a)*.45f,p.seed==Seed.Bamboo?.5f:p.seed==Seed.Ember?.2f+i*.12f:.12f,Mathf.Sin(a)*.45f);
            piece.transform.localScale=p.seed==Seed.Bamboo?new Vector3(.08f,.5f,.08f):p.seed==Seed.Lotus?new Vector3(.38f,.10f,.22f):new Vector3(.22f,.14f,.22f);
            piece.transform.localRotation=Quaternion.Euler(p.seed==Seed.Lotus?-20:0,-a*Mathf.Rad2Deg,0);
            var renderer=piece.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        return visual;
    }
    void Release(Plant p) { if(p.visual)Destroy(p.visual);if(p.material){materials.Remove(p.material);Destroy(p.material);} }
    public State CaptureState()
    {
        State state=new State { selection=(int)SelectedSeed,mana=Mana,planted=PlantedCount,charges=(int[])charges.Clone(),cooldownRemaining=new float[5] };
        for(int i=0;i<5;i++)state.cooldownRemaining[i]=Mathf.Max(0,cooldowns[i]-Time.time);return state;
    }
    public void RestoreState(State state)
    {
        ClearPlants();if(state==null)return;SelectSeed(state.selection);Mana=Mathf.Clamp(state.mana,0,maxMana);PlantedCount=state.planted;
        for(int i=0;i<5;i++){charges[i]=state.charges!=null&&i<state.charges.Length?state.charges[i]:chargesPerSeed;cooldowns[i]=Time.time+(state.cooldownRemaining!=null&&i<state.cooldownRemaining.Length?state.cooldownRemaining[i]:0);}
    }
    public void ClearPlants() { foreach(Plant p in plants)Release(p);plants.Clear();materials.Clear();LastPulseAffected=0; }
    void OnDestroy(){ClearPlants();if(root)Destroy(root);}
}
