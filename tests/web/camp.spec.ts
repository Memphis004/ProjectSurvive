import { expect, test, type Page } from "@playwright/test";

/**
 * Stage 09 acceptance in the browser: "ซื้อ → วาง → หมุน → รวม → ถวายคืน → คืนสภาพ ผ่าน UI แล้ว
 * state ตรงกับ Core", "invalid drop ไม่ทำของหาย" and "Playwright ทดสอบเมาส์และ keyboard focus
 * ใน Web จริง"; and Stage 09c's "เดิน → เช่า → ของเข้าหีบ → เปิดหน้าจัดย่าม → ลากลง Board ผ่านจริง"
 * with "Playwright กด WASD และ E ในหน้า Web จริง".
 *
 * The camp scene publishes two documents on `window` (see `CampReport`), the same way the Stage 03
 * self-check and the Stage 08 battle do, because a WebGL player cannot run Unity's test framework:
 * what a browser test can observe is what the player published about itself.
 *
 *   __projectSurviveCamp          - the run and the screen: flow, floor, bia, chest, the shop's
 *                                   four slots, every piece the run holds (kind, tier, zone,
 *                                   cell, chest slot, rotation), the command counters, where the
 *                                   figure is walking (`playerX`/`playerZ`/`moving`/`focusedShelf`),
 *                                   and the gate (`canDepart` / `departError` / `statusCode`)
 *   __projectSurviveCampGeometry  - where every board cell, chest slot, the altar pot, each shelf
 *                                   station and the player's figure are on screen, in Unity screen
 *                                   pixels with the origin at the bottom-left, plus the walk area
 *                                   and each shelf's own world position
 *
 * Both are read through the canvas element's own box, so the conversion survives any resolution
 * and either pixel ratio. A click is aimed at a control the *report* named, never at a pixel this
 * test remembered — and since Stage 09c the shop is in the world, so "the rent button" is a shelf
 * standing in the sala.
 *
 * Timing: the first load of a Unity WebGL player downloads and compiles the WASM, hence minutes in
 * the timeouts; a shorter one would report a slow machine as a broken game. The camp republishes
 * its state at most four times a second (only when the run or the walk changed and no gesture is
 * live), so every assertion here is a poll.
 */

const CAMP = "__projectSurviveCamp";
const GEOMETRY = "__projectSurviveCampGeometry";
const SCENE = "__projectSurviveScene";

/** Boot: download + compile the player. */
const PLAYER_BOOT_TIMEOUT_MS = 180_000;

/** How long a single command may take to show up in the published report. */
const COMMAND_TIMEOUT_MS = 15_000;

interface CampCounters {
  rented: number;
  refreshes: number;
  slotLocks: number;
  placed: number;
  stored: number;
  merged: number;
  recycled: number;
  rotated: number;
  resets: number;
  undos: number;
  lockToggles: number;
  dragPreviews: number;
  invalidPreviews: number;
  invalidDrops: number;
  dragMoves: number;
  dragRotations: number;
  dragCancels: number;
  departures: number;
}

interface CampPiece {
  kind: "Bag" | "Item";
  instanceId: number;
  defId: string;
  tier: number;
  zone: "Board" | "Chest";
  x: number;
  y: number;
  rotation: number;
  chestSlot: number;
  cells: number;
}

interface CampShopSlot {
  index: number;
  pieceId: string;
  kind: string;
  price: number;
  empty: boolean;
  purchased: boolean;
  locked: boolean;
  affordable: boolean;
}

interface CampState {
  scene: string;
  publishCount: number;
  startError: string;
  /** True while the จัดย่าม panel is on screen. Since 09c the camp starts with it closed. */
  bagVisible: boolean;
  moving: boolean;
  playerX: number;
  playerZ: number;
  focusedShelf: number;
  resetFocused: boolean;
  lastWorldAction: string;
  worldRents: number;
  worldLocks: number;
  worldResets: number;
  shelfCount: number;
  flow: string;
  floor: number;
  totalFloors: number;
  hp: number;
  maxHp: number;
  bia: number;
  weight: number;
  weightPenaltyPercent: number;
  focusPerSecond: number;
  chestUsed: number;
  chestSlots: number;
  bagLocked: boolean;
  canUndoEdit: boolean;
  backpackRevision: number;
  boardWidth: number;
  boardHeight: number;
  selectedPiece: string;
  statusCode: string;
  dragging: boolean;
  departed: boolean;
  departFloor: number;
  departSeed: string;
  canDepart: boolean;
  departError: string;
  counters: CampCounters;
  shop: CampShopSlot[];
  pieces: CampPiece[];
}

interface Point {
  x: number;
  y: number;
}

interface ShelfGeometry {
  index: number;
  worldX: number;
  worldZ: number;
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  lockX?: number;
  lockY?: number;
  lockWidth?: number;
  lockHeight?: number;
}

interface WalkArea {
  xMin: number;
  xMax: number;
  zMin: number;
  zMax: number;
}

interface RectEntry {
  name: string;
  layer: "ui" | "world";
  x: number;
  y: number;
  width: number;
  height: number;
}

interface CampGeometry {
  canvasWidth: number;
  canvasHeight: number;
  bagVisible: boolean;
  rects: RectEntry[];
  shelves: ShelfGeometry[];
  walk: WalkArea;
  player?: { x: number; y: number; width: number; height: number; worldX: number; worldZ: number };
  resetDrum?: { x: number; y: number; width: number; height: number };
  cellPitch: number;
  boardWidth: number;
  boardHeight: number;
  chestColumns: number;
  chestSlots: number;
  pieceViews: number;
  cells: Array<{ column: number; row: number; x: number; y: number }>;
  chest: Array<{ slot: number; x: number; y: number }>;
  altar?: Point;
  buttons: Array<{ name: string; x: number; y: number }>;
}

