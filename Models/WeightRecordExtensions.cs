namespace TolllgaFinale.Models;

public static class WeightRecordExtensions
{
    public static DateTime GetLatestWeighingDate(this WeightRecord record)
    {
        var tare = record.WeighingDateTare;
        var gross = record.WeighingDateGross;

        if (tare == default) return gross;
        if (gross == default) return tare;
        return tare >= gross ? tare : gross;
    }

    public static bool MatchesWeighingDateRange(this WeightRecord record, DateTime from, DateTime to)
    {
        return MatchesDate(record.WeighingDateTare, from, to)
            || MatchesDate(record.WeighingDateGross, from, to);
    }

    public static string GetOperatorLabel(this string? operatorName)
        => string.IsNullOrWhiteSpace(operatorName) ? "—" : operatorName;

    private static bool MatchesDate(DateTime value, DateTime from, DateTime to)
    {
        if (value == default) return false;
        var local = value.ToLocalTime().Date;
        return local >= from.Date && local <= to.Date;
    }
}
