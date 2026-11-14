using System.Reflection;
using Injaz.Application.Features.Activity;
using Injaz.Application.Features.Auth;
using Injaz.Application.Features.Invitations;
using Injaz.Application.Messaging;
using Injaz.Application.Persistence;
using Injaz.Application.Realtime;
using Injaz.Application.Security;
using Injaz.Application.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Injaz.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddRequestHandlersFrom(typeof(DependencyInjection).Assembly);

        services.AddScoped<IRequestAuthorizer, RequestAuthorizer>();
        services.AddScoped<IAccessScopeResolver, AccessScopeResolver>();
        services.AddScoped<CurrentAccess>();
        services.AddScoped<ICurrentAccess>(sp => sp.GetRequiredService<CurrentAccess>());

        services.AddScoped<AfterCommitQueue>();
        services.AddScoped<IAfterCommit>(sp => sp.GetRequiredService<AfterCommitQueue>());

        services.AddOptions<AuthOptions>();
        services.AddOptions<InvitationOptions>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<IActivityLog, ActivityLog>();

        services.AddScoped<IBoardEvents, BoardEvents>();
        services.TryAddSingleton<IRealtimePublisher, NullRealtimePublisher>();
        return services;
    }

    /// <summary>
    /// Registers every concrete command handler, query handler and validator found in <paramref name="assembly"/>.
    /// A request type with two handlers is a programming error and fails at startup.
    /// </summary>
    public static IServiceCollection AddRequestHandlersFrom(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var candidates = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

        foreach (var type in candidates)
        {
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var definition = contract.GetGenericTypeDefinition();

                if (definition == typeof(ICommandHandler<,>) || definition == typeof(IQueryHandler<,>))
                {
                    if (services.Any(d => d.ServiceType == contract))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate handler for '{contract.GenericTypeArguments[0].FullName}': '{type.FullName}'.");
                    }

                    services.AddScoped(contract, type);
                }
                else if (definition == typeof(IValidator<>))
                {
                    services.AddScoped(contract, type);
                }
            }
        }

        return services;
    }
}
