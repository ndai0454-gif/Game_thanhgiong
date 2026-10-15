# Camera qua vai, HUD và đồ họa phiêu lưu

Áp dụng trực tiếp cho cả sáu scene trong `Assets/Scenes/ThanhGiongWorld`. Bộ cài lưu bản sao scene trước khi thay đổi trong `AdventureVerification/Backups`; vị trí từng lần cài nằm trong `AdventureVerification/campaign-install.txt`.

## Điều khiển

| Thao tác | Phím |
| --- | --- |
| Di chuyển theo hướng camera | WASD |
| Phi ngựa, tiêu hao thể lực | Shift |
| Nhảy / né | Space / C hoặc Alt |
| Xoay camera tự do | Giữ chuột giữa và di chuột |
| Ngắm, xoay qua vai, khóa mềm | Giữ chuột phải và di chuột |
| Đổi vai | V |
| Đánh / ném vật đang vác | Chuột trái |
| Phóng tre khi đã nhổ được tre | Giữ chuột phải + chuột trái |
| Nhổ tre theo ba nhịp | E hoặc L |
| Nhặt đá / gốc cây | E |
| Chọn hạt thần / gieo | 1–5 / Q |
| Hỏa tuyến khi đủ nhiệt | F |
| Tạm dừng / trợ giúp / nhắc nhiệm vụ | Esc / F1 / Tab |

E/Q/F vẫn giữ ý nghĩa của nghi thức trang bị và hóa thánh tại các chương tương ứng. Vũ khí sắt và tre vẫn tuân theo tiến trình thương gãy → nhổ tre, không chuyển vũ khí vượt điều kiện nhiệm vụ.

## Camera và chiến đấu

- Camera phối cảnh, boom 5,8 m trong không gian trống, FOV khám phá 80°. Bám vị trí Ngựa Sắt đang hiển thị vì model có offset so với capsule gameplay.
- Khi ngắm: boom 4,3 m, FOV 60°, lệch vai rõ hơn. Quét cầu kiểm tra cả offset ngang và boom, thu gần ngay khi gặp vật cản rồi lùi ra mượt. Vật cản sát người có thể buộc camera vào gần hơn 3 m.
- Rung nhẹ dùng lại sự kiện va chạm/nhổ tre/hóa thánh, không thay chuyển động và hệ thống cưỡi ngựa đã có.
- Khóa mềm chọn địch sống trong góc ngắm và kiểm tra đường nhìn thực tế từ điểm phóng; không bắt xuyên tường. Đòn cận chiến được chỉnh hướng nhẹ khi mục tiêu ở gần.
- Đường chấm dùng cùng vị trí phóng, vận tốc và trọng lực với Rigidbody thật. Ngừng đường hướng dẫn ở vật cản. Đá/gốc cây giữ sát thương cũ; tre phóng gây 38 sát thương cơ sở, hồi chiêu 1,2 s. Phản ứng giáp/sơ hở của địch tiếp tục điều chỉnh sát thương.
- Tre xuất phát từ xương bàn tay phải của Gióng đang hiển thị; đá/gốc cây dùng vị trí vật đang vác. Tránh đặt quỹ đạo tại capsule bị lệch khỏi model.
- Va chạm và phép quét đường bay chỉ kích hoạt một lần; mỗi địch chỉ nhận một lần sát thương dù có nhiều collider.
- Thể lực thật: 100 điểm, chạy nhanh tiêu hao 18/s, hồi 24/s sau 0,8 s nghỉ; cạn thì trở về tốc độ đi cho đến khi hồi ít nhất 20%.

## HUD và trình bày

- Sinh lực dạng hạt gạo ở góc trái trên; số máu, vũ khí đang có và quick slots lấy từ trạng thái game thật. Sen hộ thể hiển thị thời gian giảm 32% sát thương, không giả thành máu cộng thêm.
- Tám ô thể lực/hỏa khí/linh lực xuất hiện cạnh nhân vật theo hoạt động. Tâm ngắm, mục tiêu, cảnh báo phản công và số máu mất được chiếu từ thế giới 3D.
- Minimap địa hình thật bằng camera orthographic riêng, RenderTexture 256×256, mặt nạ tròn và viền trống đồng. Pin phân biệt địch/boss, vật phẩm, vật ném và checkpoint. Chỉ hiện pin cổng khi có cổng thực trong scene; tiến trình tự chuyển màn hiện hữu vẫn được giữ.
- Nhãn vật phẩm cũ được ẩn ở chế độ qua vai; HUD chỉ hiển thị tài nguyên gần nhất để tránh nhãn trùng nhau.
- Shader URP chia tầng sáng và viền vàng nhẹ giữ texture, normal, metallic và emission của nhân vật. Material được sao chép lúc chạy; bỏ qua shader dissolve và hiệu ứng trong suốt để giữ hiệu ứng chết của boss.
- Cỏ/cây sậy có gió GPU, instancing và vùng giẫm theo vị trí ngựa hiển thị. Tre đã có chuyển động riêng vẫn giữ chuyển động đó.
- Bụi theo tiếp xúc vó, lá tre bay thưa, vật phẩm phát sáng; bloom và màu ấm nhẹ. Giữ sương/màu đã thiết kế cho pháo đài.

Chưa thêm cung/nỏ, kho chuyển nhanh vũ khí, thời tiết, nhiệt kế hay hệ thống độ ồn: dự án chưa có dữ liệu/gameplay cho các mục đó. Không cài các gói Asset Store được đề xuất trong tài liệu; hiệu ứng dùng shader và Particle System cục bộ.

## Kiểm tra

`AdventureRuntimeVerification` là probe chỉ gắn tạm trong Play Mode, không lưu vào scene. Probe dùng khu thử riêng với player/CharacterController thật, vật cản thật và Rigidbody thật; kiểm tra camera, khóa mềm, đường bay/sát thương, pause, stamina, minimap, shader/VFX và giữ rig boss. Báo cáo từng màn ở `AdventureVerification/<scene>/runtime-results.txt`.

Kết quả phiên ổn định ngày 15/10/2026: Làng Gióng 33 PASS / 0 FAIL; Pháo Đài 34 PASS / 0 FAIL, tổng 67/67. Kiểm tra binding đã lưu đạt cả sáu scene (`campaign-bindings.txt`). Sau đó chỉ chỉnh vị trí/nền của nhãn HUD; biên dịch và chụp lại giao diện để kiểm tra bố cục.

`AdventureFrameCapture` chụp Game View ở cuối frame để kiểm tra cả HUD; cũng chỉ dùng tạm. Kiểm tra shader bằng `ShaderUtil.GetShaderMessages` và Console trong Unity. Chưa tạo bản build phát hành trong lần chỉnh này.

Minimap khôi phục `RenderTexture.active` sau khi tạo mask, tránh làm đen Game View. HUD có thể tái tạo icon sau khi Unity reload script; module đồ họa dọn material/particle/volume khi tắt rồi dựng lại khi bật, đã có kiểm tra vòng đời để tránh nhân đôi hiệu ứng.

Dự án còn prefab thử nghiệm `Assets/Prefabs/Characters/ThanhGiong_Mounted_Reference.prefab` thiếu nested source GUID `7a11acebe30a77142904479d751cb85e` từ đợt thiết kế model khác. Kiểm tra `AssetDatabase.GetDependencies` xác nhận cả sáu scene chiến dịch không phụ thuộc prefab này; không tự ý thay nguồn model chưa xác định cho prefab thử nghiệm.
