# Đối chiếu game với kịch bản Thánh Gióng

Ngày kiểm tra: 10/10/2026.

Nguồn yêu cầu: [ĐỀ XUẤT Ý TƯỞNG GAME – THÁNH GIÓNG](https://docs.google.com/document/d/1MgMaOqSTwACzW-ee7lUgPC2eyA_sHQhJV7v6CPV_b1o/edit?usp=sharing).

## Kết luận

Game đã có nền tảng Unity 3D, hình ảnh low poly, điều khiển WASD và camera bám nhân vật. Phần lớn cơ chế chiến đấu được viết trong scene thử nghiệm AlbionForestMap, nhưng sáu bản đồ chính đang thiếu hệ thống gameplay để thực hiện kịch bản. Chưa thể coi bản hiện tại là một vòng chơi Arena Combat hoàn chỉnh.

Kịch bản cần một chiến trường, Gióng trưởng thành ngay từ đầu, chiến đấu bằng roi sắt qua các đợt quân, roi gãy, nhổ tre, đánh hết đợt cuối và kết thúc bằng cinematic ngắn. Dự án hiện thiên về hành trình nhiều bản đồ, cưỡi ngựa, thu thập lương thực và trang bị.

## Phạm vi và bằng chứng

- Đọc toàn bộ nội dung tài liệu được Google Drive trả về.
- Kiểm tra bảy scene bật trong Build Settings bằng Unity CLI, qua preview scene; giữ nguyên scene đang mở.
- Chạy thử scene DinhSocHoaThanh đang mở, kiểm tra thành phần runtime và chụp Game View; sau đó dừng Play Mode.
- Đối chiếu mã điều khiển, chiến đấu, AI, tương tác tre, chuyển màn và kết thúc.
- Chưa chơi toàn bộ AlbionForestMap từ đầu đến cuối, chưa kiểm chứng bản build và thời lượng 5–10 phút. Những cơ chế chỉ thấy trong mã và cấu hình được đánh dấu là có nền tảng, không khẳng định đã hoạt động hoàn chỉnh.

Báo cáo số liệu: `Temp/ScenarioAlignment/scenes.json` và `Temp/ScenarioAlignment/runtime-current-scene.json`.

## Những phần đã khớp hoặc đã có nền tảng

| Yêu cầu | Trạng thái hiện tại | Nhận xét |
| --- | --- | --- |
| Unity, game 3D, stylized/low poly | Đã khớp nền tảng | Scene và Game View xác nhận mô hình 3D, cây, đá, địa hình phong cách low poly. |
| Di chuyển WASD | Đã có | Horizontal dùng A/D, Vertical dùng W/S. Nhân vật dùng CharacterController; các scene chính có controller hoạt động. |
| Camera góc nhìn thứ ba | Khớp một phần | Có camera bám nhân vật. Runtime hiện là orthographic isometric, góc nhìn cao và hướng cố định. Cần thống nhất nếu muốn camera sau lưng nhân vật. |
| Tấn công bằng vũ khí sắt | Có nền tảng, khác vũ khí | Campaign sử dụng IronSword / “Gươm Sắt”, chưa đúng roi sắt trong tài liệu. Mã melee và sát thương đã có; sáu map chính không có campaign hoặc địch để thực hiện chuỗi chiến đấu. |
| Sự kiện vũ khí sắt gãy | Có trong prototype | AlbionForestMap đặt ngưỡng 7 địch; mã chuyển vũ khí sang None và phát hiệu ứng gãy. Sự kiện hiện dựa vào số mạng, chưa dựa vào kết thúc một đợt địch. |
| Đến bụi tre và nhổ tre | Có trong prototype | Có một collectible tre được lưu trong AlbionForestMap. Mã tìm tre ở gần, xử lý E ba lần và đổi sang Bamboo. Sáu map chính không có tre tương tác. |
| Tấn công bằng tre | Có trong prototype | Có melee tre, quét rộng, sát thương và hiệu ứng riêng; chưa nối vào các bản đồ chính. |
| Máu, sát thương, thua và chơi lại | Có trong prototype | AlbionForestMap đặt máu tối đa 100; mã DamagePlayer, vô hiệu hóa di chuyển khi chết, R để chơi lại và checkpoint. Các map chính không có CampaignController/HUD này. |
| Enemy AI đuổi theo và tấn công | Có trong prototype | Có mã AI truy đuổi, vây quanh, cảnh báo đòn đánh và gây sát thương. AlbionForestMap đặt 22 địch, gồm một boss; sáu map chính có 0 địch. |
| Điều kiện thắng | Có nhưng chưa đúng vòng chơi | Prototype dùng 18 mạng và đang cầm tre để chuyển tiếp. Không yêu cầu dọn sạch đợt cuối, cũng không kiểm tra boss đã chết. |
| Cinematic kết thúc | Có nền tảng khác cách kết thúc | Có FlyToSky kéo dài 8,5 giây và trạng thái Complete, nhưng đi qua bước chuyển màn/Ascension và QTE cởi giáp. Các map chính không có campaign để kích hoạt. |
| Dodge và mini-boss tùy chọn | Đã có nền tảng | Controller có dodge; prototype đặt một boss. Đây là phần bổ sung, không thay cho vòng chiến đấu bắt buộc. |

## Những phần chưa khớp

1. **Wave System chưa có.** Không tìm thấy bộ quản lý sinh quân theo từng đợt trong mã gameplay. Khi bắt đầu Battle, mã bật battleRoot và khởi tạo toàn bộ địch đã đặt sẵn. Tăng áp lực theo số mạng không tương đương với sinh đợt mới sau khi dọn đợt trước.
2. **Chưa gom thành một đấu trường.** Build Settings gồm sáu map chính và một prototype. Thứ tự khởi đầu là LangGiongTienTuyen, không phải một trận chiến gần làng Phù Đổng.
3. **Mở đầu khác tài liệu.** AlbionForestMap bắt đầu ở Prologue với thu thập lương thực, sau đó trang bị/cưỡi ngựa rồi mới chiến đấu. Tài liệu bắt đầu khi Gióng đã trưởng thành và chuẩn bị xuất trận.
4. **Gươm sắt thay cho roi sắt.** Cần chỉnh cả model, cách đánh, tên vũ khí và lời hướng dẫn.
5. **Thắng chưa gắn với dọn đợt cuối.** 18 mạng trong số 22 địch không bảo đảm chiến trường đã sạch. Mã hiện chuyển tiếp thay vì kết thúc một trận arena ngay bằng cinematic.
6. **Môi trường chiến trường chưa được tập trung theo yêu cầu.** Cây, đá, địa hình và kiến trúc đã có, nhưng chưa thấy một map chính kết hợp trung tâm giao chiến với bụi tre tương tác. Một số công trình đang dùng asset Medieval/KayKit; chưa xác nhận hình thức nhà truyền thống Việt Nam.
7. **Chưa có bằng chứng thời lượng 5–10 phút và vòng chơi hoàn chỉnh trong bản build.**

## Tình trạng từng scene

| Scene | CampaignController | Địch đặt sẵn | Tre tương tác | Nhận xét |
| --- | ---: | ---: | ---: | --- |
| LangGiongTienTuyen | 0 | 0 | 0 | Có nhân vật cưỡi ngựa, môi trường và camera. Là scene khởi đầu trong Build Settings. |
| KinhThanhRenThep | 0 | 0 | 0 | Có nhân vật cưỡi ngựa, môi trường và camera. |
| PhaoDaiNgamQuanAn | 0 | 0 | 0 | Có AdventurerController cho bộ chiến, môi trường và camera. |
| ThungLungVuotSong | 0 | 0 | 0 | Có nhân vật cưỡi ngựa, môi trường và camera. |
| TranTuyenNuiSoc | 0 | 0 | 0 | Chưa có trận đánh thực hiện kịch bản. |
| DinhSocHoaThanh | 0 | 0 | 0 | Chạy thử cũng xác nhận 0 campaign, 0 địch, 0 tre tương tác, 0 HUD chiến đấu. |
| AlbionForestMap | 1 | 22, gồm 1 boss | 1 | Scene chứa nền tảng gameplay đầy đủ nhất, nhưng mở đầu Prologue và chưa có Wave System. |

Các zone “Khởi hành”, “Thử thách môi trường”, “Điểm nhìn toàn cảnh”, “Đấu trường cao trào” trên map chính hiện chỉ đổi góc camera khi đi vào. Tiêu đề/storyPurpose không tạo nhiệm vụ hay tự kích hoạt chiến đấu.

## Thứ tự nên làm để bám kịch bản

1. Chọn một scene làm arena gần Phù Đổng; đặt Gióng trưởng thành, camera và gameplay hoàn chỉnh tại đó, đưa scene này lên đầu Build Settings.
2. Tái sử dụng AI, máu, melee, tre và hiệu ứng đang có trong prototype; đổi vũ khí sắt sang roi sắt.
3. Làm Wave System và nối bốn trạng thái: xuất trận → đánh bằng roi → roi gãy/nhổ tre → đánh bằng tre.
4. Chỉ kích hoạt thắng sau khi dọn sạch đợt cuối; chạy cinematic ngắn rồi cho chơi lại.
5. Chơi kiểm tra toàn bộ vòng 5–10 phút trong bản build; tiếp tục hoàn thiện animation và môi trường trong phạm vi arena.

Không cần xóa các map hoặc ngựa hiện có để làm bản arena. Có thể giữ chúng phục vụ bản mở rộng sau khi vòng chơi bắt buộc hoàn chỉnh.

## Mã dùng để đối chiếu

- `Assets/Code/Player/MountedHorseController.cs`: di chuyển, chạy, nhảy, dodge, tấn công dự phòng.
- `ProjectSettings/InputManager.asset`: WASD.
- `Assets/Code/Scripts/IsometricCameraFollow.cs`: camera bám và chiếu isometric.
- `Assets/Code/Player/ThanhGiongCampaignController.cs`: chapter, vũ khí, gãy gươm, nhổ tre, sát thương, chết, chơi lại và kết thúc.
- `Assets/Code/Enemies/ThanhGiongEnemy.cs`: AI và sát thương địch.
- `Assets/Code/Scripts/ThanhGiongStoryBeatZone.cs`: zone chỉ đổi shot camera.
- `Assets/Code/Systems/ThanhGiongSceneTransition.cs`: chuyển giữa nhiều scene.
- `Assets/Scenes/AlbionForestMap.unity`: cấu hình prototype, ngưỡng gãy 7, ngưỡng thắng 18, máu 100, startingChapter Prologue.
- `Tools/UnityDiagnostics/ScenarioAlignmentAudit.cs`: thao tác kiểm tra scene đã lưu và trạng thái runtime.