interface CanvasBox {
  x: number;
  y: number;
  width: number;
  height: number;
}

function collectPageErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on("console", (message) => {
    if (message.type() === "error") {
      errors.push(`console.error: ${message.text()}`);
    }
  });
  page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
  return errors;
}

async function waitForGlobal(page: Page, name: string): Promise<void> {
  await page.waitForFunction(
    (globalName) => Boolean((window as unknown as Record<string, unknown>)[globalName]),
    name,
    { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 },
  );
}

async function readGlobal<T>(page: Page, name: string): Promise<T> {
  return page.evaluate((globalName) => (window as unknown as Record<string, unknown>)[globalName] as T, name);
}

async function state(page: Page): Promise<CampState> {
  return readGlobal<CampState>(page, CAMP);
}

async function geometry(page: Page): Promise<CampGeometry> {
  return readGlobal<CampGeometry>(page, GEOMETRY);
}

async function counters(page: Page): Promise<CampCounters> {
  return (await state(page)).counters;
}

async function canvasBox(page: Page): Promise<CanvasBox> {
  const box = await page.locator("canvas").boundingBox();
  expect(box, "the player has no canvas element").not.toBeNull();
  return box!;
}

/** Unity screen pixels (origin bottom-left) → page coordinates inside the canvas element. */
function toPage(box: CanvasBox, layout: CampGeometry, unity: Point): Point {
  return {
    x: box.x + (unity.x / layout.canvasWidth) * box.width,
    y: box.y + ((layout.canvasHeight - unity.y) / layout.canvasHeight) * box.height,
  };
}

function boardCell(layout: CampGeometry, column: number, row: number): Point {
  const cell = layout.cells.find((candidate) => candidate.column === column && candidate.row === row);
  expect(cell, `the layout report has no board cell (${column}, ${row})`).toBeDefined();
  return { x: cell!.x, y: cell!.y };
}

function chestSlot(layout: CampGeometry, slot: number): Point {
  const cell = layout.chest.find((candidate) => candidate.slot === slot);
  expect(cell, `the layout report has no chest slot ${slot}`).toBeDefined();
  return { x: cell!.x, y: cell!.y };
}

/** The position of a control the scene drew, by the name the layout report gave it. */
function control(layout: CampGeometry, name: string): Point {
  const button = layout.buttons.find((candidate) => candidate.name === name);
  expect(button, `the layout report has no '${name}' control`).toBeDefined();
  return { x: button!.x, y: button!.y };
}

/**
 * True when two published rectangles share screen area.
 *
 * `xMin < xMax` on both axes, so two controls that merely touch along an edge — which is how a
 * row of buttons is laid out — are not "overlapping".
 */
function overlaps(a: RectEntry, b: RectEntry): boolean {
  return a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height;
}

function withLayer(layout: CampGeometry, layer: "ui" | "world"): RectEntry[] {
  return layout.rects.filter((rect) => rect.layer === layer);
}

/** The published rectangle of a named region, which the report has to have drawn. */
function rect(layout: CampGeometry, name: string): RectEntry {
  const entry = layout.rects.find((candidate) => candidate.name === name);
  expect(entry, `the layout report has no '${name}' region`).toBeDefined();
  return entry!;
}

/** One shelf station's rectangle, which is its click target in the sala. */
function shelf(layout: CampGeometry, index: number): ShelfGeometry {
  const station = layout.shelves.find((candidate) => candidate.index === index);
  expect(station, `the layout report has no shelf ${index}`).toBeDefined();
  expect(
    station!.x,
    `shelf ${index} has a world position but no on-screen rectangle, so it cannot be clicked`,
  ).toBeDefined();
  return station!;
}

/**
 * A frame's worth of time between pointer events.
 *
 * A WebGL player reads its input once per rendered frame, so a press and a release inside the same
 * frame are one event as far as the EventSystem is concerned — a synthesised click with no pause
 * between down and up can be swallowed whole. Every gesture below therefore paces itself.
 */
const FRAME_PAUSE_MS = 120;

async function dragOnCanvas(page: Page, from: Point, to: Point): Promise<void> {
  await page.mouse.move(from.x, from.y);
  await page.waitForTimeout(FRAME_PAUSE_MS);
  await page.mouse.down();
  await page.waitForTimeout(FRAME_PAUSE_MS);

  // Several steps: uGUI only starts a drag after its own threshold, and a single jump would
  // be one pointer event that never crosses it.
  await page.mouse.move(to.x, to.y, { steps: 12 });
  await page.waitForTimeout(FRAME_PAUSE_MS);
  await page.mouse.up();
  await page.waitForTimeout(FRAME_PAUSE_MS);
}

async function clickOnCanvas(page: Page, point: Point): Promise<void> {
  await page.mouse.move(point.x, point.y);
  await page.waitForTimeout(FRAME_PAUSE_MS);
  await page.mouse.down();
  await page.waitForTimeout(FRAME_PAUSE_MS);
  await page.mouse.up();
  await page.waitForTimeout(FRAME_PAUSE_MS);
}

/**
 * Clicks a published control until the run shows the command it stands for.
 *
 * A synthetic click into the WebGL player is occasionally lost — measured on this test: with a
 * single click, the panel's close control was lost in 3 of 4 runs and its open control in 1 of 5,
 * each run then waiting the full 15 s for a command that had never arrived. The loss is not the
 * canvas box moving (it reads back identical to the load-time box), and not a settle time: a
 * deliberate pointer move plus a two-second pause before the click did not prevent it (open lost 1
 * of 4, close 1 of 4 with that pause in place). Clicking a second time is what a player does, it
 * registered on every retry (0 failures in 12 runs of this test), and the assertion stays where it
 * was: the command has to appear in the published document, which the caller still asserts.
 */
