using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Hệ thống đối thoại với dân làng. Mỗi loại nguyên liệu chỉ được thu thập sau khi nói chuyện với
/// đúng dân làng (phím F). Nội dung thoại nằm ở <see cref="ThanhGiongNpcLore"/>.
/// </summary>
public static class ThanhGiongNpcLore
{
    public sealed class Topic { public string label; public string[] pages; public Topic(string l, params string[] p) { label = l; pages = p; } }
    public sealed class Npc
    {
        public string id, name, title;
        public int unlock = -1;               // OpenWorldResource được dạy
        public string[] intro, learned, repeat;
        public Topic[] topics;
    }

    static readonly Dictionary<string, Npc> byId = new Dictionary<string, Npc>();
    public static Npc Get(string id) { Build(); Npc n; return id != null && byId.TryGetValue(id, out n) ? n : null; }
    public static Npc TeacherOf(OpenWorldResource r) { Build(); foreach (var n in byId.Values) if (n.unlock == (int)r) return n; return null; }
    public static IEnumerable<string> AllIds { get { Build(); return byId.Keys; } }

    static void Add(Npc n) { byId[n.id] = n; }

    // Gợi ý nơi có nguyên liệu, tính từ bảng sinh cảnh của game.
    public static string WhereIs(OpenWorldResource r)
    {
        var rich = new List<string>(); var some = new List<string>();
        foreach (OpenWorldBiome b in System.Enum.GetValues(typeof(OpenWorldBiome)))
        {
            int rank = ThanhGiongOpenWorld.ResourceAbundance(b, r);
            if (rank == 0) rich.Add(ThanhGiongOpenWorld.BiomeLabel(b));
            else if (rank > 0) some.Add(ThanhGiongOpenWorld.BiomeLabel(b));
        }
        var sb = new StringBuilder();
        if (rich.Count > 0) sb.Append("Nhiều nhất ở: ").Append(string.Join(", ", rich.ToArray())).Append(". ");
        if (some.Count > 0) sb.Append("Còn ít hơn ở: ").Append(string.Join(", ", some.ToArray())).Append(".");
        sb.Append(" Đến sát điểm tài nguyên rồi nhấn G; càng đi xa làng, vùng càng nguy hiểm nhưng càng được nhiều.");
        return sb.ToString();
    }

