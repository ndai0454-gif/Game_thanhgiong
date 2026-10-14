# Thánh Gióng Meshy và ActionForge

## Model đã tích hợp

Prefab chính: `Assets/Prefabs/Player/Meshy_ThanhGiong_Mounted.prefab`.
Áp dụng cho sáu scene trong `Assets/Scenes/ThanhGiongWorld/`.

| Model | Tam giác | Xử lý |
| --- | ---: | --- |
| Thánh Gióng | 30.790 | Chuẩn hóa đơn vị mét, bổ sung xương bàn tay phải và trọng số cổ tay |
| Ngựa sắt | 55.000 | Giảm từ 2.702.910 tam giác; dựng skin 17 xương |
| Thương tre | 6.000 | Giảm từ 198.652 tam giác; gắn vào bàn tay phải |

Ba file gốc ở ổ E được giữ nguyên. Quy trình tái tạo model:
`Scripts/prepare_meshy_assets.py`; số liệu kiểm tra:
`MeshyVerification/asset-preparation.json`.
Model và texture tối ưu nằm trong `Assets/Art/Meshy/`.

Ngựa nguồn là mesh tĩnh và có một chân trước đang co. Ngựa sử dụng rig và
chuyển động thủ tục của game: đứng thở, đi, phi, nghiêng khi đổi hướng,
chồm và bay. Các clip humanoid ActionForge chỉ áp dụng cho người cưỡi.

## Animation đã xuất từ ActionForge

Đã đưa model Thánh Gióng đã chuẩn hóa vào trình duyệt, giữ skeleton Mixamo
của chính model, chọn chín clip và xuất một FBX với Units = Unity,
In Place bật. File: `Assets/Animations/ActionForge/Meshy_ActionForge.fbx`.

| Clip ActionForge | Vai trò trong game |
| --- | --- |
| Idle Loop | Thở và đứng chờ trên ngựa |
| Sword Regular A | Nhịp đánh 1 |
| Sword Regular B | Nhịp đánh 2 |
| Sword Attack | Nhịp đánh 3 và quét thương tre |
| Hit Chest | Phản ứng khi mất máu |
| Pickup Table | Nhặt tài nguyên |
| Interact | Chuẩn bị/trang bị |
| Spell Simple Shoot | Kích hoạt phun lửa |
| Spell Simple Idle Loop | Tư thế hóa thánh/bay lên |

Các động tác Spell được dùng làm tư thế cử chỉ; đây không phải clip cưỡi
ngựa hoặc hóa thánh được ActionForge dựng riêng cho câu chuyện.

`MeshyActionForgeAnimator` chuyển rotation theo bind pose của FBX và GLB,
bake 13 xương thân trên ở 30 fps. Không ghi đè root, hông, chân hoặc scale,
để giữ tư thế cưỡi và vị trí CharacterController. Animator có 10 state,
ba biến thể combo, chuyển về Idle và nhánh Ascension. Root Motion tắt.
Thời lượng clip đánh được khớp với thời lượng đòn của gameplay.

Thương mới được dùng ở cả pha vũ khí sắt và pha tre; tên state/nút giao diện
của hai pha vẫn theo cốt truyện hiện có. Phần tre dùng kích thước lớn hơn.

## Cập nhật từ Unity

1. Mở `Tools > Thánh Gióng > ActionForge Animation Studio`.
2. Hiển thị model Meshy, chọn file này trong `Character > + Rigged character`
   trên [ActionForge](https://actionforge.app/#clip=sword_attack).
3. Chọn đủ chín clip trong bảng, giữ skeleton của model, FBX, Unity, In Place.
4. Nhập FBX qua cửa sổ Studio; dựng prefab và áp dụng vào sáu màn.

Link rig tùy chỉnh phụ thuộc dữ liệu cục bộ trình duyệt. Trên máy/trình duyệt
khác cần nạp lại GLB. File FBX đã lưu trong dự án nên chạy game không cần web.

Installer giữ visual trước đó ở object không hoạt động
`Previous Visual - Before Meshy`. Backup scene nằm trong
`MeshyVerification/SceneBackups/`; backup FBX thay thế nằm trong
`MeshyVerification/AnimationBackups/`.

## Kiểm tra

`MeshyRuntimeVerification` là công cụ chạy chủ động, không gắn trong các scene
bàn giao. Kết quả lần chạy gần nhất và ảnh được ghi ở `MeshyVerification/`.
Kiểm tra đường đánh thật qua campaign, chuyển state, tay cầm thương, chân
ngồi, cử chỉ, bay, biến dạng skin ngựa và shader được hỗ trợ.
Ảnh cận cảnh dùng camera/đèn kiểm tra; `gameplay_full.png` là camera game.

Kết quả bàn giao: Unity biên dịch không có lỗi; 32 kiểm tra runtime đạt,
liên kết prefab ở cả sáu scene đạt. Combo được gọi qua campaign và kiểm tra
Animator bằng bước thời gian nhỏ để tránh sai lệch do chụp ảnh làm chậm frame
trong Editor; các nhánh cử chỉ và bay chạy theo thời gian Play Mode.
Xem `MeshyVerification/runtime-results.txt` và `scene-results.txt`.