async function clickUntilPublished(
  page: Page,
  target: Point,
  observed: () => Promise<boolean>,
  attempts = 3,
): Promise<void> {
  for (let attempt = 1; attempt <= attempts; attempt += 1) {
    await clickOnCanvas(page, target);
    if (await observed()) {
      return;
    }

    console.log(
      `the click at ${JSON.stringify(target)} was not registered (attempt ${attempt} of ${attempts})`,
    );

    // A pointer move and a pause before the next try: the player has to be off the control again
    // for the press that follows to be its own press.
    await page.mouse.move(target.x, target.y - 40);
    await page.waitForTimeout(500);
  }
}

/**
 * Resizes the player's **canvas element** — the size path a player has, because the template's own
 * fullscreen button and a HiDPI backing store both change the element rather than the window.
 *
 * The browser *window* is deliberately not used: Unity's default desktop template pins the canvas'
 * CSS size at 960x600, so a 1920x1080 viewport left the report at 960x600 and republished nothing
 * (measured with a throwaway probe before this test was written).
 */
async function setCanvasSize(page: Page, width: number, height: number): Promise<void> {
  await page.evaluate(
    (size) => {
      const canvas = document.querySelector("canvas") as HTMLCanvasElement;
      canvas.style.width = `${size.width}px`;
      canvas.style.height = `${size.height}px`;
      window.dispatchEvent(new Event("resize"));
    },
    { width, height },
  );

  // The player adopts the new size — and re-maps pointer coordinates to it — a moment after the
  // element changed, and *that* is what a click needs, not just the layout: measured while this
  // test was being written, a click aimed at a small panel control ~300 ms after a resize was
  // swallowed in two of four runs and landed in the two runs that waited. One second is well
  // inside the 15 s a command is allowed to take, and it keeps the assertion honest: what is
  // asserted below is the layout at each size, which is read from the document rather than clicked
  // at, plus one panel open and one panel close at the player's own size.
  await page.waitForTimeout(1000);
}

/** Loads the camp and hands back everything a test needs to drive it. */
async function enterCamp(page: Page): Promise<{ box: CanvasBox; layout: CampGeometry; first: CampState }> {
  await page.goto("/?scene=Camp", { waitUntil: "domcontentloaded" });

  await waitForGlobal(page, SCENE);
  await expect
    .poll(() => readGlobal<string>(page, SCENE), { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 })
    .toBe("Camp");

  await waitForGlobal(page, CAMP);
  await waitForGlobal(page, GEOMETRY);

  const first = await state(page);
  const layout = await geometry(page);

  console.log("camp report (start):", JSON.stringify(first));
  console.log("camp layout:", JSON.stringify(layout));

  expect(first.scene).toBe("Camp");
  expect(first.startError).toBe("");
  expect(first.flow).toBe("Camp");
  expect(first.shelfCount, "the sala must stand one shelf per shop slot").toBe(first.shop.length);

  return { box: await canvasBox(page), layout, first };
}

