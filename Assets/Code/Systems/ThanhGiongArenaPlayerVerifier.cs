#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Opt-in development player test. Uses keyboard/mouse inputs and normal melee damage.</summary>
public sealed class ThanhGiongArenaPlayerVerifier : MonoBehaviour
{
    [Serializable] public sealed class Result
    {
        public string verdict, stage;
        public int runtimeErrors, kills, attacks, bambooPresses, wave, alive, spawned;
        public string position;
        public float gameplaySeconds, realSeconds, remainingHealth, p95FrameMs, worstFrameMs;
        public bool pausePassed, movementPassed, restartPassed;
        public string[] visitedStages;
    }
    readonly List<string> visited = new List<string>();
    readonly List<float> frames = new List<float>();
    readonly Result result = new Result();
    ThanhGiongArenaFlow arena;
    ThanhGiongCampaignController campaign;
    Keyboard keyboard;
    Mouse mouse;
    string folder, previousStage;
    bool click, enter, interact, escape, retry, dodge;
    Vector2 move;
    bool run;
    float start, nextAttack, nextInteraction, nextDodge;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if (Application.isEditor || !Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "--verify-arena") < 0) return;
        new GameObject("Arena development player input verifier").AddComponent<ThanhGiongArenaPlayerVerifier>();
    }
    void Awake()
    {
        Application.runInBackground = true; Application.targetFrameRate = 60;
        Application.logMessageReceived += Log;
        keyboard = InputSystem.AddDevice<Keyboard>("Arena verification keyboard");
        mouse = InputSystem.AddDevice<Mouse>("Arena verification mouse");
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification")); Directory.CreateDirectory(folder);
    }
    void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) result.runtimeErrors++; }
    IEnumerator Start()
    {
        yield return new WaitForSeconds(1.5f);
        arena = FindAnyObjectByType<ThanhGiongArenaFlow>();
        if (arena == null) { Finish("FAIL: arena missing"); yield break; }
        campaign = arena.GetComponent<ThanhGiongCampaignController>(); start = Time.realtimeSinceStartup;
        yield return Capture("01-intro.png");
        enter = true; yield return null; yield return null; enter = false;
        float deadline = Time.realtimeSinceStartup + 600;
        bool pauseChecked = false;
        float nextReport = 0;
        Vector3 initial = arena.transform.position;
        while (Time.realtimeSinceStartup < deadline)
        {
            string stage = arena.CurrentStage.ToString();
            if (stage != previousStage)
            {
                previousStage = stage; visited.Add(stage);
                yield return Capture($"{visited.Count + 1:00}-{stage}.png");
            }
            if (Time.realtimeSinceStartup >= nextReport)
            {
                nextReport = Time.realtimeSinceStartup + 2;
                File.WriteAllText(Path.Combine(folder, "progress.json"), JsonUtility.ToJson(new Result {
                    stage = stage, gameplaySeconds = arena.ElapsedSeconds, kills = campaign.Kills, runtimeErrors = result.runtimeErrors,
                    wave = arena.waveManager.WaveIndex, alive = arena.waveManager.AliveCount, spawned = arena.waveManager.Spawned,
                    remainingHealth = campaign.Health, position = arena.transform.position.ToString(), attacks = result.attacks
                }, true));
            }
            frames.Add(Time.unscaledDeltaTime * 1000);
            result.movementPassed |= Vector3.Distance(initial, arena.transform.position) > 2;
            if (!pauseChecked && arena.CanFight && arena.waveManager.AliveCount > 0)
            {
                pauseChecked = true; move = Vector2.zero; click = false; escape = true; yield return null; yield return null; escape = false;
                yield return null;
                float elapsed = arena.ElapsedSeconds; int count = arena.waveManager.Spawned;
                yield return new WaitForSecondsRealtime(.65f);
                result.pausePassed = arena.Paused && elapsed == arena.ElapsedSeconds && count == arena.waveManager.Spawned;
                escape = true; yield return null; yield return null; escape = false; yield return null;
            }
            if (arena.CanFight)
            {
                var enemy = arena.waveManager.LivingEnemies.Where(e => e != null && e.HealthRatio > 0).OrderBy(e => (e.transform.position - arena.transform.position).sqrMagnitude).FirstOrDefault();
                click = false; move = Vector2.zero; run = false; dodge = false;
                if (enemy != null)
                {
                    Vector3 delta = enemy.transform.position - arena.transform.position; delta.y = 0;
                    float distance = delta.magnitude;
                    float facing = Vector3.Dot(arena.transform.forward, delta.normalized);
                    if (distance > 2.2f || facing < .75f) Steer(delta);
                    if (distance > 3.5f) run = true;

                    bool threatening = arena.waveManager.LivingEnemies.Any(e => e != null && e.HealthRatio > 0 &&
                        (e.transform.position - arena.transform.position).sqrMagnitude < 25f &&
                        (e.CurrentState == ThanhGiongEnemy.EnemyState.TelegraphingAttack || e.CurrentState == ThanhGiongEnemy.EnemyState.Slamming));
                    if (threatening && Time.time >= nextDodge)
                    {
                        dodge = true;
                        nextDodge = Time.time + 1.2f;
                    }

                    if (distance < 4.8f && facing > .4f && Time.time >= nextAttack)
                    {
                        click = true; result.attacks++;
                        nextAttack = Time.time + (campaign.CurrentWeapon == ThanhGiongCampaignController.Weapon.Bamboo ? .84f : .42f);
                    }
                }
            }
            else if (arena.CurrentStage == ThanhGiongArenaFlow.Stage.SeekingBamboo)
            {
                click = false; dodge = false;
                if (!arena.NearBamboo) Steer(arena.bamboo.transform.position - arena.transform.position);
                else
                {
                    move = Vector2.zero; run = false;
                    interact = Time.time >= nextInteraction;
                    if (interact) { result.bambooPresses++; nextInteraction = Time.time + .45f; }
                }
            }
            else { move = Vector2.zero; run = click = interact = dodge = false; }
            if (arena.CurrentStage == ThanhGiongArenaFlow.Stage.Defeat) { Finish("FAIL: player defeated during input playthrough"); yield break; }
            if (arena.CurrentStage == ThanhGiongArenaFlow.Stage.Victory)
            {
                result.gameplaySeconds = arena.ElapsedSeconds; result.kills = campaign.Kills; result.remainingHealth = campaign.Health;
                retry = true; yield return null; yield return null; retry = false; yield return null; yield return null;
                result.restartPassed = arena.CurrentStage == ThanhGiongArenaFlow.Stage.Intro && campaign.Health == campaign.maxHealth && arena.BambooPulls == 0 && arena.bamboo.gameObject.activeSelf && arena.waveManager.AliveCount == 0 && arena.GetComponent<CharacterController>().enabled;
                yield return Capture("10-restarted.png");
                Finish(result.runtimeErrors == 0 && result.pausePassed && result.movementPassed && result.restartPassed ? "PASS" : "FAIL: validation checks"); yield break;
            }
            yield return null;
            click = false; interact = false; dodge = false;
        }
        Finish("FAIL: playthrough timeout");
    }
    void Steer(Vector3 delta)
    {
        delta.y = 0; delta.Normalize();
        Vector3 forward = Camera.main.transform.forward; forward.y = 0; forward.Normalize();
        Vector3 right = Camera.main.transform.right; right.y = 0; right.Normalize();
        move = new Vector2(Vector3.Dot(delta, right), Vector3.Dot(delta, forward)); run = true;
    }
    void LateUpdate()
    {
        var keys = new List<Key>();
        if (move.x > .32f) keys.Add(Key.D); if (move.x < -.32f) keys.Add(Key.A);
        if (move.y > .32f) keys.Add(Key.W); if (move.y < -.32f) keys.Add(Key.S);
        if (run) keys.Add(Key.LeftShift); if (dodge) keys.Add(Key.C); if (enter) keys.Add(Key.Enter); if (interact) keys.Add(Key.E);
        if (escape) keys.Add(Key.Escape); if (retry) keys.Add(Key.R);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
        var state = new MouseState { position = new Vector2(Screen.width * .5f, Screen.height * .5f) };
        state = state.WithButton(MouseButton.Left, click);
        InputSystem.QueueStateEvent(mouse, state);
    }
    IEnumerator Capture(string file)
    {
        yield return new WaitForEndOfFrame();
        Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(folder, file), frame.EncodeToPNG()); Destroy(frame);
    }
    void Finish(string verdict)
    {
        result.verdict = verdict; result.realSeconds = Time.realtimeSinceStartup - start;
        result.stage = arena != null ? arena.CurrentStage.ToString() : "Missing";
        result.visitedStages = visited.ToArray();
        if (arena != null)
        {
            if (result.gameplaySeconds <= 0f) result.gameplaySeconds = arena.ElapsedSeconds;
            result.position = arena.transform.position.ToString();
            if (arena.waveManager != null)
            {
                result.wave = arena.waveManager.WaveIndex;
                result.alive = arena.waveManager.AliveCount;
                result.spawned = arena.waveManager.Spawned;
            }
        }
        if (campaign != null)
        {
            if (result.kills <= 0) result.kills = campaign.Kills;
            if (result.remainingHealth <= 0f) result.remainingHealth = campaign.Health;
        }
        if (frames.Count > 0) { frames.Sort(); result.p95FrameMs = frames[Mathf.Min(frames.Count - 1, Mathf.FloorToInt(frames.Count * .95f))]; result.worstFrameMs = frames[frames.Count - 1]; }
        File.WriteAllText(Path.Combine(folder, "result.json"), JsonUtility.ToJson(result, true));
        Application.Quit(verdict == "PASS" ? 0 : 1);
    }
    void OnDestroy()
    {
        Application.logMessageReceived -= Log;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (mouse != null) InputSystem.RemoveDevice(mouse);
    }
}
#endif
