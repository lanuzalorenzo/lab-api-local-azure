using EcommerceApi.Domain.Common;
using EcommerceApi.Domain.Orders;
using Xunit;

namespace EcommerceApi.UnitTests.Domain;

public sealed class MoneyTests
{
    [Fact]
    public void From_WithNegativeAmount_Throws()
    {
        Assert.Throws<DomainException>(() => Money.From(-0.01m));
    }

    [Fact]
    public void From_RoundsToTwoDecimals()
    {
        var money = Money.From(10.005m);

        Assert.Equal(10.01m, money.Amount);
    }

    [Fact]
    public void Addition_KeepsDecimalPrecision()
    {
        var total = Money.From(19.99m) + Money.From(0.01m);

        Assert.Equal(20.00m, total.Amount);
    }

    [Fact]
    public void Multiply_ByQuantity_ReturnsLineTotal()
    {
        var lineTotal = Money.From(9.99m).Multiply(3);

        Assert.Equal(29.97m, lineTotal.Amount);
    }
}
