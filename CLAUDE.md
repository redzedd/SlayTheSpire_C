# SlaytheSpire_C — Project Instructions

> **Language Rule (HARD REQUIREMENT):** All user-facing output, code comments, doc strings, and log messages MUST be written in **Traditional Chinese (繁體中文)**. This file itself is in English; everything you produce for the user/codebase is in Traditional Chinese.

---

## 0. Product & Audience

- One-line product: 復刻 Slay the Spire 核心循環(卡牌戰鬥 + 爬塔地圖 + 遺物/藥水)的學習專案,目標是做出可玩版本。
- Target platform: PC, mouse-first(拖拉出牌為主要互動)。Mobile 未列入目標。
- The user is a game developer & engineer; normal engineering vocabulary is fine.
- Every system stays designer-tunable in the editor: serialized/tunable fields + 繁中 tooltips over hardcoded values; data assets (ScriptableObject) for tuning; sensible defaults; setup >5 manual steps = smell.
- Reply shape for features: brief code summary → concrete setup/wiring steps (exact names) → risks.

## 1. Stack Overview

|Item|Value|
|---|---|
|Type|Game (Unity)|
|Engine|Unity **6000.4.3f1** (from `ProjectSettings/ProjectVersion.txt`)|
|Render pipeline|URP 17.4.0 (`Assets/Settings/PC_RPAsset.asset` is the PC pipeline asset)|
|Input|Input System 1.19.0 (`Assets/InputSystem_Actions.inputactions` — template file, NOT wired yet)|
|Target platform|PC (mouse-first)|
|Source root|`Assets/Scripts/` — `Core/` (pure logic) + `Tests/EditMode/`; glue/UI layers not created yet|
|Build command|unverified (no build has been run yet)|
|Test command|EditMode tests via TestRunnerApi — see `.claude/skills/verify/` (verified 2026-08-03, 9/9 green)|
|Version control|git, initialized 2026-08-03, standard Unity .gitignore|

## 2. Dependencies (use these — do NOT reinvent)

|Dependency|Where / Version|Role|
|---|---|---|
|URP|com.unity.render-pipelines.universal 17.4.0|Render pipeline. Settings live in `Assets/Settings/`|
|Input System|com.unity.inputsystem 1.19.0|Input. Template `.inputactions` exists but is not referenced by any code yet|
|uGUI|com.unity.ugui 2.0.0|UI candidate route (see §2.1 below — UI route NOT decided yet)|
|Unity Test Framework|com.unity.test-framework 1.6.0|**IN USE** by `STS.Core.Tests`|
|AI Assistant|com.unity.ai.assistant 2.17.0-pre.1|**Do NOT remove.** [推論] Provides the `IRunCommand` dynamic-execution base that unity-mcp `Unity_RunCommand` runs on (verify skill depends on it)|
|Visual Scripting|com.unity.visualscripting 1.9.11|**INSTALLED BUT UNUSED — do NOT use.** All gameplay logic is written in C#|
|Timeline / Multiplayer Center / Collab Proxy|template defaults|**UNUSED** — template leftovers, do not build on them without asking|

**Not installed — do not suggest / do not reference without asking first:** DOTween, Cinemachine, Animancer, any third-party plugin. Adding packages requires explicit user approval (global rule).

### 2.1 UI route — NOT DECIDED YET (decide at the first real UI task)

Analysis done 2026-08-03; the user will pick when the first UI task arrives. Do NOT start UI work without settling this:

- **Option A: uGUI + DOTween (recommended by analysis):** mature route for card feel — free card transforms, fan-shaped hands, drag physics, punch/shake effects, particle-with-UI mixing. Requires installing DOTween (ask first). Global skills `dotween-pro` + `ui-motion-fx` apply.
- **Option B: UI Toolkit:** Unity 6 native, global skill `unity-ui-toolkit` applies, but weaker ecosystem for card-game-style free-form animation; higher risk for this genre.
- Until decided: any UI prototype goes in a throwaway scene, and nothing may depend on either route.

### Usage Rules

