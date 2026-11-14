using Injaz.Application.Common.Exceptions;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Injaz.Application.Tests.Messaging;

public sealed class DispatcherTests : IDisposable
{
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CallLog _log = new();
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public DispatcherTests()
    {
        var services = new ServiceCollection()
            .AddApplication()
            .AddRequestHandlersFrom(typeof(DispatcherTests).Assembly)
            .AddSingleton<ICurrentUser>(_currentUser)
            .AddSingleton<IUnitOfWork>(_unitOfWork)
            .AddSingleton(_log);

        // Not ValidateOnBuild: the real Application handlers need persistence services these pipeline tests don't use.
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _scope = _provider.CreateScope();
    }

    private IDispatcher Dispatcher => _scope.ServiceProvider.GetRequiredService<IDispatcher>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Valid_command_runs_handler_and_commits_once()
    {
        var result = await Dispatcher.SendAsync(new CreateThing("Injaz", "team@injaz.app"), Ct);

        Assert.Equal("created:Injaz", result);
        Assert.Equal([nameof(CreateThingHandler)], _log.Calls);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Invalid_command_reports_all_fields_and_never_reaches_handler()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => Dispatcher.SendAsync(new CreateThing("much-too-long-name", null), Ct));

        Assert.Equal("validation.failed", ex.Code);

        var name = Assert.Single(ex.Errors["name"]);
        Assert.Equal("maxLength", name.Code);
        Assert.Equal(10, name.Parameters!["max"]);

        // First failing rule wins per field: "required", not "required" + "email".
        var email = Assert.Single(ex.Errors["email"]);
        Assert.Equal("required", email.Code);

        Assert.Empty(_log.Calls);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Failing_handler_does_not_commit()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Dispatcher.SendAsync(new FailingCommand(), Ct));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Query_never_commits()
    {
        _currentUser.Id = Guid.CreateVersion7();

        var id = await Dispatcher.QueryAsync(new WhoAmI(), Ct);

        Assert.Equal(_currentUser.Id, id);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Authenticated_request_rejects_anonymous_caller()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => Dispatcher.QueryAsync(new WhoAmI(), Ct));

        Assert.Equal("auth.unauthorized", ex.Code);
    }

    [Fact]
    public async Task Request_without_access_policy_fails_closed()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Dispatcher.QueryAsync(new NoPolicyQuery(), Ct));

        Assert.Contains("does not declare an access policy", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_log.Calls);
    }

    [Fact]
    public void Registering_two_handlers_for_one_request_fails_at_startup()
    {
        var services = new ServiceCollection().AddRequestHandlersFrom(typeof(DispatcherTests).Assembly);

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddRequestHandlersFrom(typeof(DispatcherTests).Assembly));
        Assert.Contains("Duplicate handler", ex.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
