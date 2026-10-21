# Game Thánh Gióng

Dự án game Unity lấy cảm hứng từ truyền thuyết Thánh Gióng. Hành trình trong game đi từ làng Gióng, qua quá trình rèn binh khí và chiến đấu với quân Ân, đến Núi Sóc và cảnh hóa thánh.

## Yêu cầu

- Unity Editor **6000.6.0f1** (phiên bản ghi trong `ProjectSettings/ProjectVersion.txt`).
- Unity Hub để thêm và mở dự án.
- Kết nối mạng khi mở lần đầu để Unity tải các package được khai báo trong `Packages/manifest.json`.

## Mở dự án

1. Clone repository này về máy.
2. Trong Unity Hub, chọn **Add project from disk** và trỏ đến **thư mục gốc của repository** (thư mục chứa `Assets`, `Packages` và `ProjectSettings`).
3. Mở bằng Unity 6000.6.0f1 và chờ Unity nhập tài nguyên, tải package.
4. Mở scene trong `Assets/Scenes/ThanhGiongWorld/`:
   - Scene thế giới liền mạch: `ThanhGiongDaiViet.unity` (scene chính trải nghiệm trọn vẹn 4 chương).
   - Hoặc các scene chiến dịch 6 màn truyền thống.
   Các scene này đều đã được cấu hình trong Build Settings.

## Điều khiển cơ bản

- **WASD / Shift / Space / C hoặc Alt:** Di chuyển, phi ngựa, nhảy, né đòn.
- **Chuột trái / Chuột phải:** Đánh cận chiến / ngắm qua vai (kèm chuột trái để phóng tre).
- **Chuột giữa (giữ & di):** Xoay camera tự do; **V:** Đổi vai ngắm.
- **E / Q / F:** Tương tác / gieo hạt hoặc vật phẩm / phun lửa (hỏa tuyến).
- **1–5:** Chọn loại hạt thiêng (Quang căn, Mầm tre, Sen hồi phục, Hạt lửa, Hạt gió).
- **M:** Mở bản đồ toàn lục địa.
- **G:** Thu thập tài nguyên gần, rèn/đúc vật phẩm tại lò rèn.
- **B:** Cất tài nguyên vào kho làng khi ở cạnh lò rèn.
- **F9 / Escape:** Lưu hành trình hoặc mở menu tạm dừng.

Thư mục `My project/` là một dự án Unity khác nằm trong repository. Nếu cần xem dự án đó, thêm chính thư mục `My project/` vào Unity Hub như một dự án riêng.

## Cấu trúc chính

| Đường dẫn | Nội dung |
| --- | --- |
| `Assets/` | Scene, mã nguồn, hình ảnh, âm thanh, prefab và các tài nguyên game chính |
| `Assets/Scenes/ThanhGiongWorld/` | Sáu scene theo hành trình Thánh Gióng |
| `Packages/` | Danh sách và khóa phiên bản package Unity |
| `ProjectSettings/` | Thiết lập của dự án Unity chính |
| `My project/` | Dự án Unity phụ |
| `Docs/` | Tài liệu thiết kế |
| `Tools/` | Công cụ hỗ trợ đi kèm workspace |

Xem thêm [tài liệu chuyển giao thiết kế](Docs/UNDERSTORY_TO_THANH_GIONG_DESIGN_TRANSFER.md) để biết vai trò của từng scene và các quy tắc xây dựng cảnh quan.

## Lưu ý khi dùng Git

Các thư mục cache và tệp riêng của máy như `Library/`, `Temp/`, `Logs/`, `UserSettings/`, môi trường ảo Python và `node_modules/` được loại khỏi Git. Unity và các công cụ sẽ tạo lại chúng khi cần. Khi thêm tài nguyên vào Unity, hãy commit cả tệp `.meta` tương ứng.
