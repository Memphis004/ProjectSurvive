# Stage 12 — Save, resume, settings

**Date:** 2026-10-22

## 1. Summary

A run now survives a reload. `RunCheckpointDto` is a plain MessagePack envelope (schema + config hash,
run id, seed, the three RNG streams, floor, HP, bia, bag with placements, chest, shop slots and their
flags, refresh count, last settlement) built from a `RunCheckpoint` snapshot the domain hands out; no
`IBattleSession`, ECS entity or `GameObject` is reachable from it, and the floor is represented by the
seed it will be fought with. `CheckpointService` is the only writer: one save at a time behind a
`SemaphoreSlim`, and a revision counter that lets a newer request win instead of an older payload
landing last. The camp autosaves half a second after a run change and **awaits** the write before the
scene changes to Battle, so the checkpoint that is on disk is the floor that is about to start.

On WebGL `Application.persistentDataPath` is an Emscripten IDBFS mount; the flush is `FS.syncfs`, but
its callback does **not** resume an awaiting `Task` in the single-threaded player. The page therefore
only records the outcome and `WebGlFlushReceiver.Update` polls it, completing the save inside a Unity
frame ([ADR 0033](../adr/0033-application-awaits-capture-the-caller-context.md)). Storage that is off,
corrupt, older, newer or saved against different data is refused and reported
(`save.storage_unavailable`, `save.corrupt`, `save.schema_too_new`/`_too_old`,
`save.config_mismatch`) — never reported as a success. A finished run deletes its checkpoint.

The title scene gained the settings screen (volume, TH/EN switching immediately through R3, starting
battle speed), stored under their own key and applied at boot before the first scene is drawn.

The settings overlay was looked at in the **real WebGL player** before closing the stage: the title screen
with the panel closed and the panel open at 1280×720 are `artifacts/ui/stage12-web-title-closed-1280x720.png`
and `artifacts/ui/stage12-web-settings-1280x720.png` (a 2.5 % pixel diff whose bounding box is the panel
itself), and the Editor capture tool reports the panel's nine controls, at both sizes, with **no
overlaps and no tiny labels** (`artifacts/ui/ui-report.md`).

One real bug came out of the PlayMode suite rather than the browser: the new save-failure label is a
1200 × 40 TMP band across the shelf row, and TMP makes new text a raycast target by default, so every
click on a shelf counted as "pointer over UI" and rented nothing. `CampSceneBuilder` now sets
`raycastTarget = false` on it and `CampWorldPlayModeTests` was 9/9 afterwards.

## 2. Acceptance

| # | Item | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Refresh at the camp resumes the shop, the bag, HP and bia from the checkpoint | **PASS** | `tests/web/persistence.spec.ts` — *a refresh at the camp resumes the shop, the bag, HP and bia*: before `{bia:5, weight:2.6, revision:6, chest used 0, purchased slot 0}` → after reload `resumed:true`, same bia/weight/revision and the same four offers ([`artifacts/stage12/playwright-final.log`](../../artifacts/stage12/playwright-final.log)) |
| 2 | Refresh during a floor returns to the checkpoint before the floor, without the floor's bia or reward | **PASS** | same spec — *a refresh during a floor returns to the camp checkpoint, without the floor's reward* (the reload lands at the camp on the saved floor with the pre-floor purse; the same floor seed is offered again) |
| 3 | Storage switched off warns and never reports a successful save | **PASS** | same spec — *storage that is switched off warns and never reports a successful save*: `storageAvailable:false`, `lastSaveOk:false`, `error:save.storage_unavailable`, and the camp's `saveFailed:true` with the localized warning |
| 4 | Save serializes no `IBattleSession`/ECS state, only `BattleSetup`'s inputs (seed / HP / floor / loadout) | **PASS** | `CheckpointTests.Checkpoint_dto_carries_no_battle_session_or_ecs_state` (reflection over the DTO graph); `RunCheckpointDto` carries `Seed`, `NextBattleSeed` and the bag snapshot, and `RunSession.RestoreCheckpoint` rebuilds the floor through the normal `StartFloor` path |

Work items, one line each:

1. **DTO** — `lib/Runtime/Contracts/Save/RunCheckpointDto.cs` (+ `RunCheckpointCodec`, MessagePack
   source generator). `SchemaVersion` 1, keys explicit and append-only.
