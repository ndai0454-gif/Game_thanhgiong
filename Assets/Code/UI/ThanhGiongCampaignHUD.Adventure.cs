using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// The campaign's actual state drives every gauge; no invented stamina, inventory or weather data.
public partial class ThanhGiongCampaignHUD
{
    [Header("Adventure HUD")]
    public bool showAdventureMinimap = true;
    [Range(15,100)] public float minimapRange = 40f;
    public bool AimReticleVisible => adventureAiming && !paused && !guidesVisible;
    public ThanhGiongEnemy AimTarget => adventureAimTarget;
    public int AdventureDamageLabelCount => damageLabels.Count;
    public int MinimapEnemyCount => mapEnemies.Length;
    public int MinimapResourceCount => mapResources.Length;

    bool adventureAiming;
    ThanhGiongEnemy adventureAimTarget;
    Camera adventureView, mapCamera;
    AdventureStamina adventureStamina;
    GameObject mapCameraHost;
    RenderTexture mapRender, mapCircle;
    Material mapMask;
    Texture2D riceGrain, mapDisc;
    GUIStyle adventureNumber, adventureWorld, adventureCentered;
    float nextAdventureScan, contextualUntil, observedMana = -1, observedHeat = -1;
    int observedSeed = -1;
    ThanhGiongEnemy[] mapEnemies = new ThanhGiongEnemy[0];
    ThanhGiongCollectible[] mapResources = new ThanhGiongCollectible[0];
    ThanhGiongImprovisedWeapon[] mapThrowables = new ThanhGiongImprovisedWeapon[0];
    readonly List<Transform> mapExits = new List<Transform>();
    readonly Dictionary<ThanhGiongEnemy,float> observedEnemyHealth = new Dictionary<ThanhGiongEnemy,float>();
    readonly List<DamageLabel> damageLabels = new List<DamageLabel>();
    readonly RaycastHit[] labelSightHits = new RaycastHit[16];
    struct DamageLabel { public Vector3 point; public float amount, started; public bool critical; }

    // Called by the aiming component. HUD remains usable in legacy camera scenes as well.
    public void SetAdventureAim(bool aiming, ThanhGiongEnemy target)
    {
        adventureAiming = aiming;
        adventureAimTarget = target;
    }

