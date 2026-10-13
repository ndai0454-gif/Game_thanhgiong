import os
import sys
import glob
import math
import struct
import wave
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFont

# Set up paths
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUTPUT_DIR = os.path.join(PROJECT_ROOT, "Builds", "Videos")
os.makedirs(OUTPUT_DIR, exist_ok=True)

VIDEO_PATH = os.path.join(OUTPUT_DIR, "ThanhGiong_Trailer_Demo.mp4")
AUDIO_PATH = os.path.join(OUTPUT_DIR, "ThanhGiong_Trailer_Soundtrack.wav")
GIF_PATH = os.path.join(OUTPUT_DIR, "ThanhGiong_Trailer_Preview.gif")

# Video Specs
WIDTH = 1920
HEIGHT = 1080
FPS = 30
TOTAL_SECONDS = 52
TOTAL_FRAMES = FPS * TOTAL_SECONDS

# Font helpers
FONT_PATH_REG = "C:/Windows/Fonts/segoeui.ttf"
FONT_PATH_BOLD = "C:/Windows/Fonts/segoeuib.ttf"
FONT_PATH_TIMES = "C:/Windows/Fonts/times.ttf"
FONT_PATH_TIMES_BOLD = "C:/Windows/Fonts/timesbd.ttf"

def get_font(path, size):
    try:
        return ImageFont.truetype(path, size)
    except:
        return ImageFont.load_default()

font_title = get_font(FONT_PATH_TIMES_BOLD, 54)
font_subtitle = get_font(FONT_PATH_REG, 26)
font_act_title = get_font(FONT_PATH_TIMES_BOLD, 42)
font_act_desc = get_font(FONT_PATH_REG, 24)
font_badge = get_font(FONT_PATH_BOLD, 18)
font_small = get_font(FONT_PATH_REG, 18)
font_huge = get_font(FONT_PATH_TIMES_BOLD, 74)

# Load screenshot images
img_sources = [
    os.path.join(PROJECT_ROOT, "Assets", "_Project", "Screenshots", "AnimatedGameAssets_PlayMode.png"),
    os.path.join(PROJECT_ROOT, "Assets", "Resources", "ThanhGiongUI", "LoadingForest.png"),
    os.path.join(PROJECT_ROOT, "Assets", "_Project", "Screenshots", "screenshot-20260916-230755.png"),
    os.path.join(PROJECT_ROOT, "Assets", "_Project", "Screenshots", "screenshot-20260916-230804.png"),
]

loaded_imgs = []
for p in img_sources:
    if os.path.exists(p):
        try:
            im = Image.open(p).convert("RGB")
            loaded_imgs.append(im)
        except Exception as e:
            print(f"Error loading {p}: {e}")

if not loaded_imgs:
    # Fallback if no images found
    loaded_imgs.append(Image.new("RGB", (WIDTH, HEIGHT), (12, 34, 30)))

print(f"Loaded {len(loaded_imgs)} background assets.")

def get_ken_burns(img, progress, zoom_start=1.0, zoom_end=1.12, pan_dir=(0.02, 0.01)):
    w, h = img.size
    t = progress
    zoom = zoom_start + (zoom_end - zoom_start) * t
    cw = int(w / zoom)
    ch = int(h / zoom)
    
    max_dx = w - cw
    max_dy = h - ch
    cx = int((w - cw) * 0.5 + max_dx * pan_dir[0] * math.sin(t * math.pi))
    cy = int((h - ch) * 0.5 + max_dy * pan_dir[1] * math.sin(t * math.pi))
    
    cx = max(0, min(w - cw, cx))
    cy = max(0, min(h - ch, cy))
    
    crop = img.crop((cx, cy, cx + cw, cy + ch))
    return crop.resize((WIDTH, HEIGHT), Image.Resampling.BILINEAR)

