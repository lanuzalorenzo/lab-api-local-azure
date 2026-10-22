namespace EcommerceApi.Domain.Common;

/// <summary>
/// Immutable monetary value object. Amounts are non-negative and rounded to two
/// decimals at construction so that equality and totals stay deterministic.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public const int Scale = 2;

    private Money(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    public static Money Zero => new(0m);

    public static Money From(decimal amount)
    {
        if (amount < 0m)
        {
            throw new DomainException("A monetary amount cannot be negative.");
        }

        return new Money(decimal.Round(amount, Scale, MidpointRounding.AwayFromZero));
    }

    public Money Multiply(int quantity)
    {
        if (quantity < 0)
        {
            throw new DomainException("A quantity cannot be negative.");
        }

        return new Money(Amount * quantity);
    }

    public static Money operator +(Money left, Money right) =>
        new(left.Amount + right.Amount);

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);
}
