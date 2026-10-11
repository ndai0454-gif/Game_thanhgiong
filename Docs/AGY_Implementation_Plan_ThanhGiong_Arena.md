# Bàn giao cho AGY: hoàn thiện Thánh Gióng theo kịch bản Arena Combat

Ngày lập: 10/10/2026.

## 1. Yêu cầu triển khai

Hãy triển khai các phần còn thiếu để game có thể chơi trọn một trận chiến 5–10 phút, theo chuỗi:

**Gióng trưởng thành xuất trận → đánh các đợt giặc bằng roi sắt → roi gãy → đến bụi tre và nhổ tre → dùng tre dọn đợt cuối → cinematic ngắn → thắng và chơi lại.**

Hoàn thành vòng chơi này trong **một map chiến trường gần làng Phù Đổng**. Tái sử dụng mã, nhân vật, AI, âm thanh và hiệu ứng hiện có. Làm đến khi có scene chơi được và bằng chứng kiểm tra; không dừng ở việc viết các lớp C# chưa được gắn vào scene.

Nguồn yêu cầu: [ĐỀ XUẤT Ý TƯỞNG GAME – THÁNH GIÓNG](https://docs.google.com/document/d/1MgMaOqSTwACzW-ee7lUgPC2eyA_sHQhJV7v6CPV_b1o/edit?usp=sharing).

Báo cáo hiện trạng: [ScenarioAlignment-2026-10-10.md](D:/School/PRU/Game_thanhgiong/Docs/ScenarioAlignment-2026-10-10.md).

## 2. Dự án và hiện trạng đã kiểm tra

| Mục | Giá trị |
| --- | --- |
| Thư mục dự án | `D:\School\PRU\Game_thanhgiong` |
| Unity thực tế | `6000.6.0f1` |
| Công cụ điều khiển Editor | Unity CLI; dự án đã có `com.unity.pipeline` |
| Scene đang mở lúc kiểm tra | `DinhSocHoaThanh`, ngoài Play Mode |
| Scene khởi đầu trong Build Settings | `LangGiongTienTuyen` |
| Các bản đồ chính | Làng Gióng, Kinh thành, Pháo đài, Thung lũng, Trận tuyến Núi Sóc, Đỉnh Sóc |
| Gameplay trên sáu map chính | Không có CampaignController, địch hoặc collectible tre |
| Prototype chứa gameplay | `AlbionForestMap` |
| Cấu hình prototype | 22 địch đặt sẵn, gồm 1 boss; 1 collectible tre; máu 100; gãy vũ khí sau 7 mạng; ngưỡng thắng 18 mạng |
| Mở đầu prototype | Prologue: thu thập lương thực, rồi trang bị và cưỡi ngựa |
| Camera hiện tại | Camera bám, runtime orthographic isometric |
| Vũ khí hiện tại | Gươm sắt, chưa đúng roi sắt |
| Wave System | Chưa có; bắt đầu Battle đang bật toàn bộ địch trong battleRoot |

Các số liệu là ảnh chụp hiện trạng ngày 10/10/2026. Kiểm tra lại trước khi sửa vì người dùng hoặc agent khác có thể đã thay đổi dự án.

Đã kiểm tra bảy scene đã lưu và chạy thử DinhSocHoaThanh. Chưa kiểm chứng toàn bộ prototype từ đầu đến cuối hoặc bản build; không mặc định các cơ chế prototype đã hoạt động hoàn chỉnh.

## 3. Phạm vi và cách giữ công việc hiện có

- Tạo một scene arena riêng, tên đề xuất `PhuDongArena`, tại `Assets/Scenes/ThanhGiongArena/PhuDongArena.unity`. Đây là đường dẫn đề xuất cho asset mới, chưa phải scene đã tồn tại.
- Giữ các map cũ và prototype để tham khảo/mở rộng. Không cần gắn lại toàn bộ campaign vào cả sáu map để hoàn thành yêu cầu này.
- Đưa arena thành scene khởi đầu của bản demo. Nếu dùng Build Profile riêng cho arena, giữ cấu hình bản cũ; nếu chỉnh Build Settings, bảo toàn các entry cũ và ghi rõ thay đổi.
- Giữ các chỉnh sửa của người dùng trong working tree. Đọc trạng thái Git trước khi sửa; không reset hoặc ghi đè hàng loạt scene/prefab/material.
- Người dùng đã yêu cầu sửa ngựa và giữ chân thuộc model gốc. **Giữ nhân vật/ngựa hiện có làm mặc định**, không tự thay toàn bộ bằng nhân vật khác hoặc thêm bốn chân giả. Ngựa không bắt buộc trong concept nhưng là tài sản người dùng đang phát triển.
- Nếu dùng nhân vật cưỡi ngựa trong arena, kiểm tra thân ngang, cả bốn chân, hai bên trái/phải và tiếp đất. Phần sửa rig hiện có cần được kiểm tra lại; chưa coi animation đã hoàn tất chỉ vì có `rigVersion = 5`.
- Ưu tiên vòng chơi bắt buộc. Dodge đã có có thể giữ. Boss là tùy chọn. Không mở rộng thêm map, skill tree, thu thập lương thực hay nghi thức trang bị trước khi arena hoàn chỉnh.
- Khi Unity Editor đang mở, dùng Unity CLI và Unity Editor API để tạo/sửa scene, prefab, material. Không sửa trực tiếp YAML của các asset đang được Editor quản lý.

## 4. Các phần nên tái sử dụng

Các đường dẫn bên dưới tính từ thư mục dự án.

| File/asset hiện có | Vai trò | Điểm cần chỉnh hoặc kiểm tra |
| --- | --- | --- |
| `Assets/Code/Player/MountedHorseController.cs` | Di chuyển, chạy, nhảy, dodge, animation và tấn công dự phòng | Chỉ một nguồn xử lý tấn công; không để tấn công dự phòng chạy song song với combat arena. |
| `Assets/Code/Player/ThanhGiongCampaignController.cs` | Máu, melee, vũ khí, gãy gươm, tre, thua, chơi lại, kết thúc | Hiện gắn chặt với campaign nhiều chapter và chuyển map. Cần chế độ arena riêng. |
| `Assets/Code/Enemies/ThanhGiongEnemy.cs` | AI, máu địch, đòn đánh, phản ứng, chết | `Initialize(owner, player)` nhận CampaignController. Cần nối chết địch với bộ quản lý wave mà không đếm trùng. |
| `Assets/Code/Scripts/ThanhGiongCollectible.cs` | Collectible lương thực/tre | Hiện OnTriggerEnter gọi TryCollect. Trong arena, vào vùng tre không được tự tính là nhổ tre. |
| `Assets/Code/Scripts/ThanhGiongCampaignVFX.cs` | Hiệu ứng đánh, gãy vũ khí, nhổ tre và kết thúc | Tái sử dụng nhưng sửa tên/hình ảnh gươm thành roi khi cần. |
| `Assets/Code/Scripts/ThanhGiongCampaignAudio.cs` | Âm thanh combat và sự kiện | Kiểm tra phát đúng thời điểm, một lần mỗi sự kiện. |
| `Assets/Code/UI/ThanhGiongCampaignHUD.cs` | HUD, pause, hướng dẫn, restart | UI arena chỉ hiển thị thông tin trận đấu; không dùng danh sách chapter cũ làm luồng chính. |
| `Assets/Code/Scripts/IsometricCameraFollow.cs` | Camera bám và các shot | Thêm/cấu hình chế độ camera phù hợp arena, giới hạn thay đổi trong arena. |
| `Assets/Code/Systems/ThanhGiongSceneTransition.cs` | Fade, tải scene và điều hướng demo | Không để arena tự chuyển sang sáu map hoặc bị phím demo bỏ qua tiến trình. |
| `Assets/Scenes/AlbionForestMap.unity` | Prototype chứa đủ nhiều hệ thống nhất | Có thể sao chép thành scene mới qua Editor API, rồi bỏ qua Prologue/Preparation riêng trong bản sao. |
| `Assets/Prefabs/Enemies/KayKit_Barbarian_Soldier.prefab` | Prefab địch | Kiểm tra collider, Rigidbody, AI, material và animation trước khi spawn. |
| `Assets/Prefabs/Enemies/KayKit_Knight_Boss_An.prefab` | Boss tùy chọn | Chỉ bật sau khi luồng đợt cuối đã ổn định. |
| `Assets/Prefabs/Player/ThanhGiong_Mounted.prefab` | Nhân vật/ngựa người dùng | Giữ model hiện có; kiểm tra gắn vũ khí và animation khi chuyển sang arena. |
| `Assets/Code/Player/MountedHorseMeshMotion.cs` và `Assets/Editor/MountedHorseMeshRigBuilder.cs` | Rig và gait ngựa gốc | Kiểm tra thực tế bốn chân; không tự rebuild mọi scene để sửa arena. |

### Các điểm dễ làm sai

1. `WarpToChapter(Battle)` đang khởi tạo mọi địch trong battleRoot. Trong arena, wave manager phải là nơi quyết định địch nào được sinh và hoạt động.
2. `NotifyEnemyDefeated` đang tự gãy gươm theo số mạng và tự chuyển màn ở ngưỡng thắng. Vô hiệu hóa các điều kiện cũ **riêng trong arena** khi tiến trình đã chuyển sang wave.
3. `RestartBattle` đang bật/reset toàn bộ địch đặt sẵn. Arena cần reset theo wave, không tái tạo tất cả đợt cùng lúc.
4. `CompleteStage3AndTransition` đang đi sang map khác. Arena phải chạy cinematic và vào màn thắng tại cùng scene.
5. `ThanhGiongSceneTransition` có instance sống qua scene và phím F5/F6/F8. Không để các phím này phá vòng chơi arena trong bản demo phát hành.
6. `LegendCheckpoint` có thể phục hồi tiến trình cũ, `LegendSeedSkills` có thêm cơ chế trồng cây. Không để chúng tự can thiệp vào một trận arena mới; xử lý theo cấu hình arena, giữ chức năng ở bản cũ.
7. `KayKitWorldMapBuilder` có auto-build theo `Application.dataPath` và phiên bản trong EditorPrefs, có thể ghi lại scene và Build Settings. `MountedThanhGiongAssetBuilder` cũng có auto-build. Giới hạn các builder vào output của chúng, bảo đảm mở lại Editor không ghi đè arena hoặc đổi scene khởi đầu.
8. Enum vũ khí và các tham chiếu prefab đã được serialize. Đổi tên sang roi sắt phải giữ giá trị đã lưu hoặc có migration; không thay thứ tự enum làm asset cũ đổi nghĩa.

## 5. Thiết kế luồng arena

Đề xuất thêm một bộ điều khiển tiến trình arena và một bộ điều khiển wave. Tên lớp có thể thay đổi cho phù hợp dự án. Có thể thêm chế độ arena vào CampaignController để tái sử dụng combat và hợp đồng AI; không cần viết lại AI/máu từ đầu.

Các trạng thái cần thể hiện rõ:

```text
Intro
  → IronCombat: đợt 1, đợt 2
  → BreakingIronRod
  → SeekingBamboo
  → BambooCombat: đợt cuối
  → VictoryCinematic
  → Victory

Trạng thái đang chơi → Defeat khi máu bằng 0
Defeat hoặc Victory → Restart → Intro
```

- Một hệ thống sở hữu trạng thái trận đấu; HUD, wave, combat và cinematic đọc hoặc nhận sự kiện từ hệ thống đó.
- Chỉ wave manager được sinh địch; chỉ arena flow quyết định gãy roi, mở đợt tre và thắng.
- Số mạng là thống kê, không thay thế điều kiện sạch wave.
- Không chạy đồng thời hai vòng Update cùng nhận chuột trái/E hoặc hai điều kiện tự chuyển tiến trình.
- Dữ liệu đợt, thời gian chờ, máu và sát thương phải chỉnh được trong Inspector hoặc asset cấu hình; tránh hard-code vào logic chuyển trạng thái.

## 6. Công việc và tiêu chí nghiệm thu

### P0-1 — Scene arena và xuất trận

- [ ] Tạo scene mới từ môi trường/asset hiện có, với spawn người chơi, trung tâm giao chiến, spawn địch, bụi tre và ranh giới arena.
- [ ] Gióng xuất hiện trưởng thành, đủ máu và cầm roi sắt ngay từ đầu; không phải nhặt lương thực hoặc làm QTE trang bị.
- [ ] Gắn đủ player controller, combat, wave manager, flow controller, camera, HUD, audio và VFX.
- [ ] Có hướng dẫn ngắn: WASD, tấn công, E để nhổ tre, pause; giữ Shift/dodge nếu đang sử dụng.

**Đạt khi:** mở scene và Play có thể di chuyển, camera bám đúng, nhìn rõ người chơi/vũ khí, bắt đầu được đợt đầu mà không dùng lệnh debug để nhảy chapter.

### P0-2 — Wave System

- [ ] Dùng prefab địch hiện có, spawn tại các điểm hợp lệ và khởi tạo target/owner.
- [ ] Cấu hình khởi đầu đề xuất: 3 đợt, lần lượt 6 / 8 / 8 địch. Đây là giá trị thử nghiệm, phải cân bằng bằng chơi thực tế; không coi 22 địch tự bảo đảm 5–10 phút.
- [ ] Đợt mới chỉ bắt đầu khi đợt hiện tại đã sinh đủ địch và không còn địch sống.
- [ ] Sau đợt 2, chuyển sang sự kiện gãy roi; đợt cuối chỉ mở sau khi nhổ tre thành công.
- [ ] Quản lý danh sách địch sống và thông báo chết đúng một lần. Có thể bổ sung sự kiện chết tại hàm chết hiện có hoặc adapter rõ ràng.
- [ ] Không tính disable do restart/despawn là một mạng; không để callback địch của trận cũ trừ địch của trận mới.
- [ ] Khi pause, chết, cinematic hoặc restart, hủy/tạm dừng đúng coroutine sinh quân và thời gian chờ.
- [ ] Spawn không chồng lên người chơi, trong tường hoặc ngoài vùng đi được. Địch còn sống nhưng bị kẹt phải có cách phục hồi về điểm hợp lệ, không âm thầm cho thắng.

**Đạt khi:** hạ hết từng đợt mới chuyển tiếp; một địch còn sống giữ đợt ở trạng thái chưa hoàn thành; restart trong lúc đang spawn không tạo địch trùng.

### P0-3 — Roi sắt và sự kiện gãy

- [ ] Thay model/slot vũ khí chiến đấu trong arena thành roi sắt, cập nhật UI và thông báo tương ứng.
- [ ] Tái sử dụng hit detection và melee hiện có, nhưng kiểm tra hướng đánh, tầm đánh và animation phù hợp roi.
- [ ] Kiểm tra vũ khí nằm đúng tay; nếu model gốc có vũ khí dính trong mesh, giải quyết phần hiển thị để tránh hai vũ khí chồng nhau.
- [ ] Sau khi sạch đợt sắt cuối cùng, phát sự kiện gãy một lần, tắt vũ khí sắt và chặn melee bằng roi.
- [ ] Hiển thị mục tiêu “Đến bụi tre và nhấn E để nhổ tre”; giữ di chuyển/camera hoạt động.
- [ ] Không dùng điều kiện `Kills >= swordBreakKills` cũ để làm gãy roi giữa một đợt arena.

**Đạt khi:** roi chỉ gãy tại mốc đúng; sau khi gãy người chơi không gây sát thương bằng roi vô hình; sự kiện không lặp khi callback chết bị gửi lại.

### P0-4 — Nhổ tre và đợt cuối

- [ ] Bụi tre nằm ở cạnh chiến trường, có collider/vùng tương tác và chỉ dẫn dễ thấy.
- [ ] Đến gần tre hiện hướng dẫn E; ở xa hoặc sai trạng thái thì không nhổ được.
- [ ] Giữ cơ chế E ba lần hiện có làm mặc định; các lần nhấn phải có phản hồi và không tăng bởi OnTriggerEnter hoặc giữ phím.
- [ ] Chỉ tính tương tác từ bụi tre hợp lệ; hoàn tất thì ẩn/tắt phần tre đã nhổ theo logic rõ ràng.
- [ ] Gắn vũ khí tre vào tay, bật melee tre, tắt roi và đổi hướng dẫn.
- [ ] Mở đợt cuối một lần sau khi trang bị tre xong; chưa lấy tre thì không mở đợt cuối.

**Đạt khi:** có thể đi đến bụi tre, nhấn E theo hướng dẫn, thấy tre trên tay và đánh đợt cuối. Không thể bỏ qua tương tác chỉ bằng bước vào collider.

### P0-5 — Máu, AI, thua và reset trận

- [ ] Địch truy đuổi, tấn công và gây sát thương; người chơi đánh trúng làm giảm máu địch.
- [ ] HUD hiển thị máu người chơi, số đợt, địch còn sống, vũ khí và mục tiêu hiện tại.
- [ ] Khi máu bằng 0: vào Defeat, chặn điều khiển chiến đấu và dừng tiến trình wave; không kích hoạt thắng cùng lúc.
- [ ] Có nút/phím chơi lại cho cả thua và thắng.
- [ ] Restart phục hồi máu, vị trí, camera, roi sắt, tre tương tác, wave index, số mạng, input và `Time.timeScale`.
- [ ] Hủy đòn đánh/cinematic/spawn đang chờ; dọn địch, vật thể và hiệu ứng runtime của trận cũ.
- [ ] Không phục hồi checkpoint của hành trình cũ vào trận arena mới.

**Đạt khi:** chết rồi chơi lại và thắng rồi chơi lại đều bắt đầu một trận mới sạch. Thử ít nhất hai lần restart liên tiếp, gồm một lần khi đang pause.

### P0-6 — Thắng và cinematic

- [ ] Thắng chỉ khi đã lấy tre, đợt cuối đã sinh đủ địch và số địch sống của đợt cuối bằng 0.
- [ ] Nếu bật boss trong đợt cuối, boss phải thuộc danh sách địch cần hạ; không thắng khi boss còn sống.
- [ ] Chạy cinematic ngắn ngay trong arena; có thể tái sử dụng camera/VFX/âm thanh của FlyToSky nhưng không bắt buộc thêm map hay QTE cởi giáp.
- [ ] Trong cinematic, chặn input combat và AI; kết thúc chuyển sang Victory đúng một lần.
- [ ] Hiển thị thắng, thời gian trận và lựa chọn chơi lại; không tự chuyển sang Kinh thành/Pháo đài/Đỉnh Sóc.

**Đạt khi:** một vòng chơi bình thường tự đi đến màn thắng; ngưỡng 18 mạng cũ không cho kết thúc sớm; restart sau cinematic trả lại camera và điều khiển.

### P1-1 — Camera, hình ảnh và môi trường

- [ ] Cấu hình camera arena theo góc nhìn thứ ba, mặc định perspective bám nhân vật; chỉ áp dụng cho arena, giữ camera cũ cho map cũ.
- [ ] Khung hình thấy được người chơi, vũ khí và vùng địch áp sát; xử lý vật cản để không bị cây/nhà che lâu.
- [ ] Có trung tâm giao chiến đủ rộng, tre ở mép, cây/đá/địa hình và một vài nhà mang hình thức Việt Nam ở xa. Tận dụng asset phù hợp; không chỉ đổi tên công trình Medieval rồi coi đã đạt.
- [ ] Hướng đi đến tre dễ nhận ra; spawn địch và ranh giới không phá bố cục.
- [ ] Nếu giữ ngựa, kiểm tra idle/walk/run từ cả hai bên: thân ngang, bốn chân di chuyển đúng, móng tiếp đất, không còn chân giả hoặc xoắn chân trước.
- [ ] Animation đánh bằng roi và tre đọc được; không để model vũ khí/model cưỡi ngựa che sự kiện đổi vũ khí.

**Đạt khi:** chụp và xem ảnh/video xuất trận, chiến đấu bằng roi, roi gãy, tương tác tre, chiến đấu bằng tre và màn thắng; không có lỗi hiển thị cản trở chơi.

### P1-2 — Thời lượng và bản demo

- [ ] Đưa arena thành điểm bắt đầu bản demo; giới hạn điều hướng debug trong cấu hình demo.
- [ ] Chơi thử toàn bộ bằng điều khiển bình thường và đo thời gian từ xuất trận tới thắng.
- [ ] Cân bằng số địch, máu/sát thương, thời gian giữa đợt và quãng đường đến tre để một lượt chơi bình thường khoảng 5–10 phút.
- [ ] Build cho nền tảng hiện được cấu hình của dự án; ghi rõ nền tảng và đường dẫn output.
- [ ] Chạy bản build và kiểm tra lại spawn, input, camera, UI, gãy roi, tre, thắng/thua và chơi lại.

**Đạt khi:** bản build mở thẳng arena và có thể chơi trọn trận; có số đo thời lượng thực tế. Không dùng lượt tự động hạ địch để kết luận thời lượng người chơi.

## 7. Thứ tự thực hiện

1. Ghi nhận trạng thái Git/Unity; kiểm tra lại prototype và các builder tự động.
2. Tạo scene arena và nối player/combat/AI/HUD để có một đợt đánh được.
3. Làm wave manager và reset trận.
4. Đổi sang roi sắt, nối gãy roi → nhổ tre → mở đợt cuối.
5. Nối thắng, cinematic, màn kết quả và chơi lại.
6. Hoàn thiện camera, animation và môi trường trong phạm vi arena.
7. Chạy kiểm thử, đo thời lượng, build và chơi xác nhận.

Hoàn thành một vòng chơi tối thiểu trước khi tăng số địch, bật boss hoặc bổ sung hiệu ứng.

## 8. Kiểm thử bắt buộc

Các test hiện có phản ánh nhiều yêu cầu của bản campaign/KayKit trước đây. Chạy và ghi nhận baseline; không xóa hoặc nới assert chỉ để báo xanh. Test không còn đúng hợp đồng phải được cập nhật có lý do; lỗi ở map cũ cần phân biệt với lỗi arena mới.

Các test assembly hiện dùng cách tìm type/reflection và chưa tham chiếu trực tiếp Assembly-CSharp. Theo cách kiểm thử đang có hoặc bổ sung reference hợp lệ; không refactor toàn bộ assembly của dự án chỉ để thêm test arena.

| Tình huống | Kết quả cần chứng minh |
| --- | --- |
| Bắt đầu trận | Gióng trưởng thành, đầy máu, roi sắt, đợt 1; không có Prologue. |
| Còn một địch trong wave | Không chuyển đợt. |
| Spawn đợt chưa hoàn tất | Chưa coi wave đã sạch dù tạm thời không có địch sống. |
| Thông báo một địch chết hai lần | Chỉ đếm một lần, không làm số địch âm. |
| Sạch đợt sắt cuối | Roi gãy một lần, vào SeekingBamboo, chưa sinh đợt cuối. |
| Bước vào vùng tre / E ở xa | Không tự nhận tre, không tăng tiến độ sai. |
| Hoàn thành tương tác tre | Nhận tre và mở đúng một đợt cuối. |
| Đã đủ ngưỡng mạng cũ nhưng còn địch cuối | Không thắng. |
| Máu bằng 0 khi đang spawn hoặc đánh | Defeat; không còn spawn/đòn đánh muộn làm đổi trạng thái. |
| Sạch đợt cuối | Cinematic rồi Victory; không tải map hành trình. |
| Restart giữa trận / khi thua / sau thắng | Trận mới sạch, tre và roi phục hồi, camera và input hoạt động. |
| Pause rồi restart | `Time.timeScale = 1`, không kẹt màn kết quả hoặc camera. |

Test logic có thể dùng spawn/death giả lập để nhanh và ổn định; vẫn cần một lượt chơi thật trong Editor và một lượt trong bản build. Kiểm tra compile/console, missing reference/script và hình ảnh thực tế. Không dùng số vertex thay đổi hoặc góc bone thay đổi làm bằng chứng duy nhất cho chất lượng animation ngựa.

## 9. Kết quả AGY cần bàn giao

- [ ] Scene arena đã lưu, prefab/config cần thiết và các tham chiếu đầy đủ.
- [ ] Vòng chơi bốn giai đoạn hoạt động bằng điều khiển bình thường.
- [ ] Build Settings/Build Profile khởi đầu đúng arena và không bị auto-builder đổi lại sau khi mở Editor.
- [ ] Kết quả test, log kiểm tra lỗi và ảnh/video các mốc chính.
- [ ] Bản build chơi được; đường dẫn output và nền tảng.
- [ ] Thời lượng lượt chơi thật và các tham số đã cân bằng.
- [ ] Ghi lại file thay đổi, phần hoàn thành, phần còn thiếu hoặc chưa kiểm chứng.

Tạo báo cáo kết quả cạnh file này, tên đề xuất `AGY_Implementation_Result_ThanhGiong_Arena.md`. Cập nhật checklist sau mỗi mốc hoàn thành. Chỉ đánh dấu hoàn tất khi tính năng đã được gắn vào scene, lưu asset và kiểm tra đúng tiêu chí.
