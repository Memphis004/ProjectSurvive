import { expect, test, type Page } from "@playwright/test";

import { PERSISTENCE, SCENE, SMOKE } from "./globals.mjs";

/**
 * Stage 12 acceptance in the browser: the checkpoint survives a real page reload.
 *
 * A WebGL player cannot run Unity's test framework, so what a reload did is read from the
 * documents the player publishes about itself:
 *
 *   __projectSurvivePersistence - storage availability, whether a checkpoint was found (and
 *                                 resumed), whether the last write succeeded, and the settings
 *                                 currently in effect (`GameSave`)
 *   __projectSurviveCamp        - the run's flow, floor, HP, bia, bag pieces and shop slots
 *                                 (`CampReport`)
 *   __projectSurviveBattle      - the floor that is running, after a departure
 *   __projectSurviveScene       - the scene on screen
 *
 * The three cases are the stage's three acceptance lines: refresh at the camp, refresh while a
 * floor is running, and storage switched off. Every one of them reloads the *page*, not the scene:
 * a scene handover would keep the run in memory and prove nothing about the checkpoint.
 *
 * Timing: the first load of the player downloads and compiles the WASM, hence the minutes-sized
 * boot budget. After that a save is a debounced write (0.5 s) plus Unity's IndexedDB flush, so the
 * persistence assertions poll rather than sleep for a fixed time.
 */

const CAMP = "__projectSurviveCamp";
const BATTLE = "__projectSurviveBattle";
const GEOMETRY = "__projectSurviveCampGeometry";
const TITLE = "__projectSurviveTitle";

/** Boot: download + compile the player. */
const PLAYER_BOOT_TIMEOUT_MS = 180_000;

/** How long a command, a save or a scene handover may take to show up. */
const COMMAND_TIMEOUT_MS = 20_000;

interface PersistenceReport {
  storageAvailable: boolean;
  storageReason: string;
  checkpointFound: boolean;
  recoveredFromBackup: boolean;
  resumed: boolean;
  lastSaveAttempted: boolean;
  lastSaveOk: boolean;
  error: string;
  language: string;
  volume: number;
  defaultSpeed: number;
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
  flow: string;
  floor: number;
  totalFloors: number;
  hp: number;
  maxHp: number;
  bia: number;
  chestUsed: number;
  chestSlots: number;
  backpackRevision: number;
  bagVisible: boolean;
  canDepart: boolean;
  departError: string;
  departed: boolean;
  saveFailed: boolean;
  saveWarning: string;
  saveError: string;
  counters: { rented: number; placed: number; departures: number };
  shop: CampShopSlot[];
  pieces: CampPiece[];
}

interface BattleReport {
  scene: string;
  phase: string;
  tick: string | number;
  bia?: number;
  hp?: number;
}

interface Point {
  x: number;
  y: number;
}

interface CampGeometry {
  canvasWidth: number;
  canvasHeight: number;
  cells: Array<{ column: number; row: number; x: number; y: number }>;
  chest: Array<{ slot: number; x: number; y: number }>;
  buttons: Array<{ name: string; x: number; y: number }>;
}

interface TitleReport {
  canvasWidth: number;
  canvasHeight: number;
  buttons: Array<{ name: string; x: number; y: number }>;
}

interface Box {
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

function readGlobal<T>(page: Page, name: string): Promise<T> {
  return page.evaluate(
    (globalName) => (window as unknown as Record<string, unknown>)[globalName] as T,
    name,
  );
}

async function waitForGlobal(page: Page, name: string): Promise<void> {
  await page.waitForFunction(
    (globalName) => Boolean((window as unknown as Record<string, unknown>)[globalName]),
    name,
    { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 },
  );
}

async function waitForScene(page: Page, scene: string): Promise<void> {
  await waitForGlobal(page, SCENE);
  await expect
    .poll(() => readGlobal<string>(page, SCENE), { timeout: PLAYER_BOOT_TIMEOUT_MS, polling: 500 })
    .toBe(scene);
}

async function canvasBox(page: Page): Promise<Box> {
  const box = await page.locator("canvas").boundingBox();
  expect(box, "the player has no canvas element").not.toBeNull();
  return box!;
}

function toPage(box: Box, layout: { canvasWidth: number; canvasHeight: number }, unity: Point): Point {
  return {
    x: box.x + (unity.x / layout.canvasWidth) * box.width,
    y: box.y + ((layout.canvasHeight - unity.y) / layout.canvasHeight) * box.height,
  };
}

function control(layout: CampGeometry, name: string): Point {
  const button = layout.buttons.find((candidate) => candidate.name === name);
  expect(button, `the layout report has no '${name}' control`).toBeDefined();
  return { x: button!.x, y: button!.y };
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

/**
 * A frame's worth of time between pointer events. A WebGL player reads input once per rendered
 * frame, so a press and a release inside the same frame are one event (the measurement is in
 * `camp.spec.ts`).
 */
const FRAME_PAUSE_MS = 120;

async function clickOnCanvas(page: Page, point: Point): Promise<void> {
  await page.mouse.move(point.x, point.y);
  await page.waitForTimeout(FRAME_PAUSE_MS);
  await page.mouse.down();
  await page.waitForTimeout(FRAME_PAUSE_MS);
  await page.mouse.up();
  await page.waitForTimeout(FRAME_PAUSE_MS);
}

async function dragOnCanvas(page: Page, from: Point, to: Point): Promise<void> {
  await page.mouse.move(from.x, from.y);
  await page.waitForTimeout(FRAME_PAUSE_MS);
  await page.mouse.down();
  await page.waitForTimeout(FRAME_PAUSE_MS);
  await page.mouse.move(to.x, to.y, { steps: 12 });
  await page.waitForTimeout(FRAME_PAUSE_MS);
  await page.mouse.up();
  await page.waitForTimeout(FRAME_PAUSE_MS);
}

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
    await page.mouse.move(target.x, target.y - 40);
    await page.waitForTimeout(500);
  }
}

