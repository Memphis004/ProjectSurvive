# ADR 0034 — Settings carry a schema version that the codec migrates, and the audio has two buses

- **Status:** Accepted (Stage 13c-2a)
- **Date:** 2026-10-22
- **Related:** [ADR 0004](0004-dotnet-layer-enforcement.md) (layers), [ADR 0033](0033-application-awaits-capture-the-caller-context.md)
  (settings saves), the audio note [`llmwiki/systems/audio.md`](../llmwiki/systems/audio.md)

## Context

Stage 12 shipped settings as one JSON-free MessagePack document with four keys: schema version,
master volume, language, starting speed; Stage 13b appended a fifth (the tutorial flag) by relying
on MessagePack leaving an **absent** key at the property's default.

Stage 13c-2a adds two more: the music bus and the effects bus. Both are volumes in `0..1`, and 0 is
a value a player can legitimately choose (silence). A settings file written before the keys existed
therefore decodes with `0` for both — not "absent", but indistinguishable from "muted" in the
decoded object.

Two ways to tell "not written" from "written as zero":

1. **Sentinel.** Give the DTO a value outside the legal range (e.g. `-1`) and treat that as absent.
2. **Version.** Read the schema version the file already carries and fill the new keys when it is
   older than the build.

## Decision

**The version decides.** `SettingsService.SchemaVersion` is now **2**, and
`OldestReadableSchemaVersion = 1` is the oldest a build still reads. The codec, not the DTO, fills
the two buses from `GameSettings.DefaultMusicVolume` / `DefaultSfxVolume` when the payload is schema
1; a schema 2 payload is validated like every other field (range, NaN) and rejected as a whole if it
is out of range. Anything else — a newer version, a corrupt envelope — keeps the existing behaviour:
`SettingsService` falls back to the defaults rather than failing the boot.

The engine listener's volume stays the **master** (`AudioListener.volume`, written by
`SettingsPresenter` and `GameSave`), and the two buses are levels the audio device multiplies its
sources by. `AudioDirector` no longer serialises its own music/effects levels: a scene that carried
them would be a second source of truth for the mix.

## Why not the sentinel

A sentinel works, and it is one line — but it makes `-1` a legal member of a `0..1` contract that
every other consumer has to remember to exclude, and it fails silently in the other direction: a
hand-built or future writer that forgets the sentinel writes 0 and mutes the player, which is exactly
the failure being avoided. The version already existed for this purpose (the checkpoint codec refuses
an unknown one), and a migration that is *declared* is testable: one xUnit case builds a real schema
1 payload and asserts the levels come out at the defaults.

## Consequences

- **A settings write is a migration point from here on.** Adding a key means: bump
  `SchemaVersion`, add the read path for the older version, and add a case that decodes a payload
  written by the previous build. `Settings_written_before_the_buses_existed_keep_the_default_mix`
  is the pattern.
- **The DTO stays a dumb carrier.** Its property defaults are 0 and its documentation says so; the
  only place that knows "0 here means absent" is the codec, next to the version check.
- **The mix is three numbers, not one.** A player can quieten the ambience without losing the hit
  sounds, and both levels persist per profile. `AudioDirector`'s published document keeps reporting
  the two levels it is actually using, which is how a browser test proves the press reached the
  device.
- **The default mix does not change for an existing player**: `DefaultMusicVolume = 0.55` and
  `DefaultSfxVolume = 1` are the values Stage 13c-1 shipped as the director's own levels.
