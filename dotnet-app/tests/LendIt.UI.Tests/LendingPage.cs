using Microsoft.Playwright;

namespace LendIt.UI.Tests;

/// <summary>Page object: the structure of the LendIt page lives here, the tests only express intent.</summary>
public class LendingPage(IPage page, string baseUrl)
{
    public Task OpenAsync() => page.GotoAsync(baseUrl);
    public Task EnterNewItemNameAsync(string name) => page.GetByTestId("new-item-name").FillAsync(name);
    public Task ClickAddItemAsync() => page.GetByTestId("add-item-button").ClickAsync();
    public Task EnterBorrowerAsync(string name) => page.GetByTestId("borrower-input").FillAsync(name);
    public Task LendAsync(int itemId) => LendButton(itemId).ClickAsync();

    public async Task AddItemAsync(string name)
    {
        await EnterNewItemNameAsync(name);
        await ClickAddItemAsync();
    }

    public ILocator Message => page.GetByTestId("message");
    // only the list entries: GetByText(name) alone also matches the "Added <name>" message (strict mode violation)
    public ILocator Item(string name) => page.GetByRole(AriaRole.Listitem).Filter(new() { HasText = name });
    public ILocator StatusOf(int itemId) => page.GetByTestId($"item-{itemId}-status");
    public ILocator LendButton(int itemId) => page.GetByTestId($"lend-button-{itemId}");
}
