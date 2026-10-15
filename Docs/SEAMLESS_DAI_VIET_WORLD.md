# Thánh Gióng — Đại Việt liền mạch

Scene chơi chính: `Assets/Scenes/ThanhGiongWorld/ThanhGiongDaiViet.unity`. Mở scene này trong Unity rồi Play. Bốn chương cùng tồn tại trong một lục địa khoảng **730 × 720 m**, không tải scene khác khi đi qua vùng hoặc hoàn thành chương. Sáu scene gốc được giữ nguyên để đối chiếu; Build Settings chỉ bật scene mới.

## Chơi và lưu hành trình

- **WASD / Shift / Space / C hoặc Alt:** đi, phi ngựa, nhảy, né như trước.
- **M:** bản đồ toàn lục địa; bấm vùng xem quy tắc và ba loại tài nguyên. M hoặc nút Đóng để quay lại. Escape cũng đóng bản đồ. Bản đồ tạm dừng mô phỏng.
- **G:** thu thập tài nguyên ở gần, đúc vật tư hoặc sửa đồ tại lò rèn.
- **B:** cất tài nguyên vào kho làng khi đứng gần lò rèn.
- **F9** hoặc nút Lưu hành trình trong menu Escape: lưu. Game cũng lưu khi đổi vùng hoặc thay đổi tài nguyên.
- **E → Q → E → F:** nhận trang bị vua ban tại lò rèn. **E × 3:** nhổ tre khi thương gãy. **E → Q → E:** nghi thức hóa thánh tại đỉnh Sóc.

Tiến trình: gom đủ 300 lương thực → thu 6 quặng sắt + 3 tre/gỗ tại các vùng xanh → trở về lò rèn nhấn G → hoàn tất nghi thức trang bị → đi qua vùng vàng/đỏ/đen dẹp đủ 57 địch, thương gãy và nhổ tre → tự đi đến khu nghi thức đỉnh Sóc → hóa thánh. Đường chiến tuyến mở sau khi trang bị; nghi thức đỉnh Sóc mở sau chiến thắng. Không có cổng tải scene hoặc dịch chuyển khi đổi chương. Hồi sinh và nút Về làng đưa nhân vật về khu an toàn trong cùng scene.

Save mới: `Application.persistentDataPath/ThanhGiongOpenWorld.v1.json`, có bản `.bak`. Lưu chương, trang bị, lương thực, vị trí, kỹ năng, kinh tế, địch đã hạ và vật phẩm cốt truyện. Không dùng/xóa checkpoint PlayerPrefs của sáu scene cũ. Lưu/về làng bị chặn trong cảnh bay hóa thánh để tránh gián đoạn đoạn kết. Tệp `ThanhGiongOpenWorld.verification.json` chỉ dùng kiểm tra tự động.

## Quy hoạch 13 vùng

| Mức rủi ro | Địa danh | Quy tắc |
|---|---|---|
| An toàn | Làng Phù Đổng / Kinh thành Phong Châu, lò rèn | Không nhận sát thương; rèn và kho làng |
| Xanh | Rừng Tre Đầu Làng, Bờ Sông Hồng, Đầm Sen Văn Lang, Gò Đá Đông Làng, Mỏ Sắt Phong Châu | Không nhận sát thương; thu thập sơ cấp |
| Vàng | Đèo Quặng Tranh Chấp, Đồi Canh Tiền Tiêu, Đồng Lúa Bị Cướp | Gục ngã: choáng 2 giây, về làng, mất 15 điểm độ bền; giữ tài nguyên |
| Đỏ | Pháo Đài Quân Ân, Thung Lũng Vượt Sông | Mất tài nguyên đang mang, mất 20 điểm độ bền; giữ kho và trang bị cốt truyện |
| Đen | Đại Bản Doanh Giặc Ân, Núi Sóc / Đường Hóa Thánh | Mất tài nguyên đang mang, mất 25 điểm độ bền; giữ kho và trang bị cốt truyện |

Trung tâm nối năm nhánh sinh thái, các nhánh có đường vòng liên kết thành mạng: **18 đường nối** với lối hẹp, biển chỉ đường và mốc màu. Mặt đất/đường giữ trong một scene; không dùng additive scene streaming khi chơi. Quái ngoài vùng hiện tại được ngủ AI/physics, giữ nguyên máu, hủy đòn đang báo hiệu; không tắt/bật GameObject làm hồi đầy máu.

## Sinh thái và kinh tế

