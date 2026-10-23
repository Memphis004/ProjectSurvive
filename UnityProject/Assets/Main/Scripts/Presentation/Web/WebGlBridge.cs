using System.Runtime.InteropServices;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Web
{
    /// <summary>
    /// Hands a build's self-check result and scene state to the page, so a browser
    /// test can assert on what the WebGL player actually did.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A WebGL build is a black box: Unity's test framework cannot run inside it,
    /// and reading the Unity console from Playwright means scraping a log.
    /// Publishing two small globals on <c>window</c> is the smallest bridge that
    /// makes "the build opened AND its serializer and config work" a real
    /// assertion.
    /// </para>
    /// <para>
    /// The native calls are compiled only for the WebGL player. Outside it they are
    /// no-ops, so the same code path runs in EditMode/PlayMode tests — a stub that
    /// threw would make the shell untestable off-platform.
    /// </para>
    /// <para>
    /// <c>Application.ExternalCall</c> is the older way to do this and is
    /// deprecated in Unity 6, so the calls go through a small <c>.jslib</c> plugin
    /// (<c>Assets/Main/Plugins/WebGL/ProjectSurviveWeb.jslib</c>) instead.
    /// </para>
    /// <para>
    /// <b>The three global names live here and nowhere else on the C# side (Stage 10t).</b>
    /// The <c>.jslib</c> functions take the name as an argument rather than
    /// hard-coding it, so the page global a report lands on is decided by the
    /// constants below — a rename is one edit here, and the browser-test side has
    /// the matching single file in <c>tests/web/globals.mjs</c>. An EditMode test
    /// asserts both halves: the constants' values, that the plugin carries no
    /// global name of its own, and that the two files agree.
    /// </para>
    /// </remarks>
    public static class WebGlBridge
    {
        /// <summary>Global the page reads for the smoke verdict.</summary>
        public const string SmokeReportGlobal = "__projectSurviveSmoke";

        /// <summary>Global the page reads for the scene on screen.</summary>
        public const string SceneGlobal = "__projectSurviveScene";

        /// <summary>Global the page reads for the Thai text spike measurement.</summary>
        public const string ThaiSpikeGlobal = "__projectSurviveThaiSpike";

        /// <summary>Global the page reads for the checkpoint/settings storage verdict (Stage 12).</summary>
        public const string PersistenceGlobal = "__projectSurvivePersistence";

        /// <summary>The audio director's play counts and music state (Stage 13c-1).</summary>
        public const string AudioGlobal = "__projectSurviveAudio";

        /// <summary>The settings panel's values, labels and control points (Stage 13c-2a).</summary>
        public const string SettingsGlobal = "__projectSurviveSettings";

        /// <summary>The battle pause overlay's state and control points (Stage 13c-2b).</summary>
        public const string PauseGlobal = "__projectSurvivePause";

        /// <summary>Event dispatched after a smoke report is published.</summary>
        public const string SmokeReportEvent = "projectsurvive:smoke";

        /// <summary>Event dispatched after a scene marker is published.</summary>
        public const string SceneEvent = "projectsurvive:scene";

        /// <summary>
        /// Publishes the smoke report under <see cref="SmokeReportGlobal"/>. Always logs
        /// locally first, so the same verdict is visible in the Editor console and in the
        /// player log.
        /// </summary>
        public static void PublishSmokeReport(string json)
        {
            Debug.Log("[ProjectSurvive] smoke report: " + json);
            ProjectSurvive_Report(SmokeReportGlobal, json);
        }

        /// <summary>Publishes the scene that is on screen under <see cref="SceneGlobal"/>.</summary>
        public static void PublishScene(string sceneName)
        {
            Debug.Log("[ProjectSurvive] scene: " + sceneName);
            ProjectSurvive_Scene(SceneGlobal, sceneName);
        }

        /// <summary>
        /// Publishes an arbitrary JSON document under <paramref name="globalName"/>.
        /// Used for reports beyond the smoke verdict (the Thai text spike is the
        /// current one) without adding a dedicated native export per document.
        /// </summary>
        public static void Publish(string globalName, string json)
        {
            // The payload is deliberately not logged here. A camp or battle report is several kilobytes
            // and publishes on a timer (camp geometry, the floor's progress), so echoing it put
            // megabytes into the Editor console and the browser console per session — and the
            // PlayMode runner keeps every captured line, which is what the 20-visit heap case was
            // measuring (200 KB of "managed growth" per camp/battle visit). The page global
            // carries the document; this line says which one moved and how big it is. The smoke
            // report is still logged in full: it is one small document per session and the verdict
            // a reader wants at a glance.
            Debug.Log("[ProjectSurvive] " + globalName + " (" + (json is null ? 0 : json.Length) + " chars)");
            ProjectSurvive_Publish(globalName, json);
        }

        /// <summary>Logs a line to the browser console (no-op outside WebGL).</summary>
        public static void Log(string message)
        {
            Debug.Log("[ProjectSurvive] " + message);
            ProjectSurvive_Log(message);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void ProjectSurvive_Report(
            [MarshalAs(UnmanagedType.LPStr)] string globalName,
            [MarshalAs(UnmanagedType.LPStr)] string json);

        [DllImport("__Internal")]
        private static extern void ProjectSurvive_Scene(
            [MarshalAs(UnmanagedType.LPStr)] string globalName,
            [MarshalAs(UnmanagedType.LPStr)] string sceneName);

        [DllImport("__Internal")]
        private static extern void ProjectSurvive_Publish(
            [MarshalAs(UnmanagedType.LPStr)] string globalName,
            [MarshalAs(UnmanagedType.LPStr)] string json);

        [DllImport("__Internal")]
        private static extern void ProjectSurvive_Log([MarshalAs(UnmanagedType.LPStr)] string message);
#else
        private static void ProjectSurvive_Report(string globalName, string json)
        {
            // No page to talk to outside the WebGL player.
        }

        private static void ProjectSurvive_Scene(string globalName, string sceneName)
        {
            // No page to talk to outside the WebGL player.
        }

        private static void ProjectSurvive_Publish(string globalName, string json)
        {
            // No page to talk to outside the WebGL player.
        }

        private static void ProjectSurvive_Log(string message)
        {
            // No browser console outside the WebGL player.
        }
#endif
    }
}
