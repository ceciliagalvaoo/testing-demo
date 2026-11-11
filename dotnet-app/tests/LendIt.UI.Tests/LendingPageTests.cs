using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace LendIt.UI.Tests;

[TestFixture]
public class LendingPageTests : PageTest
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:8080";

    private LendingPage _page = null!;

    [SetUp]
    public void CreatePageObject() => _page = new LendingPage(Page, BaseUrl);

    // keep evidence: a screenshot of every failed test (uploaded as a CI artifact)
    [TearDown]
    public async Task ScreenshotOnFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
            return;
        var dir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screenshots");
        Directory.CreateDirectory(dir);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(dir, $"{TestContext.CurrentContext.Test.Name}.png"), FullPage = true });
    }

    // test data is prepared through the API (fast, reliable), not through the UI
    private static async Task<int> CreateItemAsync(string name)
    {
        using var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        var response = await http.PostAsJsonAsync("/items", new { name });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    [Test]
    public async Task Adding_an_item_shows_it_as_available()
    {
        var name = $"Ladder-{Guid.NewGuid():N}";

        await _page.OpenAsync();
        await _page.AddItemAsync(name);

        await Expect(_page.Message).ToHaveTextAsync($"Added {name}");
        await Expect(_page.Item(name)).ToBeVisibleAsync();
    }

    [Test]
    public async Task Lending_an_item_marks_it_as_lent_out()
    {
        var itemId = await CreateItemAsync($"Drill-{Guid.NewGuid():N}");
        var borrower = $"ana-{Guid.NewGuid():N}";

        await _page.OpenAsync();
        await _page.EnterBorrowerAsync(borrower);
        await _page.LendAsync(itemId);

        await Expect(_page.Message).ToHaveTextAsync($"Lent to {borrower}");
        await Expect(_page.StatusOf(itemId)).ToHaveTextAsync("Lent out");
        await Expect(_page.LendButton(itemId)).ToBeDisabledAsync();
    }

    [Test]
    public async Task A_blank_borrower_is_rejected_and_the_item_stays_available()
    {
        var itemId = await CreateItemAsync($"Saw-{Guid.NewGuid():N}");

        await _page.OpenAsync();
        await _page.EnterBorrowerAsync("");
        await _page.LendAsync(itemId);

        await Expect(_page.Message).ToHaveTextAsync(new Regex("^Error:"));
        await Expect(_page.StatusOf(itemId)).ToHaveTextAsync("Available");
    }

    [Test]
    public async Task A_lent_item_cannot_be_lent_from_another_browser()
    {
        var itemId = await CreateItemAsync($"Tent-{Guid.NewGuid():N}");

        await _page.OpenAsync();
        await _page.EnterBorrowerAsync($"ana-{Guid.NewGuid():N}");
        await _page.LendAsync(itemId);
        await Expect(_page.LendButton(itemId)).ToBeDisabledAsync();

        // a second, completely isolated browser context (another user)
        await using var otherContext = await Browser.NewContextAsync();
        var otherPage = new LendingPage(await otherContext.NewPageAsync(), BaseUrl);
        await otherPage.OpenAsync();

        await Expect(otherPage.StatusOf(itemId)).ToHaveTextAsync("Lent out");
        await Expect(otherPage.LendButton(itemId)).ToBeDisabledAsync();
    }

    [Test]
    public async Task Adding_an_item_with_a_blank_name_shows_an_error()
    {
        await _page.OpenAsync();
        await _page.AddItemAsync("   ");

        await Expect(_page.Message).ToHaveTextAsync("Error: Name is required.");
    }

    [Test]
    public async Task Screenshot_of_the_page_after_lending()
    {
        var itemId = await CreateItemAsync($"Projector-{Guid.NewGuid():N}");

        await _page.OpenAsync();
        // unique borrower: a fixed name hit the 3-loan limit after 3 runs against the same container
        await _page.EnterBorrowerAsync($"screenshot-{Guid.NewGuid():N}");
        await _page.LendAsync(itemId);
        await Expect(_page.StatusOf(itemId)).ToHaveTextAsync("Lent out");

        var dir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screenshots");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "lending-page.png");
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
        TestContext.AddTestAttachment(path, "The LendIt page after lending an item");
        Assert.That(File.Exists(path), Is.True);
    }
}
