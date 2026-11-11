namespace LendIt.Api.Domain;

public record Item(int Id, string Name, bool Available);

public record Loan(int Id, int ItemId, string Borrower, DateOnly LentOn, DateOnly? ReturnedOn);

public record ReturnResult(Loan Loan, decimal LateFee);

public class NotFoundException(string message) : Exception(message);

public class RuleViolationException(string message) : Exception(message);