async function readCamp(page: Page): Promise<CampState> {
  return readGlobal<CampState>(page, CAMP);
}

async function readPersistence(page: Page): Promise<PersistenceReport> {
  return readGlobal<PersistenceReport>(page, PERSISTENCE);
}

/**
 * Opens the title and starts a run through the screen's own control, so the page URL stays `/`.
 * That matters: the persistence cases have to reload the *player's* URL, because a `?scene=Camp`
 * URL is a diagnostic override and deliberately skips the resume path.
 */
async function startRun(page: Page): Promise<void> {
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await waitForScene(page, "Title");
  await waitForGlobal(page, TITLE);
  await waitForGlobal(page, PERSISTENCE);

  const title = await readGlobal<TitleReport>(page, TITLE);
  const box = await canvasBox(page);
  const start = title.buttons.find((button) => button.name === "start");
  expect(start, "the title report has no start control").toBeDefined();

  const target = toPage(box, title, start!);
  let left = false;
  for (let attempt = 1; attempt <= 4 && !left; attempt += 1) {
    await clickOnCanvas(page, target);
    left = await expect
      .poll(() => readGlobal<string>(page, SCENE), { timeout: 15_000, polling: 500 })
      .toBe("Camp")
      .then(
        () => true,
        () => false,
      );

    if (!left) {
      console.log(`the 'start' click was not registered (attempt ${attempt} of 4)`);
      await page.mouse.move(target.x, target.y - 60);
      await page.waitForTimeout(800);
    }
  }

  expect(left, "the title never left for the camp: เริ่มเล่น did not start a run").toBe(true);
  await waitForGlobal(page, CAMP);
  await waitForGlobal(page, GEOMETRY);
  expect((await readCamp(page)).flow).toBe("Camp");
}

/** Waits for the debounced camp write, and returns the camp as the last save left it. */
async function waitForSavedCamp(page: Page): Promise<CampState> {
  await expect
    .poll(
      async () => {
        const saved = await readPersistence(page);
        const camp = await readCamp(page);
        return saved.lastSaveAttempted && saved.lastSaveOk && camp.saveFailed === false;
      },
      { timeout: COMMAND_TIMEOUT_MS, polling: 250 },
    )
    .toBe(true);

  return readCamp(page);
}

/** The fields a reload must reproduce exactly. Ids are not compared: a run is its content. */
function snapshot(camp: CampState): unknown {
  return {
    flow: camp.flow,
    floor: camp.floor,
    hp: camp.hp,
    maxHp: camp.maxHp,
    bia: camp.bia,
    chestUsed: camp.chestUsed,
    backpackRevision: camp.backpackRevision,
    pieces: [...camp.pieces]
      .sort((a, b) =>
        `${a.kind}/${a.defId}/${a.zone}/${a.x}/${a.y}/${a.chestSlot}`.localeCompare(
          `${b.kind}/${b.defId}/${b.zone}/${b.x}/${b.y}/${b.chestSlot}`,
        ),
      )
      .map((piece) => ({
        kind: piece.kind,
        defId: piece.defId,
        tier: piece.tier,
        zone: piece.zone,
        x: piece.x,
        y: piece.y,
        rotation: piece.rotation,
        chestSlot: piece.chestSlot,
        cells: piece.cells,
      })),
    shop: camp.shop.map((slot) => ({
      index: slot.index,
      pieceId: slot.pieceId,
      kind: slot.kind,
      price: slot.price,
      empty: slot.empty,
      purchased: slot.purchased,
      locked: slot.locked,
      affordable: slot.affordable,
    })),
  };
}

