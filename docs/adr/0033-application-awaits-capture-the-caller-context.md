# ADR 0033 — Application awaits capture the caller's context

- **Status:** Accepted (Stage 12)
- **Date:** 2026-10-21
- **Related:** [ADR 0007](0007-battle-contract-and-asmdef-placement.md) (the layers and their asmdefs),
  [ADR 0004](0004-dotnet-layer-enforcement.md) (the guards that keep them honest)

## Context

Stage 12 gave `lib/Runtime/Application` its first awaits that can actually yield: a checkpoint save
ends with a flush of Unity's IndexedDB filesystem, which finishes on a later browser event. Both
`CheckpointService` and `SettingsService` wrote their awaits the usual library way —
`.ConfigureAwait(false)` — because the Application layer has no UI context to return to and the
habit is correct for server-side .NET.

In the WebGL player that habit deadlocks the save. Measured in the shipped Development build
(`artifacts/stage12/`), with the flush model in place:

- the page answered the flush (`OnFlushed('0')` ran, the checkpoint bytes were in IndexedDB),
- `TaskCompletionSource.TrySetResult` returned normally,
- and the awaiting save never resumed: `lastSaveAttempted` stayed `false` and no error was raised.

The same build showed why. A probe component (since deleted) reported:

| probe | result |
| --- | --- |
| `await task` where the TCS is completed on the main thread | resumed, inline on the completing call |
| `await task.ConfigureAwait(false)` | **never resumed** |
| `await Task.Delay(500)` | **never resumed** |

A WebGL player is single-threaded and has no thread pool: anything the runtime schedules on the
default scheduler is queued and never run. `ConfigureAwait(false)` is exactly that queue.
`ICheckpointStore.FlushAsync` returns `Task` by design — the Application layer is engine-free and
cannot adopt `UniTask` — so the port stays `Task`; only the awaiting side changes.

## Decision

**Application-layer awaits capture the caller's context. Infrastructure that completes them does
so from the player loop.**

1. `CheckpointService` and `SettingsService` await with the default `ConfigureAwait` — no
   `.ConfigureAwait(false)` anywhere in `lib/Runtime/Application`. A host with no context (the xUnit
   tests) resumes inline, so nothing about the desktop path changes.
2. The WebGL store never completes a task from a browser callback. `ProjectSurvive_FlushBegin`
   records the `FS.syncfs` outcome in the page, `WebGlFlushReceiver.Update` polls it once per frame,
   and the task is completed there — inside a Unity frame, on the same context the save started on.
   The poll costs one boolean read per frame while no write is in flight, and a flush that never
   answers fails after 10 s instead of hanging the camp's departure.
3. The jslib entry point that used `SendMessage` to call back into `OnFlushed` is gone; the store's
   only remaining page dependency is the storage-availability check.

The alternative considered — a custom `TaskScheduler`/`SynchronizationContext` in Infrastructure that
the game pumps every frame — was rejected: it would make every Application `Task` depend on a Unity
pump to make progress, which is a larger contract than one polled flush and would have to be
re-taught to every future async port. Capturing the context costs nothing on hosts that have one and
keeps the port engine-free.

## Consequences

- Any future Application port that can yield must be completed on the player loop and awaited
  without `ConfigureAwait(false)`. This is written down here so the "library best practice" does not
  get re-introduced by a future stage; the guard is a review rule, not a compiler one.
- `Task.Delay`/`Task.Run`/`Thread` are unusable in this target (AGENTS rule 9 already forbids the
  last two). Timing waits belong to `UniTask` in Presentation, where the player loop drives them.
- The checkpoint save now reports its real outcome: a reload at the camp resumes the shop, the bag,
  HP and bia; a reload during a floor resumes the pre-floor checkpoint; storage that is switched off
  warns and reports `save.storage_unavailable` rather than a success.
