using System.Reflection;
using EcommerceApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace EcommerceApi.Tests;

/// <summary>
/// The 16 KB body limit is an MVC filter, so reaching HTTP 413 depends on the
/// server (Kestrel enforces it; TestServer does not). The test pins the declared
/// limit so a refactor cannot drop it silently.
/// </summary>
public sealed class RequestLimitTests
{
    private const long ExpectedBytes = 16 * 1024;

    [Fact]
    public void WriteEndpoints_DeclareThe16KbLimit()
    {
        Assert.Equal(ExpectedBytes, LimitOf<ProductsController>(nameof(ProductsController.CreateProduct)));
        Assert.Equal(ExpectedBytes, LimitOf<OrdersController>(nameof(OrdersController.CreateOrder)));
    }

    private static long LimitOf<TController>(string methodName)
    {
        var method = typeof(TController).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{typeof(TController).Name}.{methodName} not found.");

        var attribute = method.GetCustomAttribute<RequestSizeLimitAttribute>()
            ?? throw new InvalidOperationException($"{methodName} has no RequestSizeLimit attribute.");

        // The limit is stored in a private field; the attribute exposes no public
        // getter for the byte count.
        return typeof(RequestSizeLimitAttribute)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => field.FieldType == typeof(long))
            .Select(field => (long)field.GetValue(attribute)!)
            .Single();
    }
}
