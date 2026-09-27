namespace Application.Exceptions;

/// <summary>
/// Thrown for domain-level validation failures raised from application services
/// (as opposed to FluentValidation request-shape failures). Mapped to HTTP 400.
/// </summary>
public sealed class ValidationAppException(string message) : Exception(message);
