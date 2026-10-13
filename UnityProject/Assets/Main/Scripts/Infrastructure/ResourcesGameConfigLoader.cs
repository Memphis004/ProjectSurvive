using System;
using ProjectSurvive.Config;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Infrastructure.Config;
using UnityEngine;

namespace ProjectSurvive.Game.Infrastructure
{
    /// <summary>
    /// Unity-side adapter that hands <see cref="LubanGameConfig"/> its table bytes
    /// from <c>Assets/Main/Resources/Config/*.bytes</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the only place in the shell that knows where the balance data lives
    /// on disk. <c>lib/Runtime/Infrastructure</c> is engine-free and takes a
    /// <c>Func&lt;string, byte[]&gt;</c>; the Unity shell supplies it, so the core
    /// package stays testable with <c>dotnet test</c> (AGENTS rule 3, ADR 0006).
    /// </para>
    /// <para>
    /// <c>Resources</c> is used instead of <c>StreamingAssets</c> because a WebGL
    /// build cannot read StreamingAssets with <c>System.IO</c> (AGENTS rule 9) —
    /// on WebGL that data only exists as a URL. <c>Resources</c> is the MVP
    /// loading path fixed by <c>UnityProject/SETUP.md</c>; a later stage may move
    /// it to Addressables without changing this class's contract.
    /// </para>
    /// <para>
    /// A missing table throws instead of returning an empty array: a build with
    /// half the balance data must fail loudly, never silently play with defaults.
    /// </para>
    /// </remarks>
    public static class ResourcesGameConfigLoader
    {
        /// <summary>Resources sub-folder holding the generated Luban tables.</summary>
        public const string ConfigFolder = "Config";

        /// <summary>
        /// Loads every table and builds the <see cref="IGameConfig"/> the rule
        /// engine runs on, using the hash the generator baked into the library.
        /// </summary>
        public static IGameConfig Load() => Load(ConfigBuildMetadata.ConfigHash);

        /// <summary>
        /// As <see cref="Load"/>, but with an explicit hash — used by tests that
        /// assert the hash and the tables agree.
        /// </summary>
        public static IGameConfig Load(string configHash) =>
            LubanGameConfig.Load(ReadTable, configHash);

        /// <summary>
        /// Raw bytes of one Luban table, e.g. <c>"tbitem"</c>. Public because the
        /// smoke tests read single tables to prove the name mapping.
        /// </summary>
        public static byte[] ReadTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Table name must not be null or empty.", nameof(tableName));
            }

            string resourcePath = ConfigFolder + "/" + tableName;
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "Generated config table not found at Resources/" + resourcePath +
                    ". Run tools/gen.ps1 - the .bytes files are generated, never hand-authored.");
            }

            return asset.bytes;
        }
    }
}
