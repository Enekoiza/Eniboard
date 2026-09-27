namespace Application.Exceptions;

/// <summary>
/// Thrown when a requested entity does not exist. Mapped to HTTP 404 by the API's
/// global exception handling middleware.
/// </summary>
public sealed class NotFoundException(string message) : Exception(message);
