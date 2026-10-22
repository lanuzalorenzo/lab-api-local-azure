namespace EcommerceApi.Infrastructure.Persistence;

/// <summary>
/// Stores monetary amounts as integer cents. SQLite has no decimal type and REAL
/// would lose precision on totals, so the adapter converts explicitly.
/// </summary>
internal static class MoneyCents
{
    public static long ToCents(decimal amount) =>
        (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    public static decimal FromCents(long cents) => cents / 100m;
}
