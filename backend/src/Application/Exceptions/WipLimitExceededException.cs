namespace Application.Exceptions;

/// <summary>
/// Thrown when moving a card into a column would exceed that column's WIP limit.
/// Mapped to HTTP 409 Conflict by the API's global exception handling middleware.
/// </summary>
public sealed class WipLimitExceededException(string message) : Exception(message);
