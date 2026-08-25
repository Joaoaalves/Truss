using System.ComponentModel;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Truss.Domain;

namespace Truss.Application.Pipeline
{
    /// <summary>
    /// Receives the registrations produced at compile time by the Truss.Generators package.
    /// Not intended to be called from application code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class TrussGeneratedRegistry
    {
        private static readonly ConcurrentDictionary<Assembly, Action<IServiceCollection>> Registrations = new();

        /// <summary>
        /// Stores the generated service registration for an assembly.
        /// When present, it replaces runtime assembly scanning for that assembly.
        /// </summary>
        /// <param name="assembly">The assembly the registration was generated for.</param>
        /// <param name="registration">The generated registration action.</param>
        public static void RegisterAssembly(Assembly assembly, Action<IServiceCollection> registration)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            ArgumentNullException.ThrowIfNull(registration);

            Registrations[assembly] = registration;
        }

        /// <summary>
        /// Determines whether a generated registration exists for the given assembly.
        /// </summary>
        /// <param name="assembly">The assembly to check.</param>
        /// <returns><c>true</c> if a generated registration exists; otherwise, <c>false</c>.</returns>
        public static bool HasRegistrationFor(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            return Registrations.ContainsKey(assembly);
        }

        /// <summary>
        /// Caches the typed dispatch invoker for a request type,
        /// removing the reflection that would otherwise run on its first dispatch.
        /// </summary>
        /// <typeparam name="TRequest">The type of the request.</typeparam>
        /// <typeparam name="TResponse">The type of the response.</typeparam>
        public static void PrimeRequest<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            WrapperCache.Requests.TryAdd(typeof(TRequest), new RequestHandlerWrapperImpl<TRequest, TResponse>());
        }

        /// <summary>
        /// Caches the typed dispatch invoker for a domain event type,
        /// removing the reflection that would otherwise run on its first dispatch.
        /// </summary>
        /// <typeparam name="TEvent">The type of the domain event.</typeparam>
        public static void PrimeDomainEvent<TEvent>()
            where TEvent : IDomainEvent
        {
            WrapperCache.DomainEvents.TryAdd(typeof(TEvent), new DomainEventHandlerWrapperImpl<TEvent>());
        }

        internal static bool TryGetRegistration(Assembly assembly, [NotNullWhen(true)] out Action<IServiceCollection>? registration)
        {
            return Registrations.TryGetValue(assembly, out registration);
        }
    }
}