- **Input:** all player input goes through Input System action maps (edit `InputSystem_Actions.inputactions` or replace it); never `Input.GetKey`/legacy input.
- **Rendering:** URP is active; do not add Built-in RP shaders/assets.
- **Gameplay logic:** C# only; Visual Scripting is banned.
- **MCP bridges (verified 2026-08-03):** `unity-mcp` tools work against this project. `coplay-mcp` bridge is NOT installed in this project — its tools fail with "Editor is not running at the specified project root"; do not waste time retrying it, use unity-mcp.

## 3. Code Style

- C#: PascalCase types/methods/properties; camelCase locals/parameters; `_camelCase` private fields is acceptable but stay consistent per file.
- Namespaces: pure logic under `STS.Core.*`; tests under `STS.Core.Tests`.
- Comments: Traditional Chinese, only for non-obvious *why* (see §7).
- Serialized fields: `[SerializeField]` + 繁中 `[Tooltip]` for every designer-facing value.

## 4. Architecture

- **Data/config:** card/enemy/relic definitions will be ScriptableObject assets under `Assets/Data/<category>/` (e.g. `Assets/Data/Cards/`); none exist yet — the first one establishes the pattern.
- **Logic:** pure C# in `Assets/Scripts/Core/` (assembly `STS.Core`, `noEngineReferences: true` — it cannot touch UnityEngine, keep it that way; this is what makes it unit-testable).
- **Glue:** engine-facing layer (ScriptableObjects, MonoBehaviours, input/UI wiring) goes in `Assets/Scripts/Game/` under a new assembly named **`STS.Game`** (references `STS.Core`, engine refs allowed). Not created yet — create the asmdef together with the first glue script.

### Current Systems Map (verified against code 2026-08-03 — refresh via /harness-audit)

- **Combat math:** `STS.Core.Combat.CombatMath` (`Assets/Scripts/Core/Combat/CombatMath.cs`) — attack damage formula (strength add → weak ×0.75 → vulnerable ×1.5, float-multiply then single floor) + block absorption. This is the ONLY gameplay code that exists.
- **Tests:** `Assets/Scripts/Tests/EditMode/CombatMathTests.cs` (assembly `STS.Core.Tests`) — 9 tests, all green (2026-08-03). **This pair is the template: every new pure-logic module gets the same treatment (logic in STS.Core, NUnit tests in STS.Core.Tests).**
- **Assemblies:** `STS.Core` (pure, no engine refs) / `STS.Core.Tests` (Editor-only, references STS.Core + nunit). New runtime glue code will need a third asmdef or Assembly-CSharp.
- **Scene:** `Assets/Scenes/SampleScene.unity` — URP template scene, empty of gameplay.
- **Known stale paths:** `Assets/InputSystem_Actions.inputactions` is a template action map wired to nothing — treat contents as placeholder, not design.
- **Known debris:** `Assets/TutorialInfo/` + `Assets/Readme.asset` — URP template leftovers; ignore, do not "fix"/delete during unrelated work.

## 5. Performance / Hot Paths

- Zero allocations in per-frame paths (global rule). Card game hot spots to watch later: hand layout re-calc, hover/drag per-frame logic, damage-number spawning (pool them).
- No perf budget measured yet — establish one when a real combat scene exists.

## 6. Logging & Error Handling

- Use `Debug.Log/LogWarning/LogError` with 繁中 messages; no custom logger exists.
- No empty catch blocks; let unexpected exceptions surface — the verify smoke step scans for them.

## 7. Comments & Documentation

- Default to no comments; names explain *what*. Comment only non-obvious *why* (invariants, StS rule references, workarounds), in Traditional Chinese, one short line.

## 8. Working Protocol for the AI

1. **Plan first (in Traditional Chinese)** — approach, which dependencies apply, which files change.
2. **Check §2 triggers** — Input System for input, URP for rendering, C# only for logic; UI work requires settling §2.1 first.
3. **Confirm before large refactors.** Single-file edits and additive features proceed directly.
4. **Finish the job.** No TODO, no stubs.
5. **If unsure, say so.** Do not fabricate API signatures — verify against installed source or `unity_reflect`.
6. **Verify before "done".** Follow `.claude/skills/verify/`: compile check is mandatory after any code change; EditMode tests after logic changes; play-mode smoke + log scan after behavior/scene changes; report evidence tiers ([驗證]/[推論]/[假設]); never report completion without evidence.
7. **Pure logic goes to STS.Core with tests** — copying the CombatMath + CombatMathTests pattern is the default for rules/calculations/state machines.
