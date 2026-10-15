# Bàn giao UI bốn chương — 15/10/2026

## Tham khảo và kết quả

Đã mở trực tiếp https://www.gameuidatabase.com/gameData.php?id=35 trong trình duyệt, xác nhận bộ UI The Legend of Zelda: Breath of the Wild và quan sát ảnh HUD (Player Vitals / Minimap / Item & Ability Buttons). Chỉ vận dụng nguyên tắc bố cục; không đưa ảnh hoặc biểu tượng của game tham khảo vào dự án.

Bản thiết kế có thể chỉnh sửa: `index.html` (HTML/CSS/SVG/JavaScript tự chứa). Mở bằng trình duyệt hoặc http://127.0.0.1:4186 khi máy chủ xem thử còn chạy. Cảnh vector và số liệu trong bản này là minh họa.

## Đã áp dụng trong Unity

- `ThanhGiongCampaignHUD.Chapters.cs`: tên và màu nhận diện bốn hồi; mục tiêu góc phải; tiến độ thật từ campaign; chuỗi phím trang bị E/Q/E/F và hóa thánh E/Q/E, đánh dấu bước hiện tại.
- `ThanhGiongCampaignHUD.cs`: tích hợp UI bốn hồi, tách vị trí thông báo khỏi bảng nghi thức, thống nhất cách gọi Thương Sắt.
- `ThanhGiongCampaignHUD.Adventure.cs`: thống nhất tên vũ khí; tránh nhãn vật phẩm/năng lượng đè phần giới thiệu địa danh; vẫn giữ cảnh báo địch. Sửa phép biến đổi nét vẽ theo ma trận HUD để vòng tròn và minimap hiển thị đúng khi phóng UI.
- Không đổi tiến trình sáu scene hoặc logic cưỡi ngựa.

## Bằng chứng kiểm tra

Unity `Game_thanhgiong@a7c0dbbb`, phiên bản 6000.6.0f1, đã được chọn và kiểm tra ready_for_tools trước thao tác. Biên dịch cuối thành công, Console không có error.

| Scene mở trong Play Mode | Chapter đọc thực tế | Nghi thức hiển thị | Tạm dừng / tiếp tục |
|---|---|---|---|
| LangGiongTienTuyen | Prologue | Không | Đạt |
| KinhThanhRenThep | Preparation | Có, bước 0/4 | Đạt |
| TranTuyenNuiSoc | Battle | Không | Đạt |
| DinhSocHoaThanh | Ascension | Có, bước 0/3 | Đạt |

Ảnh Game View thực tế 2560×1440: `AdventureVerification/<scene>/four-chapters-ui.png` cho bốn scene trên. Đã quan sát cả bốn ảnh; ảnh nghi thức và minimap sau sửa nét vẽ cho thấy vòng tròn đúng vị trí. Kiểm tra hàm scale cho 1280×720 và 1920×1080 trả về 1 và 1.5; chưa chụp trực quan các độ phân giải đó. Chưa xác minh gamepad hoặc chơi trọn chiến dịch trong lượt thiết kế này.

Trình duyệt: đã bấm đủ bốn tab, mở nhật ký, đóng bằng Escape, mở tạm dừng và bấm tiếp tục. Kiểm tra tĩnh: JavaScript hợp lệ, bốn tab/hai dialog, không trùng ID, không phụ thuộc ảnh/script ngoài. Bản HTML có nhật ký riêng; trong Unity F1 vẫn là bảng trợ giúp và Tab vẫn nhắc nhiệm vụ.

Unity đã dừng Play Mode và trả về scene ban đầu PhaoDaiNgamQuanAn.

## Sự cố checkpoint trong lúc xác minh

Đã gọi công cụ sẵn có `LegendVerificationPreferences.Capture()` để chuẩn bị ảnh chụp từ đầu màn. Công cụ cũ cố định vòng lặp 7 scene nhưng danh sách thực tế có 6; công cụ xóa checkpoint từng scene trước khi ghi bản sao, nên gặp IndexOutOfRangeException sau khi xóa checkpoint cục bộ của sáu scene. Không tìm thấy tệp backup trước sự cố; không thể khẳng định khôi phục checkpoint ban đầu. Làng Phù Đổng đã tạo checkpoint đầu chương mới khi được mở lại. Các scene/assets không bị reset.

Đã sửa `LegendVerificationPreferences.cs` để dùng độ dài danh sách thực tế, đọc toàn bộ dữ liệu và ghi backup hoàn chỉnh trước lần xóa đầu tiên. Kiểm tra runtime vòng Capture/Restore sau sửa: scenes=6, completeBackup=true, roundTripPreservesValues=true. Kết quả này chỉ bảo toàn dữ liệu còn lại sau sự cố, không khôi phục dữ liệu đã mất trước đó. Bản backup mới nằm ở `IsometricVerification/preferences_backup.json`.
