using LendIt.Api.Domain;

namespace LendIt.Tests.Unit;

[Trait("Category", "Unit")]
public class LateFeeCalculatorTests
{
    private static readonly DateOnly LentOn = new(2026, 1, 1);

    [Theory]
    [InlineData(0, 0.00)]
    [InlineData(1, 0.00)]
    [InlineData(13, 0.00)]
    [InlineData(14, 0.00)]     // last free day
    [InlineData(15, 0.50)]     // first day with a fee
    [InlineData(16, 1.00)]
    [InlineData(20, 3.00)]
    [InlineData(33, 9.50)]     // last day below the cap
    [InlineData(34, 10.00)]    // cap reached exactly
    [InlineData(35, 10.00)]    // first day above the cap
    [InlineData(100, 10.00)]   // cap holds
    public void Fee_depends_on_the_number_of_days_kept(int daysKept, double expectedFee)
    {
        var fee = LateFeeCalculator.Calculate(LentOn, LentOn.AddDays(daysKept));

        Assert.Equal((decimal)expectedFee, fee);
    }

    [Fact]
    public void Returning_before_lending_is_rejected()
    {
        Assert.Throws<ArgumentException>(
            () => LateFeeCalculator.Calculate(LentOn, LentOn.AddDays(-1)));
    }

    // own boundary: a loan that crosses a leap day (29 Feb 2028) counts real calendar days
    [Fact]
    public void Fee_counts_the_leap_day_as_a_day_kept()
    {
        var lentOn = new DateOnly(2028, 2, 20);
        var returnedOn = new DateOnly(2028, 3, 6);   // 15 days later because of 29 Feb

        var fee = LateFeeCalculator.Calculate(lentOn, returnedOn);

        Assert.Equal(0.50m, fee);
    }
}
