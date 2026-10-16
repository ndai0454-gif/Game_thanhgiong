# Unity 6.3 LTS — Breaking Changes

**Last verified:** 2026-02-13

This document tracks breaking API changes and behavioral differences between Unity 2022 LTS
(likely in model training) and Unity 6.3 LTS (current version). Organized by risk level.

## HIGH RISK — Will Break Existing Code

### Entities/DOTS API Complete Overhaul
**Versions:** Entities 1.0+ (Unity 6.0+)

```csharp
// ❌ OLD (pre-Unity 6, GameObjectEntity pattern)
public class HealthComponent : ComponentData {
    public float Value;
}

// ✅ NEW (Unity 6+, IComponentData)
public struct HealthComponent : IComponentData {
    public float Value;
}

// ❌ OLD: ComponentSystem
public class DamageSystem : ComponentSystem { }

// ✅ NEW: ISystem (unmanaged, Burst-compatible)
public partial struct DamageSystem : ISystem {
    public void OnCreate(ref SystemState state) { }
    public void OnUpdate(ref SystemState state) { }
}
```

**Migration:** Follow Unity's ECS migration guide. Major architectural changes required.

---

### Input System — Legacy Input Deprecated
**Versions:** Unity 6.0+

```csharp
// ❌ OLD: Input class (deprecated)
if (Input.GetKeyDown(KeyCode.Space)) { }

// ✅ NEW: Input System package
using UnityEngine.InputSystem;
if (Keyboard.current.spaceKey.wasPressedThisFrame) { }
```

**Migration:** Install Input System package, replace all `Input.*` calls with new API.

---

### URP/HDRP Renderer Feature API Changes
**Versions:** Unity 6.0+

```csharp
// ❌ OLD: ScriptableRenderPass.Execute signature
public override void Execute(ScriptableRenderContext context, ref RenderingData data)

// ✅ NEW: Uses RenderGraph API
public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
```

**Migration:** Update custom render passes to use RenderGraph API.

---

## MEDIUM RISK — Behavioral Changes

### Addressables — Asset Loading Returns
**Versions:** Unity 6.2+

Asset loading failures now throw exceptions by default instead of returning null.
Add proper exception handling or use `TryLoad` variants.

```csharp
// ❌ OLD: Silent null on failure
var handle = Addressables.LoadAssetAsync<Sprite>("key");
var sprite = handle.Result; // null if failed

// ✅ NEW: Throws on failure, use try/catch or TryLoad
try {
    var handle = Addressables.LoadAssetAsync<Sprite>("key");
    var sprite = await handle.Task;
} catch (Exception e) {
    Debug.LogError($"Failed to load: {e}");
}
```

---

### Physics — Default Solver Iterations Changed
**Versions:** Unity 6.0+

Default solver iterations increased for better stability.
Check `Physics.defaultSolverIterations` if you rely on old behavior.

---

## LOW RISK — Deprecations (Still Functional)

### UGUI (Legacy UI)
**Status:** Deprecated but supported
**Replacement:** UI Toolkit

UGUI still works but UI Toolkit is recommended for new projects.

---

### Legacy Particle System
**Status:** Deprecated
**Replacement:** Visual Effect Graph (VFX Graph)

---

### Old Animation System
**Status:** Deprecated
**Replacement:** Animator Controller (Mecanim)

---

## Platform-Specific Breaking Changes

### WebGL
- **Unity 6.0+**: WebGPU is now the default (WebGL 2.0 fallback available)
- Update shaders for WebGPU compatibility

### Android
- **Unity 6.0+**: Minimum API level raised to 24 (Android 7.0)

### iOS
- **Unity 6.0+**: Minimum deployment target raised to iOS 13

---

## Migration Checklist

When upgrading from 2022 LTS to Unity 6.3 LTS:

