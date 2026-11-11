using LendIt.Api.Domain;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace LendIt.Tests.Unit;

[Trait("Category", "Unit")]
public class LoanServiceTests
{
    private static readonly DateOnly Today = new(2026, 1, 15);

    private readonly IItemRepository _items = Substitute.For<IItemRepository>();
    private readonly ILoanRepository _loans = Substitute.For<ILoanRepository>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
    private readonly LoanService _sut;

    public LoanServiceTests() => _sut = new LoanService(_items, _loans, _clock);

    // ------------------------------------------------------------------ Lend

    [Fact]
    public void Lend_marks_the_item_as_unavailable_and_creates_a_loan()
    {
        // Arrange
        _items.Get(1).Returns(new Item(1, "Drill", true));
        _loans.CountActive("ana").Returns(0);
        _loans.Add(1, "ana", Today).Returns(new Loan(7, 1, "ana", Today, null));

        // Act
        var loan = _sut.Lend(1, "ana");

        // Assert
        Assert.Equal(7, loan.Id);
        _items.Received(1).Save(Arg.Is<Item>(i => i.Id == 1 && !i.Available));
    }

    [Fact]
    public void Lend_creates_the_loan_with_todays_date_from_the_clock()
    {
        _items.Get(1).Returns(new Item(1, "Drill", true));

        _sut.Lend(1, "ana");

        _loans.Received(1).Add(1, "ana", Today);
    }

    [Fact]
    public void Lend_throws_when_the_item_does_not_exist()
    {
        _items.Get(99).Returns((Item?)null);

        Assert.Throws<NotFoundException>(() => _sut.Lend(99, "ana"));
    }

    [Fact]
    public void Lend_throws_when_the_item_is_already_lent_out()
    {
        _items.Get(1).Returns(new Item(1, "Drill", false));

        Assert.Throws<RuleViolationException>(() => _sut.Lend(1, "ana"));
        _loans.DidNotReceive().Add(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<DateOnly>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Lend_rejects_a_blank_or_null_borrower(string? borrower)
    {
        Assert.Throws<ArgumentException>(() => _sut.Lend(1, borrower!));
    }

    [Fact]
    public void Lend_is_rejected_when_the_borrower_has_exactly_3_active_loans()
    {
        _items.Get(1).Returns(new Item(1, "Drill", true));
        _loans.CountActive("ana").Returns(LoanService.MaxActiveLoans);

        var ex = Assert.Throws<RuleViolationException>(() => _sut.Lend(1, "ana"));

        Assert.Contains("3 active loans", ex.Message);
    }

    [Fact]
    public void Lend_does_not_change_the_item_when_the_loan_limit_is_reached()
    {
        _items.Get(1).Returns(new Item(1, "Drill", true));
        _loans.CountActive("ana").Returns(3);

        Assert.Throws<RuleViolationException>(() => _sut.Lend(1, "ana"));

        _items.DidNotReceive().Save(Arg.Any<Item>());
    }

    [Fact]
    public void Lend_is_allowed_when_the_borrower_has_2_active_loans()
    {
        _items.Get(1).Returns(new Item(1, "Drill", true));
        _loans.CountActive("ana").Returns(2);
        _loans.Add(1, "ana", Today).Returns(new Loan(9, 1, "ana", Today, null));

        var loan = _sut.Lend(1, "ana");

        Assert.Equal(9, loan.Id);
    }

    // ------------------------------------------------------------------ Return

    private Loan ActiveLoan(int id = 5, int itemId = 1) => new(id, itemId, "ana", Today, null);

    [Fact]
    public void Return_makes_the_item_available_again()
    {
        _loans.Get(5).Returns(ActiveLoan());
        _items.Get(1).Returns(new Item(1, "Drill", false));

        _sut.Return(5);

        _items.Received(1).Save(new Item(1, "Drill", true));
    }

    [Fact]
    public void Return_stores_the_return_date_on_the_loan()
    {
        _loans.Get(5).Returns(ActiveLoan());
        _items.Get(1).Returns(new Item(1, "Drill", false));
        _clock.Advance(TimeSpan.FromDays(3));

        _sut.Return(5);

        _loans.Received(1).Update(ActiveLoan() with { ReturnedOn = Today.AddDays(3) });
    }

    [Fact]
    public void Return_charges_no_late_fee_within_14_days()
    {
        _loans.Get(5).Returns(ActiveLoan());
        _items.Get(1).Returns(new Item(1, "Drill", false));
        _clock.Advance(TimeSpan.FromDays(14));

        var result = _sut.Return(5);

        Assert.Equal(0m, result.LateFee);
    }

    [Fact]
    public void Return_charges_a_late_fee_after_20_days()
    {
        _loans.Get(5).Returns(ActiveLoan());
        _items.Get(1).Returns(new Item(1, "Drill", false));
        _clock.Advance(TimeSpan.FromDays(20));

        var result = _sut.Return(5);

        Assert.Equal(3.00m, result.LateFee);
    }

    [Fact]
    public void Return_throws_when_the_loan_does_not_exist()
    {
        _loans.Get(404).Returns((Loan?)null);

        Assert.Throws<NotFoundException>(() => _sut.Return(404));
    }

    [Fact]
    public void Return_throws_when_the_loan_is_already_returned()
    {
        _loans.Get(5).Returns(ActiveLoan() with { ReturnedOn = Today });

        Assert.Throws<RuleViolationException>(() => _sut.Return(5));
        _items.DidNotReceive().Save(Arg.Any<Item>());
    }

    [Fact]
    public void Return_throws_when_the_item_of_the_loan_no_longer_exists()
    {
        _loans.Get(5).Returns(ActiveLoan(itemId: 77));
        _items.Get(77).Returns((Item?)null);

        Assert.Throws<NotFoundException>(() => _sut.Return(5));
    }
}
