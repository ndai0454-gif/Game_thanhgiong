# Thánh Gióng — Claude Code Game Studios

Indie game development managed through 49 coordinated Claude Code subagents.
Each agent owns a specific domain, enforcing separation of concerns and quality.

## Technology Stack

- **Engine**: Unity 6000.6.0f1 (URP 17.6)
- **Language**: C#
- **Version Control**: Git with trunk-based development
- **Build System**: Unity Build Pipeline — Windows x64 ra `Builds/Windows`
- **Asset Pipeline**: Unity AssetDatabase (FBX/glTF, Meshy, KayKit, Kenney); âm thanh nạp qua `Resources/ThanhGiongAudio`

> **Note**: Engine-specialist agents exist for Godot, Unity, and Unreal with
> dedicated sub-specialists. Use the set matching your engine.

## Project Structure

@.claude/docs/directory-structure.md

> Project này là project Unity có sẵn: code gameplay ở `Assets/Code/`, test ở `Assets/Tests/`,
> tài liệu ở `Docs/` (trên Windows `docs/` và `Docs/` là cùng một thư mục).

## Engine Version Reference

<!-- ENGINE-REFERENCE-IMPORT: the line below is engine-specific. /setup-engine
     rewrites it to @docs/engine-reference/<engine>/VERSION.md for the chosen
     engine, so a Unity or Unreal project stops loading the Godot reference every
     session. It defaults to Godot (the template's example engine); skills that
     need the pinned version read docs/engine-reference/<engine>/VERSION.md on
     demand regardless of this import. -->
@Docs/engine-reference/unity/VERSION.md


## Technical Preferences

`project.yaml` at the repo root is the primary config store — engine, specialists,
naming, platform, performance, modes. Skills resolve it via `resolve_config`
(see `.claude/docs/config-resolution.md`).

`.claude/docs/technical-preferences.md` is the **legacy fallback**, read on demand
when a key is absent from `project.yaml`. It is no longer imported here: before
`/setup-engine` runs it is almost entirely `[TO BE CONFIGURED]` placeholders, and
after it runs `project.yaml` holds the real values.

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
  (exempt: `active.md` appends, agent memory, a subagent's new file at the path
  its orchestrating skill named; see `.claude/rules/agent-memory.md`)
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction
- A skipped or dismissed question is not an answer: act on nothing, re-ask in
  plain text and wait
- Started outside this file's folder? Warn first: hooks, deny rules and the
  ask-first mode load only when Claude Code starts here

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

> **First session?** If the project has no engine configured and no game concept,
> run `/start` to begin the guided onboarding flow.

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

Read `.claude/docs/context-management.md` on demand — it is a reference, not
session context. Two of its conventions are load-bearing and cited by name
elsewhere in the repo, so they are restated here rather than lost:

- **`production/session-state/active.md` is the session checkpoint.** The file is
  the memory, not the conversation. Read it first after any compaction, crash, or
  `/clear`.
- **Helpers in `.claude/scripts/` emit observations, never verdicts.** A script
  that scores or judges will eventually contradict a mode or override it cannot
  see. (Cited by `artifact-check.sh` and `adr-dep-graph.sh`.)


---

## Dự án Thánh Gióng (quy ước riêng — ưu tiên cao nhất)

Game hành động 2.5D isometric về truyền thuyết Thánh Gióng. 6 màn trong `Assets/Scenes/ThanhGiongWorld/`
(Làng Gióng → Kinh thành rèn thép → Pháo đài giặc Ân → Thung lũng vượt sông → Núi Sóc → Đỉnh Sóc hóa thánh).

### Skills riêng của project (`.claude/skills/`)
- `unity-ai-game-creator`: quy trình GDD, scene blueprint, prompt tạo asset, checklist build/release.
- `game-developer`: object pooling, cache component, state machine, mục tiêu 60 FPS, profile trước khi tối ưu.
- `unity-ecs-patterns`: chỉ dùng cho hệ thống nhiều thực thể (ví dụ đám quân giặc); không ép chuyển toàn bộ sang ECS.
- Bộ Claude Code Game Studios (49 agent, 74 slash command như `/start`, `/adopt`, `/dev-story`, `/team-polish`) đã được cài thêm; dùng agent `unity-specialist` và các sub-specialist Unity.

### Quy ước
- Không `Instantiate/Destroy` trong `Update`; dùng pool cho đạn, VFX, địch.
- Cache `GetComponent` trong `Awake`; không dùng `Find*` trong vòng lặp.
- Dữ liệu cân bằng đưa vào ScriptableObject, không hardcode.
- Code gameplay ở `Assets/Code/`, kiểm thử ở `Assets/Tests/` (EditMode + PlayMode).
- Build Windows x64 ra `Builds/Windows`, video ra `Builds/Videos`.

### Quy trình phát hành
1. Chạy test EditMode + PlayMode, không còn lỗi console.
2. Build `windows64` (scenes theo Build Settings).
3. Chạy thử bản build, nén zip để phát hành.
