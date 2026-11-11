using System.Net;
using System.Net.Http.Json;
using LendIt.Api.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace LendIt.Tests.Integration;

[Trait("Category", "Integration")]
public class LoanApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // test data helper: every test creates its own item, so tests stay independent
    private async Task<Item> CreateItem(string name)
    {
        var response = await _client.PostAsJsonAsync("/items", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Item>())!;
    }

    private async Task<Loan> Lend(int itemId, string borrower)
    {
        var response = await _client.PostAsJsonAsync("/loans", new { itemId, borrower });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Loan>())!;
    }

    // ------------------------------------------------------------------ given

    [Fact]
    public async Task Lending_an_item_returns_201_and_makes_the_item_unavailable()
    {
        var item = await CreateItem("Drill");

        var response = await _client.PostAsJsonAsync("/loans", new { itemId = item.Id, borrower = "ana-1" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var updated = await _client.GetFromJsonAsync<Item>($"/items/{item.Id}");
        Assert.False(updated!.Available);
    }

    [Fact]
    public async Task Lending_the_same_item_twice_returns_409()
    {
        var item = await CreateItem("Ladder");
        await _client.PostAsJsonAsync("/loans", new { itemId = item.Id, borrower = "ana-2" });

        var second = await _client.PostAsJsonAsync("/loans", new { itemId = item.Id, borrower = "marko-2" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Lending_an_unknown_item_returns_404_with_an_error_message()
    {
        var response = await _client.PostAsJsonAsync("/loans", new { itemId = 424242, borrower = "ana-3" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Returning_after_20_days_charges_a_late_fee()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await using var timeTravelFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            }));
        var client = timeTravelFactory.CreateClient();

        var item = (await (await client.PostAsJsonAsync("/items", new { name = "Drill" }))
            .Content.ReadFromJsonAsync<Item>())!;
        var loan = (await (await client.PostAsJsonAsync("/loans", new { itemId = item.Id, borrower = "ana-4" }))
            .Content.ReadFromJsonAsync<Loan>())!;

        clock.Advance(TimeSpan.FromDays(20));
        var response = await client.PostAsync($"/loans/{loan.Id}/return", content: null);

        var result = await response.Content.ReadFromJsonAsync<ReturnResult>();
        Assert.Equal(3.00m, result!.LateFee);
    }

    // ------------------------------------------------------------------ own

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Creating_an_item_with_a_blank_name_returns_400_with_an_error(string name)
    {
        var response = await _client.PostAsJsonAsync("/items", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Name is required", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Lending_with_a_blank_borrower_returns_400()
    {
        var item = await CreateItem("Saw");

        var response = await _client.PostAsJsonAsync("/loans", new { itemId = item.Id, borrower = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lending_without_a_borrower_property_returns_400()
    {
        var item = await CreateItem("Hammer");

        var response = await _client.PostAsJsonAsync("/loans", new { itemId = item.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_4th_loan_of_a_borrower_returns_409()
    {
        var borrower = $"limit-{Guid.NewGuid():N}";
        for (var i = 0; i < 3; i++)
            await Lend((await CreateItem($"Tool {i}")).Id, borrower);
        var fourth = await CreateItem("Tool 4");

        var response = await _client.PostAsJsonAsync("/loans", new { itemId = fourth.Id, borrower });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("3 active loans", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Returning_a_loan_makes_the_item_available_again()
    {
        var item = await CreateItem("Tent");
        var loan = await Lend(item.Id, "ana-5");

        var response = await _client.PostAsync($"/loans/{loan.Id}/return", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await _client.GetFromJsonAsync<Item>($"/items/{item.Id}");
        Assert.True(updated!.Available);
    }

    [Fact]
    public async Task Returning_the_same_loan_twice_returns_409()
    {
        var item = await CreateItem("Kayak");
        var loan = await Lend(item.Id, "ana-6");
        await _client.PostAsync($"/loans/{loan.Id}/return", content: null);

        var second = await _client.PostAsync($"/loans/{loan.Id}/return", content: null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Returning_an_unknown_loan_returns_404()
    {
        var response = await _client.PostAsync("/loans/999999/return", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Listing_items_contains_the_created_item()
    {
        var item = await CreateItem($"Camera-{Guid.NewGuid():N}");

        var items = await _client.GetFromJsonAsync<List<Item>>("/items");

        Assert.Contains(items!, i => i.Id == item.Id && i.Name == item.Name && i.Available);
    }

    [Fact]
    public async Task Getting_an_unknown_item_returns_404()
    {
        var response = await _client.GetAsync("/items/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_web_page_is_served_at_the_root()
    {
        var html = await _client.GetStringAsync("/");

        Assert.Contains("data-testid=\"add-item-button\"", html);
    }
}