    static bool built;
    static void Build()
    {
        if (built) return; built = true;

        Add(new Npc {
            id = "nongdan", name = "Bác Tư", title = "Nông dân", unlock = (int)OpenWorldResource.Fiber,
            intro = new[] {
                "Chà, chàng trai đi ngựa sắt kia! Làng Phù Đổng mình có tiếng một nắng hai sương, nhưng chưa bao giờ thấy ai oai như cậu.",
                "Tôi là Tư, cấy lúa ở bãi ven sông này ba chục năm rồi. Năm nay giặc Ân hoành hành, ruộng đồng bỏ hoang hết nửa." },
            learned = new[] {
                "Cậu cần sợi vải để bện dây cương, may áo giáp lót, hả? Dễ thôi. Sợi gai, sợi lanh mọc đầy ở đầm lầy và ruộng bậc thang.",
                "Cứ cầm lấy mà tuốt từng bó, đừng giật mạnh kẻo đứt sợi. Từ giờ cậu biết cách thu sợi vải rồi đấy." },
            repeat = new[] { "Lại ghé thăm bác đấy à? Ruộng nhà này còn thiếu nước, nhưng chuyện đánh giặc cậu cứ lo trước.", "Cơm nắm bác để dành rồi, lúc nào đói cứ về làng.", "Trời mà mưa là đầm lầy khó đi lắm, cậu nhớ giữ ngựa cho chắc." },
            topics = new[] {
                new Topic("Hỏi chỗ lấy sợi vải", "{WHERE:Fiber}"),
                new Topic("Chuyện mùa màng", "Từ ngày giặc kéo tới, trâu bò bị đánh cắp hết, ruộng cày bằng tay cũng chẳng đủ ăn.", "Nhưng bà con vẫn gieo đều, vì lúa là lòng tin. Thấy lúa lên xanh, mình biết ngày mai còn có ngày mai."),
                new Topic("Hỏi về giặc Ân", "Chúng đóng ở pháo đài phía Đông, hay cướp lương thực mang đi. Đánh lẻ thì không nổi, nhưng đánh có kế thì được.", "Tôi thấy chúng sợ lửa và sợ nhất là tiếng trống đồng. Cậu cứ nhớ điều đó."),
            } });

        Add(new Npc {
            id = "baihai", name = "Bà Hai", title = "Người thuộc da", unlock = (int)OpenWorldResource.Hide,
            intro = new[] {
                "Ôi cậu bé Gióng! Ngày trước bà còn bế cậu trên tay. Mới đó mà đã thành tráng sĩ cưỡi ngựa sắt rồi.",
                "Bà làm nghề thuộc da, khâu giáp da cho người trong làng. Nghề này vất vả, mùi hôi, nhưng người đi đánh giặc không thể thiếu." },
            learned = new[] {
                "Cậu muốn tự lấy da thú sao? Được, bà chỉ cho: đừng săn bừa, chỉ lấy đủ dùng, và phải lột đúng đường để da còn nguyên.",
                "Bà dạy xong rồi, từ giờ cậu biết thu da thú. Nhớ đem về lò rèn hoặc chỗ bà để thuộc." },
            repeat = new[] { "Bà đang ngâm da, cậu đừng lại gần kẻo hôi áo.", "Ngựa sắt của cậu mà bị trầy, mang đến bà khâu yên cho.", "Đi cẩn thận nhé, thú dữ trong rừng không như thú ở làng đâu." },
            topics = new[] {
                new Topic("Hỏi chỗ lấy da thú", "{WHERE:Hide}"),
                new Topic("Về nghề thuộc da", "Da phải ngâm vỏ cây ba ngày, phơi dưới nắng nhẹ, rồi vuốt cho mềm. Vội thì da cứng như gỗ.", "Giáp da nhẹ hơn giáp sắt, hợp để chạy nhanh. Cậu đi xa thì nên mặc."),
                new Topic("Chuyện thuở nhỏ của cậu", "Hồi nhỏ cậu im lặng suốt ba năm, mẹ cậu lo lắm. Rồi một hôm nghe tiếng loa sứ giả, cậu bỗng cất lời xin gươm sắt ngựa sắt.", "Bà tin trời sinh cậu ra là để cứu nước. Đừng quên bà con quê nhà nhé."),
            } });

        Add(new Npc {
            id = "cuba", name = "Cụ Ba", title = "Già làng", unlock = (int)OpenWorldResource.Wood,
            intro = new[] {
                "Lão Ba già rồi, mắt mờ chân chậm, nhưng nghe tiếng vó ngựa là biết ngay người đến. Ngồi xuống đây, chàng trai.",
                "Làng ta dựng trên đất rồng, hưởng phúc của tre và nước. Tre là xương sống của làng: dựng nhà, làm cung, làm giáo, làm cả cầu." },
            learned = new[] {
                "Muốn lấy tre gỗ thì phải biết chọn: cây già đặc ruột làm cột, cây non dẻo làm cung. Chặt vào mùa khô để khỏi mọt.",
                "Lão truyền nghề cho cậu rồi đấy. Từ giờ cậu biết cách thu tre gỗ trong rừng." },
            repeat = new[] { "Tre còn thì làng còn. Cậu nhớ trồng lại sau khi chặt.", "Cái gậy này theo lão ba chục năm, vẫn chưa gãy.", "Đi đường rừng nhớ nghe tiếng chim, chim im là có giặc gần." },
            topics = new[] {
                new Topic("Hỏi chỗ lấy tre gỗ", "{WHERE:Wood}"),
                new Topic("Chuyện lập làng", "Tổ tiên ta tìm đất theo dòng nước. Ở đâu có sông thì ở đó có làng, ở đâu có tre thì ở đó có người.", "Ba trăm năm qua, làng chưa từng bị thua. Lão tin lần này cũng vậy, miễn là lòng người còn đoàn kết."),
                new Topic("Lời dặn của lão", "Đánh giặc không phải chỉ mạnh tay. Phải biết lúc nào tiến, lúc nào lui, lúc nào nhường.", "Cậu có sức mạnh của thần, nhưng đừng quên mình là con người của làng."),
            } });

        Add(new Npc {
            id = "betichild", name = "Bé Tí", title = "Trẻ chăn trâu",
            intro = new[] { "A! Là anh Gióng! Anh Gióng cưỡi ngựa sắt đến rồi! Anh cho em sờ cái giáo được không?", "Em là Tí, chăn trâu cho nhà bác Tư. Ngày nào em cũng mong anh đánh tan giặc để em được ra đồng chơi thả diều." },
            repeat = new[] { "Anh Gióng ơi, anh có mệt không?", "Em hái được nhiều quả ngon lắm, nhưng bí mật nhé!", "Lớn lên em cũng sẽ đánh giặc giống anh!" },
            topics = new[] {
                new Topic("Hỏi chỗ chơi của em", "Em hay ra bãi sông đằng kia. Có rất nhiều đá tròn xếp thành hình ngôi sao, em lượm về làm bi.", "Cụ Tám bảo đá ở núi mới là đá tốt, nhưng em chưa dám lên đó. Nghe nói trên đó có cọp!"),
                new Topic("Nghe em kể chuyện", "Em nghe bà kể: ngày xưa ngựa sắt phun lửa, đánh giặc thua tơi bời. Có thật không anh?", "Nếu thật thì em xin được cưỡi một lần, chỉ một lần thôi."),
                new Topic("Hỏi đường", "Anh muốn đi đâu thì em chỉ cho. Phía Tây là rừng tre, phía Bắc là núi, phía Đông là pháo đài giặc. Mở bản đồ M, chọn điểm đến, mũi tên sẽ chỉ đường cho anh!"),
            } });

        Add(new Npc {
            id = "tholencai", name = "Thợ rèn Cả", title = "Chủ lò rèn", unlock = (int)OpenWorldResource.Ore,
            intro = new[] {
                "Tiếng búa này cả làng nghe từ sáng tới tối. Cậu chính là người mang vũ khí ra trận chứ gì? Tôi là Cả, thợ rèn của làng.",
                "Muốn đúc binh khí tốt thì trước hết phải có quặng sắt tốt. Mà quặng thì nằm sâu trong núi, khó tìm lắm." },
            learned = new[] {
                "Nghe kỹ đây: quặng sắt có vân đỏ sẫm, gõ vào nghe tiếng đanh. Phải đập tách đá bao ngoài, không thì lẫn tạp chất.",
                "Tôi dạy xong rồi, từ giờ cậu biết khai thác quặng sắt. Thu đủ quặng thì mang về lò rèn, nhấn G để đúc vật tư." },
            repeat = new[] { "Mang quặng về đây, tôi đúc cho mà dùng.", "Lửa lò mà tắt là coi như hỏng cả ngày, tôi phải trông chừng.", "Giáp cậu có chỗ nào móp thì đem tới, tôi gò lại." },
            topics = new[] {
                new Topic("Hỏi chỗ lấy quặng sắt", "{WHERE:Ore}"),
                new Topic("Hỏi về lò rèn", "Cậu đứng cạnh lò rèn rồi nhấn G là đúc vật tư hoặc sửa trang bị. Mỗi lần đều tốn quặng, tre gỗ, da hoặc sợi.", "Đồ đúc xong càng bền thì càng đánh được lâu. Đừng tiếc tay mà để áo giáp hỏng giữa trận."),
                new Topic("Nghề rèn của tôi", "Ông nội tôi rèn rựa, cha tôi rèn dao, đến đời tôi thì rèn gươm. Ba đời đều làm cho làng.", "Đợt này giặc Ân gom hết sắt cả vùng, chợ chẳng còn gì bán. Cho nên mỗi mẩu quặng đều quý."),
            } });

        Add(new Npc {
            id = "chusau", name = "Chú Sáu", title = "Ngư dân",
            intro = new[] { "Hô, cậu cưỡi ngựa sắt đi qua đầm lầy mà chẳng lún? Đúng là thần nhân. Tôi là Sáu, đánh cá ở đầm.", "Mấy tháng nay cá vắng, tôm vắng, vì giặc đốt thuyền và phá bãi. Dân chài khổ lắm." },
            repeat = new[] { "Hôm nay cá cắn câu chậm, nhưng chú vẫn kiên nhẫn.", "Nước đầm lên xuống thất thường, cậu đi cẩn thận.", "Mang cá về cho bà con ăn, chú cũng mừng." },
            topics = new[] {
                new Topic("Hỏi về đầm lầy", "Đầm lầy dưới kia có nhiều sợi vải mọc dại, nhưng đất lún và có đỉa. Đi một mình thì dễ lạc.", "Chú hay đặt vài cây tre đánh dấu lối. Cậu thấy tre cắm thì đi theo, đừng đi lệch."),
                new Topic("Hỏi mẹo đánh cá", "Muốn cá cắn thì phải im lặng. Nói nhiều, cá chạy hết.", "Cậu có sức mạnh nhưng đi câu thì phải kiên nhẫn. Đánh giặc cũng vậy, đợi đúng lúc mới ra tay."),
                new Topic("Về giặc Ân", "Chúng có một tướng đứng đầu, to như núi, cầm trượng sắt. Dân chài thấy hắn từ xa là rạch thuyền chạy.", "Hắn sợ lửa và nước ngập. Cậu dẫn hắn vào chỗ lầy mà đánh thì lợi lắm."),
            } });

        Add(new Npc {
            id = "colan", name = "Cô Lan", title = "Thợ dệt",
            intro = new[] { "Chào anh Gióng. Em là Lan, dệt vải trong làng. Chiếc khăn đỏ trên cờ nghĩa quân là em dệt đấy.", "Em dệt đêm dệt ngày để may áo cho các chiến sĩ, mong họ trở về lành lặn." },
            repeat = new[] { "Khung cửi chưa dừng, anh cứ yên tâm.", "Anh nhớ giữ gìn sức khỏe nhé.", "Hôm nay em dệt được năm tấm, vẫn chưa đủ cho nghĩa quân." },
            topics = new[] {
                new Topic("Hỏi về nghề dệt", "Sợi gai kéo từ đầm lầy, sợi lanh trồng ở ruộng. Phải ngâm, phải đập, phải se thành chỉ rồi mới lên khung.", "Nhuộm màu thì dùng vỏ cây, củ nâu, lá chàm. Mỗi màu một công thức, mẹ em truyền cho em đấy."),
                new Topic("Chuyện cờ nghĩa quân", "Lá cờ đỏ có mặt trời kia là em thêu bằng chỉ vàng. Mỗi mũi chỉ em cầu một lời chúc.", "Dẫu đánh giặc có gian nan, hễ thấy cờ là biết làng vẫn đợi mình về."),
                new Topic("Lời nhắn của em", "Anh Gióng, nếu gặp ai bị thương ở ngoài kia, xin hãy giúp họ. Tin vào anh thì dân mới dám ở lại làng.", "Em tin anh làm được."),
            } });

        Add(new Npc {
            id = "bena", name = "Bé Na", title = "Trẻ làng",
            intro = new[] { "Anh ơi, anh có phải người đã ăn ba nong cơm bảy nong cà không? Mẹ em kể anh to lắm!", "Em là Na. Em hay giúp mẹ gánh nước nhưng em chưa làm được nhiều." },
            repeat = new[] { "Anh Gióng ơi, mai anh về sớm nhé!", "Em đã tập đánh giặc bằng que tre rồi!", "Anh thích kẹo mạch nha không? Em có để dành cho anh nè." },
            topics = new[] {
                new Topic("Hỏi về tre", "Anh thấy mấy bụi tre phía Tây không? Mùa xuân măng mọc rất nhiều, nhưng ở đó cũng nhiều rắn lắm.", "Cụ Ba dặn tụi em không được vào rừng tre khi trời tối."),
                new Topic("Chơi trò đố", "Em đố anh: cái gì càng lấy càng lớn? Là cái hố! Anh có đoán ra không?", "Còn cái này: cái gì đi ba chân? Là cái kiềng bếp! Anh giỏi quá!"),
                new Topic("Xin lời khuyên", "Mẹ em nói người dũng cảm không phải là người không sợ, mà là người sợ mà vẫn đi.", "Anh cũng sợ không? Anh đừng sợ, tụi em đang cổ vũ anh!"),
            } });

        Add(new Npc {
            id = "cutam", name = "Cụ Tám", title = "Thầy đá", unlock = (int)OpenWorldResource.Stone,
            intro = new[] { "Tiếng vó ngựa sắt, lão nghe mấy hôm nay rồi. Lại đây ngồi, lão có đôi lời.", "Lão Tám làm nghề đục đá, dựng bia, xây cột đình. Cả đời này lão cứ ngồi nghe đá nói chuyện." },
            learned = new[] {
                "Đá cũng có mạch có thớ, chỉ cần nhìn vân là biết chỗ nào nên đục. Than đá thì đen óng, gõ nghe trầm.",
                "Từ giờ cậu đã biết cách thu đá và than rồi nhé. Nhớ chọn khối vừa sức khiêng." },
            repeat = new[] { "Đá nặng mà tình nghĩa còn nặng hơn.", "Cái đình làng này lão dựng ba mươi năm trước.", "Có đá có than thì lò rèn mới cháy lâu." },
            topics = new[] {
                new Topic("Hỏi chỗ lấy đá và than", "{WHERE:Stone}"),
                new Topic("Chuyện đình làng", "Đình làng dựng bằng đá chân cột và gỗ lim. Bão lớn qua mà không đổ, ấy là nhờ đá chân tốt.", "Cậu nhớ rằng nền vững thì nhà mới vững. Người đánh giặc cũng cần có nền, là quê hương."),
                new Topic("Lời dạy của lão", "Đá không biết nói nhưng nhớ hết mọi chuyện. Dấu chân người, dấu vó ngựa, tất cả đều in lại.", "Cậu cứ đi, đá sẽ ghi nhận công cậu."),
            } });

        Add(new Npc {
            id = "diybay", name = "Dì Bảy", title = "Bà bán hàng",
            intro = new[] { "Cậu Gióng ơi, mua gì không? À mà thôi, cậu đang đi đánh giặc, dì tặng cậu nắm xôi đây.", "Dì Bảy bán hàng ở chợ làng. Giặc đến thì chợ vắng, nhưng dì vẫn bày hàng, để bà con thấy làng còn sống." },
            repeat = new[] { "Hôm nay chợ ế, nhưng dì vẫn vui vì thấy cậu.", "Dì có tin này cho cậu, ghé nghe nhé.", "Xôi vẫn nóng, cậu ăn đi kẻo nguội." },
            topics = new[] {
                new Topic("Hỏi tin chợ", "Thương lái nói phía Đông có tên tướng giặc đóng ở pháo đài, ngày nào cũng thu thuế lương thực.", "Dân chợ cùng nhau góp một ít để làm chiến phí. Dù ít, đó là tấm lòng cả làng."),
                new Topic("Hỏi về nguyên liệu", "Cậu muốn lấy nguyên liệu thì phải hỏi đúng người trong làng: Bác Tư biết về sợi vải, Bà Hai về da thú, Cụ Ba về tre gỗ, Cụ Tám về đá than, còn Thợ rèn Cả thì chuyện quặng sắt.", "Cậu cứ đi hỏi từng người, ai cũng sẵn lòng giúp."),
                new Topic("Hỏi về đường đi", "Cậu bấm M mở bản đồ lục địa, chọn một vùng làm điểm đến. Trên màn hình sẽ có mũi tên chỉ đường và khoảng cách.", "Đường lớn thì an toàn, đường tắt thì nguy hiểm. Cậu cân nhắc nhé."),
            } });
    }
}

