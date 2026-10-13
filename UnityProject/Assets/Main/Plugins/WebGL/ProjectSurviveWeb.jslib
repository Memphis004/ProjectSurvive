// ProjectSurvive - WebGL page bridge.
//
// The Unity player and the page need exactly two things from each other in
// Stage 03: the page must be able to see the build's self-check result, and it
// must be able to tell which scene is on screen. Both are small JSON/string
// hand-offs, so this stays a tiny plugin rather than a general messaging layer.
//
// Handles are UTF-8 C strings allocated by the marshaller for the duration of the
// call; never keep one. See ProjectSurvive.Game.Presentation.Web.WebGlBridge.

mergeInto(LibraryManager.library, {
  // Publishes the smoke report as window.__projectSurviveSmoke.
  // A JSON.parse failure is still published, with passed=false, so a browser test
  // fails loudly instead of timing out on a missing global.
  ProjectSurvive_Report: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    try {
      window.__projectSurviveSmoke = JSON.parse(json);
    } catch (error) {
      window.__projectSurviveSmoke = {
        passed: false,
        failedCount: 1,
        probes: [],
        parseError: String(error),
        raw: json
      };
    }
    window.dispatchEvent(new CustomEvent("projectsurvive:smoke", { detail: json }));
  },

  // Publishes the currently loaded scene name as window.__projectSurviveScene.
  ProjectSurvive_Scene: function (namePtr) {
    window.__projectSurviveScene = UTF8ToString(namePtr);
    window.dispatchEvent(new CustomEvent("projectsurvive:scene", { detail: window.__projectSurviveScene }));
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
  }
});
