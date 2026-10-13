using System;

namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// Outcome of a command that changes state and returns nothing (AGENTS.md
    /// rule 7). Either <see cref="IsSuccess"/> with no error, or a failure that
    /// carries an <see cref="Error"/> — and, by contract, no state change.
    /// </summary>
    /// <remarks>
    /// A readonly struct: returning it on a command path allocates nothing and
    /// cannot be confused with a thrown exception, which stays reserved for bugs.
    /// </remarks>
    public readonly struct Result : IEquatable<Result>
    {
        private readonly Error _error;
        private readonly bool _isSuccess;

        private Result(bool isSuccess, Error error)
        {
            if (!isSuccess && string.IsNullOrEmpty(error.Code))
            {
                throw new ArgumentException("A failure must carry an error code.", nameof(error));
            }

            _isSuccess = isSuccess;
            _error = error;
        }

        /// <summary>True when the command was applied.</summary>
        public bool IsSuccess => _isSuccess;

        /// <summary>True when the command was rejected.</summary>
        public bool IsFailure => !_isSuccess;

        /// <summary>The failure reason; <c>default</c> when <see cref="IsSuccess"/> is true.</summary>
        public Error Error => _error;

        /// <summary>A successful outcome.</summary>
        public static Result Ok() => new Result(true, default);

        /// <summary>A rejected outcome carrying <paramref name="error"/>.</summary>
        public static Result Fail(Error error) => new Result(false, error);

        /// <summary>A rejected outcome carrying a code and kind.</summary>
        public static Result Fail(string code, ErrorKind kind = ErrorKind.Validation) =>
            new Result(false, new Error(code, kind));

        /// <summary>A successful outcome carrying <paramref name="value"/>.</summary>
        public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);

        /// <summary>A rejected outcome for a value-carrying command.</summary>
        public static Result<T> Fail<T>(Error error) => Result<T>.Fail(error);

        /// <summary>A rejected outcome for a value-carrying command.</summary>
        public static Result<T> Fail<T>(string code, ErrorKind kind = ErrorKind.Validation) =>
            Result<T>.Fail(new Error(code, kind));

        /// <inheritdoc />
        public bool Equals(Result other) => _isSuccess == other._isSuccess && _error.Equals(other._error);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Result other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (_isSuccess ? 397 : 1) ^ _error.GetHashCode();

        /// <summary>Developer-facing representation. Do not show it to a player.</summary>
        public override string ToString() => _isSuccess ? "Ok" : "Fail(" + _error + ")";

        public static bool operator ==(Result left, Result right) => left.Equals(right);

        public static bool operator !=(Result left, Result right) => !left.Equals(right);
    }
}