public static class ThanhGiongVillagerProgress
{
    const string Prefix = "TG.Learn.";

    public static bool IsUnlocked(OpenWorldResource r, ThanhGiongOpenWorld world = null)
    {
        if (PlayerPrefs.GetInt(Prefix + (int)r, 0) == 1) return true;
        // Người chơi đã có nguyên liệu này từ trước (save cũ) thì mặc định đã biết.
        if (world != null && (world.GetCarriedAmount(r) > 0 || world.GetStoredAmount(r) > 0)) { Unlock(r); return true; }
        return false;
    }
    public static void Unlock(OpenWorldResource r) { PlayerPrefs.SetInt(Prefix + (int)r, 1); PlayerPrefs.Save(); }
    public static bool HasTalked(string id) => PlayerPrefs.GetInt("TG.Talked." + id, 0) == 1;
    public static void MarkTalked(string id) { PlayerPrefs.SetInt("TG.Talked." + id, 1); }
    public static void ResetAll()
    {
        for (int i = 0; i < 5; i++) PlayerPrefs.DeleteKey(Prefix + i);
        foreach (var id in ThanhGiongNpcLore.AllIds) PlayerPrefs.DeleteKey("TG.Talked." + id);
        PlayerPrefs.Save();
    }
}

/// <summary>Gợi ý [F], hộp thoại dưới màn hình, chọn chủ đề bằng phím 1–4, Space/chuột để qua câu.</summary>
public sealed class ThanhGiongDialogueUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static KeyCode TalkKey = KeyCode.F;
    const float TalkRange = 4.2f;

    enum Stage { Pages, Menu }
    ThanhGiongNpcLore.Npc npc;
    ThanhGiongVillager target;
    readonly Queue<string> pages = new Queue<string>();
    string currentPage = ""; float revealStart; Stage stage;
    bool learnAfterPages; float savedScale = 1f;
    Transform player; float findTimer;
    GUIStyle nameStyle, bodyStyle, choiceStyle, promptStyle, tagStyle;
    int fontSizeCache;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Application.isBatchMode) return;
        if (FindFirstObjectByType<ThanhGiongDialogueUI>() != null) return;
        var go = new GameObject("ThanhGiongDialogueUI"); DontDestroyOnLoad(go); go.AddComponent<ThanhGiongDialogueUI>();
    }

    void OnDestroy() { if (IsOpen) { IsOpen = false; Time.timeScale = savedScale; } }

    void Update()
    {
        if (IsOpen) { UpdateOpen(); return; }
        findTimer -= Time.unscaledDeltaTime;
        if (findTimer <= 0f || player == null)
        {
            findTimer = 1f;
            var p = FindFirstObjectByType<AdventureStamina>(); player = p != null ? p.transform : null;
        }
        if (player == null || Time.timeScale == 0f) { target = null; return; }
        target = Nearest();
        if (target != null && Input.GetKeyDown(TalkKey)) Open(target);
    }

    ThanhGiongVillager Nearest()
    {
        ThanhGiongVillager best = null; float bd = TalkRange * TalkRange;
        foreach (var v in ThanhGiongVillager.All)
        {
            if (v == null || string.IsNullOrEmpty(v.npcId)) continue;
            float d = (v.transform.position - player.position).sqrMagnitude;
            if (d < bd) { bd = d; best = v; }
        }
        return best;
    }

    void Open(ThanhGiongVillager v)
    {
        npc = ThanhGiongNpcLore.Get(v.npcId); if (npc == null) return;
        target = v; IsOpen = true; savedScale = Time.timeScale <= 0f ? 1f : Time.timeScale; Time.timeScale = 0f;
        pages.Clear(); learnAfterPages = false;
        var world = FindFirstObjectByType<ThanhGiongOpenWorld>();
        bool first = !ThanhGiongVillagerProgress.HasTalked(npc.id);
        if (first) { foreach (var p in npc.intro) pages.Enqueue(p); ThanhGiongVillagerProgress.MarkTalked(npc.id); }
        else if (npc.repeat != null && npc.repeat.Length > 0) pages.Enqueue(npc.repeat[Random.Range(0, npc.repeat.Length)]);
        if (npc.unlock >= 0 && !ThanhGiongVillagerProgress.IsUnlocked((OpenWorldResource)npc.unlock))
        {
            if (npc.learned != null) foreach (var p in npc.learned) pages.Enqueue(p);
            learnAfterPages = true;
        }
        stage = Stage.Pages; Next();
    }

    void Next()
    {
        if (pages.Count > 0) { currentPage = pages.Dequeue(); revealStart = Time.unscaledTime; stage = Stage.Pages; return; }
        if (learnAfterPages && npc.unlock >= 0)
        {
            learnAfterPages = false;
            var r = (OpenWorldResource)npc.unlock; ThanhGiongVillagerProgress.Unlock(r);
            var w = FindFirstObjectByType<ThanhGiongOpenWorld>();
            if (w != null) w.ShowNotice("Đã học: cách thu thập " + ThanhGiongOpenWorld.ResourceLabel(r) + " (nhấn G ở điểm tài nguyên)", 4.5f);
        }
        stage = Stage.Menu; currentPage = npc.name + " nhìn bạn, chờ bạn hỏi tiếp."; revealStart = Time.unscaledTime;
    }

    void Close() { IsOpen = false; Time.timeScale = savedScale; npc = null; pages.Clear(); }

    void UpdateOpen()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        if (stage == Stage.Pages)
        {
            bool advance = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(TalkKey);
            if (!advance) return;
            if (Revealed(currentPage) < currentPage.Length) { revealStart = -999f; return; }
            Next(); return;
        }
        var topics = npc.topics;
        for (int i = 0; i < topics.Length && i < 5; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { Pick(topics[i]); return; }
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0) || Input.GetKeyDown(TalkKey)) Close();
    }

    void Pick(ThanhGiongNpcLore.Topic t)
    {
        pages.Clear();
        foreach (var p in t.pages) pages.Enqueue(ResolvePage(p));
        Next();
    }

    static string ResolvePage(string p)
    {
        if (p.StartsWith("{WHERE:")) { var key = p.Substring(7, p.Length - 8); OpenWorldResource r; if (System.Enum.TryParse(key, out r)) return ThanhGiongNpcLore.WhereIs(r); }
        return p;
    }

    int Revealed(string s) => Mathf.Min(s.Length, Mathf.FloorToInt((Time.unscaledTime - revealStart) * 70f));

    void EnsureStyles(float s)
    {
        int size = Mathf.RoundToInt(22 * s); if (nameStyle != null && size == fontSizeCache) return; fontSizeCache = size;
        nameStyle = new GUIStyle(GUI.skin.label) { font = ThanhGiongUITheme.Medium, fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = ThanhGiongUITheme.Brass } };
        bodyStyle = new GUIStyle(GUI.skin.label) { font = ThanhGiongUITheme.Regular, fontSize = 22, wordWrap = true, normal = { textColor = ThanhGiongUITheme.Paper } };
        choiceStyle = new GUIStyle(GUI.skin.label) { font = ThanhGiongUITheme.Medium, fontSize = 20, wordWrap = false, normal = { textColor = ThanhGiongUITheme.Paper } };
        promptStyle = new GUIStyle(GUI.skin.label) { font = ThanhGiongUITheme.Medium, fontSize = 20, alignment = TextAnchor.MiddleCenter, normal = { textColor = ThanhGiongUITheme.Paper } };
        tagStyle = new GUIStyle(GUI.skin.label) { font = ThanhGiongUITheme.Regular, fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = ThanhGiongUITheme.Muted } };
    }

    void OnGUI()
    {
        float sx = Screen.width / 1280f, sy = Screen.height / 720f;
        var old = GUI.matrix; GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(sx, sy, 1f));
        EnsureStyles(1f);
        var cam = Camera.main;
        if (!IsOpen)
        {
            if (cam != null && player != null && Time.timeScale > 0f)
            {
                foreach (var v in ThanhGiongVillager.All)
                {
                    if (v == null || string.IsNullOrEmpty(v.npcId)) continue;
                    var n = ThanhGiongNpcLore.Get(v.npcId); if (n == null) continue;
                    float dist = Vector3.Distance(cam.transform.position, v.transform.position); if (dist > 14f) continue;
                    Vector3 sp = cam.WorldToScreenPoint(v.transform.position + Vector3.up * 2.6f); if (sp.z <= 0f) continue;
                    float x = sp.x / sx, y = (Screen.height - sp.y) / sy;
                    if (v == target)
                    {
                        ThanhGiongUITheme.ShadowLabel(new Rect(x - 160, y - 34, 320, 28), "[" + TalkKey + "] Nói chuyện · " + n.name, promptStyle);
                    }
                    else ThanhGiongUITheme.ShadowLabel(new Rect(x - 100, y - 4, 200, 22), n.name + " · " + n.title, tagStyle);
                }
            }
            GUI.matrix = old; return;
        }

        // Hộp thoại
        var r = new Rect(70, 410, 960, 228);
        var c = GUI.color; GUI.color = new Color(.07f, .035f, .03f, .82f); GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(ThanhGiongUITheme.Brass.r, ThanhGiongUITheme.Brass.g, ThanhGiongUITheme.Brass.b, .85f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.x, r.yMax - 2, r.width, 2), Texture2D.whiteTexture);
        GUI.color = c;
        ThanhGiongUITheme.ShadowLabel(new Rect(r.x + 26, r.y + 12, 600, 36), npc.name + "  ·  " + npc.title, nameStyle);
        string shown = currentPage.Substring(0, Revealed(currentPage));
        ThanhGiongUITheme.ShadowLabel(new Rect(r.x + 26, r.y + 54, r.width - 52, 90), shown, bodyStyle);
        if (stage == Stage.Pages)
        {
            ThanhGiongUITheme.ShadowLabel(new Rect(r.x + 26, r.yMax - 38, r.width - 52, 28), "[Space] Tiếp tục    [Esc] Thoát", tagStyle);
        }
        else
        {
            float y = r.y + 112;
            for (int i = 0; i < npc.topics.Length && i < 5; i++) { ThanhGiongUITheme.ShadowLabel(new Rect(r.x + 40, y, r.width - 80, 26), "[" + (i + 1) + "] " + npc.topics[i].label, choiceStyle); y += 24; }
            ThanhGiongUITheme.ShadowLabel(new Rect(r.x + 40, y, r.width - 80, 26), "[0] Tạm biệt", choiceStyle);
        }
        GUI.matrix = old;
    }
}
