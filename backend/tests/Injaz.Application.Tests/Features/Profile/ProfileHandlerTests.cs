using Injaz.Application.Common.Exceptions;
using Injaz.Application.Features.Auth;
using Injaz.Application.Features.Profile;
using Injaz.Application.Tests.Infrastructure;
using Injaz.Domain.Users;

namespace Injaz.Application.Tests.Features.Profile;

public sealed class ProfileHandlerTests(DatabaseFixture database) : IAsyncDisposable
{
    private readonly TestApp _app = database.CreateApp();

    [Fact]
    public async Task GetMe_returns_the_callers_profile()
    {
        var session = await _app.RegisterAsync();

        var me = await _app.QueryAsync(new GetMe());

        Assert.Equal(session.User, me);
    }

    [Fact]
    public async Task GetMe_requires_a_signed_in_caller()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _app.QueryAsync(new GetMe()));

        Assert.Equal("auth.unauthorized", ex.Code);
    }

    [Fact]
    public async Task UpdateProfile_changes_only_the_fields_that_were_sent()
    {
        var session = await _app.RegisterAsync();
        await _app.SendAsync(new UpdateProfile(null, "https://cdn.example.com/a.png", null));

        var updated = await _app.SendAsync(new UpdateProfile(null, null, Locale.En));

        Assert.Equal(session.User.DisplayName, updated.DisplayName);
        Assert.Equal("https://cdn.example.com/a.png", updated.AvatarUrl);
        Assert.Equal(Locale.En, updated.Locale);
        Assert.Equal(updated, await _app.QueryAsync(new GetMe()));
    }

    [Fact]
    public async Task UpdateProfile_with_empty_avatar_removes_it()
    {
        await _app.RegisterAsync();
        await _app.SendAsync(new UpdateProfile(null, "https://cdn.example.com/a.png", null));

        var updated = await _app.SendAsync(new UpdateProfile(null, "", null));

        Assert.Null(updated.AvatarUrl);
    }

    [Fact]
    public async Task UpdateProfile_validates_what_was_sent()
    {
        await _app.RegisterAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _app.SendAsync(new UpdateProfile("   ", "javascript:alert(1)", null)));

        Assert.Equal("required", Assert.Single(ex.Errors["displayName"]).Code);
        Assert.Equal("url", Assert.Single(ex.Errors["avatarUrl"]).Code);
    }

    [Fact]
    public async Task ChangePassword_rejects_a_wrong_current_password_as_a_field_error()
    {
        await _app.RegisterAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _app.SendAsync(new ChangePassword("not-my-password", "a-brand-new-password")));

        Assert.Equal("invalidPassword", Assert.Single(ex.Errors["currentPassword"]).Code);
    }

    [Fact]
    public async Task ChangePassword_signs_out_other_sessions_and_keeps_this_one()
    {
        var original = await _app.RegisterAsync();
        var otherDevice = await _app.SendAsync(new Login(original.User.Email, TestData.Password));

        var fresh = await _app.SendAsync(new ChangePassword(TestData.Password, "a-brand-new-password"));

        await Assert.ThrowsAsync<UnauthorizedException>(() => _app.SendAsync(new RefreshSession(otherDevice.RefreshToken)));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _app.SendAsync(new RefreshSession(original.RefreshToken)));
        await _app.SendAsync(new RefreshSession(fresh.RefreshToken));

        await Assert.ThrowsAsync<UnauthorizedException>(() => _app.SendAsync(new Login(original.User.Email, TestData.Password)));
        await _app.SendAsync(new Login(original.User.Email, "a-brand-new-password"));
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
