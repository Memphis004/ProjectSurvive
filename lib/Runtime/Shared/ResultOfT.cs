using System;

namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// Outcome of a command that changes state and returns a value (AGENTS.md
    /// rule 7) — for example a purchase that reports the new wallet balance.
    /// </summary>
    /// <typeparam name="T">The value produced on success.</typeparam>
    /// <remarks>
    /// <see cref="Value"/> is only valid when <see cref="IsSuccess"/> is true;
    /// <see cref="TryGetValue"/> is the non-throwing way to read it.
    /// </remarks>
    public readonly struct Result<T> : IEquatable<Result<T>>
    {
        private readonly Error _error;
        private readonly bool _isSuccess;
        private readonly T _value;

        private Result(bool isSuccess, T value, Error error)
        {
            _isSuccess = isSuccess;
            _value = value;
            _error = error;
        }

        /// <summary>True when the command was applied.</summary>
        public bool IsSuccess => _isSuccess;

        /// <summary>True when the command was rejected.</summary>
        public bool IsFailure => !_isSuccess;

        /// <summary>The failure reason; <c>default</c> when <see cref="IsSuccess"/> is true.</summary>
        public Error Error => _error;

        /// <summary>
        /// The produced value.
        /// </summary>
        /// <exception cref="InvalidOperationException">The result is a failure.</exception>
        public T Value =>
            _isSuccess
                ? _value
                : throw new InvalidOperationException(
                    "Result is a failure ('" + (_error.Code ?? "<no error>") + "'); Value is not available.");

        /// <summary>Reads the value without throwing. Returns false on failure.</summary>
        public bool TryGetValue(out T value)
        {
            value = _value;
            return _isSuccess;
        }

        /// <summary>A successful outcome carrying <paramref name="value"/>.</summary>
        public static Result<T> Ok(T value) => new Result<T>(true, value, default);

        /// <summary>A rejected outcome carrying <paramref name="error"/>.</summary>
        public static Result<T> Fail(Error error)
        {
            if (string.IsNullOrEmpty(error.Code))
            {
                throw new ArgumentException("A failure must carry an error code.", nameof(error));
            }

            return new Result<T>(false, default!, error);
        }

        /// <summary>A rejected outcome carrying a code and kind.</summary>
        public static Result<T> Fail(string code, ErrorKind kind = ErrorKind.Validation) =>
            Fail(new Error(code, kind));

        /// <inheritdoc />
        public bool Equals(Result<T> other) =>
            _isSuccess == other._isSuccess
            && _error.Equals(other._error)
            && (ReferenceEquals(_value, other._value)
                || (_value is not null && _value.Equals(other._value)));

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int valueHash = _value is null ? 0 : _value.GetHashCode();
            return ((_isSuccess ? 397 : 1) ^ _error.GetHashCode() ^ valueHash);
        }

        /// <summary>Developer-facing representation. Do not show it to a player.</summary>
        public override string ToString() =>
            _isSuccess ? "Ok(" + _value + ")" : "Fail(" + _error + ")";

        public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

        public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);
    }
}
