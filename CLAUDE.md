# Thánh Gióng (Unity 6000.6.0f1, URP)

Game hành động 2.5D isometric về truyền thuyết Thánh Gióng. 6 màn trong `Assets/Scenes/ThanhGiongWorld/`
(Làng Gióng → Kinh thành rèn thép → Pháo đài giặc Ân → Thung lũng vượt sông → Núi Sóc → Đỉnh Sóc hóa thánh).

## Skills dùng cho project (`.claude/skills/`)
- `unity-ai-game-creator`: quy trình GDD, scene blueprint, prompt tạo asset, checklist build/release.
- `game-developer`: object pooling, cache component, state machine, mục tiêu 60 FPS, profile trước khi tối ưu.
- `unity-ecs-patterns`: chỉ dùng cho hệ thống nhiều thực thể (ví dụ đám quân giặc); không ép chuyển toàn bộ sang ECS.

## Quy ước
- Không `Instantiate/Destroy` trong `Update`; dùng pool cho đạn, VFX, địch.
- Cache `GetComponent` trong `Awake`; không dùng `Find*` trong vòng lặp.
- Dữ liệu cân bằng đưa vào ScriptableObject, không hardcode.
- Code gameplay ở `Assets/Code/`, kiểm thử ở `Assets/Tests/` (EditMode + PlayMode).
- Build Windows x64 ra `Builds/Windows`, video ra `Builds/Videos`.

## Quy trình phát hành
1. Chạy test EditMode + PlayMode, không còn lỗi console.
2. Build `windows64` (scenes theo Build Settings).
3. Chạy thử bản build, nén zip để phát hành.
