# System — audio (clips, cues, the one device)

Stage 13c-1. The game had been silent until here; this is the whole path from a rule
outcome to a `Play()` call, and the rules a new sound has to obey to join it.

- Clips: `UnityProject/Assets/Main/Audio/*.wav`, all written by `tools/gen-audio.py`
  (project-owned, CC0 — see [`ATTRIBUTION.md`](../../ATTRIBUTION.md)).
- Names and order: `Assets/Main/Scripts/Presentation/Audio/AudioCue.cs`
  (`AudioCue` ← index → `AudioCueTable.Names`).
- The one device: `Assets/Main/Scripts/Presentation/Audio/AudioDirector.cs`, on the **App**
  scope root (created by Boot, persisted across scene loads).
- Stage report: [`docs/stages/13c-1-report.md`](../../docs/stages/13c-1-report.md).

## 1. One audio device, eight voices, no instantiation

`AudioDirector` preallocates eight `AudioSource` voices plus one looping music source in
`Awake` and reuses them round-robin. A cue is an `int++` and a `Play()`: no
`PlayClipAtPoint`, no `AudioSource` created per hit, no `Instantiate` (AGENTS rule 8 — a
floor-1 wave lands dozens of hits a second). It also adds the game's only `AudioListener`
if the scene has none; a listener per scene is how a project ends up with Unity's two-listener
warning on every load.

Two counters make the mix observable instead of guessable, and both are published:
`stolen` (every voice was busy, so a playing one was taken) and `throttled` (the same cue
within `_minRetriggerSeconds`, default 35 ms, which stops a burst phasing into a buzz).

It lives on the App scope on purpose: the music then survives a scene change instead of
restarting at every crossing.

## 2. Cues come from state, never from a view

A presenter does not touch `AudioDirector`. It publishes on an R3 `Subject<AudioCue>`:
`UiState.Cues` (App scope) and `CampUiState.Cues` (camp scene). The director subscribes to
the App channel; `CampSceneDirector` subscribes to its own screen's channel and forwards.
That keeps AGENTS rule 10 intact — the presenter names an event, the scene owns the object —
and a screen with no listener costs a sound and no state.

The convention that matters: **only a command that succeeded asks for a sound.** In
`CampPresenter`, `Rent`, `Place`, `Rotate` and `Merge` fire after the run returned a typed
success; `Refuse` fires only when the command came back with an error. A refused merge is
therefore audible as a refusal, not as a merge.

Battle sound goes through `BattleAudioListener`, registered on
`BattleEventChannel.Audio` — the channel the pump reserved for exactly this in Stage 08. It
reads only `BattleEvent.Type` (damage, player damage, kill, pickup, telegraph, boss phase),
so a burst the VFX channel is still drawing cannot cost the sound its events, and nothing
here can change an outcome.

## 3. What the browser can see

`__projectSurviveAudio` (`AudioDirector.Publish`, every 250 ms while dirty):

`voices`, `totalPlays`, `stolen`, `throttled`, `missingClips`, `musicPlaying`,
`musicCue`, `sfxVolume`, `musicVolume`, and `cueCounts` keyed by clip name
(`sfx-hit`, `music-camp`, …).

A WebGL player cannot run Unity's test framework, so this document is what
`tests/web/audio.spec.ts` asserts: the arena plays `MusicBattle` and lands `sfx-hit`
during a floor; the camp plays `MusicCamp`. What it cannot prove is that a human hears
anything — the source started and the clip was found, not that the speakers are on.

## 4. Two traps this stage paid for

**(a) A browser will not start an `AudioContext` before a gesture.** Nothing plays during
Boot; the first track starts on the scene the player walks into after pressing a control.
The browser test clicks the canvas before it reads the report, and Boot deliberately keeps
the music stopped (`AudioDirector.OnSceneLoaded` has no `Boot` case). Any future "play a
jingle on the title" idea has to survive the same rule.

**(b) A `CompressedInMemory` clip that is played again after a scene change leaks a decoder
buffer.** The camp → arena → title memory test (20 visits) caught the managed heap growing
by ~3.9 MB with the music on. Both music and effects are imported `DecompressOnLoad` now;
a six-second loop decoded once is 264 KB and is a cheaper thing to keep than a buffer per
`Play`. `ProjectSetup.ConfigureAudio()` owns those settings, and
`AudioAssetTests.TheImportSettingsAreTheOnesTheWebBuildNeeds` pins them.

Import settings otherwise: mono (a cue is a UI sound, `forceToMono` and
`spatialBlend = 0`), `PreserveSampleRate` so the generator's 22 050 Hz survives, Vorbis
0.5 for music and PCM for effects, and no per-platform override — Unity transcodes the
Web build to AAC itself (Unity 6.3 manual, "Audio in Web").

## 5. Adding a sound

1. Add a function to `tools/gen-audio.py`'s `CLIPS` dict; run it. Deterministic by
   construction (seeded LCG, never `random`), so a diff in the `.wav` means somebody
   changed the sound.
2. Append the cue name to `AudioCueTable.Names` in the **same index** as the new `AudioCue`
   value — that pairing is the one thing that silently breaks if reordered.
3. Run `ProjectSetup.ConfigureAudio()` and rebuild the Boot scene (`LoadAudioClips` throws
   when a clip is missing, rather than shipping a silent game).
4. Ask for it from a presenter (state channel) or the battle listener; never from a view.

`AudioAssetTests` checks the artefact rather than the script — every cue has its file, the
file is the generated rate and format, effects stay under 1.2 s, music is at least 3 s, and
the samples are not silent (RMS, so an all-zero generator bug fails).
