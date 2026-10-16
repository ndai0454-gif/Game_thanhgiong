# Game Brief: Thánh Gióng

**One-sentence pitch:** Hóa thân Thánh Gióng, từ đứa trẻ làng Gióng lớn vụt thành tráng sĩ, rèn sắt, cưỡi ngựa sắt đánh tan giặc Ân qua 6 màn rồi hóa thánh trên đỉnh Sóc.

## Core loop
- Khám phá màn, nhặt đồ ăn và vật liệu để lớn lên và mạnh hơn.
- Giao chiến với quân giặc: cận chiến, tre, đánh trên ngựa, giẫm bẹp quân địch.
- Tiếp tế (tre, đồ ăn), hồi thể lực, rồi đi tiếp tới điểm chuyển màn.
- Qua cổng để sang màn kế tiếp.

## Player goal & fail state — what "working" looks like
- **Mục tiêu:** đi hết 6 màn từ Làng Gióng đến Đỉnh Sóc và xem cảnh hóa thánh.
- **Thất bại:** hết máu thì thua và phải chơi lại từ đầu màn. *(ASSUMED — chưa xác nhận với code.)*

## MVP — what must exist to be the game
- 6 màn chơi được và nối liền nhau (`Assets/Scenes/ThanhGiongWorld/`).
- Nhân vật chính: di chuyển, thể lực, tấn công, ngắm.
- Ngựa sắt: cưỡi, xông trận, giẫm quân địch.
- Quân giặc Ân và tướng, có AI, hoạt ảnh và ragdoll.
- Tăng trưởng nhờ đồ ăn và tiếp tế tre.
- Cảnh hóa thánh làm kết thúc.
- Âm thanh theo màn (nhạc nền, âm môi trường, hiệu ứng đánh).

## Out of scope — not building this
- Không chơi mạng.
- Không hệ thống lưu game phức tạp hay cây kỹ năng/meta-progression. *(ASSUMED.)*
- Không thêm màn ngoài 6 màn.
- Không làm bản mobile hay web (chỉ Windows x64).

## Build order
1. Giao chiến cốt lõi trên bộ ngựa sắt và quân giặc (rủi ro và cảm giác nhất).
2. Vòng ăn lớn và tiếp tế.
3. Chuyển màn liền mạch giữa 6 màn.
4. Âm thanh và hoàn thiện từng màn.
5. Cảnh hóa thánh và đóng gói (zip Windows).

---
**Who it's for / what they feel:** Người chơi hành động thích truyền thuyết Việt, muốn cảm giác lớn vụt thành anh hùng và xông trận oai hùng.

**Art & audio direction:** 3D isometric 2.5D dùng mô hình Meshy, KayKit và Kenney. Âm thanh dân gian, trống trận, môi trường theo từng màn (làng, lò rèn, sông, đỉnh thiêng).

**Reference game:** Dynasty Warriors — giữ khoảng 10%: xông vào đám quân đông và hạ gục nhiều địch; không lấy hệ thống tướng, vũ khí hay chiến dịch.
