using System.Text.RegularExpressions;

namespace Kargo.Domain;

public sealed partial record TrackingNumber
{
    public string Value { get; }

    public TrackingNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Takip numarası boş olamaz.");

        var normalized = value.Trim().ToUpperInvariant();

        if (!FormatRegex().IsMatch(normalized))
            throw new DomainException(
                $"'{value}' geçerli bir takip numarası değil. Beklenen format: iki harf ve rakamlar, örneğin SK1001.");

        Value = normalized;
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z]{2}[0-9]{3,18}$")]
    private static partial Regex FormatRegex();
    public static bool TryCreate(string? value, out TrackingNumber? result, out string? error)
    {
        try
        {
            result = new TrackingNumber(value ?? string.Empty);
            error = null;
            return true;
        }
        catch (DomainException ex)
        {
            result = null;
            error = ex.Message;
            return false;
        }
    }
}