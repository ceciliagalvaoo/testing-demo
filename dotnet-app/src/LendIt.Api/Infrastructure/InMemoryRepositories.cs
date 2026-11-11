using System.Collections.Concurrent;
using LendIt.Api.Domain;

namespace LendIt.Api.Infrastructure;

public class InMemoryItemRepository : IItemRepository
{
    private readonly ConcurrentDictionary<int, Item> _items = new();
    private int _nextId;

    public Item? Get(int id) => _items.TryGetValue(id, out var item) ? item : null;

    public IReadOnlyList<Item> All() => _items.Values.OrderBy(i => i.Id).ToList();

    public Item Add(string name)
    {
        var item = new Item(Interlocked.Increment(ref _nextId), name, true);
        _items[item.Id] = item;
        return item;
    }

    public void Save(Item item) => _items[item.Id] = item;
}

public class InMemoryLoanRepository : ILoanRepository
{
    private readonly ConcurrentDictionary<int, Loan> _loans = new();
    private int _nextId;

    public Loan? Get(int id) => _loans.TryGetValue(id, out var loan) ? loan : null;

    public Loan Add(int itemId, string borrower, DateOnly lentOn)
    {
        var loan = new Loan(Interlocked.Increment(ref _nextId), itemId, borrower, lentOn, null);
        _loans[loan.Id] = loan;
        return loan;
    }

    public void Update(Loan loan) => _loans[loan.Id] = loan;

    public int CountActive(string borrower) =>
        _loans.Values.Count(l => l.Borrower == borrower && l.ReturnedOn is null);
}
