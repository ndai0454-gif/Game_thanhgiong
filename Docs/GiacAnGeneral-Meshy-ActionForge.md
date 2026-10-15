# Tướng quân giặc Ân — Meshy và ActionForge

## Nguồn và cấu hình đã duyệt

- Ảnh tham chiếu do người dùng cung cấp: `codex-clipboard-b949d305-7d6c-42d3-978e-0fe42bc499de.png`.
- Meshy image-to-3D task: `01a13aaa-d8f2-72ea-979f-666cc69a2926`.
- Model: `meshy-t2.5`, `smart-topology`, mục tiêu 30.000 tam giác, T-pose, texture PBR 4K, FBX và GLB.
- Chi phí được duyệt: tạo model 15 credits, rigging 5 credits, tổng 20 credits.
- Thiết kế: tướng giặc Ân giả tưởng dựa trên ảnh; giáp đen, họa tiết rồng, ngọc đỏ, mũ gai, râu và áo choàng đỏ sẫm.

## Animation đã xuất từ ActionForge

Sword Idle, Sword Attack, Sword Block, Sword Heavy Combo, Sword Regular A,
Sword Regular B, Walk Loop, Jog Fwd Loop, Sprint Loop, Hit Chest, Death01.

Giữ skeleton của model, FBX với Units = Unity và In Place.
Animation là chuyển động từ thư viện ActionForge; không phải clip AI Meshy tạo mới.

## Kết quả tạo model và gắn xương

Meshy tạo thành công model gốc có 31.646 tam giác, texture base color/normal 4K
và metallic/roughness 2K. API rigging Meshy từ chối với lỗi 422
`Pose estimation failed`; không có tác vụ rig mới và không trừ thêm credits.
Tổng chi phí thực tế 15 credits; số dư sau thực hiện là 672 credits.

Đã gắn xương và skinning thủ công bằng trình rigger của ActionForge, không tốn
credits Meshy. Bản gốc được giữ ở `GiacAnGeneralVerification/Source/`.
Bản dùng cho animation được bỏ 4.513 tam giác của bệ đá liền thân, còn 27.133
tam giác, 71 xương. Phần lưỡi kiếm được chỉnh trọng số cứng vào `hand_r`.
Script tái lập: `Scripts/prepare_giac_an_general.py`; cấu hình cắt và số liệu
nằm trong `GiacAnGeneralVerification/GiacAnGeneral_ActionForge_Rigged.json`.

## File bàn giao

- `GiacAnGeneralVerification/GiacAnGeneral_11_Animations.glb`: model có đủ 11 animation.
- `Assets/Art/Meshy/GiacAnGeneral/GiacAnGeneral_ActionForge.fbx`: FBX từ ActionForge.
- `Assets/Art/Meshy/GiacAnGeneral/Textures/`: texture gốc Meshy.
- `Assets/Prefabs/Enemies/GiacAnGeneral_Meshy.prefab`: prefab riêng cho Unity.
- `Assets/Animations/ActionForge/GiacAnGeneral/GiacAnGeneral.controller`: Generic controller 11 state/trigger.
- `Assets/Scenes/Showcase/GiacAnGeneral_Showcase.unity`: scene xem thử với các nút chọn clip.

Builder: `ThanhGiong.EditorTools.GiacAnGeneralBuilder.Build()`; menu
`Tools > Thánh Gióng > ActionForge > Build Giac An General`.
Prefab có root motion tắt; chuyển động vị trí trong gameplay do logic điều khiển.
Áo choàng chuyển động bằng skinning theo skeleton, chưa có mô phỏng vải riêng.

## Kiểm tra

Unity đã biên dịch thành công. Kiểm tra đủ 11 state, avatar Generic hợp lệ,
71 liên kết xương không bị thiếu, và Play Mode xác nhận mỗi clip làm mesh biến dạng.
Thời lượng cùng độ thay đổi đỉnh ghi ở `GiacAnGeneralVerification/unity-runtime-checks.txt`.
Đã kiểm tra hình ảnh Play Mode của đứng, combo, chạy và chết; ảnh `unity-final-*.png`
trong thư mục verification. Prefab được chuẩn hóa theo mesh ở tư thế idle cao 2,15 m,
chân chạm mặt đất, không trôi root sau kiểm tra 60 giây. Với FBX này phải dùng
`BakeMesh(mesh, true)` để đo mesh theo đơn vị đã nhập; phép đo mặc định làm sai scale.
Console trả về 0 lỗi sau kiểm tra. Scene chiến dịch ban đầu được mở lại sau khi xem thử.

Mở scene Showcase và nhấn Play để chọn từng clip hoặc tự động chạy cả 11 clip.
## Tích hợp boss chiến dịch

Đã thay toàn bộ boss hiện có sau khi quét sáu scene chiến dịch:

- `PhaoDaiNgamQuanAn`: 1 boss.
- `TranTuyenNuiSoc`: 1 boss.
- Bốn scene còn lại không có actor `isBoss`.

Prefab gameplay mới: `Assets/Prefabs/Enemies/GiacAnGeneral_Boss_An.prefab`.
Prefab gameplay cũ `KayKit_Knight_Boss_An.prefab` cũng được cập nhật tại chỗ để giữ
các tham chiếu GUID có sẵn. Installer `GiacAnGeneralCampaignInstaller.InstallAll()`
lưu bản sao scene và prefab trước khi thay tại `GiacAnGeneralVerification/CampaignBackups/`.
Model cũ được giữ ở nhánh không hoạt động để phục hồi khi cần.

`GiacAnGeneralCombatBuilder` tạo controller riêng có `State`, `Phase`, `Speed`,
`MoveRate`, gán qua motion set rõ ràng để tránh fallback sang skeleton KayKit.
Đòn nặng dùng clip đã chia các pha theo đồng hồ coroutine gameplay. Khi né,
pha kẹt giữ tư thế tại điểm chạm (`Strike` .35484 tương ứng source .35), rồi
hồi phục về idle. Tên xương `hand_r/l`, `foot_r/l` và marker đầu kiếm được hỗ trợ
trong hiệu ứng. Hạt va chạm bắt kịp khi coroutine chuyển thẳng sang kẹt ở frame chạm.

Giữ nguyên AI, collider, thông số chiến đấu và hệ thống chết/tan biến/chơi lại.
Vật liệu PBR gốc được giữ khi chạy; hiệu chỉnh chân dựa trên gốc prefab đã được
đặt đúng sàn trong idle, tránh dùng bounds bảo thủ của FBX.

## Kết quả kiểm tra chiến dịch

Ngày 14/10/2026: **38/38 kiểm tra Play Mode đạt** (19 ở mỗi màn có boss).
Bao gồm model/rig/PBR, bám đất, di chuyển AI, các pha đòn thật, vệt kiếm và hạt
va chạm, nhận sát thương, chết/tan biến và chơi lại. Console không có lỗi.
Chi tiết: `GiacAnGeneralVerification/PhaoDaiNgamQuanAn/runtime-results.txt`
và `GiacAnGeneralVerification/TranTuyenNuiSoc/runtime-results.txt`.
Ảnh trong game được lưu ở `boss-replaced.png` trong từng thư mục trên.

Kiểm tra tự động dùng `GiacAnGeneralCampaignVerification` được gắn chủ động
chỉ trong Play Mode, tăng máu người chơi và tạm tắt minion để cô lập boss;
các thay đổi phục vụ kiểm tra không được lưu vào scene. Kích thước boss giữ
theo transform/collider gốc của từng màn, gồm boss lớn hơn ở Trận Tuyến Núi Sóc.
