using System;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// AGENTS.md rule 7: every state-changing command returns a typed result, so
    /// a failure can be inspected by code (and mapped to localization) instead of
    /// being an exception or a string.
    /// </summary>
    public sealed class ResultTests
    {
        [Fact]
        public void Ok_carries_no_error()
        {
            Result result = Result.Ok();

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Equal(default(Error), result.Error);
        }

        [Fact]
        public void Fail_carries_the_code_and_kind()
        {
            Result result = Result.Fail("bag.overlap", ErrorKind.Conflict);

            Assert.False(result.IsSuccess);
            Assert.True(result.IsFailure);
            Assert.Equal("bag.overlap", result.Error.Code);
            Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
            Assert.Equal("bag.overlap", result.Error.ToString());
        }

        [Fact]
        public void Fail_defaults_to_the_validation_kind()
        {
            Result result = Result.Fail("item.unknown_shape");

            Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        }

        [Fact]
        public void Generic_ok_exposes_its_value()
        {
            Result<int> result = Result.Ok(42);

            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.Value);
            Assert.True(result.TryGetValue(out int value));
            Assert.Equal(42, value);
        }

        [Fact]
        public void Generic_failure_refuses_to_hand_out_a_value()
        {
            Result<int> result = Result<int>.Fail("shop.insufficient_bia", ErrorKind.Insufficient);

            Assert.True(result.IsFailure);
            Assert.Equal("shop.insufficient_bia", result.Error.Code);
            Assert.Equal(ErrorKind.Insufficient, result.Error.Kind);
            Assert.False(result.TryGetValue(out int value));
            Assert.Equal(0, value);
            Assert.Throws<InvalidOperationException>(() => result.Value);
        }

        [Fact]
        public void Non_generic_helpers_build_generic_results()
        {
            Result<string> ok = Result.Ok("KaliFlameYant");
            Result<string> failed = Result.Fail<string>("floor.invalid", ErrorKind.InvalidState);

            Assert.Equal("KaliFlameYant", ok.Value);
            Assert.Equal("floor.invalid", failed.Error.Code);
            Assert.Equal(ErrorKind.InvalidState, failed.Error.Kind);
        }

        [Fact]
        public void An_empty_error_code_is_rejected()
        {
            Assert.Throws<ArgumentException>(() => new Error(string.Empty));
            Assert.Throws<ArgumentException>(() => new Error(null!));
            Assert.Throws<ArgumentException>(() => Result.Fail(string.Empty));
            Assert.Throws<ArgumentException>(() => Result<int>.Fail(new Error(string.Empty)));
        }

        [Fact]
        public void Results_compare_by_value()
        {
            Assert.Equal(Result.Ok(), Result.Ok());
            Assert.Equal(Result.Fail("x"), Result.Fail("x", ErrorKind.Validation));
            Assert.NotEqual(Result.Fail("x"), Result.Fail("y"));
            Assert.NotEqual(Result.Fail("x", ErrorKind.Validation), Result.Fail("x", ErrorKind.Conflict));
            Assert.NotEqual(Result.Ok(), Result.Fail("x"));

            Assert.Equal(Result.Ok(3), Result.Ok(3));
            Assert.NotEqual(Result.Ok(3), Result.Ok(4));
            Assert.NotEqual(Result.Ok(3), Result<int>.Fail("x"));
        }

        [Fact]
        public void Operators_and_hash_codes_are_consistent()
        {
            Result a = Result.Ok();
            Result b = Result.Ok();
            Result c = Result.Fail("bag.overlap", ErrorKind.Conflict);

            Assert.True(a == b);
            Assert.False(a == c);
            Assert.True(a != c);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());

            Result<int> d = Result.Ok(7);
            Result<int> e = Result.Ok(7);
            Assert.True(d == e);
            Assert.Equal(d.GetHashCode(), e.GetHashCode());
        }

        [Fact]
        public void Error_equality_is_code_and_kind()
        {
            var a = new Error("shop.refresh_limit", ErrorKind.InvalidState);
            var b = new Error("shop.refresh_limit", ErrorKind.InvalidState);
            var c = new Error("SHOP.REFRESH_LIMIT", ErrorKind.InvalidState);

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.NotEqual(a, c); // codes are case-sensitive identifiers
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Errors_never_carry_player_facing_text()
        {
            Result result = Result.Fail("bag.overlap", ErrorKind.Conflict);

            // The code is an identifier, not a sentence: the UI resolves it
            // through localization (error.<code>).
            Assert.DoesNotContain(" ", result.Error.Code);
            Assert.Equal(result.Error.Code, result.ToString().Substring("Fail(".Length, result.Error.Code.Length));
        }
    }
}
