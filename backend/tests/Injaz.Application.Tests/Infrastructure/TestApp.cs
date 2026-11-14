using Injaz.Application.Features.Auth;
using Injaz.Application.Messaging;
using Injaz.Application.Notifications;
using Injaz.Application.Security;
using Injaz.Infrastructure;
using Injaz.Infrastructure.Persistence;
using Injaz.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Injaz.Application.Tests.Infrastructure;

/// <summary>
/// The real Application + Infrastructure wiring with a controllable clock and caller. Every dispatch runs in its
/// own DI scope (fresh DbContext), just like separate HTTP requests.
/// </summary>
public sealed class TestApp : IAsyncDisposable
{
    public TestApp(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Jwt:SigningKey"] = "application-tests-signing-key-0123456789abcdef",
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddInfrastructure(configuration);

        services.AddSingleton<TimeProvider>(new DatabasePrecisionTimeProvider(Clock));
        services.AddSingleton<ICurrentUser>(CurrentUser);
        services.AddSingleton<IEmailSender>(Emails);

        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    internal FakeCurrentUser CurrentUser { get; } = new();

    public FakeEmailSender Emails { get; } = new();

    public ServiceProvider Services { get; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async Task<TResult> SendAsync<TResult>(ICommand<TResult> command)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(command, Ct);
    }

    public async Task<TResult> QueryAsync<TResult>(IQuery<TResult> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().QueryAsync(query, Ct);
    }

    /// <summary>Direct database access for assertions (separate scope, untracked by the code under test).</summary>
    public async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Registers a fresh user and makes them the current caller.</summary>
    public async Task<AuthSession> RegisterAsync(string password = TestData.Password, string? email = null)
    {
        var session = await SendAsync(new Register(email ?? TestData.UniqueEmail(), password, "Test User", null));
        CurrentUser.Id = session.User.Id;
        return session;
    }

    /// <summary>Makes <paramref name="session"/>'s user the current caller.</summary>
    public void ActAs(AuthSession session) => CurrentUser.Id = session.User.Id;

    public ValueTask DisposeAsync() => Services.DisposeAsync();
}

public static class TestData
{
    public const string Password = "correct-horse-battery";

    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";
}

public sealed class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> Sent
    {
        get
        {
            lock (_sent)
            {
                return [.. _sent];
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        lock (_sent)
        {
            _sent.Add(message);
        }

        return Task.CompletedTask;
    }
}