2. **Write ordering** — `CheckpointService` keeps `_requestedRevision`/`_writtenRevision` and skips a
   request that a newer one superseded; `CampSceneDirector.UpdateAutosave` debounces 0.5 s on a cheap
   run key and `DepartAsync` awaits `SaveAsync` before `SceneLoader.TryGo(Battle)`.
3. **Adapters** — `FileCheckpointStore` (desktop, temp-then-replace, `key.bak` rotation) and
   `PersistentDataCheckpointStore` (Unity `persistentDataPath`, `ProjectSurvive_StorageAvailable`, polled
   `FS.syncfs` flush with a 10 s timeout). Both report `IsAvailable`/`UnavailableReason` and typed write results.
4. **Reload during a floor** — the camp checkpoint is the last one written, so a reload returns to it;
   `RunCheckpoint` restores the shop without re-rolling (`Restored_shop_continues_the_same_rolls`).
5. **Validation and backup** — schema first, then config hash/schema version, then bounds; a corrupt
   primary falls back to `.bak`; a run that ends deletes both. Tests cover old/new schema, config
   mismatch, missing, corrupt with and without backup, and concurrent writes.
6. **Settings** — `GameSettings` (volume 0–1, TH/EN, speed 1×/2×), `SettingsService` with the same
   write interlock, `SettingsPresenter` + `SettingsView` on the title canvas; language writes
   `UiState.Language` (R3) and the localization service in one call, so the next `Get` is the new language.

Gates: `-Suite Full` **PASS 12 | FAIL 0 | NOT RUN 2 | WARN 0** (259.4 s, `artifacts/stage12-full.log`)
— the two NOT RUN steps are the Unity ones, which need an Editor attached to the Pipeline, so they
were collected separately: `dotnet test` **591/591**, `unity: EditMode` **172/172**
(`artifacts/stage12/editmode-final.xml`), PlayMode **66/66 across the nine classes**
(`artifacts/stage12/pm-*.json`). The harness also built the WebGL player (**OK**, 121 546 640 B,
`configHash 1066A9BE…`) and ran Playwright: **13 passed in Chrome**, 13 opt-in screenshot cases
skipped (`artifacts/dotnet/playwright.log`; the standalone run is `artifacts/stage12/playwright-final.log`). One earlier `dotnet test` run failed
`BattleFrameBufferTests.Publishing_and_reading_do_not_allocate` with 3 712 B; it passes alone and in
the next full run — the same measurement flake 11d recorded.

## 3. What was not tested, and the risks

- **The bulk PlayMode run is still unusable as a single gate.** All 66 tests in one process reports
  31/65: `IndexOutOfRangeException` from `InputSystemUIInputModule.OnEnable` and *Cached unprocessed
  value unexpectedly became outdated* arrive as unhandled ambient logs, plus the known "click never
  reached the control" input flake (Stage 09l/09k). Every class passes on its own, which is the
  evidence table above; this is a harness limitation, not a claim that the bulk run is green.
- **The save warning is a UI element with no automated geometry check** beyond the PlayMode campaign
  finding it. Its `raycastTarget = false` is now pinned only by those click tests.
- **The IndexedDB flush is proven by the browser tests, not by a crash test.** A tab killed between
  `syncfs` and its callback loses that write; the previous checkpoint (and `.bak`) remains.
- **Release build not measured.** Every browser number here is a Development WebGL build.
- **Firefox is not installed**, so cross-browser persistence is NOT RUN.
- **The camp layout browser case is flaky** (it passed 2/3 in this stage): after two canvas resizes
  its final close click occasionally lands before the player's input module is ready. The click is
  now retried five times instead of three; the flake itself is the one recorded since Stage 09l.
- **Settings are exercised in EditMode and by the capture tool, not by a Playwright keystroke**: the
  browser suite does not press the TH/EN button.

## 4. Commands actually run

```powershell
# library and editor
dotnet test                                                   # 591 passed
unity test --mode EditMode --output artifacts/stage12/editmode.xml UnityProject   # 172/172
# PlayMode per class through the open Editor (Pipeline), e.g.
unity command run_tests --mode playmode --filter CampWorldPlayModeTests --filter_type testName --async_tests
unity command test_status --result-only                       # -> artifacts/stage12/pm-*.json
# browser gate (Editor closed)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -Configuration Development -Force
node tools/serve-web.mjs --root UnityProject/Builds/Web
cd tests/web; npx playwright test --reporter=list              # -> artifacts/stage12/playwright-final.log
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen-wiki.ps1
```
