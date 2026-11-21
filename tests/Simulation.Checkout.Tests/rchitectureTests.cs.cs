using FluentAssertions;
using NetArchTest.Rules;
using Simulation.Checkout.Application;
using Simulation.Checkout.Application.Abstractions;
using Simulation.Checkout.Domain;
using Simulation.Checkout.Infrastructure;
using Xunit;

namespace Simulation.Checkout.Tests;

public class ArchitectureTests
{
    [Fact] // 5.1
    public void Domain_ShouldNotDependOn_ApplicationOrInfrastructure()
    {
        var result = Types.InAssembly(typeof(Order).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                typeof(CheckoutService).Assembly.GetName().Name,
                typeof(InMemoryOrderRepository).Assembly.GetName().Name)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact] // 5.2
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(typeof(CheckoutService).Assembly)
            .ShouldNot()
            .HaveDependencyOn(typeof(InMemoryOrderRepository).Assembly.GetName().Name)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact] // 5.3
    public void Interfaces_ShouldStartWithI()
    {
        var result = Types.InAssemblies(new[]
            {
                typeof(Order).Assembly,
                typeof(CheckoutService).Assembly,
                typeof(InMemoryOrderRepository).Assembly
            })
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact] // 5.4
    public void ClassesImplementingIOrderRepository_ShouldEndWithRepository()
    {
        var result = Types.InAssemblies(new[]
            {
                typeof(Order).Assembly,
                typeof(CheckoutService).Assembly,
                typeof(InMemoryOrderRepository).Assembly
            })
            .That()
            .ImplementInterface(typeof(IOrderRepository))
            .Should()
            .HaveNameEndingWith("Repository")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}