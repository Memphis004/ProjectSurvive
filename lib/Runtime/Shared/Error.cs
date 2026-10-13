using System;

namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// A typed, machine-readable failure reason returned by every state-changing
    /// command (AGENTS.md rule 7).
    /// </summary>
    /// <remarks>
    /// <see cref="Code"/> is a stable identifier such as <c>"bag.overlap"</c> or
    /// <c>"shop.insufficient_bia"</c>, never a sentence. Player-facing text comes
    /// from localization (AGENTS.md rule 5), so the UI maps a code to a
    /// localization key (<c>error.&lt;code&gt;</c>) instead of printing this value.
    /// Codes are API: rename them only with a matching localization change.
    /// <para>
    /// <c>default(Error)</c> means "no error" and is produced only by
    /// <see cref="Result.Ok()"/>; a real failure always carries a non-empty code.
    /// </para>
    /// </remarks>
    public readonly struct Error : IEquatable<Error>
    {
        /// <summary>Creates a failure reason.</summary>
        /// <exception cref="ArgumentException"><paramref name="code"/> is null or empty.</exception>
        public Error(string code, ErrorKind kind = ErrorKind.Validation)
        {
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("An error code is required.", nameof(code));
            }

            Code = code;
            Kind = kind;
        }

        /// <summary>Stable machine-readable identifier. Never a player-facing sentence.</summary>
        public string Code { get; }

        /// <summary>Broad classification of the failure.</summary>
        public ErrorKind Kind { get; }

        /// <inheritdoc />
        public bool Equals(Error other) =>
            Kind == other.Kind && string.Equals(Code, other.Code, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Error other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (Code is null ? 0 : StringComparer.Ordinal.GetHashCode(Code) * 397) ^ (int)Kind;

        /// <summary>Developer-facing representation. Do not show it to a player.</summary>
        public override string ToString() => Code is null ? "<no error>" : Code;

        public static bool operator ==(Error left, Error right) => left.Equals(right);

        public static bool operator !=(Error left, Error right) => !left.Equals(right);
    }
}
