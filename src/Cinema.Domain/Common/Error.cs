namespace Cinema.Domain.Common;

/// <summary>
/// Categoría de un error de negocio. Expresa el significado del error, no un código HTTP:
/// la capa API decide cómo se traduce cada tipo (ADR-005).
/// </summary>
public enum ErrorType
{
    NotFound,
    Conflict,
    Forbidden,
    Gone,
}

/// <summary>
/// Error de negocio esperado. <paramref name="Code"/> es estable y apto para que un cliente lo interprete
/// (ej. "seat.already_selected"); <paramref name="Message"/> es para humanos.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type);
