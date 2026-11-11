using LendIt.Api.Domain;
using LendIt.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IItemRepository, InMemoryItemRepository>();
builder.Services.AddSingleton<ILoanRepository, InMemoryLoanRepository>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LoanService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/items", (IItemRepository items) => items.All());

app.MapGet("/items/{id:int}", (int id, IItemRepository items) =>
    items.Get(id) is { } item
        ? Results.Ok(item)
        : Results.NotFound(new { error = $"Item {id} not found." }));

app.MapPost("/items", (CreateItemRequest request, IItemRepository items) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { error = "Name is required." });

    var item = items.Add(request.Name.Trim());
    return Results.Created($"/items/{item.Id}", item);
});

app.MapPost("/loans", (LendRequest request, LoanService service) =>
    Handle(() =>
    {
        var loan = service.Lend(request.ItemId, request.Borrower ?? string.Empty);
        return Results.Created($"/loans/{loan.Id}", loan);
    }));

app.MapPost("/loans/{id:int}/return", (int id, LoanService service) =>
    Handle(() => Results.Ok(service.Return(id))));

app.Run();

static IResult Handle(Func<IResult> action)
{
    try { return action(); }
    catch (NotFoundException e) { return Results.NotFound(new { error = e.Message }); }
    catch (RuleViolationException e) { return Results.Conflict(new { error = e.Message }); }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
}

public record CreateItemRequest(string? Name);
public record LendRequest(int ItemId, string? Borrower);

public partial class Program { }   // makes the entry point visible to the integration tests
