using System.Net;
using System.Net.Http.Json;

namespace LendIt.E2E.Tests;

// Black box: no reference to the application project. The DTOs are declared here.
public record ItemDto(int Id, string Name, bool Available);
public record LoanDto(int Id, int ItemId, string Borrower);

[Trait("Category", "E2E")]
public class LoanJourneyTests
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:8080")
    };

    private async Task<ItemDto> CreateItem(string name)
    {
        var created = await _http.PostAsJsonAsync("/items", new { name });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<ItemDto>())!;
    }

    private Task<HttpResponseMessage> Lend(int itemId, string borrower) =>
        _http.PostAsJsonAsync("/loans", new { itemId, borrower });

    [Fact]
    public async Task A_borrower_can_lend_and_return_an_item()
    {
        // create the test data through the public API
        var item = await CreateItem("Projector");
        var borrower = $"ana-{Guid.NewGuid():N}";

        // lend
        var lend = await Lend(item.Id, borrower);
        Assert.Equal(HttpStatusCode.Created, lend.StatusCode);
        var loan = (await lend.Content.ReadFromJsonAsync<LoanDto>())!;
        Assert.False((await _http.GetFromJsonAsync<ItemDto>($"/items/{item.Id}"))!.Available);

        // a second loan of the same item is rejected
        var second = await Lend(item.Id, "marko");
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // return
        var returned = await _http.PostAsync($"/loans/{loan.Id}/return", content: null);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        Assert.True((await _http.GetFromJsonAsync<ItemDto>($"/items/{item.Id}"))!.Available);
    }

    [Fact]
    public async Task A_borrower_is_limited_to_3_loans_until_one_is_returned()
    {
        var borrower = $"limit-{Guid.NewGuid():N}";
        var items = new List<ItemDto>();
        for (var i = 1; i <= 4; i++)
            items.Add(await CreateItem($"Board game {i}"));

        // three loans succeed
        var loans = new List<LoanDto>();
        foreach (var item in items.Take(3))
        {
            var response = await Lend(item.Id, borrower);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            loans.Add((await response.Content.ReadFromJsonAsync<LoanDto>())!);
        }

        // the 4th is rejected
        var fourth = await Lend(items[3].Id, borrower);
        Assert.Equal(HttpStatusCode.Conflict, fourth.StatusCode);

        // after returning one, the 4th succeeds
        var returned = await _http.PostAsync($"/loans/{loans[0].Id}/return", content: null);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);

        var retry = await Lend(items[3].Id, borrower);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task Invalid_input_is_rejected_and_nothing_is_created()
    {
        var before = (await _http.GetFromJsonAsync<List<ItemDto>>("/items"))!;
        var item = await CreateItem($"Validation-{Guid.NewGuid():N}");

        // a blank item name is rejected
        var blankName = await _http.PostAsJsonAsync("/items", new { name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, blankName.StatusCode);

        // a blank borrower is rejected
        var blankBorrower = await Lend(item.Id, "");
        Assert.Equal(HttpStatusCode.BadRequest, blankBorrower.StatusCode);

        // only our valid item was added, and it is still available (no loan was created)
        var after = (await _http.GetFromJsonAsync<List<ItemDto>>("/items"))!;
        var added = after.Where(a => before.All(b => b.Id != a.Id)).ToList();
        Assert.Contains(added, a => a.Id == item.Id);
        Assert.DoesNotContain(added, a => string.IsNullOrWhiteSpace(a.Name));
        Assert.True(after.Single(a => a.Id == item.Id).Available);
    }
}
