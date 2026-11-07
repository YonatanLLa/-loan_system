using LoanSystem.Common.Adapters.Outbox;
using LoanSystem.Common.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace LoanSystem.Adapters.UnitTests;

public class IntegrationEventRegistrationTests
{
    public sealed record FirstEvent(string Value) : IntegrationEvent;
    public sealed record SecondEvent(int Value) : IntegrationEvent;

    private sealed class FirstHandler : IIntegrationEventHandler<FirstEvent>
    {
        public Task HandleAsync(FirstEvent integrationEvent, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class SecondHandler : IIntegrationEventHandler<SecondEvent>
    {
        public Task HandleAsync(SecondEvent integrationEvent, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public void AddIntegrationEventHandler_RegistersHandlers_AndOneSharedRegistry()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEventHandler<FirstEvent, FirstHandler>();
        services.AddIntegrationEventHandler<SecondEvent, SecondHandler>();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IntegrationEventTypeRegistry>();

        services.Count(d => d.ServiceType == typeof(IntegrationEventTypeRegistry)).ShouldBe(1);
        registry.TryResolve(typeof(FirstEvent).FullName!, out var first).ShouldBeTrue();
        first.ShouldBe(typeof(FirstEvent));
        registry.TryResolve(typeof(SecondEvent).FullName!, out var second).ShouldBeTrue();
        second.ShouldBe(typeof(SecondEvent));

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetServices<IIntegrationEventHandler<FirstEvent>>().ShouldHaveSingleItem().ShouldBeOfType<FirstHandler>();
    }

    [Fact]
    public void Registry_DoesNotResolve_UnknownTypes()
    {
        new IntegrationEventTypeRegistry().TryResolve("Some.Unknown.Event", out _).ShouldBeFalse();
    }

    [Fact]
    public void Registry_Rejects_TypesThatAreNotIntegrationEvents()
    {
        Should.Throw<ArgumentException>(() => new IntegrationEventTypeRegistry().Register(typeof(string)));
    }
}
