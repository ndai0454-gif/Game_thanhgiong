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

    public enum NavTargetMode { Auto, Exit, Combat, Resource }
    NavTargetMode navMode = NavTargetMode.Auto;
    Vector3 currentNavTarget;
    string currentNavLabel = string.Empty;
    Color currentNavColor = Color.white;
    bool hasNavTarget;
    float navDistance;
    static readonly float[] MinimapZoomLevels = { 30f, 55f, 95f };
    int currentZoomIndex = 1;

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
        if (Input.GetKeyDown(KeyCode.Z))
        {
            currentZoomIndex = (currentZoomIndex + 1) % MinimapZoomLevels.Length;
            minimapRange = MinimapZoomLevels[currentZoomIndex];
            campaign?.ShowMessage($"BẢN ĐỒ: TẦM NHÌN {(int)minimapRange}M", 1.2f);
        }
        if (Input.GetKeyDown(KeyCode.T))
        {
            navMode = (NavTargetMode)(((int)navMode + 1) % 4);
            campaign?.ShowMessage("CHỈ ĐƯỜNG: " + GetNavModeName(navMode).ToUpperInvariant(), 1.5f);
        }
        UpdateNavigationTarget();
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
        if(!RegionIntroVisible && !ChapterRitualVisible) DrawAdventure3DWaypoints(w,h,safe,scale);
        if(showAdventureMinimap)DrawAdventureMinimap(w,h);
    }

    void DrawRiceVitals()
    {
        Color saved=GUI.color;
        ShadowLabel(new Rect(21,14,300,31),"Thánh Gióng",hudTitle);
        GUI.color=Color.white;
        DrawEnchantedHealthBar(new Rect(24,42,250,22),campaign.Health01,false,$"SINH LỰC  {Mathf.CeilToInt(campaign.Health)} / {Mathf.CeilToInt(campaign.maxHealth)}");
        if(campaign.LotusWardRemaining>0)
        {
            GUI.color=Gold;GUI.DrawTexture(new Rect(292,40,16,23),riceGrain);GUI.color=saved;
            ShadowLabel(new Rect(316,38,260,36),$"SEN HỘ THỂ {campaign.LotusWardRemaining:0.0}s\nGiảm 32% sát thương",small);
        }
        GUI.color=saved;
    }

    void DrawAdventureQuickSlots()
    {
        Color saved=GUI.color;
        // There is no switchable sword/bamboo inventory: the current weapon is displayed honestly.
        string weapon=campaign.CarriedWeapon!=null ? (campaign.CarriedWeapon.type==ThanhGiongImprovisedWeapon.WeaponType.Boulder?"ĐÁ NÉM":"GỐC CÂY")
            :campaign.CurrentWeapon==ThanhGiongCampaignController.Weapon.IronSword?"THƯƠNG SẮT"
            :campaign.CurrentWeapon==ThanhGiongCampaignController.Weapon.Bamboo?campaign.BambooVariantName.ToUpperInvariant():"TAY KHÔNG";
        ShadowLabel(new Rect(22,88,320,23),weapon+"  ·  "+(campaign.CarriedWeapon!=null?"CHUỘT: NÉM":"CHUỘT: ĐÁNH"),small);
        if(seeds!=null&&seeds.IsPlantingContext)
        {
            string[] names={"SÁNG","TRE","SEN","LỬA","GIÓ"};
            for(int i=0;i<5;i++)
            {
                bool selected=(int)seeds.SelectedSeed==i;
                Rect r=new Rect(22+i*50,118,44,42);
                GUI.color=Color.clear;
                GUI.color=selected?Gold:Muted;
                DrawHudLine(new Vector2(r.x+3,r.yMax),new Vector2(r.xMax-3,r.yMax),selected?Gold:Muted,1);
                ShadowLabel(new Rect(r.x+1,r.y+1,r.width-2,22),$"{i+1} {names[i]}",tile);
                ShadowLabel(new Rect(r.x+1,r.y+21,r.width-2,21),seeds.CooldownFor(i)>0?$"{seeds.CooldownFor(i):0}s":$"×{seeds.ChargesFor(i)}",tile);
            }
            GUI.color=saved;
            ShadowLabel(new Rect(278,117,270,49),$"Q gieo · {seeds.Mana:0}/{seeds.MaxMana:0} linh lực\n{seeds.SelectedName}",small);
        }
        else
        {
            string status=campaign.CurrentChapter==ThanhGiongCampaignController.Chapter.Prologue?$"Lương thực {campaign.Food:0}/{campaign.foodPerGrowth*3:0} · Lớn {campaign.GrowthPhase+1}/4":campaign.IsBattleActive?$"Hạ giặc {campaign.Kills}/{campaign.RequiredBattleKills}":Sequence();
            ShadowLabel(new Rect(22,115,330,27),status,small);
        }
        GUI.color=saved;
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
        float x=Mathf.Clamp(anchor.x+42,320,w-190),y=Mathf.Clamp(anchor.y-22,170,h-180);
        if(stamina)
        {
            DrawEnergyCells(x,y,adventureStamina.Ratio,new Color(.57f,.64f,.43f),$"THỂ LỰC  {adventureStamina.Current:0}/{adventureStamina.capacity:0}");
            y+=40;
        }
        if(bambooCharge)
        {
            DrawEnergyCells(x,y,campaign.BambooPull01,new Color(.52f,.63f,.45f),$"GỒNG NHỔ TRE  {campaign.BambooPullStep}/3");
            y+=40;
        }
        if(heat)
        {
            DrawEnergyCells(x,y,campaign.Heat01,new Color(1,.61f,.25f),campaign.Heat01>=.99f?"HỎA KHÍ ĐẦY · F":"HỎA KHÍ  "+Mathf.RoundToInt(campaign.Heat01*100)+"%");
            y+=40;
        }
        if(mana)DrawEnergyCells(x,y,seeds.Mana/Mathf.Max(1,seeds.MaxMana),new Color(.53f,.65f,.64f),$"LINH LỰC  {seeds.Mana:0}/{seeds.MaxMana:0}");
    }

    void DrawEnergyCells(float x,float y,float value,Color color,string label)
    {
        Color saved=GUI.color;
        GUI.color=new Color(.075f,.032f,.026f,.66f);
        // no backing box
        for(int i=0;i<8;i++)
        {
            Rect cell=new Rect(x+i*13,y,10,8);GUI.color=new Color(.075f,.032f,.026f,.78f);GUI.DrawTexture(cell,Texture2D.whiteTexture);
            GUI.color=color;float fill=Mathf.Clamp01(value*8-i);GUI.DrawTexture(new Rect(cell.x,cell.y,cell.width*fill,cell.height),Texture2D.whiteTexture);
        }
        GUI.color=saved;
        ShadowLabel(new Rect(x,y+11,180,24),label,small);
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
            if(selected)DrawEnchantedHealthBar(new Rect(pos.x-42,pos.y+32,84,7),enemy.HealthRatio,true);
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
            Rect prompt=new Rect(Mathf.Clamp(at.x-170,20,w-360),Mathf.Clamp(at.y+45,160,h-90),340,48);
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
        mapCamera.enabled = !paused && !guidesVisible && !ThanhGiongStartScreen.IsMenuVisible;
        mapCamera.orthographicSize = Mathf.Max(15, minimapRange);
        mapCamera.transform.SetPositionAndRotation(campaign.transform.position+Vector3.up*100,Quaternion.Euler(90,0,0));
    }

    void DrawAdventureMinimap(float w,float h)
    {
        const float radius = 94f;
        Vector2 center = new Vector2(w - 124, h - 138);
        Color saved = GUI.color;

        // Dark outer shadow and disc backing
        GUI.color = new Color(.04f, .06f, .05f, .95f);
        GUI.DrawTexture(new Rect(center.x - radius - 10, center.y - radius - 10, (radius + 10) * 2, (radius + 10) * 2), mapDisc);
        GUI.color = Color.white;

        if (mapRender != null && mapCircle != null && mapMask != null)
        {
            if (Event.current.type == EventType.Repaint) {
                // Blit changes the active render target. IMGUI must continue on the Game View backbuffer.
                RenderTexture previousTarget = RenderTexture.active;
                Graphics.Blit(mapRender, mapCircle, mapMask);
                RenderTexture.active = previousTarget;
            }
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), mapCircle, ScaleMode.StretchToFill, true);
        }

        // Luminous Golden Ring with tick marks
        DrawHudArc(center, radius + 4, 1, new Color(Gold.r, Gold.g, Gold.b, .78f), 1.6f);
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI / 12;
            Vector2 ray = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            bool isCardinal = (i % 6 == 0);
            DrawHudLine(center + ray * (radius + 2), center + ray * (radius + (isCardinal ? 9 : 6)), new Color(Gold.r, Gold.g, Gold.b, isCardinal ? .95f : .6f), isCardinal ? 2.2f : 1f);
        }

        // Cardinal directions
        ShadowLabel(new Rect(center.x - 12, center.y - radius - 24, 24, 20), "B", adventureCentered);
        ShadowLabel(new Rect(center.x - 12, center.y + radius + 4, 24, 20), "N", adventureCentered);
        ShadowLabel(new Rect(center.x + radius + 4, center.y - 10, 24, 20), "Đ", adventureCentered);
        ShadowLabel(new Rect(center.x - radius - 28, center.y - 10, 24, 20), "T", adventureCentered);

        // Navigation Line & Breadcrumbs
        if (hasNavTarget)
        {
            Vector3 delta = currentNavTarget - campaign.transform.position;
            Vector2 navOffset = new Vector2(delta.x, -delta.z) * (radius / Mathf.Max(15, minimapRange));
            float offsetLen = navOffset.magnitude;
            Vector2 navEnd = center + (offsetLen > radius - 6 ? navOffset.normalized * (radius - 6) : navOffset);

            // Subtle path line
            DrawHudLine(center, navEnd, new Color(currentNavColor.r, currentNavColor.g, currentNavColor.b, 0.40f), 1.8f);

            // Animated breadcrumb dots along line
            int breadcrumbs = Mathf.Clamp(Mathf.RoundToInt(Vector2.Distance(center, navEnd) / 16f), 2, 7);
            float pulsePhase = Mathf.Repeat(Time.unscaledTime * 2.5f, 1f);
            for (int i = 0; i < breadcrumbs; i++)
            {
                float t = (i + pulsePhase) / breadcrumbs;
                if (t > 1f) t -= 1f;
                Vector2 beadPos = Vector2.Lerp(center, navEnd, t);
                DrawMapPinAt(beadPos, currentNavColor, 2.5f);
            }

            // Rim indicator arrow pointing towards destination
            float navAngle = Mathf.Atan2(delta.x, -delta.z);
            Vector2 rimDir = new Vector2(Mathf.Sin(navAngle), -Mathf.Cos(navAngle));
            Vector2 rimPos = center + rimDir * (radius + 2);
            Vector2 rimPerp = new Vector2(-rimDir.y, rimDir.x);
            float arrowPulse = 1f + Mathf.PingPong(Time.unscaledTime * 3f, 0.35f);
            DrawHudLine(rimPos + rimDir * (9f * arrowPulse), rimPos - rimDir * 4f + rimPerp * 5.5f, currentNavColor, 2.5f);
            DrawHudLine(rimPos + rimDir * (9f * arrowPulse), rimPos - rimDir * 4f - rimPerp * 5.5f, currentNavColor, 2.5f);
            DrawHudLine(rimPos - rimDir * 4f - rimPerp * 5.5f, rimPos - rimDir * 4f + rimPerp * 5.5f, currentNavColor, 2.5f);
        }

        // Map Pins
        foreach (ThanhGiongCollectible item in mapResources)
            if (item != null && !item.IsCollected && item.gameObject.activeInHierarchy)
                DrawMapPin(center, radius, item.transform.position, new Color(.35f, .92f, .45f), 3.5f, false);
        foreach (ThanhGiongImprovisedWeapon item in mapThrowables)
            if (item != null && !item.IsCarried && item.gameObject.activeInHierarchy)
                DrawMapPin(center, radius, item.transform.position, new Color(.75f, .85f, .85f), 3f, false);
        foreach (ThanhGiongEnemy enemy in mapEnemies)
            if (enemy != null && enemy.HealthRatio > 0 && enemy.gameObject.activeInHierarchy)
                DrawMapPin(center, radius, enemy.transform.position, enemy.isBoss ? new Color(1f, .40f, .22f) : new Color(.95f, .28f, .25f), enemy.isBoss ? 6f : 3.5f, false);
        foreach (Transform exit in mapExits)
            if (exit != null && exit.gameObject.activeInHierarchy)
            {
                float pulse = 5f + Mathf.PingPong(Time.unscaledTime * 4f, 2.5f);
                DrawMapPin(center, radius, exit.position, new Color(.4f, .88f, 1f), pulse, true);
            }
        if (campaign.openWorldCampaign != null) {
            foreach (var node in ThanhGiongResourceNode.ActiveNodes)
                if (node != null && node.IsReady) DrawMapPin(center, radius, node.transform.position, new Color(.98f, .81f, .35f), 3.5f, false);
            foreach (var forge in ThanhGiongForgeStation.ActiveStations)
                if (forge != null) DrawMapPin(center, radius, forge.transform.position, new Color(1f, .55f, .18f), 5.5f, true);
        }
        if (checkpoint != null && checkpoint.HasCheckpoint)
            DrawMapPin(center, radius, checkpoint.CheckpointPosition, Gold, 4.5f, false);

        // Player / Hero Avatar (Golden Spearhead/Horse with glowing vision cone)
        float playerAngle = -campaign.transform.eulerAngles.y * Mathf.Deg2Rad;
        Vector2 playerAhead = new Vector2(-Mathf.Sin(playerAngle), -Mathf.Cos(playerAngle));
        Vector2 playerSide = new Vector2(-playerAhead.y, playerAhead.x);

        // Forward vision rays
        DrawHudLine(center, center + (playerAhead * 1.5f + playerSide * 0.6f).normalized * 24f, new Color(Gold.r, Gold.g, Gold.b, 0.25f), 1.4f);
        DrawHudLine(center, center + (playerAhead * 1.5f - playerSide * 0.6f).normalized * 24f, new Color(Gold.r, Gold.g, Gold.b, 0.25f), 1.4f);
        DrawHudLine(center, center + playerAhead * 28f, new Color(1f, .95f, .75f, 0.50f), 1.8f);

        // Dark outer backing for high contrast
        DrawHudLine(center + playerAhead * 12f, center - playerAhead * 7f + playerSide * 7f, new Color(.04f, .02f, .02f, .95f), 4.2f);
        DrawHudLine(center + playerAhead * 12f, center - playerAhead * 7f - playerSide * 7f, new Color(.04f, .02f, .02f, .95f), 4.2f);
        DrawHudLine(center - playerAhead * 7f - playerSide * 7f, center - playerAhead * 2f, new Color(.04f, .02f, .02f, .95f), 4.2f);
        DrawHudLine(center - playerAhead * 7f + playerSide * 7f, center - playerAhead * 2f, new Color(.04f, .02f, .02f, .95f), 4.2f);

        // Vibrant gold hero chevron
        DrawHudLine(center + playerAhead * 11f, center - playerAhead * 6f + playerSide * 6f, Gold, 2.4f);
        DrawHudLine(center + playerAhead * 11f, center - playerAhead * 6f - playerSide * 6f, Gold, 2.4f);
        DrawHudLine(center - playerAhead * 6f - playerSide * 6f, center - playerAhead * 1.5f, Gold, 2.4f);
        DrawHudLine(center - playerAhead * 6f + playerSide * 6f, center - playerAhead * 1.5f, Gold, 2.4f);

        // Center pearl
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(center.x - 2.5f, center.y - 2.5f, 5, 5), mapDisc);

        GUI.color = saved;

        // Bottom labels
        if (hasNavTarget)
            ShadowLabel(new Rect(center.x - 120, center.y + radius + 11, 240, 18), $"▶ {currentNavLabel}  ·  {navDistance:0}m", adventureCentered);
        else
        {
            Vector3 pos = campaign.transform.position;
            ShadowLabel(new Rect(center.x - 110, center.y + radius + 11, 220, 18), $"{pos.x:0}, {pos.z:0} · Cao {pos.y:0}m", adventureCentered);
        }
        ShadowLabel(new Rect(center.x - 120, center.y + radius + 29, 240, 18), $"[Z] Zoom {(int)minimapRange}m  ·  [T] Đích ({GetNavModeShort(navMode)})", adventureCentered);
    }

    void DrawMapPin(Vector2 center,float radius,Vector3 world,Color color,float size,bool clampEdge)
    {
        Vector3 delta=world-campaign.transform.position;
        Vector2 offset=new Vector2(delta.x,-delta.z)*(radius/Mathf.Max(15,minimapRange));
        if(offset.magnitude>radius-8){if(!clampEdge)return;offset=offset.normalized*(radius-8);}
        DrawMapPinAt(center+offset, color, size);
    }

    void DrawMapPinAt(Vector2 point, Color color, float size)
    {
        Color saved = GUI.color;
        GUI.color = new Color(.05f, .02f, .02f, .85f);
        GUI.DrawTexture(new Rect(point.x - size - 1, point.y - size - 1, size * 2 + 2, size * 2 + 2), mapDisc);
        GUI.color = color;
        GUI.DrawTexture(new Rect(point.x - size, point.y - size, size * 2, size * 2), mapDisc);
        GUI.color = saved;
    }

    void DrawAdventure3DWaypoints(float w, float h, Rect safe, float scale)
    {
        if (!hasNavTarget || adventureView == null || campaign == null || campaign.IsDead) return;

        Vector3 targetWorld = currentNavTarget + Vector3.up * 1.6f;
        Vector3 screenPos = adventureView.WorldToScreenPoint(targetWorld);

        bool behind = screenPos.z <= 0;
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        float pad = 44f * scale;
        float minX = safe.x + pad, maxX = safe.xMax - pad;
        float minY = safe.y + pad, maxY = safe.yMax - pad;

        bool onScreen = !behind && screenPos.x >= minX && screenPos.x <= maxX && screenPos.y >= minY && screenPos.y <= maxY;

        Color saved = GUI.color;
        if (onScreen)
        {
            Vector2 hudPos = new Vector2((screenPos.x - safe.x) / scale, (Screen.height - screenPos.y - (Screen.height - safe.yMax)) / scale);

            GUI.color = new Color(.07f, .035f, .025f, .88f);
            Rect pill = new Rect(hudPos.x - 62, hudPos.y - 34, 124, 24);
            GUI.DrawTexture(pill, Texture2D.whiteTexture);

            GUI.color = currentNavColor;
            DrawHudLine(new Vector2(pill.x, pill.yMax), new Vector2(pill.xMax, pill.yMax), currentNavColor, 2f);

            ShadowLabel(new Rect(pill.x + 2, pill.y + 2, pill.width - 4, 20), $"{currentNavLabel} {navDistance:0}m", adventureCentered);
            ShadowLabel(new Rect(hudPos.x - 10, hudPos.y - 12, 20, 20), "▼", adventureCentered);
        }
        else
        {
            Vector2 dir = new Vector2(screenPos.x - screenCenter.x, screenPos.y - screenCenter.y);
            if (behind) dir = -dir;
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
            dir.Normalize();

            float boundHalfW = (safe.width * 0.5f - pad);
            float boundHalfH = (safe.height * 0.5f - pad);
            float scaleX = Mathf.Abs(dir.x) > 0.0001f ? boundHalfW / Mathf.Abs(dir.x) : float.MaxValue;
            float scaleY = Mathf.Abs(dir.y) > 0.0001f ? boundHalfH / Mathf.Abs(dir.y) : float.MaxValue;
            float clampFactor = Mathf.Min(scaleX, scaleY);

            Vector2 clampedScreen = screenCenter + dir * clampFactor;
            Vector2 hudPos = new Vector2((clampedScreen.x - safe.x) / scale, (Screen.height - clampedScreen.y - (Screen.height - safe.yMax)) / scale);
            Vector2 hudDir = new Vector2(dir.x, -dir.y);

            GUI.color = new Color(.07f, .035f, .025f, .90f);
            Rect pill = new Rect(hudPos.x - 56, hudPos.y - 14, 112, 26);
            GUI.DrawTexture(pill, Texture2D.whiteTexture);

            GUI.color = currentNavColor;
            DrawHudLine(new Vector2(pill.x, pill.yMax), new Vector2(pill.xMax, pill.yMax), currentNavColor, 2f);

            Vector2 arrowHead = hudPos + hudDir * 18f;
            Vector2 arrowPerp = new Vector2(-hudDir.y, hudDir.x);
            DrawHudLine(arrowHead, arrowHead - hudDir * 9f + arrowPerp * 5.5f, currentNavColor, 2.2f);
            DrawHudLine(arrowHead, arrowHead - hudDir * 9f - arrowPerp * 5.5f, currentNavColor, 2.2f);

            ShadowLabel(new Rect(pill.x + 2, pill.y + 2, pill.width - 4, 20), $"{currentNavLabel} {navDistance:0}m", adventureCentered);
        }
        GUI.color = saved;
    }

    void UpdateNavigationTarget()
    {
        hasNavTarget = false;
        if (campaign == null || campaign.IsDead) return;

        if (campaign.openWorldCampaign != null && campaign.openWorldCampaign.world != null)
        {
            var selected = campaign.openWorldCampaign.world.SelectedZone;
            if (selected != null && selected != campaign.openWorldCampaign.world.CurrentZone)
            {
                currentNavTarget = selected.center;
                currentNavLabel = selected.displayName.ToUpperInvariant();
                currentNavColor = new Color(1f, .85f, .35f);
                hasNavTarget = true;
                navDistance = Vector3.Distance(campaign.transform.position, currentNavTarget);
                return;
            }
        }

        if (navMode == NavTargetMode.Exit)
        {
            if (TryGetNearestExit(out Vector3 exitPos))
            {
                currentNavTarget = exitPos;
                currentNavLabel = "CỔNG QUA MÀN";
                currentNavColor = new Color(.4f, .85f, 1f);
                hasNavTarget = true;
                navDistance = Vector3.Distance(campaign.transform.position, currentNavTarget);
                return;
            }
        }
        else if (navMode == NavTargetMode.Combat)
        {
            if (TryGetNearestEnemyOrBoss(out Vector3 combatPos, out string combatLabel, out bool isBoss))
            {
                currentNavTarget = combatPos;
                currentNavLabel = combatLabel;
                currentNavColor = isBoss ? new Color(1f, .4f, .22f) : new Color(.95f, .28f, .25f);
                hasNavTarget = true;
                navDistance = Vector3.Distance(campaign.transform.position, currentNavTarget);
                return;
            }
        }
        else if (navMode == NavTargetMode.Resource)
        {
            if (TryGetNearestResourceOrForge(out Vector3 resPos, out string resLabel))
            {
                currentNavTarget = resPos;
                currentNavLabel = resLabel;
                currentNavColor = new Color(.45f, .90f, .40f);
                hasNavTarget = true;
                navDistance = Vector3.Distance(campaign.transform.position, currentNavTarget);
                return;
            }
        }

        switch (campaign.CurrentChapter)
        {
            case ThanhGiongCampaignController.Chapter.Prologue:
                if (campaign.Food < campaign.foodPerGrowth * 3f)
                {
                    if (TryGetNearestCollectible(out Vector3 foodPos))
                    {
                        currentNavTarget = foodPos;
                        currentNavLabel = "LƯƠNG THỰC";
                        currentNavColor = new Color(.45f, .90f, .40f);
                        hasNavTarget = true;
                    }
                }
                else if (TryGetNearestExit(out Vector3 exitPos))
                {
                    currentNavTarget = exitPos;
                    currentNavLabel = "CỔNG QUA MÀN";
                    currentNavColor = new Color(.4f, .85f, 1f);
                    hasNavTarget = true;
                }
                break;

            case ThanhGiongCampaignController.Chapter.Preparation:
                if (campaign.bambooRoot != null)
                {
                    currentNavTarget = campaign.bambooRoot.transform.position;
                    currentNavLabel = "TRANG BỊ VUA BAN";
                    currentNavColor = Gold;
                    hasNavTarget = true;
                }
                else if (TryGetNearestExit(out Vector3 prepExit))
                {
                    currentNavTarget = prepExit;
                    currentNavLabel = "XUẤT QUÂN";
                    currentNavColor = new Color(.4f, .85f, 1f);
                    hasNavTarget = true;
                }
                break;

            case ThanhGiongCampaignController.Chapter.Battle:
                if (campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.None)
                {
                    if (campaign.bambooRoot != null)
                    {
                        currentNavTarget = campaign.bambooRoot.transform.position;
                        currentNavLabel = "KHÓM TRE THẦN";
                        currentNavColor = new Color(.55f, .88f, .27f);
                        hasNavTarget = true;
                    }
                    else if (TryGetNearestCollectible(out Vector3 bambooPos, ThanhGiongCollectible.Kind.Bamboo))
                    {
                        currentNavTarget = bambooPos;
                        currentNavLabel = "TRE NGÀ";
                        currentNavColor = new Color(.55f, .88f, .27f);
                        hasNavTarget = true;
                    }
                }
                else if (!campaign.CanAdvanceToNextScene(out _))
                {
                    if (TryGetNearestEnemyOrBoss(out Vector3 targetEnemy, out string enemyName, out bool boss))
                    {
                        currentNavTarget = targetEnemy;
                        currentNavLabel = enemyName;
                        currentNavColor = boss ? new Color(1f, .4f, .22f) : new Color(.95f, .28f, .25f);
                        hasNavTarget = true;
                    }
                }
                else if (TryGetNearestExit(out Vector3 battleExit))
                {
                    currentNavTarget = battleExit;
                    currentNavLabel = "CỔNG QUA MÀN";
                    currentNavColor = new Color(.4f, .85f, 1f);
                    hasNavTarget = true;
                }
                break;

            case ThanhGiongCampaignController.Chapter.Ascension:
                if (checkpoint != null && checkpoint.HasCheckpoint)
                {
                    currentNavTarget = checkpoint.CheckpointPosition;
                    currentNavLabel = "ĐỈNH SÓC HÓA THÁNH";
                    currentNavColor = Gold;
                    hasNavTarget = true;
                }
                else if (TryGetNearestExit(out Vector3 ascExit))
                {
                    currentNavTarget = ascExit;
                    currentNavLabel = "ĐỈNH SÓC";
                    currentNavColor = Gold;
                    hasNavTarget = true;
                }
                break;

            default:
                if (TryGetNearestExit(out Vector3 defExit))
                {
                    currentNavTarget = defExit;
                    currentNavLabel = "ĐÍCH ĐẾN";
                    currentNavColor = new Color(.4f, .85f, 1f);
                    hasNavTarget = true;
                }
                break;
        }

        if (hasNavTarget)
            navDistance = Vector3.Distance(campaign.transform.position, currentNavTarget);
    }

    bool TryGetNearestExit(out Vector3 pos)
    {
        pos = Vector3.zero;
        Transform nearest = null; float minDistSq = float.MaxValue;
        foreach (Transform exit in mapExits)
        {
            if (exit != null && exit.gameObject.activeInHierarchy)
            {
                float d = (exit.position - campaign.transform.position).sqrMagnitude;
                if (d < minDistSq) { minDistSq = d; nearest = exit; }
            }
        }
        if (nearest != null) { pos = nearest.position; return true; }
        return false;
    }

    bool TryGetNearestEnemyOrBoss(out Vector3 pos, out string label, out bool isBoss)
    {
        pos = Vector3.zero; label = "QUÂN GIẶC"; isBoss = false;
        if (cachedBoss != null && cachedBoss.HealthRatio > 0 && cachedBoss.gameObject.activeInHierarchy)
        {
            pos = cachedBoss.transform.position; label = "TƯỚNG GIẶC ÂN"; isBoss = true; return true;
        }
        ThanhGiongEnemy nearest = null; float minDistSq = float.MaxValue;
        foreach (ThanhGiongEnemy enemy in mapEnemies)
        {
            if (enemy != null && enemy.HealthRatio > 0 && enemy.gameObject.activeInHierarchy)
            {
                if (enemy.isBoss) { pos = enemy.transform.position; label = "TƯỚNG GIẶC ÂN"; isBoss = true; return true; }
                float d = (enemy.transform.position - campaign.transform.position).sqrMagnitude;
                if (d < minDistSq) { minDistSq = d; nearest = enemy; }
            }
        }
        if (nearest != null) { pos = nearest.transform.position; return true; }
        return false;
    }

    bool TryGetNearestCollectible(out Vector3 pos, ThanhGiongCollectible.Kind? filterKind = null)
    {
        pos = Vector3.zero;
        ThanhGiongCollectible nearest = null; float minDistSq = float.MaxValue;
        foreach (ThanhGiongCollectible item in mapResources)
        {
            if (item != null && !item.IsCollected && item.gameObject.activeInHierarchy)
            {
                if (filterKind.HasValue && item.kind != filterKind.Value) continue;
                float d = (item.transform.position - campaign.transform.position).sqrMagnitude;
                if (d < minDistSq) { minDistSq = d; nearest = item; }
            }
        }
        if (nearest != null) { pos = nearest.transform.position; return true; }
        return false;
    }

    bool TryGetNearestResourceOrForge(out Vector3 pos, out string label)
    {
        pos = Vector3.zero; label = "TÀI NGUYÊN";
        if (campaign.openWorldCampaign != null)
        {
            foreach (var forge in ThanhGiongForgeStation.ActiveStations)
            {
                if (forge != null && forge.gameObject.activeInHierarchy)
                {
                    pos = forge.transform.position; label = "LÒ RÈN"; return true;
                }
            }
            foreach (var node in ThanhGiongResourceNode.ActiveNodes)
            {
                if (node != null && node.IsReady && node.gameObject.activeInHierarchy)
                {
                    pos = node.transform.position; label = "MỎ TÀI NGUYÊN"; return true;
                }
            }
        }
        return TryGetNearestCollectible(out pos);
    }

    static string GetNavModeName(NavTargetMode mode) => mode switch
    {
        NavTargetMode.Auto => "Tự động theo nhiệm vụ",
        NavTargetMode.Exit => "Ưu tiên Cổng qua màn",
        NavTargetMode.Combat => "Ưu tiên Quân giặc / Tướng",
        _ => "Ưu tiên Tài nguyên / Khóm tre"
    };

    static string GetNavModeShort(NavTargetMode mode) => mode switch
    {
        NavTargetMode.Auto => "Tự động",
        NavTargetMode.Exit => "Cổng",
        NavTargetMode.Combat => "Giặc",
        _ => "Vật phẩm"
    };

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
        hasNavTarget=false;
    }
}
