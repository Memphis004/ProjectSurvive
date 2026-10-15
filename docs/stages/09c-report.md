# Stage 09c — ศาลาพัก that can be walked, with the shop on shelves

The prompt's shape is a change of *place*: the camp stops being a menu with a sala painted behind it
and becomes a room the player walks in, with the shop standing in it. This report covers 09c's six
items; the stage's own summary and the acceptance tables for 09, 09b and 09c are in
[`09-report.md`](09-report.md) §7.

## 1. Item by item

**1. เดินด้วย WASD ในฉาก 2.5D กล้องเฉียงตามผู้เล่น.** `CampPlayerView` moves the figure on the ground
plane at a speed from `data/` (through the presenter — the view adds nothing up), clamped to the walk
area the report publishes as `walk: { xMin: -8.4, xMax: 8.4, zMin: -3.6, zMax: 2.6 }`, and
`CampWorldView` carries an oblique follow camera. `CampWorldPlayModeTests.The_player_walks_with_wasd_
and_the_camera_follows` drives `W`/`A`/`S`/`D` and asserts both the movement and the camera;
`The_player_cannot_walk_out_of_the_sala` pins the clamp, and the browser's own test measures it end to
end (`camp: W walked the figure 1.25 units in 0.4 s`).

**2. ชั้นวางสินค้า 4 จุด แสดงไอเทมและป้ายราคา (world-space); ไม่มีรายการร้านใน UI ซ้ำอีกชุด.** One
shelf per shop slot — four stations at `worldX -4.5 / -1.5 / 1.5 / 4.5`, all at `worldZ 3.4`, which is
*behind* the walk area's far edge (`zMax 2.6`), so the figure stands in front of them and "walk up to
a shelf" is a real gesture with a reachable end. Each station draws its own piece, price, and its
rented/empty state, and its own lock pin; the Stage 09 panel-era list of shop cards is gone, so there
is exactly one affordance per slot. The layout report publishes `shelfN` (the station's on-screen
rectangle) and `shelfLockN` (its sign's pin) at every resolution, plus `rentN`/`pinN` as the click
targets — browser test 1 checks all of them at 1280×720 and 1920×1080 as well as the native size.

**3. เดินเข้าใกล้ชั้นแล้วกด E หรือคลิกที่ชั้นเพื่อเช่า (use case เดียวกับ Stage 06); ของเข้าหีบ;
หีบเต็มหรือเงินไม่พอให้แสดงเหตุผลและไม่หักเงิน; ชั้นที่เช่าแล้วว่าง.** `E` acts on `focusedShelf` —
the station the figure is standing at, which the HUD shows as a detail sign while it is near — and the
command behind it is the same `RunSession` purchase the panel-era button calls, through the same
presenter method. The refusals are the run's own (the chest-full and insufficient-bia paths are
`CampWorldPlayModeTests.Renting_without_enough_bia_is_refused_and_charges_nothing`, which asserts the
money did not move), and a rented slot is published as `empty: true` — the run empties it
([ADR 0016](../adr/0016-run-settlement-and-shop-commit-boundaries.md)) rather than flagging it.
Browser evidence, verbatim: `lastWorldAction: "rent:3"`, `worldRents: 1`, `bia: 15 → 7`,
`chestUsed: 0 → 1`, and `shop[3].empty: true`.

**4. หีบรีเซ็ตสินค้า (เสี่ยงเซียมซี) เป็นวัตถุในฉาก มีป้ายราคา เข้าใกล้แล้วกด E; ล็อกสินค้าก่อนรีเซ็ต
ทำได้จากป้ายของชั้น.** The drum is a world object with its own price and its own focus flag
(`resetFocused`), reached by walking up and pressing `E`, or by clicking it; the pin on a station's
sign locks that slot against the reset and is a world click of its own. Covered by
`Casting_the_sticks_at_the_drum_rerolls_the_stock` and
`Clicking_a_shelfs_lock_pins_it_against_the_reset`.

**5. ปุ่ม จัดย่าม กลางล่างของจอ เปิดหน้าจาก 09b; ระหว่างหน้าจัดย่ามเปิดอยู่ ตัวละครหยุดเดิน.** The camp
now **starts with the panel closed** and `openBag` is the one control that lives on the canvas rather
than in the panel (and is hidden while the panel is open, so it cannot be drawn over it). While the
panel is open the walker does not move (`The_character_stops_walking_while_the_bag_panel_is_open`),
which is also what keeps a drag from moving the figure underneath it.

**6. คลิกชั้นได้โดยไม่ต้องเดิน (สำหรับมือถือและเทสต์).** `CampWorldView`'s own click resolution rents
the station under the pointer without any walking — the path a phone has, and the path the browser
tests and `Clicking_a_shelf_rents_it_without_walking` use. Pressing `E` and clicking the station reach
the *same* presenter method, so the two paths cannot disagree about a rule.

## 2. The one place this stage touched the rules

Nothing in `lib/` changed for 09c: the world shop is a second *affordance*, not a second rule. What
did change is the read model the views ask — `PlayerX`/`PlayerZ`/`Moving`/`FocusedShelf`/`ResetFocused`
/`LastWorldAction`/`WorldRents`/`WorldLocks`/`WorldResets` — which the camp publishes so a browser can
see the walk at all, plus the geometry refresh rule: the layout document is republished when the
camera has moved (a shelf's on-screen position must stay true while the figure walks), which is why
`shelfN`/`rentN`/`pinN` are correct after a walk and not just after a resize.

## 3. Evidence

| Gate | Result | Artifact |
| --- | --- | --- |
| Walk + camera, the walk-area clamp, `E` at a shelf, click-without-walking, the drum, the pin, the refused rent, the panel stopping the walk | **8 / 8 passed** (`CampWorldPlayModeTests`) | MCP result payload |
| Browser: WASD walking, `E` renting, goods into the chest, price paid = price shown | **passed** | `artifacts/stage09/playwright-web.log`, `tests/web/camp.spec.ts` *walks the figure with WASD and rents a shelf by pressing E* |
| Browser: the sala's controls (four stations, their pins, the drum, the figure) at three canvas sizes | **passed** | same log; `artifacts/stage09/screenshots/web-camp-sala.png`, `web-camp-rented-walking.png` |
| Screenshots with nothing overlapping (canvas-level UI) | **passed** at 960×600, 1280×720 and 1920×1080 | `artifacts/stage09/screenshots/web-camp-*.png` |

## 4. What is deliberately *not* claimed

- World silhouettes overlap on screen by design: a tilted 2.5D sala draws four stations and a drum
  whose *pictures* share pixels. The disjointness rule the browser asserts is about **UI** elements,
  and the panel's regions are checked in PlayMode; the station rectangles are checked for area and for
  being on the canvas instead (`web-camp-1280x720.png` and the two other sizes show why that is the
  honest test).
- The camp has no occlusion culling, no pathfinding and no moving obstacles: it is a walk area on the
  ground plane with a clamp. `E`'s reach is a distance test at the station, not a navmesh.
