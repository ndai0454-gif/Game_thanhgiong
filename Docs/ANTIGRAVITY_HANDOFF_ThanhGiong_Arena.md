# Prompt bàn giao cho Antigravity

Ngày bàn giao: 11/10/2026. Có thể sao chép toàn bộ phần bên dưới làm prompt, hoặc yêu cầu Antigravity đọc file này trong workspace.

---

Bạn đang tiếp tục dự án Unity Thánh Gióng tại `D:\School\PRU\Game_thanhgiong` trên Windows. Hãy thực hiện các phần còn thiếu, sửa lỗi và kiểm chứng tới khi có bản arena chơi trọn trận. Ưu tiên Unity CLI và Unity Editor API để kiểm tra, tạo scene/material, chạy test và build. Không chỉ đưa kế hoạch hoặc viết code chưa gắn vào scene.

Đọc theo thứ tự:

1. `Docs/AGY_Implementation_Plan_ThanhGiong_Arena.md`: yêu cầu và checklist nghiệm thu.
2. `Docs/AGY_Implementation_Result_ThanhGiong_Arena.md`: kết quả thực tế và các giới hạn đã biết.
3. `Docs/ScenarioAlignment-2026-10-10.md`: hiện trạng trước triển khai và đối chiếu kịch bản.

Kịch bản gốc: https://docs.google.com/document/d/1MgMaOqSTwACzW-ee7lUgPC2eyA_sHQhJV7v6CPV_b1o/edit?usp=sharing

## Mục tiêu và phần đã có

Một trận third-person arena gần làng Phù Đổng, khoảng 5–10 phút: Gióng trưởng thành cầm roi sắt → đợt 1/2 → roi gãy → đi tới tre, nhấn E ba lần → tre đánh đợt cuối → cinematic ngắn → thắng/chơi lại. Có máu, AI, thua, pause và reset.

Scene đã lưu: `Assets/Scenes/ThanhGiongArena/PhuDongArena.unity`. Scene đứng đầu Build Settings, bảy scene cũ vẫn giữ nguyên. Đã gắn input, campaign/combat, flow, wave manager, camera, HUD, audio/VFX và ngựa hiện có. Ba đợt mặc định 6/8/8 địch, khoảng spawn 12/14/16 giây, máu địch 180/180/230; player máu 180, đi 5,2/chạy 8,8. Không dùng ngưỡng mạng campaign cũ để chuyển tiến trình arena.

Mã mới chính: `ThanhGiongArenaFlow`, `ThanhGiongArenaWaveManager`, `ThanhGiongArenaInput`, `ThanhGiongArenaHUD`, `ThanhGiongArenaPlayerVerifier`, `ThanhGiongArenaBuilder`. Kiểm tra diff để hiểu các tích hợp vào mã cũ.

## Việc tiếp tục, theo ưu tiên

1. **Sửa hiển thị hai vũ khí.** Model gốc có giáo dính trong mesh, vẫn hiện cạnh roi mới. Xử lý phần vũ khí trong arena và đặt roi/tre đúng tay, tránh phá ngựa/rider/material hoặc sửa hàng loạt prefab/map cũ.
2. **Sửa dòng hướng dẫn HUD bị cắt** ở 1280×720; kiểm tra độ rõ ở các tỉ lệ màn hình.
3. **Điều tra combat và cân bằng lượt chơi.** Lượt Windows dùng bàn phím/chuột giả lập, sát thương thật, đã thua sau 67,25 giây thực ở đợt 1: cuối progress còn 4 địch sống trong 6 đã spawn, 2 mạng. Bot chưa dùng dodge; không kết luận game không thể thắng chỉ từ lượt này. Kiểm tra AI, hướng/tầm đánh, knockback, trample, lượng sát thương và khả năng tiếp cận địch. Không cho bot bất tử, hồi máu bí mật hoặc hạ địch bằng debug để báo hoàn thành lượt thật.
4. **Hoàn tất an toàn spawn và phục hồi địch kẹt** theo checklist. Wave manager hiện dùng điểm spawn cố định; chưa có cơ chế riêng phục hồi địch rơi/ra ngoài/kẹt. Địch còn sống phải giữ wave, không âm thầm tính đã chết.
5. **Kiểm tra lại ngựa bằng hình ảnh thực tế.** Giữ bốn chân thuộc model gốc, thân ngang, cả trái/phải và hai chi trước tiếp đất. Xem idle/walk/run từ cả hai bên. Không coi vertex/bone thay đổi là đủ bằng chứng chất lượng. Test `LevelRestPosePlacesAllFourOriginalHoovesOnOnePlane` đang thất bại: `BakeMesh(..., true)` lệch 2,23 mm; thử `false` làm phép so sai lớn 2,286 m nên đã trả về `true`. Điều tra scale/không gian tọa độ/bind pose; không nới assert chỉ để xanh.
6. **Sửa báo cáo verifier.** `Finish()` hiện chỉ có thống kê đầy đủ khi Victory; kết quả FAIL có kills/gameplaySeconds mặc định 0. Luôn điền số mạng, thời gian gameplay, máu, wave/alive/spawned và vị trí từ trạng thái thật.
7. Chạy test rồi chơi trọn vòng trong Editor và Windows, gồm pause, chết/chơi lại và thắng/chơi lại. Xem ảnh xuất trận, combat roi, roi gãy, tre, combat tre, cinematic/thắng; đo thời lượng bằng điều khiển bình thường. Lượt tự động phải ghi rõ là tự động, không gọi là thời lượng người chơi đã xác nhận.
8. Build lại sau sửa, cập nhật báo cáo kết quả và chỉ đánh dấu checklist khi có bằng chứng.

