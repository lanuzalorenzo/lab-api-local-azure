using System.Reflection;
using EcommerceApi.Application.Abstractions;
using EcommerceApi.Application.Orders;
using EcommerceApi.Domain.Orders;
using EcommerceApi.Domain.Products;
using Xunit;

namespace EcommerceApi.Architecture.Tests;

/// <summary>
/// Enforces the dependency rule (ADR-0002). These tests fail the build if a layer
/// reaches into a layer it must not know about, which is the only way a diagram
/// stays true over time.
/// </summary>
public sealed class LayerDependencyTests
{
    private static IReadOnlyList<string> ReferencedAssemblies(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToList();

    private static bool IsProjectAssembly(string name) =>
        name == "EcommerceApi" || name.StartsWith("EcommerceApi.", StringComparison.Ordinal);

    [Fact]
    public void Domain_DoesNotReferenceAnyOtherProject()
    {
        var forbidden = ReferencedAssemblies(typeof(Order).Assembly)
            .Where(IsProjectAssembly)
            .Where(name => name != "EcommerceApi.Domain")
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Domain_DoesNotReferenceAspNetCore()
    {
        var forbidden = ReferencedAssemblies(typeof(Order).Assembly)
            .Where(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructureOrHost()
    {
        var forbidden = ReferencedAssemblies(typeof(ICreateOrderHandler).Assembly)
            .Where(name =>
                name is "EcommerceApi" or "EcommerceApi.Infrastructure" ||
                name.StartsWith("EcommerceApi.Infrastructure.", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceTheHost()
    {
        var forbidden = ReferencedAssemblies(typeof(Infrastructure.Persistence.SqliteOrderRepository).Assembly)
            .Where(name => name == "EcommerceApi")
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Controllers_DoNotDependOnInfrastructure()
    {
        var controllerAssembly = typeof(Controllers.OrdersController).Assembly;

        var offenders = controllerAssembly.GetTypes()
            .Where(type => type.Namespace == "EcommerceApi.Controllers" && type.IsClass && !type.IsAbstract)
            .SelectMany(type => type.GetConstructors())
            .SelectMany(constructor => constructor.GetParameters())
            .Where(parameter => parameter.ParameterType.Namespace?.StartsWith(
                "EcommerceApi.Infrastructure", StringComparison.Ordinal) == true)
            .Select(parameter => $"{parameter.Member.DeclaringType?.Name} -> {parameter.ParameterType.Name}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Aggregates_DoNotExposePublicSetters()
    {
        var aggregateTypes = new[] { typeof(Order), typeof(Product) };

        var offenders = aggregateTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.SetMethod?.IsPublic == true)
            .Select(property => $"{property.DeclaringType?.Name}.{property.Name}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Domain_DoesNotUseDataAnnotations()
    {
        var forbidden = ReferencedAssemblies(typeof(Order).Assembly)
            .Where(name => name is "System.ComponentModel.Annotations"
                or "System.ComponentModel.DataAnnotations")
            .ToList();

        Assert.Empty(forbidden);
    }
}
