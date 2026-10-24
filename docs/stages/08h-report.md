# Stage 09o — survey of ศาลาพัก before the yard (recorded as 08h)

Written before any scene change, from the shipped `Camp.unity` and `CampWorldView`. The prompt
names this file `08h-report.md`; the numbers are what Stage 09o was planned against.

## 1. The camera (read from the scene, not assumed)

| what | value | where |
| --- | --- | --- |
| projection | orthographic | `CampWorldView.ApplyCamera` |
| size | 6.6 (vertical half-height, world units) | `_cameraSize` |
| rotation | Euler (32, 0, 0) — fixed, never changes | `_cameraEuler` |
| follow | `camera.xz = player.xz + (0, 7.2, −10.5)` every `LateUpdate` | `_cameraOffset`, `ApplyCamera` |
| follow clamp | camera x ∈ [−4, 4], camera z ∈ [−13, −8] | `_cameraXLimit`, `_cameraZLimit` |
| aspect | 16:9 in both captured sizes | 1920×1080, 1280×720 |

Because the camera is clamped, it does not track the player one-to-one: at the spawn (z = −2.2) the
unclamped camera z would be −12.7, inside the box; standing at the shelf row (z ≤ 2.6) the
unclamped value is −7.9 and the camera stays at −8. The camera moves 4.7 units over the whole of
the walk area's 6.2.

## 2. What one frame covers on the ground

Derived from the values above (orthographic, pitch 32°, camera 7.2 units up): the centre ray meets
the ground 11.52 units in front of the camera, the near edge of the frame lands 0.93 units *behind*
the camera, the far edge 23.98 units in front of it — a visible ground depth of 24.9 units, and a
half-width of 6.6 × 16/9 = 11.73 units.

Both captured sizes are 16:9, so the world −2.6 … +13.5 around the player in Z and ±11.7 in X is the
same at 1920×1080 and 1280×720; what differs is density, 81.8 px per world unit at 1080p and 54.5 at
720p (a 1.5× difference, which is why the 12 px price ink at 1080p measures 8 px at 720p).

Consequences the yard was built around:

- anything at z < −4 stands **between the camera and the player** and fills the lower frame;
- anything at z > 9 is never seen at all;
- the sides are only reachable as frame at |x| ≥ 9.5, outside the walk area's 8.4.

## 3. The walk area and the usage points (unchanged by this stage)

Walk area (`_walkXLimit` × `_walkZLimit`): x ∈ [−8.4, 8.4], z ∈ [−3.6, 2.6].

| usage point | world (x, z) | source |
| --- | --- | --- |
| shelf 0 | (−4.5, 3.4) | `_shelfZ` 3.4, `_shelfSpacing` 3 |
| shelf 1 | (−1.5, 3.4) | " |
| shelf 2 | (1.5, 3.4) | " |
| shelf 3 | (4.5, 3.4) | " |
| เซียมซี cylinder | (−7.2, 0.4) | `_resetPosition` |
| incense pot (กระถางถวายคืน) | (7.6, 2.2) | `AltarPot` |
| stall / grandmother | (−7.4, 4.6) / (−8.6, 4.1) | `Stall`, `Grandmother` |
| player spawn | (0, −2.2) | `_spawn` |

The whole furniture row stands *behind* the walk area's far edge (z = 2.6), which is why the player
never collides with it and why the yard may not put anything standing in z < 2.6.

## 4. The "before" frames

`artifacts/ui/camp-env/before-before-{centre,shelf0..3,bag}-{1920x1080,1280x720}.png`, with
`stage09o-before-metrics.txt` (32–39 batches, 2.0–2.5 ms/frame) and
`stage09o-before-contrast.txt` (the baseline the after frames are compared with — the price board
measured 1.95–2.16 before the yard).

## 5. What the yard therefore has to be

Cards and decals only inside the walk area (decals are flat, grass is under
`ArenaTheme.CenterMaxHeight`); the standing frame only in the band |x| ≥ 9.5 or z ≥ 5.4, never
nearer than z = −4; the camera offset, rotation, size and clamps untouched; and the four stations,
the drum, the pot and the stall left exactly where they are, with light pools added under them
rather than around them.
