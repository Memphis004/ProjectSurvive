using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Data.Validator
{
    /// <summary>One validation finding: a stable code, the row it is about, and a readable message.</summary>
    /// <remarks>
    /// The code is machine-readable and stable (<c>localization.duplicate_key</c>), never a
    /// sentence meant for a player (AGENTS rule 5). The message is for the developer running the
    /// data gate.
    /// </remarks>
    public sealed class Finding
    {
        /// <summary>Creates a finding.</summary>
        public Finding(string code, string target, string message)
        {
            Code = code;
            Target = target;
            Message = message;
        }

        /// <summary>Stable identifier, e.g. <c>constant.non_positive</c>.</summary>
        public string Code { get; }

        /// <summary>Where the problem is, e.g. <c>localization['ui.title']</c>.</summary>
        public string Target { get; }

        /// <summary>Developer-facing description.</summary>
        public string Message { get; }

        /// <inheritdoc />
        public override string ToString() => Code + "  " + Target + "  " + Message;
    }

    /// <summary>
    /// Cross-row rules of the two tables the template keeps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A game adds its own rules here, next to these, and adds a fixture under
    /// <c>lib/Tests~/fixtures/</c> for each one: the fixture proves the rule fires end-to-end
    /// (data → Luban → <c>.bytes</c> → findings), while <c>ConfigRulesTests</c> proves it on the
    /// loaded snapshot.
    /// </para>
    /// <para>
    /// These rules are deliberately about <i>shape</i>, not balance: a number being small is a
    /// design decision, a number being zero or negative where the code divides by it is a bug.
    /// </para>
    /// </remarks>
    public static class ConfigValidator
    {
        /// <summary>Applies every rule to a loaded snapshot.</summary>
        public static IReadOnlyList<Finding> Validate(IGameConfig config)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var findings = new List<Finding>();

            ValidateConstant(config.Constant, findings);
            ValidateLocalization(config, findings);

            findings.Sort((left, right) =>
                string.CompareOrdinal(left.Code + left.Target, right.Code + right.Target));

            return findings;
        }

        private static void ValidateConstant(ConstantConfig constant, List<Finding> findings)
        {
            RequirePositive(constant.MaxHp, "max_hp", findings);
            RequireNonNegative(constant.MaxMp, "max_mp", findings);
            RequirePositive(constant.MoveSpeed, "move_speed", findings);
            RequireNonNegative(constant.PickupRadius, "pickup_radius", findings);

            // A non-positive move_speed already has its own finding, and every radius is bigger
            // than a non-positive threshold - report the cause, not the consequence.
            if (constant.MoveSpeed > 0f && constant.PickupRadius > constant.MoveSpeed * 10f)
            {
                findings.Add(new Finding(
                    "constant.out_of_range",
                    "tbconstant.pickup_radius",
                    "pickup_radius (" + constant.PickupRadius + ") is more than ten times move_speed ("
                    + constant.MoveSpeed + "); a pickup that large usually means a unit mistake"));
            }
        }

        private static void ValidateLocalization(IGameConfig config, List<Finding> findings)
        {
            if (config.Localization.Count == 0)
            {
                findings.Add(new Finding(
                    "localization.empty", "tblocalization", "the localization table has no rows"));
                return;
            }

            // The dictionary is built while loading, so a duplicate key that reached this point
            // was added by a snapshot rather than by the file - still worth naming.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (LocalizationEntry entry in config.Localization)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    findings.Add(new Finding(
                        "localization.blank_key", "tblocalization", "a row has an empty key"));
                    continue;
                }

                if (!seen.Add(entry.Key))
                {
                    findings.Add(new Finding(
                        "localization.duplicate_key",
                        "tblocalization['" + entry.Key + "']",
                        "the key appears more than once"));
                }

                if (string.IsNullOrEmpty(entry.Th) && string.IsNullOrEmpty(entry.En))
                {
                    findings.Add(new Finding(
                        "localization.blank_text",
                        "tblocalization['" + entry.Key + "']",
                        "both languages are empty, so the row can never be shown"));
                }
            }
        }

        private static void RequirePositive(int value, string field, List<Finding> findings)
        {
            if (value <= 0)
            {
                findings.Add(new Finding(
                    "constant.non_positive", "tbconstant." + field, field + " must be greater than zero"));
            }
        }

        private static void RequireNonNegative(int value, string field, List<Finding> findings)
        {
            if (value < 0)
            {
                findings.Add(new Finding(
                    "constant.negative", "tbconstant." + field, field + " must not be negative"));
            }
        }

        private static void RequirePositive(float value, string field, List<Finding> findings)
        {
            if (!(value > 0f))
            {
                findings.Add(new Finding(
                    "constant.non_positive", "tbconstant." + field, field + " must be greater than zero"));
            }
        }

        private static void RequireNonNegative(float value, string field, List<Finding> findings)
        {
            if (value < 0f)
            {
                findings.Add(new Finding(
                    "constant.negative", "tbconstant." + field, field + " must not be negative"));
            }
        }
    }
}
