using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Auth;
using Injaz.Application.Tests.Infrastructure;
using Injaz.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Injaz.Application.Tests.Features.Auth;

public sealed class AuthHandlerTests(DatabaseFixture database) : IAsyncDisposable
{
    private readonly TestApp _app = database.CreateApp();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------- register ----------

    [Fact]
    public async Task Register_normalizes_email_defaults_to_arabic_and_starts_a_session()
    {
        var email = $"Mixed.Case-{Guid.NewGuid():N}@Example.COM";

        var session = await _app.SendAsync(new Register(email, TestData.Password, "  Ahmad  ", null));

        Assert.Equal(email.ToLowerInvariant(), session.User.Email);
        Assert.Equal("Ahmad", session.User.DisplayName);
        Assert.Equal(Locale.Ar, session.User.Locale);
        Assert.False(string.IsNullOrEmpty(session.AccessToken));

        // Only the hash of the refresh token is stored.
        var stored = await _app.DbAsync(db => db.RefreshTokens.SingleAsync(t => t.UserId == session.User.Id, Ct));
        Assert.NotEqual(session.RefreshToken, stored.TokenHash);
        Assert.Equal(session.RefreshTokenExpiresAt, stored.ExpiresAt);
    }

    [Fact]
    public async Task Register_rejects_an_email_that_differs_only_by_case()
    {
        var email = TestData.UniqueEmail();
        await _app.RegisterAsync(email: email);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _app.RegisterAsync(email: email.ToUpperInvariant()));

        Assert.Equal("auth.emailTaken", ex.Code);
    }

    [Fact]
    public async Task Register_reports_every_invalid_field()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _app.SendAsync(new Register("not-an-email", "short", "", (Locale)42)));

        Assert.Equal("email", Assert.Single(ex.Errors["email"]).Code);
        Assert.Equal("minLength", Assert.Single(ex.Errors["password"]).Code);
        Assert.Equal("required", Assert.Single(ex.Errors["displayName"]).Code);
        Assert.Equal("invalidValue", Assert.Single(ex.Errors["locale"]).Code);
    }

    // ---------- login ----------

    [Fact]
    public async Task Login_succeeds_with_correct_credentials_in_any_email_case()
    {
        var registered = await _app.RegisterAsync();

        var session = await _app.SendAsync(new Login(registered.User.Email.ToUpperInvariant(), TestData.Password));

        Assert.Equal(registered.User.Id, session.User.Id);
    }

    [Fact]
    public async Task Login_gives_the_same_error_for_wrong_password_and_unknown_email()
    {
        var registered = await _app.RegisterAsync();

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _app.SendAsync(new Login(registered.User.Email, "wrong-password")));
        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _app.SendAsync(new Login(TestData.UniqueEmail(), TestData.Password)));

        Assert.Equal("auth.invalidCredentials", wrongPassword.Code);
        Assert.Equal(wrongPassword.Code, unknownEmail.Code);
        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
    }

    // ---------- refresh ----------

    [Fact]
    public async Task Refresh_rotates_the_token_within_the_same_family()
    {
        var first = await _app.RegisterAsync();

        var second = await _app.SendAsync(new RefreshSession(first.RefreshToken));

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        var tokens = await _app.DbAsync(db => db.RefreshTokens.Where(t => t.UserId == first.User.Id).ToListAsync(Ct));
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens.Select(t => t.FamilyId).Distinct());

        var old = Assert.Single(tokens, t => t.RevokedAt is not null);
        var current = Assert.Single(tokens, t => t.RevokedAt is null);
        Assert.Equal(current.Id, old.ReplacedByTokenId);

        // The successor keeps working.
        await _app.SendAsync(new RefreshSession(second.RefreshToken));
    }

    [Fact]
    public async Task Reusing_a_just_rotated_token_is_a_benign_race_and_keeps_the_session()
    {
        var first = await _app.RegisterAsync();
        var second = await _app.SendAsync(new RefreshSession(first.RefreshToken));
        _app.Clock.Advance(TimeSpan.FromSeconds(10));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _app.SendAsync(new RefreshSession(first.RefreshToken)));

        Assert.Equal("auth.refreshSuperseded", ex.Code);
        await _app.SendAsync(new RefreshSession(second.RefreshToken)); // still valid
    }

    [Fact]
    public async Task Reusing_a_rotated_token_after_the_grace_period_revokes_the_whole_family()
    {
        var first = await _app.RegisterAsync();
        var second = await _app.SendAsync(new RefreshSession(first.RefreshToken));
        _app.Clock.Advance(TimeSpan.FromMinutes(1));

        var reuse = await Assert.ThrowsAsync<UnauthorizedException>(() => _app.SendAsync(new RefreshSession(first.RefreshToken)));
        var afterTheft = await Assert.ThrowsAsync<UnauthorizedException>(() => _app.SendAsync(new RefreshSession(second.RefreshToken)));

        Assert.Equal("auth.invalidRefreshToken", reuse.Code);
        Assert.Equal("auth.invalidRefreshToken", afterTheft.Code);
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var session = await _app.RegisterAsync();
        _app.Clock.Advance(TimeSpan.FromDays(31));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _app.SendAsync(new RefreshSession(session.RefreshToken)));

        Assert.Equal("auth.invalidRefreshToken", ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task Missing_or_unknown_refresh_token_is_rejected(string? token)
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _app.SendAsync(new RefreshSession(token)));

        Assert.Equal("auth.invalidRefreshToken", ex.Code);
    }

    [Fact]
    public async Task Concurrent_refreshes_of_the_same_token_produce_exactly_one_new_session()
    {
        var session = await _app.RegisterAsync();

        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                await _app.SendAsync(new RefreshSession(session.RefreshToken));
                return (Exception?)null;
            }
            catch (AppException ex)
            {
                return ex;
            }
        }));

        Assert.Single(attempts, a => a is null);
        var loser = Assert.Single(attempts, a => a is not null)!;
        Assert.True(loser is ConflictException or UnauthorizedException { Code: "auth.refreshSuperseded" }, loser.GetType().Name);

        var active = await _app.DbAsync(db => db.RefreshTokens.CountAsync(t => t.UserId == session.User.Id && t.RevokedAt == null, Ct));
        Assert.Equal(1, active);
    }

    // ---------- logout ----------

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var session = await _app.RegisterAsync();
        var rotated = await _app.SendAsync(new RefreshSession(session.RefreshToken));

        await _app.SendAsync(new Logout(rotated.RefreshToken));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _app.SendAsync(new RefreshSession(rotated.RefreshToken)));
        Assert.Equal("auth.invalidRefreshToken", ex.Code);
    }

    [Fact]
    public async Task Logout_only_ends_its_own_session()
    {
        var phone = await _app.RegisterAsync();
        var laptop = await _app.SendAsync(new Login(phone.User.Email, TestData.Password));

        await _app.SendAsync(new Logout(phone.RefreshToken));

        await _app.SendAsync(new RefreshSession(laptop.RefreshToken)); // laptop stays signed in
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown-token")]
    public async Task Logout_is_idempotent(string? token) =>
        await _app.SendAsync(new Logout(token));

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
