// ProjectSurvive - WebGL page bridge.
//
// The Unity player and the page need exactly two things from each other in
// Stage 03: the page must be able to see the build's self-check result, and it
// must be able to tell which scene is on screen. Both are small JSON/string
// hand-offs, so this stays a tiny plugin rather than a general messaging layer.
//
// Handles are UTF-8 C strings allocated by the marshaller for the duration of the
// call; never keep one. See ProjectSurvive.Game.Presentation.Web.WebGlBridge.
//
// The global names are NOT written here (Stage 10t): every function below takes the
// name the C# side publishes under, so `WebGlBridge`'s constants are the only place
// a page global is named on the C# side, and `tests/web/globals.mjs` is the only
// place on the browser-test side.

mergeInto(LibraryManager.library, {
  // Publishes the smoke report under the named global.
  // A JSON.parse failure is still published, with passed=false, so a browser test
  // fails loudly instead of timing out on a missing global.
  ProjectSurvive_Report: function (globalNamePtr, jsonPtr) {
    var name = UTF8ToString(globalNamePtr);
    var json = UTF8ToString(jsonPtr);
    try {
      window[name] = JSON.parse(json);
    } catch (error) {
      window[name] = {
        passed: false,
        failedCount: 1,
        probes: [],
        parseError: String(error),
        raw: json
      };
    }
    window.dispatchEvent(new CustomEvent("projectsurvive:smoke", { detail: json }));
  },

  // Publishes the currently loaded scene name under the named global.
  ProjectSurvive_Scene: function (globalNamePtr, namePtr) {
    var name = UTF8ToString(globalNamePtr);
    window[name] = UTF8ToString(namePtr);
    window.dispatchEvent(new CustomEvent("projectsurvive:scene", { detail: window[name] }));
  },

  // Publishes an arbitrary JSON document under a page-chosen global name, so a
  // new report does not need a new native export (window[name] = parsed json).
  ProjectSurvive_Publish: function (namePtr, jsonPtr) {
    var name = UTF8ToString(namePtr);
    var json = UTF8ToString(jsonPtr);
    try {
      window[name] = JSON.parse(json);
    } catch (error) {
      window[name] = { passed: false, parseError: String(error), raw: json };
    }
  },

  // Forwards a line to the browser console so a Playwright run can collect it.
  ProjectSurvive_Log: function (messagePtr) {
    if (window.console && window.console.log) {
      window.console.log("[ProjectSurvive] " + UTF8ToString(messagePtr));
    }
  },

  // Stage 12: is the browser's IndexedDB usable? Unity mounts
  // Application.persistentDataPath on an Emscripten IDBFS when IndexedDB exists
  // (WebGLSupport/BuildTools/prejs/IdbFs.js, Unity 6000.3.9f1). A test hook can
  // force the unavailable path before the player boots.
  ProjectSurvive_StorageAvailable: function () {
    if (window.projectSurviveStorageDisabled === true) {
      return 0;
    }
    return (typeof indexedDB !== 'undefined' && indexedDB !== null) ? 1 : 0;
  },

  // Stage 12: flush the IDBFS mount that backs Application.persistentDataPath.
  // FS.syncfs is Emscripten's own API; Unity's WebGL support calls it through
  // IDBFS.queuePersist (BuildTools/lib/FileSystem.js).
  //
  // The result is POLLED, not called back. `FS.syncfs` finishes on a browser
  // event, and a C# Task completed from there never resumed its awaiting code
  // in the player (measured in Stage 12: the write reached IndexedDB, and the
  // save callers waited forever with no error). Recording the outcome lets the
  // player complete the task from its own Update, where continuations run.
  //
  // The outcome lives in a `$`-prefixed library symbol: it is scoped to the generated
  // player code and is NOT a page global, so no global name is written here (Stage 10t:
  // a page global's name is passed in by WebGlBridge). 0 idle, 1 in flight, 2 written,
  // 3 failed — see ProjectSurvive_FlushPoll.
  $ProjectSurviveFlushState: 0,

  ProjectSurvive_FlushBegin: function () {
    $ProjectSurviveFlushState = 1;
    if (typeof FS === 'undefined' || !FS.syncfs) {
      $ProjectSurviveFlushState = 3;
      return;
    }
    try {
      FS.syncfs(false, function (error) {
        $ProjectSurviveFlushState = error ? 3 : 2;
      });
    } catch (error) {
      $ProjectSurviveFlushState = 3;
    }
  },

  // 0 idle, 1 in flight, 2 written, 3 failed. A finished state is reported once.
  ProjectSurvive_FlushPoll: function () {
    var state = $ProjectSurviveFlushState | 0;
    if (state === 2 || state === 3) {
      $ProjectSurviveFlushState = 0;
    }
    return state;
  },

  // ---------------------------------------------------------------------------
  // Stage 14: the development-only test harness' command queue.
  //
  // The page can only read globals, so a command that the page *sends* (advance n
  // ticks, start a run with a named seed) needs a queue the player polls. The queue
  // is a library symbol, not a page global: the send function's name and the
  // opcodes come from the C# side (`HarnessBridge`), exactly like every other page
  // global name (Stage 10t).
  //
  // `ProjectSurvive_HarnessEnable` is called once by `HarnessBridge` at boot, and
  // only in a development build or the Editor. A release player never calls it, so
  // the send function is never defined there - which is what the release absence
  // test reads.
  //
  // A command is two integers: opcode and one argument (a seed, a tick count, or 0).
  // Numbers rather than JSON because the reader is a C# `int` return value; a string
  // would need a callback and a marshalled buffer for no gain.
  //
  // The queue is created HERE, by assignment, not by a `$ProjectSurviveHarnessQueue: []`
  // declaration. Unity 6.3's jsifier drops a `$symbol: value` member from the generated
  // framework (the symbol is listed in `unexportedSymbols` and no `var`/initialiser is
  // emitted), so a function that *reads* it throws `$symbol is not defined` and halts the
  // player. `$ProjectSurviveFlushState` above survives only by accident: assigning to an
  // undeclared name in the non-strict framework creates the global, and every read there
  // happens after an assignment. This queue is read before anything is assigned, so it
  // must be initialised explicitly (measured: the first harness build halted with exactly
  // that ReferenceError).
  // ---------------------------------------------------------------------------
  ProjectSurvive_HarnessEnable: function (sendNamePtr) {
    if (typeof $ProjectSurviveHarnessQueue === 'undefined') {
      $ProjectSurviveHarnessQueue = [];
    }

    var name = UTF8ToString(sendNamePtr);
    $ProjectSurviveHarnessQueue.length = 0;
    window[name] = function (op, arg) {
      if (typeof op !== 'number' || typeof arg !== 'number') {
        return false;
      }
      if (!isFinite(op) || !isFinite(arg)) {
        return false;
      }
      $ProjectSurviveHarnessQueue.push([op | 0, arg | 0]);
      return true;
    };
  },

  // The opcode at the head of the queue, or 0 when nothing is waiting.
  ProjectSurvive_HarnessPoll: function () {
    return $ProjectSurviveHarnessQueue.length > 0 ? ($ProjectSurviveHarnessQueue[0][0] | 0) : 0;
  },

  // The argument of the command at the head of the queue (0 when none).
  ProjectSurvive_HarnessArg: function () {
    return $ProjectSurviveHarnessQueue.length > 0 ? ($ProjectSurviveHarnessQueue[0][1] | 0) : 0;
  },

  // Drops the command at the head and returns how many are left.
  ProjectSurvive_HarnessShift: function () {
    if ($ProjectSurviveHarnessQueue.length > 0) {
      $ProjectSurviveHarnessQueue.shift();
    }
    return $ProjectSurviveHarnessQueue.length;
  }
});