def draw_dong_son_star(draw, center, radius, points=12, angle_deg=0, color=(218, 178, 86, 120)):
    cx, cy = center
    rad = math.radians(angle_deg)
    r_inner = radius * 0.38
    poly = []
    for i in range(points * 2):
        a = rad + i * math.pi / points
        r = radius if (i % 2 == 0) else r_inner
        x = cx + r * math.cos(a)
        y = cy + r * math.sin(a)
        poly.append((x, y))
    draw.polygon(poly, outline=color, width=2)
    # Outer circle rings
    draw.ellipse([cx - radius * 1.25, cy - radius * 1.25, cx + radius * 1.25, cy + radius * 1.25], outline=color, width=2)
    draw.ellipse([cx - radius * 1.45, cy - radius * 1.45, cx + radius * 1.45, cy + radius * 1.45], outline=color, width=1)

def render_frame(frame_idx):
    sec = frame_idx / FPS
    # Create base image
    base = Image.new("RGBA", (WIDTH, HEIGHT), (6, 18, 16, 255))
    draw = ImageDraw.Draw(base)

    letterbox_h = int(HEIGHT * 0.12)
    content_rect = (0, letterbox_h, WIDTH, HEIGHT - letterbox_h)

    # Determine Act
    # 0s - 7s: Prologue
    # 7s - 17s: Act 1 (Phù Đổng)
    # 17s - 27s: Act 2 (Rèn Thép)
    # 27s - 41s: Act 3 (Núi Sóc)
    # 41s - 48s: Act 4 (Hóa Thánh)
    # 48s - 52s: Outro

    if sec < 7.0:
        # Act 0: PROLOGUE & LOGO
        t = sec / 7.0
        bg = get_ken_burns(loaded_imgs[1 % len(loaded_imgs)], t, 1.0, 1.08, (0.01, 0.02)).convert("RGBA")
        # Dark overlay
        dark = Image.new("RGBA", (WIDTH, HEIGHT), (8, 22, 20, int(180 + 40 * math.sin(t * math.pi))))
        bg = Image.alpha_composite(bg, dark)
        base.paste(bg, (0, 0))

        # Dong Son rotating star in center
        overlay = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
        odraw = ImageDraw.Draw(overlay)
        draw_dong_son_star(odraw, (WIDTH // 2, HEIGHT // 2 - 40), 220, 14, t * 45, (230, 190, 85, 90))
        base = Image.alpha_composite(base, overlay)
        draw = ImageDraw.Draw(base)

        # Title text
        alpha_t = min(1.0, t * 2.0)
        gold = (238, 202, 102)
        cream = (248, 242, 224)
        green_tag = (95, 230, 150)

        # Draw Title
        title_text = "THÁNH GIÓNG"
        sub_text = "BẢN HÙNG CA TRÁNG SĨ ĐẤT VIỆT"
        genre_text = "DỰ ÁN GAME HÀNH ĐỘNG 3D DÂN GIAN  ·  POWERED BY UNITY 6 URP"

        tb = draw.textbbox((0, 0), title_text, font=font_huge)
        tw = tb[2] - tb[0]
        draw.text(((WIDTH - tw) // 2 + 2, HEIGHT // 2 - 120 + 2), title_text, font=font_huge, fill=(20, 20, 20))
        draw.text(((WIDTH - tw) // 2, HEIGHT // 2 - 120), title_text, font=font_huge, fill=gold)

        sb = draw.textbbox((0, 0), sub_text, font=font_title)
        sw = sb[2] - sb[0]
        draw.text(((WIDTH - sw) // 2, HEIGHT // 2 - 25), sub_text, font=font_title, fill=cream)

        gb = draw.textbbox((0, 0), genre_text, font=font_badge)
        gw = gb[2] - gb[0]
        draw.text(((WIDTH - gw) // 2, HEIGHT // 2 + 65), genre_text, font=font_badge, fill=green_tag)

    elif sec < 17.0:
        # Act 1: LÀNG PHÙ ĐỔNG - TĂNG TRƯỞNG KÍCH THƯỚC
        t = (sec - 7.0) / 10.0
        bg = get_ken_burns(loaded_imgs[0 % len(loaded_imgs)], t, 1.05, 1.18, (0.03, -0.02)).convert("RGBA")
        base.paste(bg, (0, 0))

        # Dynamic Growth Pulse ring
        pulse_phase = (sec * 2.5) % 1.0
        r_pulse = int(pulse_phase * 350)
        overlay = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
        odraw = ImageDraw.Draw(overlay)
        odraw.ellipse([WIDTH // 2 - r_pulse, HEIGHT // 2 - r_pulse, WIDTH // 2 + r_pulse, HEIGHT // 2 + r_pulse],
                      outline=(245, 215, 60, int((1.0 - pulse_phase) * 190)), width=4)
        base = Image.alpha_composite(base, overlay)
        draw = ImageDraw.Draw(base)

        act_title = "HỒI I · TIẾNG RAO DƯỚI MÁI TRANH (LÀNG PHÙ ĐỔNG)"
        act_desc = "Giặc Ân tràn bờ cõi. Cơm cà dân làng nuôi lớn Gióng, vươn vai biến thành đại tráng sĩ làm sập mái tranh!"
        act_badge = "CƠ CHẾ: TĂNG TRƯỞNG KÍCH THƯỚC THỰC TẾ (DYNAMIC SCALE SYSTEM)"
        draw_hud_banners(draw, act_title, act_desc, act_badge, sec, 7.0, 17.0)

    elif sec < 27.0:
        # Act 2: KINH THÀNH RÈN THÉP - TRANG BỊ & NGỰA SẮT PHUN LỬA
        t = (sec - 17.0) / 10.0
        bg = get_ken_burns(loaded_imgs[1 % len(loaded_imgs)], t, 1.02, 1.15, (-0.02, 0.02)).convert("RGBA")
        base.paste(bg, (0, 0))

        # Fiery sparks and fire streak
        overlay = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
        odraw = ImageDraw.Draw(overlay)
        for i in range(16):
            phase = (t * 5.0 + i * 0.35) % 1.0
            sx = int(WIDTH * 0.4 + phase * 650 + math.sin(i + sec * 4) * 80)
            sy = int(HEIGHT * 0.65 - phase * 320 + math.cos(i) * 50)
            sz = int(3 + (1.0 - phase) * 8)
            odraw.ellipse([sx - sz, sy - sz, sx + sz, sy + sz], fill=(255, int(140 + 80 * math.sin(i)), 30, int((1.0 - phase) * 220)))
        
        # Fire breath arc
        f_arc_phase = min(1.0, ((sec - 17.0) % 3.0) / 2.0)
        odraw.arc([WIDTH * 0.35, HEIGHT * 0.45, WIDTH * 0.35 + f_arc_phase * 700, HEIGHT * 0.45 + f_arc_phase * 250],
                  start=-30, end=45, fill=(255, 90, 20, 180), width=8)

        base = Image.alpha_composite(base, overlay)
        draw = ImageDraw.Draw(base)

        act_title = "HỒI II · RÈN THÉP & XUẤT QUÂN (LÒ RÈN TRIỀU ĐÌNH)"
        act_desc = "Sứ giả trao Giáp Sắt, Gươm Sắt, Thần Mã. Lửa thiêng bùng cháy xả Hỏa Tuyến dọn sạch đợt giặc mở màn!"
        act_badge = "CƠ CHẾ: MOUNTED COMBAT & HỆ THỐNG NHIỆT LƯỢNG (HEAT BAR FIRE BREATH)"
        draw_hud_banners(draw, act_title, act_desc, act_badge, sec, 17.0, 27.0)

    elif sec < 41.0:
        # Act 3: TRẬN TUYẾN NÚI SÓC - GÃY GƯƠM & QUẬT TRE NGÀ 360°
        t = (sec - 27.0) / 14.0
        bg_idx = 2 if sec < 34.0 else 3
        sub_t = ((sec - 27.0) % 7.0) / 7.0
        bg = get_ken_burns(loaded_imgs[bg_idx % len(loaded_imgs)], sub_t, 1.0, 1.14, (0.02, 0.01)).convert("RGBA")
        base.paste(bg, (0, 0))

        # Dynamic Weapon Transition VFX
        overlay = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
        odraw = ImageDraw.Draw(overlay)

        if sec < 32.0:
            # Calligraphy Sword Slash arcs
            odraw.arc([WIDTH * 0.3, HEIGHT * 0.35, WIDTH * 0.7, HEIGHT * 0.75], start=-140, end=-20, fill=(230, 215, 120, 220), width=7)
            odraw.text((WIDTH // 2 - 170, HEIGHT // 2 - 160), "⚡ CHÉM GƯƠM THƯ PHÁP", font=font_act_title, fill=(255, 230, 130, 240))
        elif sec < 35.0:
            # Sword break shatter particles
            odraw.text((WIDTH // 2 - 210, HEIGHT // 2 - 160), "💥 GƯƠM SẮT ĐÃ GÃY!", font=font_act_title, fill=(255, 80, 80, 240))
            for i in range(24):
                sa = i * math.pi / 12
                dist = 60 + ((sec - 32.0) / 3.0) * 280
                px = WIDTH // 2 + dist * math.cos(sa)
                py = HEIGHT // 2 + dist * math.sin(sa)
                odraw.rectangle([px - 4, py - 4, px + 4, py + 4], fill=(210, 230, 255, 200))
        else:
            # Bamboo 360 Whirlwind ring sweep!
            sweep_phase = ((sec - 35.0) * 1.5) % 1.0
            r_sw = int(140 + sweep_phase * 340)
            odraw.ellipse([WIDTH // 2 - r_sw, HEIGHT // 2 - r_sw * 0.65, WIDTH // 2 + r_sw, HEIGHT // 2 + r_sw * 0.65],
                          outline=(70, 240, 110, int((1.0 - sweep_phase) * 230)), width=8)
            odraw.text((WIDTH // 2 - 250, HEIGHT // 2 - 170), "🎋 TRE NGÀ QUẬT BÃO TỐ 360°", font=font_act_title, fill=(100, 255, 150, 240))

        base = Image.alpha_composite(base, overlay)
        draw = ImageDraw.Draw(base)

        act_title = "HỒI III · TRẬN TUYẾN NÚI SÓC (HACK & SLASH WARFARE)"
        act_desc = "Gươm gãy trước đao mẻ tướng giặc -> Nhổ cụm tre ngà quạt bão tố 360° & Hỏa Tuyến thiêu đồn Ân (Làng Cháy)!"
        act_badge = "CƠ CHẾ: DYNASTY WARRIORS STYLE · VŨ KHÍ ỨNG BIẾN & TRE NGÀ QUÉT DIỆN RỘNG"
        draw_hud_banners(draw, act_title, act_desc, act_badge, sec, 27.0, 41.0)

    elif sec < 48.0:
        # Act 4: ĐỈNH NÚI SÓC - HÓA THÁNH VỀ TRỜI
        t = (sec - 41.0) / 7.0
        bg = get_ken_burns(loaded_imgs[1 % len(loaded_imgs)], t, 1.08, 1.25, (0.0, -0.03)).convert("RGBA")
        base.paste(bg, (0, 0))

        # Ascension golden beam and upward particles
        overlay = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
        odraw = ImageDraw.Draw(overlay)
        # Vertical light pillar
        odraw.polygon([(WIDTH // 2 - 140, HEIGHT), (WIDTH // 2 + 140, HEIGHT), (WIDTH // 2 + 60, 0), (WIDTH // 2 - 60, 0)],
                      fill=(255, 235, 160, 75))
        # Ascending stars
        for i in range(25):
            up_p = (t * 2.0 + i * 0.17) % 1.0
            px = int(WIDTH // 2 + math.sin(i * 1.5 + sec * 3) * 160)
            py = int(HEIGHT * (1.0 - up_p))
            odraw.ellipse([px - 4, py - 4, px + 4, py + 4], fill=(255, 250, 200, int((1.0 - up_p) * 230)))

        base = Image.alpha_composite(base, overlay)
        draw = ImageDraw.Draw(base)

        act_title = "HỒI IV · HÓA THÁNH VỀ TRỜI (ĐỈNH NÚI SÓC)"
        act_desc = "Gác lại Giáp Sắt trên đỉnh núi. Cưỡi thần mã bay vút về trời, lửa rơi tạo Làng Cháy, chân ngựa thành Ao Hồ!"
        act_badge = "DI TÍCH LỊCH SỬ BẤT TỬ · PHÙ ĐỔNG THIÊN VƯƠNG PHI THĂNG"
        draw_hud_banners(draw, act_title, act_desc, act_badge, sec, 41.0, 48.0)

    else:
        # Outro: CREDITS & RELEASE SUMMARY
        t = (sec - 48.0) / 4.0
        bg = get_ken_burns(loaded_imgs[0 % len(loaded_imgs)], t, 1.0, 1.06, (0.01, 0.01)).convert("RGBA")
        dark = Image.new("RGBA", (WIDTH, HEIGHT), (6, 16, 14, 210))
        bg = Image.alpha_composite(bg, dark)
        base.paste(bg, (0, 0))
        draw = ImageDraw.Draw(base)

        gold = (238, 202, 102)
        cream = (248, 242, 224)
        green = (95, 230, 150)

        t_text = "THÁNH GIÓNG (3D ACTION-ADVENTURE)"
        tb = draw.textbbox((0, 0), t_text, font=font_title)
        draw.text(((WIDTH - (tb[2] - tb[0])) // 2, HEIGHT // 2 - 160), t_text, font=font_title, fill=gold)

        f1 = "✓ Tăng trưởng thể chất Dynamic Scale   |   ✓ Gươm Sắt, Tre Ngà & Thần Mã Phun Lửa"
        f2 = "✓ Chiến trường Dynasty Warriors 7 Màn   |   ✓ Đồ họa URP Dân gian & Trống Đồng Đông Sơn"
        f3 = "✓ Bản Phát Hành Standalone Windows x64 Đã Sẵn Sàng (ThanhGiong.exe)"

        for idx, text in enumerate([f1, f2, f3]):
            b = draw.textbbox((0, 0), text, font=font_subtitle)
            draw.text(((WIDTH - (b[2] - b[0])) // 2, HEIGHT // 2 - 60 + idx * 45), text, font=font_subtitle, fill=cream if idx < 2 else green)

        cta = "BẤM F10 TRONG GAME ĐỂ XEM LẠI TRAILER DEMO  ·  CHÚC BẠN CHƠI GAME VUI VẺ!"
        cb = draw.textbbox((0, 0), cta, font=font_badge)
        draw.text(((WIDTH - (cb[2] - cb[0])) // 2, HEIGHT // 2 + 105), cta, font=font_badge, fill=gold)

    # Letterbox Bars (Top & Bottom)
    overlay = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    odraw = ImageDraw.Draw(overlay)
    letterbox_color = (6, 12, 10, 248)
    odraw.rectangle([0, 0, WIDTH, letterbox_h], fill=letterbox_color)
    odraw.rectangle([0, HEIGHT - letterbox_h, WIDTH, HEIGHT], fill=letterbox_color)
    # Filigree lines
    gold_line = (218, 178, 86, 180)
    odraw.line([(0, letterbox_h - 2), (WIDTH, letterbox_h - 2)], fill=gold_line, width=2)
    odraw.line([(0, HEIGHT - letterbox_h), (WIDTH, HEIGHT - letterbox_h)], fill=gold_line, width=2)

    # Top brand bar
    top_brand = "GAME THÁNH GIÓNG 3D  ·  OFFICIAL GAMEPLAY TRAILER & DEMO"
    odraw.text((40, 32), top_brand, font=font_badge, fill=(218, 178, 86, 220))
    # Time stamp
    time_str = f"{int(sec//60):02d}:{int(sec%60):02d} / {int(TOTAL_SECONDS//60):02d}:{int(TOTAL_SECONDS%60):02d}"
    tb = odraw.textbbox((0, 0), time_str, font=font_badge)
    odraw.text((WIDTH - (tb[2] - tb[0]) - 40, 32), time_str, font=font_badge, fill=(230, 230, 230, 200))

    # Progress bar at bottom
    prog_w = int(WIDTH * (frame_idx / TOTAL_FRAMES))
    odraw.rectangle([0, HEIGHT - 5, prog_w, HEIGHT], fill=(225, 185, 80, 255))

    base = Image.alpha_composite(base, overlay)
    return cv2.cvtColor(np.array(base), cv2.COLOR_RGBA2BGR)

def draw_hud_banners(draw, title, desc, badge, sec, start_s, end_s):
    gold = (238, 202, 102)
    cream = (248, 242, 224)
    green = (95, 230, 150)

    # Top Sub-Header
    tb = draw.textbbox((0, 0), title, font=font_act_title)
    draw.text(((WIDTH - (tb[2] - tb[0])) // 2, 60), title, font=font_act_title, fill=gold)

    # Bottom Subtitle & Badge
    db = draw.textbbox((0, 0), desc, font=font_act_desc)
    draw.text(((WIDTH - (db[2] - db[0])) // 2, HEIGHT - 110), desc, font=font_act_desc, fill=cream)

    bb = draw.textbbox((0, 0), badge, font=font_badge)
    draw.text(((WIDTH - (bb[2] - bb[0])) // 2, HEIGHT - 72), badge, font=font_badge, fill=green)

def generate_soundtrack():
    print("Synthesizing and mixing trailer soundtrack from Assets/Resources/ThanhGiongAudio/...")
    audio_dir = os.path.join(PROJECT_ROOT, "Assets", "Resources", "ThanhGiongAudio")
    
    target_rate = 44100
    total_samples = target_rate * TOTAL_SECONDS
    master_track = np.zeros(total_samples, dtype=np.float32)

    def load_clip(name):
        path = os.path.join(audio_dir, name + ".wav")
        if not os.path.exists(path):
            return np.zeros(0, dtype=np.float32)
        try:
            with wave.open(path, 'rb') as w:
                nch = w.getnchannels()
                sw = w.getsampwidth()
                fr = w.getframerate()
                nframes = w.getnframes()
                raw = w.readframes(nframes)
                if sw == 2:
                    data = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0
                else:
                    data = np.frombuffer(raw, dtype=np.uint8).astype(np.float32) / 128.0 - 1.0
                if nch > 1:
                    data = data[0::nch]
                # Resample to target_rate if needed
                if fr != target_rate:
                    new_len = int(len(data) * target_rate / fr)
                    data = np.interp(np.linspace(0, len(data), new_len), np.arange(len(data)), data)
                return data
        except Exception as e:
            print(f"Error loading clip {name}: {e}")
            return np.zeros(0, dtype=np.float32)

    def add_clip(name, start_sec, gain=1.0, loop_until=None):
        clip = load_clip(name)
        if len(clip) == 0:
            return
        start_idx = int(start_sec * target_rate)
        if loop_until is not None:
            end_idx = min(total_samples, int(loop_until * target_rate))
            idx = start_idx
            while idx < end_idx:
                chunk = min(len(clip), end_idx - idx)
                master_track[idx:idx + chunk] += clip[:chunk] * gain
                idx += chunk
        else:
            end_idx = min(total_samples, start_idx + len(clip))
            master_track[start_idx:end_idx] += clip[:end_idx - start_idx] * gain

    # Act 0: Village ambient & music
    add_clip("ambient_village", 0.0, gain=0.35, loop_until=17.0)
    add_clip("music_village", 0.5, gain=0.45, loop_until=17.0)

    # Act 2: Forge & Fire
    add_clip("metal_equip", 17.2, gain=0.7)
    add_clip("metal_equip", 19.5, gain=0.7)
    add_clip("fire", 21.0, gain=0.85)

    # Act 3: Battle War & Combos
    add_clip("music_battle", 26.5, gain=0.65, loop_until=41.0)
    add_clip("attack_sword", 28.0, gain=0.8)
    add_clip("sword_break", 32.2, gain=0.9)
    add_clip("bamboo_pull", 34.5, gain=0.85)
    add_clip("attack_bamboo", 36.0, gain=0.85)
    add_clip("attack_bamboo", 38.0, gain=0.85)
    add_clip("fire", 38.5, gain=0.9)

    # Act 4: Ascension
    add_clip("music_ascension", 41.0, gain=0.6, loop_until=TOTAL_SECONDS)

    # Normalize audio to prevent clipping
    max_amp = np.max(np.abs(master_track))
    if max_amp > 0:
        master_track = (master_track / max(1.0, max_amp)) * 0.92

    # Write WAV file
    int16_data = (master_track * 32767.0).astype(np.int16)
    with wave.open(AUDIO_PATH, 'wb') as out_wav:
        out_wav.setnchannels(1)
        out_wav.setsampwidth(2)
        out_wav.setframerate(target_rate)
        out_wav.writeframes(int16_data.tobytes())
    print(f"Soundtrack exported successfully to {AUDIO_PATH}")

def main():
    print("==================================================")
    print("THÁNH GIÓNG 3D: OFFICIAL TRAILER & DEMO GENERATOR")
    print("==================================================")
    print(f"Output Video: {VIDEO_PATH}")
    print(f"Resolution: {WIDTH}x{HEIGHT} @ {FPS} fps | Duration: {TOTAL_SECONDS}s ({TOTAL_FRAMES} frames)")

    generate_soundtrack()

    # Video Writer
    fourcc = cv2.VideoWriter_fourcc(*'mp4v')
    out = cv2.VideoWriter(VIDEO_PATH, fourcc, FPS, (WIDTH, HEIGHT))

    if not out.isOpened():
        print(f"Error: Could not open VideoWriter for {VIDEO_PATH}")
        sys.exit(1)

    print("Rendering cinematic frames...")
    preview_frames = []

    for i in range(TOTAL_FRAMES):
        frame = render_frame(i)
        out.write(frame)

        # Collect 1 frame every second for GIF preview
        if i % (FPS * 2) == 0:
            rgb_small = cv2.resize(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB), (640, 360))
            preview_frames.append(Image.fromarray(rgb_small))

        if i % 150 == 0 or i == TOTAL_FRAMES - 1:
            prog = (i + 1) / TOTAL_FRAMES * 100
            print(f"Progress: {prog:.1f}% ({i + 1}/{TOTAL_FRAMES} frames)")

    out.release()
    print(f"Official Trailer Video successfully rendered to:\n-> {VIDEO_PATH}")

    # Generate animated GIF preview
    if preview_frames:
        print(f"Generating lightweight animated preview GIF: {GIF_PATH}...")
        preview_frames[0].save(
            GIF_PATH,
            save_all=True,
            append_images=preview_frames[1:],
            duration=600,
            loop=0
        )
        print(f"GIF preview exported to: {GIF_PATH}")

    print("==================================================")
    print("TRAILER & DEMO GENERATION COMPLETED SUCCESSFULLY!")
    print("==================================================")

if __name__ == "__main__":
    main()
