# Thánh Gióng — UI cho bốn chương

Bản xem trước: [index.html](index.html). Mở trực tiếp trong trình duyệt; không cần máy chủ, package hoặc mạng. Đây là thiết kế UI với cảnh vector nguyên bản, **không phải ảnh chụp hay game đang chạy**. Tất cả số liệu và bản đồ đều được ghi rõ là minh họa.

## Tham chiếu và hướng thị giác

Tham chiếu bố cục: [The Legend of Zelda: Breath of the Wild trên Game UI Database](https://www.gameuidatabase.com/gameData.php?id=35). Chỉ dùng nguyên tắc thị giác: sinh lực ở góc trái trên, thông tin phụ nhỏ, bản đồ tròn ở góc phải dưới, vùng giữa thoáng và chữ màu kem. Không sao chép ảnh, icon, logo, tài nguyên hoặc hình bản đồ của trò chơi tham chiếu.

Ngôn ngữ riêng: hạt gạo cho sinh lực; đồng cổ và ngọc xanh cho trạng thái; chữ serif cho tên miền đất, chương và lời kể; hình học gợi họa tiết trống đồng. Tranh SVG là núi, ruộng, làng, lò rèn, tre và người cưỡi ngựa được dựng riêng bằng vector. Tranh phục vụ bố cục, không khẳng định vị trí hay hình dáng tài sản trong Unity.

## Bốn chương, sáu cảnh

| Chương | Cảnh Unity | Nội dung và UI |
| --- | --- | --- |
| I · Tiếng rao dưới mái tranh | `LangGiongTienTuyen` | Gom lương thực. Theo cấu hình mặc định, `foodPerGrowth = 100`, tổng 300, `GrowthPhase` 0–3. Bản mẫu 120/300 là số minh họa. |
| II · Rèn thép & xuất quân | `KinhThanhRenThep` | E mặc giáp → Q đội nón → E lên ngựa sắt → F phun lửa. Chuỗi có trạng thái hoàn tất / kế tiếp. |
| III · Càn quét giặc Ân | `PhaoDaiNgamQuanAn`, `ThungLungVuotSong`, `TranTuyenNuiSoc` | Thương Sắt gãy → đến khóm tre, nhấn E ba lần → tiếp chiến bằng tre. Số giặc dùng `Kills / RequiredBattleKills`, thay đổi theo cảnh. Hỏa khí dùng `Heat01`; F khi đầy. 8/18 và 62% chỉ là minh họa. |
| IV · Hóa thánh về trời | `DinhSocHoaThanh` | Chuỗi E → Q → E đặt giáp, nón và hoàn tất nghi lễ. |

Wording và hành vi đối chiếu với `Assets/Code/Player/ThanhGiongCampaignController.cs`, `Assets/Code/UI/ThanhGiongCampaignHUD.cs`; mục tiêu giữ cùng bốn `Chapter` hiện có, không thêm nhiệm vụ mới. Tên nội bộ vũ khí `IronSword` đang được diễn đạt trong gameplay là Thương Sắt; bản mẫu dùng thuật ngữ này cho nhất quán với mục tiêu campaign.

## Bố cục và tương tác

- Thanh chọn chương hoạt động bằng chuột, phím trái/phải, Home/End; mỗi lựa chọn đổi cảnh vector, tên vùng, mục tiêu, tiến độ, prompt và nhật ký.
- HUD có sinh lực góc trái, quest tracker góc phải, thể lực gần nhân vật, tên vùng góc trái dưới, chuỗi thao tác giữa dưới, bản đồ góc phải dưới. Các phần bám tỷ lệ vùng cảnh 16:9; trên màn hình nhỏ vùng cảnh giữ chiều cao tối thiểu và đề nghị xoay ngang.
- Nhấn Tab khi vùng cảnh được focus hoặc bấm Nhật ký để mở bảng; Tab trong menu vẫn dùng để đi qua nút. Esc mở tạm dừng hoặc đóng bảng trên cùng. Bảng giữ focus, trả focus khi đóng; click ngoài cũng đóng.
- Chỉ dẫn E/Q/F trong bản mẫu mô tả gameplay, không giả lập input hay thay đổi trạng thái campaign. Nút menu chỉ điều khiển bản xem trước. Không có nút giả lưu game, thiết lập hoặc thoát ứng dụng.
- Bản mẫu luôn hiện vòng thể lực để đánh giá bố cục. Trong game, vòng này nên hiện theo hành động tiêu hao thể lực. Thiết kế hỗ trợ `prefers-reduced-motion`.

## Ranh giới và kiểm chứng

File HTML tự chứa CSS, SVG và JavaScript; không tải ảnh/font/script ngoài, không dùng screenshot có bản quyền. Không chỉnh scene, prefab hay script Unity trong phạm vi bản mẫu này. Tích hợp Unity và kiểm thử trạng thái runtime thuộc phần thực hiện riêng.

Kiểm tra nên gồm: đổi đủ bốn chương; đối chiếu nội dung nhật ký; mở/đóng tạm dừng bằng Esc và nút; focus trap và trả focus; kích thước desktop/tablet; xác nhận không tràn/che hướng dẫn. Không dùng kết quả kiểm tra HTML để suy ra game Unity đã chạy đúng.

Kiểm tra tĩnh đã thực hiện ngày 15/10/2026: JavaScript qua `node --check` không lỗi; đúng 4 tab chương và 2 dialog; không có ID trùng hoặc tham chiếu DOM tĩnh thiếu; HTML chuẩn NFC; không có ảnh hoặc script tải ngoài. Đã đối chiếu `ThanhGiongCollectible.OnTriggerEnter` để không gợi phím E cho nhặt lương thực tự động theo khoảng cách. Kiểm tra này không thay thế kiểm chứng hình ảnh và tương tác trong trình duyệt.
