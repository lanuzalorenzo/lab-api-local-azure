using EcommerceApi.Domain.Common;
using EcommerceApi.Domain.Products;
using Xunit;

namespace EcommerceApi.UnitTests.Domain;

public sealed class ProductTests
{
    [Fact]
    public void Create_TrimsName()
    {
        var product = Product.Create("  Keyboard  ", 49.99m, 10);

        Assert.Equal("Keyboard", product.Name);
    }

    [Fact]
    public void Create_WithBlankName_Throws()
    {
        Assert.Throws<DomainException>(() => Product.Create("   ", 10m, 1));
    }

    [Fact]
    public void Create_WithNameTooLong_Throws()
    {
        var name = new string('a', Product.NameMaxLength + 1);

        Assert.Throws<DomainException>(() => Product.Create(name, 10m, 1));
    }

    [Fact]
    public void Create_WithNegativePrice_Throws()
    {
        Assert.Throws<DomainException>(() => Product.Create("Keyboard", -0.01m, 1));
    }

    [Fact]
    public void Create_WithPriceAboveMax_Throws()
    {
        Assert.Throws<DomainException>(() => Product.Create("Keyboard", Product.MaxPrice + 1, 1));
    }

    [Fact]
    public void Create_WithNegativeStock_Throws()
    {
        Assert.Throws<DomainException>(() => Product.Create("Keyboard", 10m, -1));
    }

    [Fact]
    public void ChangeStock_WithNegativeValue_Throws()
    {
        var product = Product.Create("Keyboard", 10m, 1);

        Assert.Throws<DomainException>(() => product.ChangeStock(-1));
    }
}
