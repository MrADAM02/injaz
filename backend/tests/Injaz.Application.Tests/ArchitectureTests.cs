using Injaz.Application.Messaging;
using Injaz.Application.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Injaz.Application.Tests;

/// <summary>Guards that hold for every use case in the Application assembly, current and future.</summary>
public sealed class ArchitectureTests
{
    private static readonly Type[] AccessPolicies = [typeof(IPublicRequest), typeof(IAuthenticatedRequest)];

    private static IEnumerable<Type> RequestTypes() =>
        typeof(DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)));

    [Fact]
    public void Every_request_declares_exactly_one_access_policy()
    {
        var offenders = RequestTypes()
            .Where(t => AccessPolicies.Count(p => p.IsAssignableFrom(t)) != 1)
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_request_has_a_registered_handler()
    {
        var services = new ServiceCollection().AddApplication();

        var offenders = RequestTypes()
            .Where(t => !services.Any(d => IsHandlerFor(d.ServiceType, t)))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(offenders);
    }

    private static bool IsHandlerFor(Type serviceType, Type requestType) =>
        serviceType.IsGenericType
        && (serviceType.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
            || serviceType.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))
        && serviceType.GenericTypeArguments[0] == requestType;
}
