namespace LendIt.Api.Domain;

public interface IItemRepository
{
    Item? Get(int id);
    IReadOnlyList<Item> All();
    Item Add(string name);
    void Save(Item item);
}

public interface ILoanRepository
{
    Loan? Get(int id);
    Loan Add(int itemId, string borrower, DateOnly lentOn);
    void Update(Loan loan);
    int CountActive(string borrower);
}