- [ ] Audit all DOTS/ECS code (complete rewrite likely needed)
- [ ] Replace `Input` class with Input System package
- [ ] Update custom render passes to RenderGraph API
- [ ] Add exception handling to Addressables calls
- [ ] Test physics behavior (solver iterations changed)
- [ ] Consider migrating UGUI to UI Toolkit for new UI
- [ ] Update WebGL shaders for WebGPU
- [ ] Verify minimum platform versions (Android/iOS)

---

**Sources:**
- https://docs.unity3d.com/6000.0/Documentation/Manual/upgrade-guides.html
- https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/upgrade-guide.html

---

## 6.3 → 6.6 (6000.6.0f1)

**Last verified:** 2026-10-16
**Sources:** https://docs.unity.com/en-us/engine/6000.7/manual/upgrade-guides/upgrade-guide-unity66 (page titled "Upgrade to Unity 6.6", 6.5 → 6.6 only) · https://docs.unity3d.com/6000.6/Documentation/Manual/WhatsNewUnity66.html
**NOT SOURCEABLE — 6.3 → 6.4 and 6.4 → 6.5 changes: those upgrade guides were not fetched this run.**

### Likely to touch this project
- **Dynamic batching is obsolete** (Graphics). See Unity's "Choose a method for optimizing calls". Do not rely on it for many small meshes (soldiers, props); use SRP Batcher / GPU Resident Drawer / instancing.
- **YAML serialization change**: word wrap disabled, *Reduce Version Control Noise* removed, `serializeInlineMappingsOnOneLine` setter is a no-op. Expect large unrelated diffs in `.unity`/`.prefab`/`.asset` once; reserialize once with `AssetDatabase.ForceReserializeAssets` and commit it on its own.
- **`UNITY_64` and `DEVELOPMENT_BUILD` deprecated** (analyzer warnings UAC0008 / UAC0009; removed with a hard compile error in 6.8). Use `UNITY_ENABLE_CHECKS`, `UNITY_INCLUDE_INSTRUMENTATION`, or `Debug.isDebugBuild`. New *Managed Code Variant* player setting (`PlayerSettings.Get/SetManagedCodeVariant`); the default Release variant strips SRP debug overlays and the Rendering Debugger. `BuildOptions.ForceEnableAssertions` and `CoreBuildData.developmentBuild` are `[Obsolete]`.
- **Cinemachine 3 is a core package**; Cinemachine 2 projects are migrated (different API and data format).
- **Burst** is a built-in module (`Unity.Burst`); **Performance Testing** (`com.unity.test-framework.performance`) is a core package.
- **Rendering Debugger legacy state removed** (`DebugState*`, legacy `DebugUIDrawer`, `DebugStateAttribute`, `LegacyStyles` → compile error). Use `ISerializedDebugDisplaySettings` and `DebugUI.Widget.Create()` returning a `VisualElement`.
- **Progressive CPU Lightmapper deprecated** → Unity Compute Light Baker or Progressive GPU.
- **`com.unity.render-pipelines.core` no longer depends on Terrain**; add `com.unity.modules.terrain` if SRP code needs Terrain.
- **UI Toolkit**: `DefaultEventSystem.LegacyInputProcessor` removed (`UIToolkitInputConfiguration.SetRuntimeInputBackend` is a compile error); `UxmlFactory`/`UxmlTraits` removed → `UxmlElement`/`UxmlAttribute`.
- **Enter Play Mode** defaults to "Reload Scene only" in *new* projects (existing projects keeping their setting is an inference from "in new projects").

### Platform-specific (not Windows x64)
- Android: Legacy/Round icons removed; OpenGL ES 3.0 dropped (min 3.1); `PlayerSettings.openGLRequireES31` deprecated; `-androidChainedSignalHandlerBehavior legacy` removed.
- Intel macOS (x86_64) deprecated for Editor, standalone players and simulators.

### Entities (only if the ECS path is adopted)
- Managed components (class `IComponentData`, managed shared components, `SystemAPI.ManagedAPI`) deprecated; Entities Hierarchy window deprecated.