| Sinh thái | Chính | Phụ | Hiếm |
|---|---|---|---|
| Rừng | Tre/gỗ | Da thú | Đá/than |
| Núi | Quặng sắt | Đá/than | Sợi |
| Cao nguyên | Đá/than | Quặng sắt | Tre/gỗ |
| Đồng lúa | Da thú | Sợi | Quặng sắt |
| Đầm lầy | Sợi | Tre/gỗ | Da thú |

**120 điểm thu thập**, mỗi vùng ngoài trung tâm có 6 điểm chính, 3 điểm phụ, 1 điểm hiếm. Sản lượng cơ sở mỗi lần tương ứng 6:3:1, hồi lại sau 45 giây; vùng vàng/đỏ/đen nhân lần lượt 2/3/4. Lương thực/gạo/cà tiếp tục dùng hệ thu thập cốt truyện hiện có. Sửa trang bị cần 2 quặng + 1 đá/than; độ bền thấp giảm sát thương cận chiến xuống tối thiểu 55%.

Nhãn T1–T8 hiện biểu thị mức vùng, sản lượng và độ khó, **chưa là các phẩm cấp vật phẩm riêng trong kho**. Tre/gỗ và đá/than là hai nhóm tài nguyên gộp; chưa có hệ săn thú/khai thác than riêng, giao thương nhiều người hoặc PvP/full-loot trang bị. Đây là cách chuyển mô hình Albion sang chiến dịch đơn người chơi của Thánh Gióng.

## Bằng chứng kiểm tra ngày 15/10/2026

- Unity `Game_thanhgiong@a7c0dbbb`, 6000.6.0f1: biên dịch thành công, Console không có error. Một nhân vật, camera, HUD, nguồn sáng và hệ VFX chính.
- `OpenWorldVerification/build-report.txt`: 13 vùng, 18 đường nối, 120 điểm tài nguyên, 57 địch, 966 cụm trang trí; tất cả SHA256 của sáu scene gốc không đổi. Cây/đá/cỏ dùng tài sản sẵn có và gộp mesh theo material.
- `OpenWorldVerification/road-audit.txt`: **554 mẫu có nền đỡ, 0 thiếu nền, 0 vật cản ở giữa đường**, sau lần tạo map cuối.
- `OpenWorldVerification/runtime-results.txt`: **75 PASS / 0 FAIL**, gồm ma trận tài nguyên, điểm thu thật, rèn/sửa/kho, an toàn, gục ngã từng mức rủi ro, bản đồ tạm dừng và giữ nguyên scene. Đã chạy lại sau tích hợp cuối.
- `OpenWorldVerification/seamless-results.txt`: **51 PASS / 0 FAIL**, gồm đi qua đủ 18 đường bằng CharacterController thật, thu thập/trang bị/nhổ tre, thông báo chết của đủ 57 địch, lưu/đọc checkpoint và đoạn bay hóa thánh tới Complete. Scene handle/count giữ nguyên qua cả bốn chương. Phép thử di chuyển mô phỏng tốc độ 60 m/s; phép thử chiến đấu gọi API sát thương để xác minh tiến trình, không đại diện một lượt chơi thủ công đầy đủ.
- `OpenWorldVerification/regression-results.txt`: **2 PASS / 0 FAIL**, hủy đòn khi ngủ quái và chặn R khởi động lại chiến trận trong khi hồi sinh. Đã kiểm tra thêm ranh giới chiến tuyến từ chối nhân vật chưa trang bị.
- Ảnh Game View thật: `AdventureVerification/ThanhGiongDaiViet/world-map.png`, `world-forest.png`, `world-village.png`. Đã xem giao diện ở 2560×1440; không dùng tranh minh họa thay ảnh gameplay.

Chưa tạo bản Player phát hành hoặc đo FPS trên thiết bị đích; không khẳng định đạt 60 FPS. Mỗi điểm tài nguyên giữ cooldown trong phiên chơi, chưa lưu thời gian hồi của từng điểm vào checkpoint. Địch hiện dùng đường đi/raycast dự phòng có sẵn; chưa bake NavMesh toàn lục địa.

Tạo lại bản đồ: `Tools → Thanh Giong → Open World → Build Seamless Dai Viet World`. Công cụ có seed cố định, lưu bản sao scene đầu ra và Build Settings trong `OpenWorldVerification/Backups`, không ghi lại sáu scene nguồn. Các probe chỉ gắn tạm trong Play Mode, không nằm trong scene bàn giao.