/** Opens the จัดย่าม panel by clicking its own button, the way a player does. */
async function openBagPanel(page: Page, box: CanvasBox, layout: CampGeometry): Promise<void> {
  expect((await state(page)).bagVisible, "the panel should start closed: the camp is a place you walk")
    .toBe(false);

  await clickOnCanvas(page, toPage(box, layout, control(layout, "openBag")));

  await expect
    .poll(async () => (await state(page)).bagVisible, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
    .toBe(true);
}

/**
 * Holds walking keys until the figure stands at a world XZ, then lets them go.
 *
 * The keys are chosen from the figure's *published* position each iteration, so this depends on
 * neither a frame rate nor a walking speed — only on WASD actually moving the figure, which is the
 * thing under test. The target is the shelf's own column clamped into the walk area: the furniture
 * is not walkable, so the shelf itself can never be stood on.
 */
async function walkTo(page: Page, targetX: number, targetZ: number): Promise<void> {
  const tolerance = 0.35;
  const deadline = Date.now() + 25_000;
  let held = new Set<string>();

  try {
    for (;;) {
      const at = await state(page);
      const dx = targetX - at.playerX;
      const dz = targetZ - at.playerZ;

      if (Math.abs(dx) <= tolerance && Math.abs(dz) <= tolerance) {
        return;
      }

      if (Date.now() > deadline) {
        throw new Error(
          `the figure never reached (${targetX}, ${targetZ}); it is at (${at.playerX}, ${at.playerZ})`,
        );
      }

      const want = new Set<string>();
      if (dz > tolerance) want.add("w");
      if (dz < -tolerance) want.add("s");
      if (dx > tolerance) want.add("d");
      if (dx < -tolerance) want.add("a");

      for (const key of held) {
        if (!want.has(key)) {
          await page.keyboard.up(key);
        }
      }
      for (const key of want) {
        if (!held.has(key)) {
          await page.keyboard.down(key);
        }
      }

      held = want;

      if (held.size === 0) {
        // Inside the tolerance on one axis already; give the report a tick to catch up.
        await page.waitForTimeout(250);
        continue;
      }

      // Long enough for the report (four publishes a second) to show the walk.
      await page.waitForTimeout(300);
    }
  } finally {
    for (const key of held) {
      await page.keyboard.up(key);
    }
  }
}

test.describe("ProjectSurvive ศาลาพัก in the browser", () => {
  test("boots the sala, names every control a player can press and opens จัดย่าม", async ({ page }) => {
    test.setTimeout(240_000);
    const pageErrors = collectPageErrors(page);

    const { box, layout, first } = await enterCamp(page);

    // The run the screen is editing: the starting kit on the board, the purse, and the gate.
    expect(first.floor).toBe(1);
    expect(first.boardWidth).toBe(9);
    expect(first.boardHeight).toBe(7);
    expect(first.bia).toBeGreaterThan(0);
    expect(first.chestUsed).toBe(0);
    expect(first.chestSlots).toBeGreaterThan(0);
    expect(first.canDepart, `the camp cannot depart: ${first.departError}`).toBe(true);
    expect(first.departError).toBe("");

    const bags = first.pieces.filter((piece) => piece.kind === "Bag");
    const boardItems = first.pieces.filter((piece) => piece.kind === "Item" && piece.zone === "Board");

    expect(bags.length).toBeGreaterThan(0);
    expect(boardItems.length).toBeGreaterThan(0);
    expect(bags.every((piece) => piece.cells > 0)).toBe(true);

    // --- the sala is the shop now, and it is closed ------------------------------------------
    expect(first.bagVisible, "the จัดย่าม panel is not the camp: it starts closed").toBe(false);
    expect(first.moving).toBe(false);
    expect(first.worldRents).toBe(0);

    // One station per shop slot, each with a world position (where it stands) and an on-screen
    // rectangle (where a click lands). The name `rentN` still means "rent slot N", it is simply a
    // shelf in the sala rather than a button in a list.
    expect(layout.shelves.length).toBe(first.shop.length);
    expect(layout.walk.zMax).toBeGreaterThan(layout.walk.zMin);

    for (const slot of first.shop) {
      const station = shelf(layout, slot.index);
      // Two live controls per shelf with the panel closed — a station is rented by clicking it and
      // locked by clicking its own sign — so both have to be somewhere a pointer can reach.
      for (const name of [`rent${slot.index}`, `pin${slot.index}`]) {
        const point = control(layout, name);
        expect(point.x, `'${name}' is off the canvas horizontally`).toBeGreaterThan(0);
        expect(point.x).toBeLessThan(layout.canvasWidth);
        expect(point.y, `'${name}' is off the canvas vertically`).toBeGreaterThan(0);
        expect(point.y).toBeLessThan(layout.canvasHeight);
      }

      // The shelf row stands behind the walk area's far edge, which is what makes the clamped
      // walk-up in the third test meaningful.
      expect(station.worldZ).toBeGreaterThanOrEqual(layout.walk.zMax);
      expect(station.lockX, `shelf ${slot.index} has no lock control on screen`).toBeDefined();
    }

    // The เซียมซี cylinder and the figure are objects in the sala, and the report says where.
    expect(layout.resetDrum, "the layout report must place the เซียมซี cylinder").toBeDefined();
    expect(layout.player, "the layout report must place the figure").toBeDefined();
    control(layout, "resetDrum");
    expect(layout.player!.worldX).toBeCloseTo(first.playerX, 1);
    expect(layout.player!.worldZ).toBeCloseTo(first.playerZ, 1);

    // --- no UI element on top of another, with the panel closed --------------------------------
    // The 09b defect this guards against is a control drawn over the element it belongs beside. The
    // panel nests its own controls inside its own regions by design (that rule lives in the PlayMode
    // layout test, where it can name the pairs that are meant to nest); what has to hold in the
    // *browser* is the closed camp's own UI — the จัดย่าม button and the bottom bar, which used to
    // be drawn over one another. World geometry is not part of this: a tilted 2.5D sala has
    // stations whose silhouettes overlap on screen, and no two of them are UI.
    const closedUi = withLayer(layout, "ui");
    expect(closedUi.length, "the closed camp publishes the bottom bar and the จัดย่าม button")
      .toBeGreaterThanOrEqual(2);

    for (const rect of closedUi) {
      expect(rect.width, `'${rect.name}' has no width`).toBeGreaterThan(0);
      expect(rect.height, `'${rect.name}' has no height`).toBeGreaterThan(0);
    }

    for (let i = 0; i < closedUi.length; i += 1) {
      for (let j = i + 1; j < closedUi.length; j += 1) {
        expect(
          overlaps(closedUi[i], closedUi[j]),
          `'${closedUi[i].name}' ${JSON.stringify(closedUi[i])} overlaps ` +
            `'${closedUi[j].name}' ${JSON.stringify(closedUi[j])}`,
        ).toBe(false);
      }
    }

    // Each shelf's own click target is on screen with a real area. `shelfLockN` is drawn on the
    // station's sign by design, so it is deliberately not part of the disjointness rule above; that
    // it is a reachable control is asserted through `pinN`.
    for (const slot of first.shop) {
      const station = withLayer(layout, "world").find((rect) => rect.name === `shelf${slot.index}`);
      expect(station, `no 'shelf${slot.index}' rectangle`).toBeDefined();
      expect(station!.width, `'shelf${slot.index}' has no width`).toBeGreaterThan(0);
      expect(station!.height, `'shelf${slot.index}' has no height`).toBeGreaterThan(0);

      const lock = withLayer(layout, "world").find((rect) => rect.name === `shelfLock${slot.index}`);
      expect(lock, `no 'shelfLock${slot.index}' rectangle`).toBeDefined();
      expect(lock!.width, `'shelfLock${slot.index}' has no width`).toBeGreaterThan(0);
    }

    // --- opening the panel, and what it carries ------------------------------------------------
    await openBagPanel(page, box, layout);

    const opened = await geometry(page);
    expect(opened.bagVisible).toBe(true);
    expect(opened.boardWidth).toBe(first.boardWidth);
    expect(opened.boardHeight).toBe(first.boardHeight);
    expect(opened.cells.length).toBe(first.boardWidth * first.boardHeight);
    expect(opened.chest.length).toBe(first.chestSlots);
    expect(opened.cellPitch).toBeGreaterThan(0);
    expect(opened.altar, "the layout report must place the incense pot").toBeDefined();

    // Everything the report places has to be somewhere a pointer can reach it: a control the scene
    // drew but placed off the canvas is a control no player can press.
    for (const point of opened.buttons) {
      expect(point.x, `'${point.name}' is off the canvas horizontally (${point.x} of ${opened.canvasWidth})`)
        .toBeGreaterThan(0);
      expect(point.x).toBeLessThan(opened.canvasWidth);
      expect(point.y, `'${point.name}' is off the canvas vertically (${point.y} of ${opened.canvasHeight})`)
        .toBeGreaterThan(0);
      expect(point.y).toBeLessThan(opened.canvasHeight);
    }

    // Every control the panel's own work items name is now on screen, and so is every region the
    // panel is made of.
    for (const name of ["closeBag", "rotate", "undo", "restore", "lockBag", "depart"]) {
      control(opened, name);
    }

    for (const name of ["board", "chestPanel", "chest", "altar", "instructions", "stats", "statusBar"]) {
      const region = opened.rects.find((rect) => rect.name === name);
      expect(region, `the layout report has no '${name}' region`).toBeDefined();
      expect(region!.width, `'${name}' has no width`).toBeGreaterThan(0);
      expect(region!.height, `'${name}' has no height`).toBeGreaterThan(0);
    }

    // The จัดย่าม button is the one control that lives on the canvas rather than in the panel, and
    // it is hidden while the panel is open so it cannot be drawn over it.
    expect(opened.buttons.find((candidate) => candidate.name === "openBag")).toBeUndefined();

    console.log(
      "camp at 960x600 (panel open): board " + JSON.stringify(rect(opened, "board")) +
        " closeBag " + JSON.stringify(control(opened, "closeBag")),
    );

    await page.screenshot({ path: "test-results/web-camp-open.png", fullPage: false });

    // And closing it puts the จัดย่าม button back, with the sala behind.
    await clickOnCanvas(page, toPage(box, opened, control(opened, "closeBag")));

    await expect
      .poll(async () => (await state(page)).bagVisible, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(false);

    const closed = await geometry(page);
    control(closed, "openBag");

    await page.screenshot({ path: "test-results/web-camp-sala.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });

  test("rents a shelf, then places, rotates, merges, gives back and restores through the real UI", async ({ page }) => {
    test.setTimeout(300_000);
    const pageErrors = collectPageErrors(page);

    const { box, layout, first } = await enterCamp(page);

    // The station's *centre*, as the report publishes it under the station's own name — never the
    // rectangle's corner, which lies on the boundary the hit test excludes (the Stage 09b defect).
    const shelfPoint = (l: CampGeometry, index: number): Point => control(l, `rent${index}`);

    // --- เตรียม: the bag piece and the item the run starts with -------------------------------
    const boardItem = first.pieces.find((piece) => piece.kind === "Item" && piece.zone === "Board");
    expect(boardItem, "the starting kit must carry an item on the board").toBeDefined();
    expect(boardItem!.tier).toBe(1);
    expect(boardItem!.rotation).toBe(0);

    const itemCell = { x: boardItem!.x, y: boardItem!.y };

    // --- 1. เช่า: click a shelf in the sala, before the panel is open --------------------------
    // A merge needs two copies, so the offer has to be one of them. If the camp's seed ever stops
    // offering the starting weapon this fails here, loudly.
    const offer = first.shop.find((slot) => !slot.empty && !slot.purchased && slot.pieceId === boardItem!.defId);

    expect(
      offer,
      `the camp must offer a second ${boardItem!.defId} for a merge to be reachable; the shop rolled ` +
        first.shop.map((slot) => slot.pieceId || "(empty)").join(", "),
    ).toBeDefined();
    expect(offer!.affordable, "the purse must be able to pay for it").toBe(true);

    const biaBeforeRent = first.bia;

    await clickOnCanvas(page, toPage(box, layout, shelfPoint(layout, offer!.index)));

    await expect
      .poll(async () => (await counters(page)).rented, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(1);

    const afterRent = await state(page);
    const rented = afterRent.pieces.find((piece) => piece.zone === "Chest");

    expect(afterRent.bia, "the price paid must be the price the shelf showed").toBe(biaBeforeRent - offer!.price);
    expect(afterRent.chestUsed).toBe(1);
    expect(afterRent.worldRents, "the click went through the sala's own rent path").toBe(1);
    expect(rented, "a rented piece is carried in the chest").toBeDefined();
    expect(rented!.defId).toBe(boardItem!.defId);
    expect(rented!.tier).toBe(1);

    console.log("camp report (after rent):", JSON.stringify(afterRent));

    // --- 2. เปิดหน้าจัดย่าม ---------------------------------------------------------------------
    await openBagPanel(page, box, layout);

    const opened = await geometry(page);

    // --- 3 (acceptance: invalid drop ไม่ทำของหาย) ---------------------------------------------
    // Dropping the chest piece on a corner of the board that no bag covers is refused, and a
    // refusal must leave the run exactly as it was.
    const revisionBeforeInvalid = afterRent.backpackRevision;

    await dragOnCanvas(
      page,
      toPage(box, opened, chestSlot(opened, rented!.chestSlot)),
      toPage(box, opened, boardCell(opened, first.boardWidth - 1, first.boardHeight - 1)),
    );

    await expect
      .poll(async () => (await counters(page)).invalidDrops, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(1);

    const afterInvalid = await state(page);
    const stillInChest = afterInvalid.pieces.find((piece) => piece.instanceId === rented!.instanceId);

    expect(afterInvalid.backpackRevision, "a refused drop must not move the bag").toBe(revisionBeforeInvalid);
    expect(afterInvalid.bia).toBe(afterRent.bia);
    expect(stillInChest?.zone, "the piece stayed in the chest").toBe("Chest");
    expect(stillInChest?.chestSlot).toBe(rented!.chestSlot);
    expect(afterInvalid.statusCode, "the screen says which rule refused it").not.toBe("");

    // --- 4. วาง: drag it onto the board, one cell to the right of the item --------------------
    // Inside the 3x3 starting bag, so the cell is a legal one.
    const target = { column: itemCell.x + 1, row: itemCell.y };

    await dragOnCanvas(
      page,
      toPage(box, opened, chestSlot(opened, rented!.chestSlot)),
      toPage(box, opened, boardCell(opened, target.column, target.row)),
    );

    await expect
      .poll(async () => (await counters(page)).placed, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(1);

    const afterPlace = await state(page);
    const placed = afterPlace.pieces.find((piece) => piece.instanceId === rented!.instanceId);

    expect(placed?.zone).toBe("Board");
    expect(placed?.x, "the piece is where the preview said it would go").toBe(target.column);
    expect(placed?.y).toBe(target.row);
    expect(afterPlace.chestUsed).toBe(0);

    // --- 5. หมุน: the keyboard, on the piece the drag left selected ----------------------------
    await page.keyboard.press("r");

    await expect
      .poll(async () => (await counters(page)).rotated, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(1);

    // --- 6. รวม: drop it onto the piece it matches --------------------------------------------
    await dragOnCanvas(
      page,
      toPage(box, opened, boardCell(opened, target.column, target.row)),
      toPage(box, opened, boardCell(opened, itemCell.x, itemCell.y)),
    );

    await expect
      .poll(async () => (await counters(page)).merged, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(1);

    const afterMerge = await state(page);
    const merged = afterMerge.pieces.filter((piece) => piece.kind === "Item");

    expect(merged.length, "the merge consumed one of the two").toBe(1);
    expect(merged[0].tier, "the survivor went up a tier").toBe(2);
    expect(merged[0].x).toBe(itemCell.x);
    expect(merged[0].y).toBe(itemCell.y);

    // --- 7. ถวายคืน: the pot ----------------------------------------------------------------
    const biaBeforeAltar = afterMerge.bia;

    expect(opened.altar, "the layout report must place the incense pot").toBeDefined();

    await dragOnCanvas(
      page,
      toPage(box, opened, boardCell(opened, merged[0].x, merged[0].y)),
      toPage(box, opened, opened.altar!),
    );

    await expect
      .poll(async () => (await counters(page)).recycled, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(1);

    const afterAltar = await state(page);

    expect(afterAltar.bia, "the altar must pay something for a tier 2 piece").toBeGreaterThan(biaBeforeAltar);
    expect(afterAltar.pieces.filter((piece) => piece.kind === "Item").length).toBe(0);

    // --- 8. คืนสภาพ: the restore button, through the UI ---------------------------------------
    await clickOnCanvas(page, toPage(box, opened, control(opened, "restore")));

    await expect
      .poll(async () => (await counters(page)).resets, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(1);

    const afterRestore = await state(page);
    const restored = afterRestore.pieces.filter((piece) => piece.kind === "Item");

    expect(restored.length, "the restore brought the weapon back").toBe(1);
    expect(restored[0].tier).toBe(1);
    expect(restored[0].zone).toBe("Board");
    expect(restored[0].x).toBe(itemCell.x);
    expect(restored[0].y).toBe(itemCell.y);

    console.log("camp report (after restore):", JSON.stringify(afterRestore));

    // --- 9. Escape cancels a drag and puts the picture back -----------------------------------
    const revisionBeforeEscape = afterRestore.backpackRevision;

    const from = toPage(box, opened, boardCell(opened, restored[0].x, restored[0].y));
    const away = toPage(box, opened, boardCell(opened, restored[0].x + 2, restored[0].y + 2));

    // Paced like every other gesture here: uGUI only starts a drag once its own threshold is
    // crossed, and Escape only cancels a drag that is already live — earlier, and it takes the
    // other branch instead (clear the selection) so the gesture under test never runs at all.
    //
    // The wait goes on the release rather than on `dragging`, because the scene freezes the state
    // document while a gesture is live (see CampSceneDirector.Publish): a test cannot ask the page
    // whether the piece is in flight, only what the gesture did once it ended. The two counters
    // separate the cases — dragPreviews for the pick-up, dragCancels for the Escape that undid it.
    await page.mouse.move(from.x, from.y);
    await page.waitForTimeout(FRAME_PAUSE_MS);
    await page.mouse.down();
    await page.waitForTimeout(FRAME_PAUSE_MS);
    await page.mouse.move(away.x, away.y, { steps: 12 });
    await page.waitForTimeout(FRAME_PAUSE_MS);
    await page.keyboard.press("Escape");
    await page.waitForTimeout(FRAME_PAUSE_MS);
    await page.mouse.up();

    await expect
      .poll(async () => (await counters(page)).dragCancels, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(1);

    const afterEscape = await state(page);
    const survivor = afterEscape.pieces.find((piece) => piece.instanceId === restored[0].instanceId);

    expect(afterEscape.dragging).toBe(false);
    expect(afterEscape.backpackRevision, "a cancelled drag never reached the run").toBe(revisionBeforeEscape);
    expect(survivor?.x).toBe(itemCell.x);
    expect(survivor?.y).toBe(itemCell.y);

    await page.screenshot({ path: "test-results/web-camp-after-flow.png", fullPage: false });

    // --- 10. ออกเดินทาง: the real command, once -----------------------------------------------
    const depart = control(await geometry(page), "depart");
    await clickOnCanvas(page, toPage(box, opened, depart));

    await expect
      .poll(async () => (await counters(page)).departures, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(1);

    const departed = await state(page);

    expect(departed.departed).toBe(true);
    expect(departed.flow, "the run is where Core says a departure leaves it").toBe("Battle");
    expect(departed.departFloor).toBe(1);
    expect(departed.canDepart, "the button is dead once the run has left camp").toBe(false);
    expect(departed.statusCode).toBe("");

    // The click guard: a second press cannot reach the run again.
    await clickOnCanvas(page, toPage(box, opened, depart));

    await page.waitForTimeout(1000);

    const afterSecondPress = await state(page);

    expect(afterSecondPress.counters.departures, "a second press must not depart again").toBe(1);
    expect(afterSecondPress.flow).toBe("Battle");

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });

  test("walks the figure with WASD and rents a shelf by pressing E", async ({ page }) => {
    test.setTimeout(300_000);
    const pageErrors = collectPageErrors(page);

    const { layout, first } = await enterCamp(page);

    // --- เดิน: WASD moves the figure, and the report says so -----------------------------------
    const start = { x: first.playerX, z: first.playerZ };
    expect(first.focusedShelf, "the figure should start with no shelf in reach").toBe(-1);

    await page.keyboard.down("w");
    await page.waitForTimeout(400);

    const walking = await state(page);
    expect(walking.moving, "the report does not see the figure walking while W is held").toBe(true);

    await page.keyboard.up("w");

    await expect
      .poll(async () => Math.abs((await state(page)).playerZ - start.z), {
        timeout: COMMAND_TIMEOUT_MS,
        polling: 250,
      })
      .toBeGreaterThan(0.4);

    const afterForward = await state(page);
    const forwardDistance = afterForward.playerZ - start.z;
    console.log(`camp: W walked the figure ${forwardDistance.toFixed(2)} units in 0.4 s`);

    expect(forwardDistance, "W must walk away from the camera").toBeGreaterThan(0.4);
    expect(Math.abs(afterForward.playerX - start.x), "W must not slide the figure sideways").toBeLessThan(0.1);

    await expect
      .poll(async () => (await state(page)).moving, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(false);

    // --- เดินเข้าใกล้ชั้น: walk to a shelf's own column ---------------------------------------
    // The last station, so the walk crosses the sala and the test is not passing on a shelf that
    // happened to be in reach from the spawn.
    const target = shelf(layout, first.shelfCount - 1);
    const walk = layout.walk;

    await walkTo(
      page,
      Math.min(Math.max(target.worldX, walk.xMin), walk.xMax),
      Math.min(Math.max(target.worldZ, walk.zMin), walk.zMax),
    );

    await expect
      .poll(async () => (await state(page)).focusedShelf, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(target.index);

    const stood = await state(page);
    expect(stood.bagVisible, "the panel is not what makes E work").toBe(false);

    // --- กด E: the shelf is rented into the chest --------------------------------------------
    const biaBefore = stood.bia;
    const chestBefore = stood.chestUsed;
    const price = stood.shop[target.index].price;

    expect(stood.shop[target.index].empty, "the fixture needs an unrented shelf").toBe(false);
    expect(price, "the purse must be able to pay for it").toBeLessThanOrEqual(biaBefore);

    await page.keyboard.press("e");

    await expect
      .poll(async () => (await counters(page)).rented, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(1);

    const after = await state(page);

    expect(after.lastWorldAction, "E acted on the shelf the figure is standing at").toBe(`rent:${target.index}`);
    expect(after.worldRents).toBe(1);
    expect(after.bia, "the price paid is the price the shelf showed").toBe(biaBefore - price);
    expect(after.chestUsed, "the goods went into the chest").toBe(chestBefore + 1);
    expect(after.shop[target.index].empty, "the rented shelf is empty").toBe(true);
    expect(after.pieces.some((piece) => piece.zone === "Chest"), "the chest holds the rent").toBe(true);

    console.log("camp report (after walking and pressing E):", JSON.stringify(after));

    await page.screenshot({ path: "test-results/web-camp-rented-walking.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });

  test("lays the camp out at 1280x720 and 1920x1080 and follows a canvas resize", async ({ page }) => {
    test.setTimeout(240_000);
    const pageErrors = collectPageErrors(page);

    const { layout } = await enterCamp(page);

    // The player's own canvas is what has to be resized, not the browser window. Unity's default
    // desktop template pins the canvas' CSS size at 960x600 (measured: a 1920x1080 *viewport* left
    // the report at 960x600 and republished nothing), so the size paths a player actually has are
    // the template's fullscreen button and a HiDPI backing store - both of which resize the canvas
    // element itself. Writing the element's style is that path, and it is why this test drives the
    // canvas rather than the viewport.
    expect(layout.canvasWidth, "the fixture starts at the template's own canvas size").toBe(960);
    expect(layout.canvasHeight).toBe(600);

    for (const size of [
      { width: 1280, height: 720 },
      { width: 1920, height: 1080 },
    ]) {
      await setCanvasSize(page, size.width, size.height);

      // The camp republishes its layout when the canvas changed size, and the document says which
      // canvas it measured - so waiting for the reported size is waiting for the layout that
      // belongs to this resize, not for the one before it.
      await expect
        .poll(async () => (await geometry(page)).canvasWidth, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
        .toBe(size.width);

      const resized = await geometry(page);
      const at = `${size.width}x${size.height}`;

      expect(resized.canvasHeight, `the report did not follow the canvas to ${at}`).toBe(size.height);
      expect(resized.bagVisible, "the panel is closed while the closed camp is measured").toBe(false);

      // --- nothing the player can press is off the canvas, and no two UI elements are on top ---
      const ui = withLayer(resized, "ui");
      expect(ui.length, `the closed camp publishes its own UI at ${at}`).toBeGreaterThanOrEqual(2);

      for (const rect of resized.rects) {
        expect(rect.width, `'${rect.name}' has no width at ${at}`).toBeGreaterThan(0);
        expect(rect.height, `'${rect.name}' has no height at ${at}`).toBeGreaterThan(0);
      }

      for (let i = 0; i < ui.length; i += 1) {
        for (let j = i + 1; j < ui.length; j += 1) {
          expect(
            overlaps(ui[i], ui[j]),
            `at ${at}, '${ui[i].name}' ${JSON.stringify(ui[i])} overlaps ` +
              `'${ui[j].name}' ${JSON.stringify(ui[j])}`,
          ).toBe(false);
        }
      }

      // The shelves are the shop, so each one still has to be somewhere a pointer can reach: a
      // shelf pushed off a wider canvas would be a shelf nobody can rent on that display.
      for (const slot of resized.shelves) {
        expect(slot.x, `shelf ${slot.index} is off the canvas at ${at}`).toBeDefined();
        expect(slot.width, `shelf ${slot.index} has no width at ${at}`).toBeGreaterThan(0);
        expect(slot.height, `shelf ${slot.index} has no height at ${at}`).toBeGreaterThan(0);
      }

      for (const point of resized.buttons) {
        expect(
          point.x,
          `'${point.name}' is off the canvas horizontally at ${at} (${point.x} of ${resized.canvasWidth})`,
        ).toBeGreaterThan(0);
        expect(point.x).toBeLessThan(resized.canvasWidth);
        expect(
          point.y,
          `'${point.name}' is off the canvas vertically at ${at} (${point.y} of ${resized.canvasHeight})`,
        ).toBeGreaterThan(0);
        expect(point.y).toBeLessThan(resized.canvasHeight);
      }

      console.log(
        `camp at ${at} (closed): statusBar ${JSON.stringify(rect(resized, "statusBar"))} ` +
          `openBag ${JSON.stringify(control(resized, "openBag"))} ` +
          `shelves ${resized.shelves.length} controls ${resized.buttons.length}`,
      );

      await page.screenshot({ path: `test-results/web-camp-${at}.png`, fullPage: false });
    }

    // --- back to the size the template ships, where the panel is opened ------------------------
    // The panel's own clicks are made here, at the player's native size, and not straight after a
    // resize: a click aimed at a small control immediately after the canvas changed size can be
    // swallowed (seen once while this test was being written — the same gesture then succeeded
    // twice in a row). What this test is for is the *layout* at the two sizes, which is read from
    // the document rather than clicked at, so the resize path stays out of the click path.
    await setCanvasSize(page, 960, 600);

    await expect
      .poll(async () => (await geometry(page)).canvasWidth, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(960);

    const native = await geometry(page);
    const nativeBox = await canvasBox(page);

    await clickUntilPublished(
      page,
      toPage(nativeBox, native, control(native, "openBag")),
      async () => (await state(page)).bagVisible,
    );

    await expect
      .poll(async () => (await state(page)).bagVisible, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(true);

    // --- the panel, open, at both of the GDD's resolutions -------------------------------------
    for (const size of [
      { width: 1280, height: 720 },
      { width: 1920, height: 1080 },
    ]) {
      await setCanvasSize(page, size.width, size.height);

      await expect
        .poll(async () => (await geometry(page)).canvasWidth, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
        .toBe(size.width);

      const opened = await geometry(page);
      const at = `${size.width}x${size.height}`;

      expect(opened.canvasHeight, `the report did not follow the canvas to ${at}`).toBe(size.height);
      expect(opened.bagVisible, `the panel closed itself on a resize to ${at}`).toBe(true);

      // The board and the chest still draw their cells: a panel that only kept its background would
      // pass a rect check and be unusable.
      expect(opened.boardWidth).toBe(9);
      expect(opened.boardHeight).toBe(7);
      expect(opened.cells.length, `the board lost its cells at ${at}`).toBe(9 * 7);
      expect(opened.chest.length, `the chest lost its slots at ${at}`).toBe(opened.chestSlots);
      expect(opened.cellPitch, `the cell pitch collapsed at ${at}`).toBeGreaterThan(0);

      for (const name of ["board", "chestPanel", "chest", "altar", "instructions", "stats", "statusBar"]) {
        const region = opened.rects.find((candidate) => candidate.name === name);
        expect(region, `at ${at}, the layout report has no '${name}' region`).toBeDefined();
        expect(region!.width, `at ${at}, '${name}' has no width`).toBeGreaterThan(0);
        expect(region!.height, `at ${at}, '${name}' has no height`).toBeGreaterThan(0);
      }

      for (const name of ["closeBag", "rotate", "undo", "restore", "lockBag", "depart"]) {
        const point = control(opened, name);
        expect(
          point.x,
          `at ${at}, '${name}' is off the canvas horizontally (${point.x} of ${opened.canvasWidth})`,
        ).toBeGreaterThan(0);
        expect(point.x).toBeLessThan(opened.canvasWidth);
        expect(
          point.y,
          `at ${at}, '${name}' is off the canvas vertically (${point.y} of ${opened.canvasHeight})`,
        ).toBeGreaterThan(0);
        expect(point.y).toBeLessThan(opened.canvasHeight);
      }

      console.log(
        `camp at ${at} (panel open): board ${JSON.stringify(rect(opened, "board"))} ` +
          `closeBag ${JSON.stringify(control(opened, "closeBag"))} ` +
          `depart ${JSON.stringify(control(opened, "depart"))}`,
      );

      await page.screenshot({ path: `test-results/web-camp-panel-${at}.png`, fullPage: false });
    }

    // --- back to the player's own size, and the panel still closes -----------------------------
    await setCanvasSize(page, 960, 600);

    await expect
      .poll(async () => (await geometry(page)).canvasWidth, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(960);

    const back = await geometry(page);
    expect(back.canvasHeight).toBe(600);
    expect(back.bagVisible, "the panel is still open, back at the player's own size").toBe(true);

    // Closed the way a player closes it — กลับ, at the size the template ships — and the จัดย่าม
    // button is back where the closed camp puts it. The canvas box is read *here* rather than
    // reused from the load: a click has to be aimed at where the element is now, after the two
    // resizes above, and not at where it was 30 s ago.
    const backBox = await canvasBox(page);
    const closeTarget = toPage(backBox, back, control(back, "closeBag"));

    await clickUntilPublished(
      page,
      closeTarget,
      async () => (await state(page)).bagVisible === false,
    );

    await expect
      .poll(async () => (await state(page)).bagVisible, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(false);

    control(await geometry(page), "openBag");

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });
});