    void UpdateAdventureHUD()
    {
        if (adventureView == null) adventureView = Camera.main;
        if (adventureStamina == null) adventureStamina = campaign.GetComponent<AdventureStamina>();
        var shoulder=IsometricCameraFollow.Instance;
        if(shoulder!=null)SetAdventureAim(shoulder.IsAiming,shoulder.SoftLockTarget);
        if (Time.time >= nextAdventureScan)
        {
            nextAdventureScan = Time.time + 1.5f;
            mapEnemies = FindObjectsByType<ThanhGiongEnemy>(FindObjectsSortMode.None);
            mapResources = FindObjectsByType<ThanhGiongCollectible>(FindObjectsSortMode.None);
            mapThrowables = FindObjectsByType<ThanhGiongImprovisedWeapon>(FindObjectsSortMode.None);
            // Old scene gates can come from packages: inspect their real component rather than
            // fabricating a destination. Only named exit roots or SceneGate behaviours qualify.
            mapExits.Clear();
            foreach (Transform node in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string name = node.name.ToLowerInvariant();
                if (name.Contains("zoneexit") || name.Contains("zone exit") || name.Contains("scene gate") || name.Contains("sceneexit"))
                    mapExits.Add(node);
            }
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (behaviour != null && behaviour.GetType().Name.Contains("SceneGate") && !mapExits.Contains(behaviour.transform))
                    mapExits.Add(behaviour.transform);
            var expired = new List<ThanhGiongEnemy>();
            foreach (var pair in observedEnemyHealth)
                if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy) expired.Add(pair.Key);
            foreach (ThanhGiongEnemy enemy in expired) observedEnemyHealth.Remove(enemy);
        }
        foreach (ThanhGiongEnemy enemy in mapEnemies)
        {
            if (enemy == null) continue;
            float hp = enemy.HealthRatio * enemy.maxHealth;
            if (observedEnemyHealth.TryGetValue(enemy,out float previous) && previous-hp > .25f)
            {
                damageLabels.Add(new DamageLabel { point=EnemyLabelPoint(enemy), amount=previous-hp, started=Time.time, critical=enemy.IsStuckInGround });
                if (damageLabels.Count > 24) damageLabels.RemoveAt(0);
                contextualUntil = Time.time + 2.5f;
            }
            observedEnemyHealth[enemy] = hp;
        }
        damageLabels.RemoveAll(label => Time.time-label.started>1.15f);
        if (seeds != null)
        {
            if (observedMana >= 0 && (seeds.Mana < observedMana-.01f || observedSeed != (int)seeds.SelectedSeed)) contextualUntil=Time.time+3;
            observedMana=seeds.Mana; observedSeed=(int)seeds.SelectedSeed;
        }
        if (observedHeat >= 0 && Mathf.Abs(campaign.Heat-observedHeat)>.01f) contextualUntil=Time.time+3;
        observedHeat=campaign.Heat;
        if (showAdventureMinimap) UpdateMinimapCamera();
        else if (mapCamera != null) mapCamera.enabled=false;
    }

    void EnsureAdventureStyles()
    {
        if (adventureNumber != null && riceGrain != null && mapDisc != null) return;
        adventureNumber=Style(18,Paper,true); adventureNumber.alignment=TextAnchor.MiddleCenter;
        adventureWorld=Style(13,Paper,true); adventureWorld.alignment=TextAnchor.MiddleCenter;
        adventureCentered=Style(11,Paper); adventureCentered.alignment=TextAnchor.MiddleCenter;
        if(riceGrain==null)riceGrain=CreateHudTexture(24,(x,y) =>
        {
            float rx=(x-11.5f)/7.1f, ry=(y-11.5f)/10.1f;
            float edge=Mathf.Clamp01((1-rx*rx-ry*ry)*5);
            return new Color(1,1,1,edge);
        });
        if(mapDisc==null)mapDisc=CreateHudTexture(128,(x,y) => new Color(1,1,1,Mathf.Clamp01((63.3f-Vector2.Distance(new Vector2(x,y),new Vector2(63.5f,63.5f)))*2)));
    }

    static Texture2D CreateHudTexture(int size,System.Func<int,int,Color> sample)
    {
        var result=new Texture2D(size,size,TextureFormat.RGBA32,false) { name="Adventure HUD procedural icon",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear };
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)pixels[y*size+x]=sample(x,y);
        result.SetPixels(pixels);result.Apply(false,true);return result;
    }

    void DrawAdventureHUD(float w,float h,Rect safe,float scale)
    {
        EnsureAdventureStyles();
        Color previous=GUI.color;
        GUI.color=new Color(.105f,.055f,.045f,.78f);
        // text-only panel: no background box
        GUI.color=previous;
        DrawRiceVitals();
        DrawAdventureQuickSlots();
        if(!RegionIntroVisible) DrawContextualEnergy(w,h,safe,scale);
        if(!ChapterRitualVisible) DrawAdventureWorldLabels(w,h,safe,scale);
        if(showAdventureMinimap)DrawAdventureMinimap(w,h);
    }

    void DrawRiceVitals()
    {
        Color saved = GUI.color;
        bool hasWard = campaign != null && campaign.LotusWardRemaining > 0;
        float h01 = campaign != null ? campaign.Health01 : 1f;
        float curHp = campaign != null ? campaign.Health : 100f;
        float maxHp = campaign != null ? campaign.maxHealth : 100f;

        // Vitals frame (Obsidian & Antique Gold panel)
        bool hasStamina = adventureStamina != null;
        bool hasHeat = campaign != null && campaign.Heat01 > 0.01f;
        float panelHeight = 62f + (hasStamina ? 8f : 0f) + (hasHeat ? 7f : 0f) + (hasWard ? 18f : 0f);
        Rect vitalsRect = new Rect(18, 12, 332, panelHeight);
        DrawMysticPanel(vitalsRect, ObsidianSoft, new Color(MutedBronze.r, MutedBronze.g, MutedBronze.b, 0.40f), true);

        // Header: "❖ THÁNH GIÓNG ❖"
        GUI.color = Gold;
        ShadowLabel(new Rect(26, 16, 216, 20), "❖  THÁNH GIÓNG  ❖", hudTitle);

        // HP numbers on the right
        GUI.color = AncientPaper;
        ShadowLabel(new Rect(242, 17, 100, 18), $"{Mathf.CeilToInt(curHp)} / {Mathf.CeilToInt(maxHp)}", small);

        // Health bar: Cinnabar red with gold trims
        float currentY = 38f;
        DrawModernHealthBar(new Rect(26, currentY, 216, 12), h01, ref heroGhostHealth01, ref heroLastDamageTime, ref previousHeroHealth01, false, 4, hasWard);
        currentY += 15f;

        // Jade Stamina Bar (Thể lực)
        if (hasStamina)
        {
            float stamRatio = adventureStamina.Ratio;
            ThinBar(new Rect(26, currentY, 216, 4), stamRatio, JadeStamina);
            ShadowLabel(new Rect(244, currentY - 5, 95, 14), $"KHÍ {Mathf.RoundToInt(stamRatio * 100)}%", small);
            currentY += 8f;
        }

        // Ember Flame Bar (Hỏa khí)
        if (hasHeat)
        {
            float heatRatio = campaign.Heat01;
            Color heatColor = heatRatio >= 0.99f ? Color.Lerp(EmberFlame, Gold, Mathf.PingPong(Time.time * 4f, 1f)) : EmberFlame;
            ThinBar(new Rect(26, currentY, 216, 3), heatRatio, heatColor);
            string heatLabel = heatRatio >= 0.99f ? "HỎA [F]!" : $"HỎA {Mathf.RoundToInt(heatRatio * 100)}%";
            ShadowLabel(new Rect(244, currentY - 6, 95, 14), heatLabel, small);
            currentY += 7f;
        }

        if (hasWard)
        {
            GUI.color = Color.Lerp(Gold, new Color(0.7f, 1f, 0.85f), Mathf.PingPong(Time.time * 2.5f, 1f));
            ShadowLabel(new Rect(26, currentY, 310, 18), $"★ SEN HỘ THỂ  {campaign.LotusWardRemaining:0.0}s  ·  -32% SÁT THƯƠNG", small);
        }
        GUI.color = saved;
    }

    void DrawAdventureQuickSlots()
    {
        Color saved = GUI.color;
        bool hasWard = campaign != null && campaign.LotusWardRemaining > 0;
        bool hasStamina = adventureStamina != null;
        bool hasHeat = campaign != null && campaign.Heat01 > 0.01f;
        float startY = 12f + 62f + (hasStamina ? 8f : 0f) + (hasHeat ? 7f : 0f) + (hasWard ? 18f : 0f) + 6f;

        // Weapon badge with obsidian stone panel
        string weapon = campaign.CarriedWeapon != null ? (campaign.CarriedWeapon.type == ThanhGiongImprovisedWeapon.WeaponType.Boulder ? "ĐÁ NÉM" : "GỐC CÂY")
            : campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.IronSword ? "THƯƠNG SẮT"
            : campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo ? campaign.BambooVariantName.ToUpperInvariant() : "TAY KHÔNG";
        string action = campaign.CarriedWeapon != null ? "CHUỘT: NÉM" : "CHUỘT: ĐÁNH";

        Rect weaponRect = new Rect(18, startY, 332, 22);
        DrawMysticPanel(weaponRect, ObsidianSoft, new Color(MutedBronze.r, MutedBronze.g, MutedBronze.b, 0.35f), false);
        ShadowLabel(new Rect(24, startY + 2, 320, 18), $"❖  {weapon}  ·  {action}", small);

        float itemsY = startY + 26f;
        if (seeds != null && seeds.IsPlantingContext)
        {
            string[] names = { "SÁNG", "TRE", "SEN", "LỬA", "GIÓ" };
            Color[] seedColors = {
                new Color(1f, 0.95f, 0.75f),
                new Color(0.45f, 0.82f, 0.45f),
                new Color(0.4f, 0.85f, 0.85f),
                new Color(1f, 0.45f, 0.25f),
                new Color(0.65f, 0.75f, 1f)
            };

            for (int i = 0; i < 5; i++)
            {
                bool selected = (int)seeds.SelectedSeed == i;
                Rect r = new Rect(18 + i * 50, itemsY, 46, 44);
                Color border = selected ? Gold : new Color(MutedBronze.r, MutedBronze.g, MutedBronze.b, 0.4f);
                Color bg = selected ? Obsidian : ObsidianSoft;
                DrawMysticPanel(r, bg, border, selected);

                // Element top accent mark
                GUI.color = seedColors[i];
                GUI.DrawTexture(new Rect(r.x + 3, r.y + 2, r.width - 6, 2), Texture2D.whiteTexture);

                // Slot index & Name
                GUI.color = selected ? Gold : Paper;
                ShadowLabel(new Rect(r.x + 1, r.y + 4, r.width - 2, 20), $"{i + 1} {names[i]}", tile);

                // Cooldown or Charges
                float cd = seeds.CooldownFor(i);
                if (cd > 0)
                {
                    GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.65f);
                    GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), Texture2D.whiteTexture);
                    GUI.color = GhostAmber;
                    ShadowLabel(new Rect(r.x + 1, r.y + 22, r.width - 2, 19), $"{cd:0}s", tile);
                }
                else
                {
                    GUI.color = selected ? AntiqueGold : Muted;
                    ShadowLabel(new Rect(r.x + 1, r.y + 22, r.width - 2, 19), $"×{seeds.ChargesFor(i)}", tile);
                }
            }

            // Mana / Linh lực indicator
            Rect manaRect = new Rect(272, itemsY, 150, 44);
            DrawMysticPanel(manaRect, ObsidianSoft, new Color(MutedBronze.r, MutedBronze.g, MutedBronze.b, 0.35f), false);
            GUI.color = saved;
            ShadowLabel(new Rect(276, itemsY + 4, 142, 38), $"[Q] Gieo  ·  {seeds.Mana:0}/{seeds.MaxMana:0} Linh\n{seeds.SelectedName}", small);
        }
        else
        {
            string status = campaign.CurrentChapter == ThanhGiongCampaignController.Chapter.Prologue
                ? $"Lương thực {campaign.Food:0}/{campaign.foodPerGrowth * 3:0}  ·  Lớn {campaign.GrowthPhase + 1}/4"
                : campaign.IsBattleActive
                    ? $"Hạ giặc {campaign.Kills}/{campaign.RequiredBattleKills}"
                    : Sequence();
            Rect statusRect = new Rect(18, itemsY, 332, 24);
            DrawMysticPanel(statusRect, ObsidianSoft, new Color(MutedBronze.r, MutedBronze.g, MutedBronze.b, 0.35f), false);
            ShadowLabel(new Rect(24, itemsY + 3, 320, 18), $"❖  {status}", small);
        }
        GUI.color = saved;
    }

    void DrawContextualEnergy(float w,float h,Rect safe,float scale)
    {
        if(campaign.IsDead || adventureView==null)return;
        bool heat=campaign.IsBattleActive&&(campaign.Heat01>=.99f||campaign.AttackPending||Time.time<contextualUntil);
        bool mana=seeds!=null&&seeds.IsPlantingContext&&(Time.time<contextualUntil||seeds.CooldownRemaining>0);
        bool stamina=adventureStamina!=null&&(adventureStamina.IsConsuming||adventureStamina.Ratio<.999f);
        bool bambooCharge=campaign.IsBattleActive&&campaign.CurrentWeapon==ThanhGiongCampaignController.Weapon.None&&campaign.BambooPullStep>0;
        if(!heat&&!mana&&!stamina&&!bambooCharge)return;
        // Anchor beside the actor, with safe-area clamping when the shoulder approaches an edge.
        Vector3 point=campaign.transform.position+Vector3.up*2.9f;
        CharacterController capsule=campaign.GetComponent<CharacterController>();
        if(capsule!=null)point.y=Mathf.Lerp(capsule.bounds.center.y,capsule.bounds.max.y,.7f);
        if(IsometricCameraFollow.Instance!=null&&IsometricCameraFollow.Instance.useShoulderCamera) point=IsometricCameraFollow.Instance.BodyFocus;
        if(!WorldToHud(point,safe,scale,out Vector2 anchor))return;
        float x=Mathf.Clamp(anchor.x+42,320,w-210),y=Mathf.Clamp(anchor.y-22,170,h-180);
        if(stamina)
        {
            DrawEnergyCells(x,y,adventureStamina.Ratio,JadeStamina,$"THỂ LỰC  {adventureStamina.Current:0}/{adventureStamina.capacity:0}",12);
            y+=40;
        }
        if(bambooCharge)
        {
            DrawEnergyCells(x,y,campaign.BambooPull01,new Color(.52f,.63f,.45f),$"GỒNG NHỔ TRE  {campaign.BambooPullStep}/3");
            y+=40;
        }
        if(heat)
        {
            DrawEnergyCells(x,y,campaign.Heat01,EmberFlame,campaign.Heat01>=.99f?"HỎA KHÍ ĐẦY · F":"HỎA KHÍ  "+Mathf.RoundToInt(campaign.Heat01*100)+"%");
            y+=40;
        }
        if(mana)DrawEnergyCells(x,y,seeds.Mana/Mathf.Max(1,seeds.MaxMana),new Color(.53f,.65f,.64f),$"LINH LỰC  {seeds.Mana:0}/{seeds.MaxMana:0}");
    }

    void DrawEnergyCells(float x,float y,float value,Color color,string label,int cellCount=8)
    {
        Color saved=GUI.color;
        Rect bgRect = new Rect(x - 4, y - 3, cellCount * 13 + 8, 28);
        DrawMysticPanel(bgRect, ObsidianSoft, new Color(MutedBronze.r, MutedBronze.g, MutedBronze.b, 0.35f), false);

        for(int i=0;i<cellCount;i++)
        {
            Rect cell=new Rect(x+i*13,y,10,8);
            GUI.color=new Color(.03f,.02f,.02f,.85f);
            GUI.DrawTexture(new Rect(cell.x-1, cell.y-1, cell.width+2, cell.height+2), Texture2D.whiteTexture);
            GUI.color=new Color(.08f,.06f,.05f,.90f);
            GUI.DrawTexture(cell,Texture2D.whiteTexture);
            GUI.color=color;
            float fill=Mathf.Clamp01(value*cellCount-i);
            GUI.DrawTexture(new Rect(cell.x,cell.y,cell.width*fill,cell.height),Texture2D.whiteTexture);
        }
        GUI.color=saved;
        ShadowLabel(new Rect(x,y+11,Mathf.Max(180,cellCount*13+20),24),label,small);
    }

    bool WorldToHud(Vector3 point,Rect safe,float scale,out Vector2 result)
    {
        result=Vector2.zero;if(adventureView==null)return false;
        Vector3 pixel=adventureView.WorldToScreenPoint(point);if(pixel.z<=0)return false;
        result=new Vector2((pixel.x-safe.x)/scale,(Screen.height-pixel.y-(Screen.height-safe.yMax))/scale);return true;
    }

    static Vector3 EnemyLabelPoint(ThanhGiongEnemy enemy)
    {
        Collider capsule=enemy.GetComponent<Collider>();
        return capsule!=null?new Vector3(capsule.bounds.center.x,capsule.bounds.max.y+.3f,capsule.bounds.center.z):enemy.transform.position+Vector3.up*(enemy.isBoss?5:2.2f);
    }

    bool LabelUnoccluded(ThanhGiongEnemy enemy,Vector3 point)
    {
        Vector3 origin=adventureView.transform.position,delta=point-origin;
        int count=Physics.RaycastNonAlloc(origin,delta.normalized,labelSightHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)
        {
            Transform hit=labelSightHits[i].transform;
            if(hit==null||hit.IsChildOf(enemy.transform)||hit.IsChildOf(campaign.transform))continue;
            if(labelSightHits[i].distance<delta.magnitude-.4f)return false;
        }
        return true;
    }

    void DrawAdventureWorldLabels(float w,float h,Rect safe,float scale)
    {
        if(adventureView==null||campaign.IsDead)return;
        Color saved=GUI.color;
        if(adventureAiming)
        {
            Vector2 center=new Vector2(w*.5f,h*.5f);
            GUI.color=new Color(Paper.r,Paper.g,Paper.b,.92f);
            for(int i=0;i<4;i++)
            {float a=(45+i*90)*Mathf.Deg2Rad;Vector2 ray=new Vector2(Mathf.Cos(a),Mathf.Sin(a));DrawHudLine(center+ray*6,center+ray*13,GUI.color,1.6f);}
        }
        foreach(ThanhGiongEnemy enemy in mapEnemies)
        {
            if(enemy==null||enemy.HealthRatio<=0||!enemy.gameObject.activeInHierarchy)continue;
            bool selected=adventureAiming&&enemy==adventureAimTarget;
            bool warning=enemy.CurrentState==ThanhGiongEnemy.EnemyState.TelegraphingAttack||enemy.IsStuckInGround;
            if(!selected&&!warning)continue;
            if(Vector3.Distance(enemy.transform.position,campaign.transform.position)>(selected?65:15))continue;
            Vector3 point=EnemyLabelPoint(enemy);
            if(!WorldToHud(point,safe,scale,out Vector2 pos)||pos.x<80||pos.x>w-80||pos.y<28||pos.y>h-30||!LabelUnoccluded(enemy,point))continue;
            GUI.color=selected?Gold:Paper;
            if(selected)DrawHudArc(pos+new Vector2(0,-10),18,1,Gold,1.5f);
            string text=enemy.IsStuckInGround?"SƠ HỞ · PHẢN CÔNG":warning?"NÉ ĐÒN!":enemy.enemyName;
            ShadowLabel(new Rect(pos.x-100,pos.y+8,200,22),text,adventureWorld);
            if(selected)ThinBar(new Rect(pos.x-37,pos.y+32,74,3),enemy.HealthRatio,Gold);
        }
        foreach(DamageLabel label in damageLabels)
        {
            float age=Time.time-label.started;
            if(!WorldToHud(label.point+Vector3.up*age*.85f,safe,scale,out Vector2 pos)||pos.x<0||pos.x>w||pos.y<0||pos.y>h)continue;
            GUI.color=new Color(1,1,1,Mathf.Clamp01((1.15f-age)/.35f));
            adventureNumber.normal.textColor=label.critical?Gold:Paper;
            ShadowLabel(new Rect(pos.x-45,pos.y-13,90,30),Mathf.CeilToInt(label.amount).ToString(),adventureNumber);
        }
        // Nearby resources provide context without inventing NPC dialogue.
        ThanhGiongCollectible closest=null;float nearest=4.5f*4.5f;
        foreach(ThanhGiongCollectible item in mapResources)
        {
            if(item==null||item.IsCollected||!item.gameObject.activeInHierarchy)continue;
            float d=(item.transform.position-campaign.transform.position).sqrMagnitude;
            if(d<nearest){nearest=d;closest=item;}
        }
        if(!RegionIntroVisible&&closest!=null&&WorldToHud(closest.transform.position+Vector3.up*1.3f,safe,scale,out Vector2 at)&&at.x>90&&at.x<w-90&&at.y>150&&at.y<h-100)
        {
            GUI.color=saved;
            Rect prompt=new Rect(Mathf.Clamp(at.x-230,20,w-240),Mathf.Clamp(at.y+45,160,h-90),220,35);
            GUI.color=new Color(.075f,.032f,.026f,.66f);GUI.color=saved;
            string resourcePrompt=closest.kind==ThanhGiongCollectible.Kind.Bamboo
                ?(campaign.IsBattleActive&&campaign.CurrentWeapon==ThanhGiongCampaignController.Weapon.None?"E · NHỔ TRE (3 NHỊP)":"TRE NGÀ")
                :closest.displayName;
            ShadowLabel(prompt,resourcePrompt,adventureWorld);
        }
        GUI.color=saved;
    }

    void UpdateMinimapCamera()
    {
        if(mapCamera==null)
        {
            Shader shader=Resources.Load<Shader>("ThanhGiongUI/AdventureMinimap");
            if(shader==null)return;
            mapRender=new RenderTexture(256,256,16,RenderTextureFormat.ARGB32){name="Adventure minimap terrain",hideFlags=HideFlags.DontSave};mapRender.Create();
            mapCircle=new RenderTexture(256,256,0,RenderTextureFormat.ARGB32){name="Adventure minimap circular mask",hideFlags=HideFlags.DontSave};mapCircle.Create();
            mapMask=new Material(shader){hideFlags=HideFlags.DontSave};
            mapCameraHost=new GameObject("Adventure Minimap Camera"){hideFlags=HideFlags.DontSave};
            mapCamera=mapCameraHost.AddComponent<Camera>();
            mapCamera.orthographic=true;mapCamera.depth=-30;mapCamera.nearClipPlane=.3f;mapCamera.farClipPlane=240;
            mapCamera.clearFlags=CameraClearFlags.SolidColor;mapCamera.backgroundColor=new Color(.055f,.14f,.12f);
            mapCamera.allowHDR=false;mapCamera.allowMSAA=false;mapCamera.targetTexture=mapRender;
            mapCamera.cullingMask=~(1<<5); // Screen/UI objects do not belong to the terrain map.
            var data=mapCameraHost.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=false;
        }
        mapCamera.enabled=!paused&&!guidesVisible;
        mapCamera.orthographicSize=Mathf.Max(15,minimapRange);
        mapCamera.transform.SetPositionAndRotation(campaign.transform.position+Vector3.up*100,Quaternion.Euler(90,0,0));
    }

    void DrawAdventureMinimap(float w,float h)
    {
        const float radius=88;
        Vector2 center=new Vector2(w-119,h-132);
        Color saved=GUI.color;
        GUI.color=new Color(.04f,.03f,.03f,.95f);
        GUI.DrawTexture(new Rect(center.x-radius-10,center.y-radius-10,(radius+10)*2,(radius+10)*2),mapDisc);

        GUI.color=Color.white;
        if(mapRender!=null&&mapCircle!=null&&mapMask!=null)
        {
            if(Event.current.type==EventType.Repaint) {
                // Blit changes the active render target. IMGUI must continue on the Game View backbuffer.
                RenderTexture previousTarget=RenderTexture.active;
                Graphics.Blit(mapRender,mapCircle,mapMask);
                RenderTexture.active=previousTarget;
            }
            GUI.DrawTexture(new Rect(center.x-radius,center.y-radius,radius*2,radius*2),mapCircle,ScaleMode.StretchToFill,true);
        }

        // Antique bronze outer compass rings (Dong Son / Bagua astrolabe style)
        DrawHudArc(center,radius+7,1,MutedBronze,1.5f);
        DrawHudArc(center,radius+4,1,new Color(AntiqueGold.r,AntiqueGold.g,AntiqueGold.b,.85f),1.2f);
        DrawHudArc(center,radius-1,1,new Color(MutedBronze.r,MutedBronze.g,MutedBronze.b,.45f),1f);

        // 24 Astrological / Bagua dial marks around compass
        for(int i=0;i<24;i++)
        {
            float a=i*Mathf.PI/12;
            Vector2 ray=new Vector2(Mathf.Sin(a),-Mathf.Cos(a));
            bool isCardinal = i%6==0; // 4 main directions (N, E, S, W)
            bool isSemi = i%3==0;     // 8 directions
            float tickLen = isCardinal ? 8f : (isSemi ? 5f : 3f);
            Color tickCol = isCardinal ? AntiqueGold : (isSemi ? MutedBronze : new Color(MutedBronze.r, MutedBronze.g, MutedBronze.b, 0.4f));
            DrawHudLine(center+ray*(radius+2),center+ray*(radius+2+tickLen),tickCol,isCardinal?2f:1.2f);
        }

        // Cardinal Direction Labels: B (Bắc / North) in antique gold, with diamond accents for E, S, W
        ShadowLabel(new Rect(center.x-10,center.y-radius-24,20,20),"B",adventureCentered); // Bắc / north.
        ShadowLabel(new Rect(center.x+radius+3,center.y-10,20,20),"Đ",adventureCentered);   // Đông / east.
        ShadowLabel(new Rect(center.x-10,center.y+radius+5,20,20),"N",adventureCentered);   // Nam / south.
        ShadowLabel(new Rect(center.x-radius-23,center.y-10,20,20),"T",adventureCentered);  // Tây / west.

        foreach(ThanhGiongCollectible item in mapResources)
            if(item!=null&&!item.IsCollected&&item.gameObject.activeInHierarchy)DrawMapPin(center,radius,item.transform.position,new Color(.51f,.85f,.44f),3,false);
        foreach(ThanhGiongImprovisedWeapon item in mapThrowables)
            if(item!=null&&!item.IsCarried&&item.gameObject.activeInHierarchy)DrawMapPin(center,radius,item.transform.position,new Color(.65f,.77f,.75f),3,false);
        foreach(ThanhGiongEnemy enemy in mapEnemies)
            if(enemy!=null&&enemy.HealthRatio>0&&enemy.gameObject.activeInHierarchy)DrawMapPin(center,radius,enemy.transform.position,enemy.isBoss?new Color(1,.35f,.18f):BossCrimson,enemy.isBoss?5.5f:2.5f,false);
        foreach(Transform exit in mapExits)
            if(exit!=null&&exit.gameObject.activeInHierarchy)DrawMapPin(center,radius,exit.position,new Color(.4f,.85f,1),5,true);
        if(campaign.openWorldCampaign!=null){
            foreach(var node in ThanhGiongResourceNode.ActiveNodes)
                if(node!=null&&node.IsReady)DrawMapPin(center,radius,node.transform.position,new Color(.98f,.81f,.35f),3,false);
            foreach(var forge in ThanhGiongForgeStation.ActiveStations)
                if(forge!=null)DrawMapPin(center,radius,forge.transform.position,new Color(1,.5f,.16f),5,true);
        }
        if(checkpoint!=null&&checkpoint.HasCheckpoint)DrawMapPin(center,radius,checkpoint.CheckpointPosition,AntiqueGold,4,false);

        // Player orientation arrowhead in antique gold / bronze
        float angle=-campaign.transform.eulerAngles.y*Mathf.Deg2Rad;
        Vector2 ahead=new Vector2(-Mathf.Sin(angle),-Mathf.Cos(angle)),side=new Vector2(-ahead.y,ahead.x);
        DrawHudLine(center+ahead*9,center-ahead*4+side*4,AntiqueGold,2f);
        DrawHudLine(center+ahead*9,center-ahead*4-side*4,AntiqueGold,2f);
        DrawHudLine(center-ahead*4-side*4,center-ahead*4+side*4,MutedBronze,1.5f);

        GUI.color=saved;
        Vector3 position=campaign.transform.position;
        // Position info panel
        Rect coordRect = new Rect(center.x-105,center.y+radius+25,210,38);
        DrawMysticPanel(coordRect, ObsidianSoft, new Color(MutedBronze.r, MutedBronze.g, MutedBronze.b, 0.35f), false);
        ShadowLabel(new Rect(center.x-100,center.y+radius+27,200,16),$"{position.x:0}, {position.z:0} · Cao {position.y:0}m",adventureCentered);
        ShadowLabel(new Rect(center.x-105,center.y+radius+43,210,16),campaign.openWorldCampaign!=null?"Vàng: quặng · Cam: rèn":"Đỏ: giặc · Lục: ngọc · Lam: cổng",adventureCentered);
    }

    void DrawMapPin(Vector2 center,float radius,Vector3 world,Color color,float size,bool clampEdge)
    {
        Vector3 delta=world-campaign.transform.position;
        Vector2 offset=new Vector2(delta.x,-delta.z)*(radius/Mathf.Max(15,minimapRange));
        if(offset.magnitude>radius-8){if(!clampEdge)return;offset=offset.normalized*(radius-8);}
        Vector2 point=center+offset;Color saved=GUI.color;
        GUI.color=new Color(.06f,.025f,.02f,.85f);GUI.DrawTexture(new Rect(point.x-size-1,point.y-size-1,size*2+2,size*2+2),mapDisc);
        GUI.color=color;GUI.DrawTexture(new Rect(point.x-size,point.y-size,size*2,size*2),mapDisc);GUI.color=saved;
    }

    static void DrawHudLine(Vector2 start,Vector2 end,Color color,float thickness)
    {
        Matrix4x4 matrix=GUI.matrix;Color old=GUI.color;
        Vector2 delta=end-start;
        // Compose in logical HUD space; RotateAroundPivot applies the scaled pivot twice.
        GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(start.x,start.y,0),Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg),Vector3.one);
        GUI.color=color;GUI.DrawTexture(new Rect(0,-thickness*.5f,delta.magnitude,thickness),Texture2D.whiteTexture);
        GUI.matrix=matrix;GUI.color=old;
    }

    static void DrawHudArc(Vector2 center,float radius,float fill,Color color,float thickness)
    {
        int segments=Mathf.CeilToInt(Mathf.Clamp01(fill)*48);
        for(int i=0;i<segments;i++)
        {
            float a=(i/48f*360-90)*Mathf.Deg2Rad,b=(Mathf.Min((i+1)/48f,fill)*360-90)*Mathf.Deg2Rad;
            DrawHudLine(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,color,thickness);
        }
    }

    void ReleaseAdventureHUD()
    {
        if(mapCamera!=null)mapCamera.targetTexture=null;
        if(mapCameraHost!=null)Destroy(mapCameraHost);
        if(mapRender!=null){mapRender.Release();Destroy(mapRender);}
        if(mapCircle!=null){mapCircle.Release();Destroy(mapCircle);}
        if(mapMask!=null)Destroy(mapMask);
        if(riceGrain!=null)Destroy(riceGrain);
        if(mapDisc!=null)Destroy(mapDisc);
        mapCamera=null;mapCameraHost=null;mapRender=null;mapCircle=null;mapMask=null;riceGrain=null;mapDisc=null;
        adventureNumber=null;adventureWorld=null;adventureCentered=null;
        observedEnemyHealth.Clear();damageLabels.Clear();nextAdventureScan=0;
    }
}