## Bằng chứng hiện có

- `Artifacts/ArenaValidation/play-tests.xml`: 8/8 test logic arena đạt, khoảng 6,16 giây; cấu hình đợt nhỏ cho test chuyển trạng thái, không dùng để đo thời lượng trận.
- `Artifacts/ArenaValidation/editor-tests.xml`: 7/10 đạt; ba lỗi gồm rest pose và hai baseline map cũ.
- `Artifacts/ArenaValidation/editor-tests-final.xml`: vẫn 7/10; test rest pose ở lượt này dùng thử `false`, mã hiện đã trả về `true`, cần chạy lại.
- Hai baseline ngoài phạm vi arena: map hành trình không có campaign; map battle cũ không có quân/boss. Không xóa test hoặc mở rộng thành sửa toàn bộ sáu map chỉ để xanh.
- `Artifacts/ArenaValidation/build-result.json`: Windows x64 development build Succeeded, 0 lỗi, 538 cảnh báo, 617,41 giây, ~227,4 MiB.
- `Builds/PhuDongArena/ThanhGiongArena.exe`: bản build trước các sửa còn thiếu.
- `Builds/PhuDongArena/Verification/`: ảnh intro/IronCombat/Defeat, progress và result của lượt input thua; `Artifacts/ArenaValidation/player.log` ghi log. Verifier báo 0 runtime error trong lượt cửa sổ bình thường; đây chưa phải lượt thắng.
- Cửa sổ player ẩn làm ScreenCapture trả texture null; cửa sổ game bình thường chụp được. Không dùng ảnh đen/null làm bằng chứng.

## Công cụ và nguyên tắc làm việc

- Unity thực tế `6000.6.0f1`, URP 17.6, Input System 1.20.0.
- Editor exe: `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`.
- Unity CLI host: `C:\Users\ANKIN\AppData\Local\Unity\bin\unity.exe`, package `com.unity.pipeline` đã có. Có thể dùng native Unity command line nếu live CLI không ổn định.
- Đọc `AGENTS.md` nếu có và các skill Unity phù hợp trong `.agents/skills` nếu môi trường cung cấp. Các đường dẫn/công cụ Antigravity có thể khác; kiểm tra trước khi dùng, đừng giả định có tool của Codex.
- Đầu tiên đọc `git status`/diff và kiểm tra tiến trình/lock. Tại lúc bàn giao đã kiểm tra không còn Unity Editor hoặc player chạy. Toàn bộ công việc đang **chưa commit**, gồm nhiều file mới chưa track; giữ chúng và các thay đổi người dùng. Không reset, stash hoặc ghi đè hàng loạt.
- Không mở hai Unity Editor cùng project. Editor đang mở thì dùng CLI/API, không sửa YAML asset đang được quản lý. Batch chỉ chạy khi project không bị Editor giữ. Với `-runTests` không thêm `-quit`.
- Menu tạo scene: `Tools/Thanh Giong/Build Phu Dong Arena`. Menu build: `Tools/Thanh Giong/Build Arena Windows Demo`. Batch `-executeMethod ThanhGiongArenaBuilder.BuildDemo -quit` sẽ **tạo lại scene arena rồi build**, nên không gọi sau chỉnh tay scene mà chưa cập nhật builder/giữ các chỉnh sửa đó. `BuildWindows` chỉ build scene đã lưu.
- Test Editor assembly `ThanhGiong.ProjectTests`; Play Mode assembly `ThanhGiong.PlayModeTests`, filter `ThanhGiongArenaPlayModeTests`.
- Verifier development player chỉ bật bằng `--verify-arena`; không bật tự động cho người chơi bình thường.
- Lưu bằng chứng bền vững trong `Artifacts/ArenaValidation` hoặc Docs; Unity xóa Temp khi thoát.
- Không commit/publish/PR nếu chưa được yêu cầu. Báo cáo trung thực phần đạt, lỗi còn lại và phần chưa kiểm chứng.

## Nếu chuyển sang máy khác

Cần toàn bộ `Assets` (kèm **tất cả `.meta`**), `Packages`, `ProjectSettings`, `Docs`, `Tools`. Mang thêm `Artifacts/ArenaValidation` và `Builds/PhuDongArena/Verification` để giữ bằng chứng; toàn bộ bản Windows là tùy chọn. `.git` tùy chọn nếu cần lịch sử và diff, nhưng phải mang cả file working tree/untracked, không chỉ clone commit hiện tại.

Không cần Library, Temp, Obj hoặc cache. `Packages/manifest.json` hiện có tham chiếu local `file:E:/Game/AgentSkillRepos/Unity-Skills/SkillsForUnity`; dự án hiện resolve `com.besty.unity-skills` bằng package embedded. Khi đổi máy, xác nhận thư mục embedded `Packages/com.besty.unity-skills` đã được mang theo và dependency resolve đúng, hoặc điều chỉnh tham chiếu tới package thực có; không giả định ổ E tồn tại.

## Bàn giao cuối cùng cần có

Scene đã lưu và chơi được, bản Windows mới, kết quả test/log/ảnh, thời lượng được ghi đúng phương pháp, báo cáo cập nhật cùng checklist. Giải thích cụ thể mọi giới hạn còn lại.
