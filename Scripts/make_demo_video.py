"""Assemble the demo reel and add music.

Inputs (PNG frames written by Unity in Play mode):
  Builds/Videos/DemoFrames/frame_*.png       open-world riding reel   (ThanhGiongDemoRecorder)
  Builds/Videos/DemoFramesStory/frame_*.png  forge, combat, ascension (ThanhGiongDemoStoryRecorder)
Output: Builds/Videos/ThanhGiong_Gameplay_Demo.mp4 (H.264 + AAC, 1920x1080, 30 fps).

Needs: opencv-python, numpy, Pillow, imageio-ffmpeg (pip install --user imageio-ffmpeg).
Music is the game's own soundtrack from Assets/Resources/ThanhGiongAudio, cross-faded per section.
"""
import glob
import os
import subprocess
import sys
import wave

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
VID = os.path.join(ROOT, "Builds", "Videos")
REEL = sorted(glob.glob(os.path.join(VID, "DemoFrames", "frame_*.png")))
STORY = sorted(glob.glob(os.path.join(VID, "DemoFramesStory", "frame_*.png")))
SILENT = os.path.join(VID, "_demo_silent.mp4")
MIX = os.path.join(VID, "_demo_music.wav")
OUT = os.path.join(VID, "ThanhGiong_Gameplay_Demo.mp4")
AUDIO = os.path.join(ROOT, "Assets", "Resources", "ThanhGiongAudio")
W, H, FPS, FADE, CARD = 1920, 1080, 30, 15, 3 * 30
SR = 44100

GOLD = (235, 199, 97)
CREAM = (245, 240, 225)


def font(name, size):
    try:
        return ImageFont.truetype(os.path.join("C:/Windows/Fonts", name), size)
    except OSError:
        return ImageFont.load_default()


TITLE, SUB = font("timesbd.ttf", 96), font("segoeui.ttf", 38)


def card(lines):
    img = Image.new("RGB", (W, H), (8, 10, 14))
    d = ImageDraw.Draw(img)
    y = H // 2 - 110
    for text, f, color in lines:
        d.text(((W - d.textlength(text, font=f)) / 2, y), text, font=f, fill=color)
        y += f.size + 26
    return cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)


def fit(frame):
    h, w = frame.shape[:2]
    if (w, h) == (W, H):
        return frame
    s = min(W / w, H / h)
    r = cv2.resize(frame, (int(w * s), int(h * s)), interpolation=cv2.INTER_AREA if s < 1 else cv2.INTER_CUBIC)
    canvas = np.zeros((H, W, 3), np.uint8)
    y0, x0 = (H - r.shape[0]) // 2, (W - r.shape[1]) // 2
    canvas[y0:y0 + r.shape[0], x0:x0 + r.shape[1]] = r
    return canvas


def fade(i, n):
    return max(0.0, min(1.0, (i + 1) / FADE, (n - i) / FADE))


def write_video():
    writer = cv2.VideoWriter(SILENT, cv2.VideoWriter_fourcc(*"mp4v"), FPS, (W, H))
    if not writer.isOpened():
        sys.exit("could not open the video writer")
    sections = []  # (name, frames)

    def put_card(img, name):
        for i in range(CARD):
            writer.write((img * fade(i, CARD)).astype(np.uint8))
        sections.append((name, CARD))

    def put_frames(paths, name):
        n = len(paths)
        for i, p in enumerate(paths):
            f = fit(cv2.imread(p))
            a = fade(i, n)
            writer.write(f if a >= 1 else (f * a).astype(np.uint8))
        sections.append((name, n))

    put_card(card([("THÁNH GIÓNG", TITLE, GOLD), ("Bản hùng ca đất Việt · Gameplay Demo", SUB, CREAM)]), "title")
    put_frames(REEL, "reel")
    if STORY:
        put_card(card([("HỒI II · RÈN THÉP & XUẤT TRẬN", font("timesbd.ttf", 70), GOLD),
                       ("Trang bị vua ban · Gươm sắt · Tre ngà", SUB, CREAM)]), "story-card")
        put_frames(STORY, "story")
    put_card(card([("THÁNH GIÓNG", TITLE, GOLD), ("Unity 6 · URP · Windows x64", SUB, CREAM)]), "end")
    writer.release()
    return sections


def load(name):
    with wave.open(os.path.join(AUDIO, name)) as w:
        assert w.getframerate() == SR and w.getsampwidth() == 2
        data = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float32) / 32768.0
        return data.reshape(-1, w.getnchannels()).mean(axis=1)


def mix(sections):
    """Music bed: village for the open-world reel, forge for the story card, battle for the story, ascension for the finish."""
    total = sum(n for _, n in sections) / FPS
    out = np.zeros(int(total * SR) + SR, np.float32)

    def lay(track, start_s, end_s, gain, fade_s=1.2, offset_s=0.0):
        a, b = int(start_s * SR), min(int(end_s * SR), len(out))
        seg = track[int(offset_s * SR): int(offset_s * SR) + (b - a)]
        seg = np.pad(seg, (0, max(0, (b - a) - len(seg))))
        env = np.ones(len(seg), np.float32)
        k = min(len(seg) // 2, int(fade_s * SR))
        env[:k] = np.linspace(0, 1, k)
        env[-k:] = np.linspace(1, 0, k)
        out[a:b] += seg * env * gain

    t, starts = 0.0, {}
    for name, n in sections:
        starts[name] = (t, t + n / FPS)
        t += n / FPS
    village, forge, battle, asc = load("music_village.wav"), load("music_forge.wav"), load("music_battle.wav"), load("music_ascension.wav")
    s0, e0 = starts["title"][0], starts["reel"][1]
    lay(village, s0, e0, 0.55)
    if "story" in starts:
        story_start, story_end = starts["story-card"][0], starts["story"][1]
        mid = story_start + (story_end - story_start) * 0.72   # combat first, ascension music for the last stretch
        lay(forge, story_start, story_start + 12, 0.55)
        lay(battle, story_start + 10, mid, 0.6)
        lay(asc, mid - 1.0, story_end + CARD / FPS, 0.65, fade_s=2.0)
    else:
        lay(village, e0, total, 0.45)
    peak = float(np.abs(out).max())
    if peak > 0:
        out *= 0.85 / peak   # the source music is quiet; normalise the mix to a sensible level
    pcm = (out[: int(total * SR)] * 32767).astype(np.int16)
    with wave.open(MIX, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())


def mux():
    import imageio_ffmpeg
    exe = imageio_ffmpeg.get_ffmpeg_exe()
    cmd = [exe, "-y", "-i", SILENT, "-i", MIX, "-c:v", "libx264", "-preset", "medium", "-crf", "20", "-pix_fmt", "yuv420p",
           "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", OUT]
    subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)


def main():
    if not REEL:
        sys.exit("No reel frames. Run ThanhGiongDemoRecorder in Play mode first.")
    sections = write_video()
    mix(sections)
    mux()
    for p in (SILENT, MIX):
        os.remove(p)
    print("wrote", OUT, "%.1f MB" % (os.path.getsize(OUT) / 1048576), sections)


if __name__ == "__main__":
    main()
