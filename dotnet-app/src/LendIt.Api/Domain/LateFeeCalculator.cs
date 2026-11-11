namespace LendIt.Api.Domain;

public static class LateFeeCalculator
{
    public const int FreeDays = 14;
    public const decimal FeePerDay = 0.50m;
    public const decimal MaxFee = 10.00m;

    public static decimal Calculate(DateOnly lentOn, DateOnly returnedOn)
    {
        if (returnedOn < lentOn)
            throw new ArgumentException("Return date cannot be before the lending date.");

        var daysLate = returnedOn.DayNumber - lentOn.DayNumber - FreeDays;
        return daysLate <= 0 ? 0m : Math.Min(daysLate * FeePerDay, MaxFee);
    }
}