/** Rents one shelf and puts the rented piece on the board, so shop, purse and bag all moved. */
async function buyAndPlace(page: Page): Promise<CampState> {
  const geometry = await readGlobal<CampGeometry>(page, GEOMETRY);
  const box = await canvasBox(page);
  const first = await readCamp(page);
  const offer = first.shop.find((slot) => !slot.empty && !slot.purchased && slot.affordable);
  expect(offer, "the shop must offer at least one affordable piece").toBeDefined();

  await clickUntilPublished(
    page,
    toPage(box, geometry, control(geometry, `rent${offer!.index}`)),
    async () => (await readCamp(page)).counters.rented === 1,
  );

  const afterRent = await readCamp(page);
  expect(afterRent.bia, "the shelf charged its own price").toBe(first.bia - offer!.price);
  expect(afterRent.chestUsed, "the goods went into the chest").toBe(1);

  const rented = afterRent.pieces.find((piece) => piece.zone === "Chest");
  expect(rented, "a rented piece is carried in the chest").toBeDefined();

  // Open the panel and place the chest piece on the board, beside the starting item. The cell is
  // one the camp's own preview accepts; a fixed one is enough because the starting bag is 3×3.
  await clickUntilPublished(
    page,
    toPage(box, geometry, control(geometry, "openBag")),
    async () => (await readCamp(page)).bagVisible,
  );

  const opened = await readGlobal<CampGeometry>(page, GEOMETRY);
  const item = afterRent.pieces.find((piece) => piece.kind === "Item" && piece.zone === "Board");
  expect(item, "the run starts with an item on the board").toBeDefined();

  const target = { column: item!.x + 1, row: item!.y };
  await dragOnCanvas(
    page,
    toPage(box, opened, chestSlot(opened, rented!.chestSlot)),
    toPage(box, opened, boardCell(opened, target.column, target.row)),
  );

  await expect
    .poll(async () => (await readCamp(page)).counters.placed, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
    .toBe(1);

  const afterPlace = await readCamp(page);
  expect(afterPlace.pieces.find((piece) => piece.zone === "Chest"), "the chest is empty again").toBeUndefined();
  expect(afterPlace.chestUsed).toBe(0);

  // The camp's write is debounced by half a second; the wait below is for the write, not a guess
  // at when it happened.
  await page.waitForTimeout(1_200);
  return waitForSavedCamp(page);
}

test.describe("ProjectSurvive persistence in the browser", () => {
  test("a refresh at the camp resumes the shop, the bag, HP and bia", async ({ page }) => {
    test.setTimeout(330_000);
    const pageErrors = collectPageErrors(page);

    await startRun(page);
    const before = await buyAndPlace(page);

    const saved = await readPersistence(page);
    console.log("persistence before reload:", JSON.stringify(saved));
    console.log("camp before reload:", JSON.stringify(before));

    expect(saved.storageAvailable, saved.storageReason).toBe(true);
    expect(saved.lastSaveOk, saved.error).toBe(true);
    expect(saved.checkpointFound, "a successful save leaves a checkpoint to find").toBe(true);

    await page.reload({ waitUntil: "domcontentloaded" });

    // A reload is a fresh player process: Boot reads the checkpoint before it hands over, so the
    // first scene this page shows is the camp, not the title.
    await waitForScene(page, "Camp");
    await waitForGlobal(page, CAMP);
    await waitForGlobal(page, GEOMETRY);

    const resumed = await readPersistence(page);
    console.log("persistence after reload:", JSON.stringify(resumed));

    expect(resumed.storageAvailable, resumed.storageReason).toBe(true);
    expect(resumed.resumed, "Boot did not restore the checkpoint").toBe(true);
    expect(resumed.error).toBe("");

    const after = await readCamp(page);
    console.log("camp after reload:", JSON.stringify(after));

    expect(after.flow).toBe("Camp");
    expect(after.departed).toBe(false);
    expect(after.canDepart, after.departError).toBe(true);

    // The whole checkpoint: floor, HP, purse, the bag's revision and every piece and shop slot.
    // A shop that re-rolled on load would differ in `pieceId`/`price`; a bag read from the wrong
    // snapshot would differ in `zone` or the cell. The comparison is content, not object identity.
    expect(snapshot(after)).toEqual(snapshot(before));

    await page.screenshot({ path: "test-results/web-persistence-camp.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });

  test("a refresh during a floor returns to the camp checkpoint, without the floor's reward", async ({ page }) => {
    test.setTimeout(330_000);
    const pageErrors = collectPageErrors(page);

    await startRun(page);
    const before = await waitForSavedCamp(page);
    const savedBefore = await readPersistence(page);
    expect(savedBefore.lastSaveOk, savedBefore.error).toBe(true);

    const geometry = await readGlobal<CampGeometry>(page, GEOMETRY);
    const box = await canvasBox(page);

    await clickUntilPublished(
      page,
      toPage(box, geometry, control(geometry, "openBag")),
      async () => (await readCamp(page)).bagVisible,
    );

    const opened = await readGlobal<CampGeometry>(page, GEOMETRY);
    await clickUntilPublished(
      page,
      toPage(box, opened, control(opened, "depart")),
      async () => (await readCamp(page)).departed,
    );

    // The departure awaited its checkpoint before the scene changed, so the arena is proof that a
    // save exists for this floor.
    await waitForScene(page, "Battle");
    await waitForGlobal(page, BATTLE);
    await expect
      .poll(async () => {
        const battle = await readGlobal<BattleReport>(page, BATTLE);
        return Number(battle.tick);
      }, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBeGreaterThan(0);

    // Let the floor run: anything it did here is not in the run until it is settled, and the
    // reload must not accept any of it.
    await page.waitForTimeout(2_000);
    const duringBattle = await readGlobal<BattleReport>(page, BATTLE);
    console.log("battle before reload:", JSON.stringify(duringBattle));

    await page.reload({ waitUntil: "domcontentloaded" });

    await waitForScene(page, "Camp");
    await waitForGlobal(page, CAMP);
    await waitForGlobal(page, GEOMETRY);

    const resumed = await readPersistence(page);
    console.log("persistence after battle reload:", JSON.stringify(resumed));

    expect(resumed.resumed, "a floor in progress must resume its pre-floor checkpoint").toBe(true);
    expect(resumed.error).toBe("");

    const after = await readCamp(page);
    console.log("camp after battle reload:", JSON.stringify(after));

    expect(after.flow, "a reload during Battle goes back to the camp, not into the arena").toBe("Camp");
    expect(after.floor, "the floor about to be fought is the same one").toBe(before.floor);

    // No reward, no HP change: the battle never settled, so none of its numbers reached the run.
    expect(after.bia, "an unfinished floor must not pay bia").toBe(before.bia);
    expect(after.hp, "an unfinished floor must not change HP").toBe(before.hp);

    // Shop and bag are the checkpoint's, not a re-roll.
    expect(snapshot(after)).toEqual(snapshot(before));

    await page.screenshot({ path: "test-results/web-persistence-battle-reload.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });

  test("storage that is switched off warns and never reports a successful save", async ({ page }) => {
    test.setTimeout(330_000);
    const pageErrors = collectPageErrors(page);

    // Before the player boots, as if the browser blocked IndexedDB (private mode, a policy, or a
    // quota of zero). The player asks the page once, in the store's constructor.
    await page.addInitScript(() => {
      (window as unknown as { projectSurviveStorageDisabled?: boolean }).projectSurviveStorageDisabled =
        true;
    });

    await startRun(page);

    const atTitle = await readPersistence(page);
    console.log("persistence with storage off (title):", JSON.stringify(atTitle));

    expect(atTitle.storageAvailable, "the page said IndexedDB is unavailable").toBe(false);
    expect(atTitle.storageReason).toContain("IndexedDB");
    expect(atTitle.lastSaveOk, "unavailable storage must never read as saved").toBe(false);
    expect(atTitle.error).toBe("save.storage_unavailable");

    // The camp tries a real write (the debounced autosave). It must fail, say so on screen and in
    // the report, and still leave the game playable for this session.
    await expect
      .poll(async () => (await readCamp(page)).saveFailed, { timeout: COMMAND_TIMEOUT_MS, polling: 250 })
      .toBe(true);

    const camp = await readCamp(page);
    const persisted = await readPersistence(page);
    console.log("camp with storage off:", JSON.stringify(camp));
    console.log("persistence with storage off (camp):", JSON.stringify(persisted));

    expect(camp.saveWarning, "the camp must show the localized warning").toMatch(/[\u0E00-\u0E7F]/);
    expect(camp.saveError).toBe("save.storage_unavailable");
    expect(persisted.lastSaveAttempted).toBe(true);
    expect(persisted.lastSaveOk, "a failed write must not be reported as a success").toBe(false);
    expect(persisted.error).toBe("save.storage_unavailable");

    // The run itself is untouched by the storage failure.
    expect(camp.flow).toBe("Camp");
    expect(camp.canDepart, camp.departError).toBe(true);
    expect(camp.bia).toBeGreaterThan(0);

    await page.screenshot({ path: "test-results/web-persistence-storage-off.png", fullPage: false });

    expect(pageErrors, `the page reported errors:\n${pageErrors.join("\n")}`).toEqual([]);
  });
});
