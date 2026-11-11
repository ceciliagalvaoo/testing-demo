namespace LendIt.Api.Domain;

public class LoanService(IItemRepository items, ILoanRepository loans, TimeProvider clock)
{
    public const int MaxActiveLoans = 3;

    public Loan Lend(int itemId, string borrower)
    {
        if (string.IsNullOrWhiteSpace(borrower))
            throw new ArgumentException("Borrower is required.");

        var item = items.Get(itemId) ?? throw new NotFoundException($"Item {itemId} not found.");

        if (!item.Available)
            throw new RuleViolationException("Item is already lent out.");

        if (loans.CountActive(borrower) >= MaxActiveLoans)
            throw new RuleViolationException($"{borrower} already has {MaxActiveLoans} active loans.");

        items.Save(item with { Available = false });
        return loans.Add(itemId, borrower, Today());
    }

    public ReturnResult Return(int loanId)
    {
        var loan = loans.Get(loanId) ?? throw new NotFoundException($"Loan {loanId} not found.");

        if (loan.ReturnedOn is not null)
            throw new RuleViolationException("Loan is already returned.");

        var today = Today();
        var returned = loan with { ReturnedOn = today };
        loans.Update(returned);

        var item = items.Get(loan.ItemId) ?? throw new NotFoundException($"Item {loan.ItemId} not found.");
        items.Save(item with { Available = true });

        return new ReturnResult(returned, LateFeeCalculator.Calculate(loan.LentOn, today));
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
