using Cinema.Domain.Common;

namespace Cinema.Domain.Seats;

/// <summary>
/// Asiento de la sala. Es su propio agregado y unidad de consistencia (ADR-002).
/// </summary>
/// <remarks>
/// Invariantes del estado persistido:
/// <list type="bullet">
///   <item><c>Selected</c> ⇔ tiene hold token y vencimiento.</item>
///   <item><c>Free</c> y <c>Reserved</c> ⇒ sin hold token ni vencimiento.</item>
/// </list>
/// El estado persistido puede ser <c>Selected</c> con la selección ya vencida: el estado efectivo
/// se calcula con <see cref="GetStatus"/> (expiración perezosa, ADR-004).
/// </remarks>
public sealed class Seat
{
    private enum SeatAction
    {
        Select,
        Confirm,
        Release,
    }

    /// <summary>
    /// Tabla de transiciones permitidas: (estado efectivo, acción) → nuevo estado (ADR-006).
    /// Lo que no figura acá no está permitido.
    /// </summary>
    private static readonly IReadOnlyDictionary<(SeatStatus From, SeatAction Action), SeatStatus> Transitions =
        new Dictionary<(SeatStatus, SeatAction), SeatStatus>
        {
            [(SeatStatus.Free, SeatAction.Select)] = SeatStatus.Selected,
            [(SeatStatus.Selected, SeatAction.Confirm)] = SeatStatus.Reserved,
            [(SeatStatus.Selected, SeatAction.Release)] = SeatStatus.Free,
        };

    public Seat(string row, int number)
    {
        if (row is not { Length: 1 } || row[0] is < 'A' or > 'Z')
        {
            throw new ArgumentException("La fila debe ser una única letra mayúscula (A-Z).", nameof(row));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);

        Id = $"{row}{number}";
        Row = row;
        Number = number;
        Status = SeatStatus.Free;
    }

    /// <summary>Clave natural: fila + número, ej. "C7".</summary>
    public string Id { get; private set; }

    public string Row { get; private set; }

    public int Number { get; private set; }

    /// <summary>
    /// Vencimiento de la selección. Solo tiene valor cuando el estado persistido es <c>Selected</c>;
    /// puede estar en el pasado si la selección venció y nadie volvió a tocar el asiento.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// Estado persistido. Es privado para que nadie lo lea por error en lugar del estado efectivo:
    /// la única forma de consultarlo desde afuera es <see cref="GetStatus"/>.
    /// </summary>
    private SeatStatus Status { get; set; }

    /// <summary>Privado: el token solo sale del asiento al seleccionarlo, dentro de <see cref="SeatHold"/> (ADR-003).</summary>
    private Guid? HoldToken { get; set; }

    /// <summary>Estado efectivo en el instante <paramref name="now"/>: una selección vencida cuenta como libre.</summary>
    public SeatStatus GetStatus(DateTimeOffset now) => IsExpired(now) ? SeatStatus.Free : Status;

    /// <summary>
    /// Selecciona el asiento por <paramref name="ttl"/>. Funciona también sobre una selección vencida:
    /// se emite un token nuevo y el anterior queda invalidado.
    /// </summary>
    public Result<SeatHold> Select(DateTimeOffset now, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "El TTL de la selección debe ser positivo.");
        }

        var current = GetStatus(now);
        if (!Transitions.TryGetValue((current, SeatAction.Select), out var next))
        {
            return current == SeatStatus.Reserved
                ? SeatErrors.AlreadyReserved(Id)
                : SeatErrors.AlreadySelected(Id);
        }

        var hold = new SeatHold(Guid.NewGuid(), now + ttl);
        Status = next;
        HoldToken = hold.Token;
        ExpiresAt = hold.ExpiresAt;
        return hold;
    }

    /// <summary>Confirma la reserva. Exige el token de la selección vigente y que no haya vencido.</summary>
    public Result Confirm(Guid holdToken, DateTimeOffset now)
    {
        var next = NextStatusForHolder(SeatAction.Confirm, holdToken, now);
        if (next.IsFailure)
        {
            return next.Error;
        }

        Status = next.Value;
        ClearHold();
        return Result.Success();
    }

    /// <summary>Libera la selección. Exige el token de la selección vigente y que no haya vencido.</summary>
    public Result Release(Guid holdToken, DateTimeOffset now)
    {
        var next = NextStatusForHolder(SeatAction.Release, holdToken, now);
        if (next.IsFailure)
        {
            return next.Error;
        }

        Status = next.Value;
        ClearHold();
        return Result.Success();
    }

    private bool IsExpired(DateTimeOffset now) =>
        Status == SeatStatus.Selected && now >= ExpiresAt!.Value;

    /// <summary>
    /// Validaciones comunes a las acciones que solo puede hacer quien tiene la selección.
    /// El orden define qué error ve el cliente cuando aplica más de uno:
    /// vencida (410) → estado que no lo permite (409) → token incorrecto (403).
    /// </summary>
    private Result<SeatStatus> NextStatusForHolder(SeatAction action, Guid holdToken, DateTimeOffset now)
    {
        // Primero el vencimiento, para distinguir "llegaste tarde" de "nunca estuvo seleccionado" (ADR-009).
        // No se compara el token: que la selección venció ya es visible para cualquiera en GET /seats.
        if (IsExpired(now))
        {
            return SeatErrors.SelectionExpired(Id);
        }

        var current = GetStatus(now);
        if (!Transitions.TryGetValue((current, action), out var next))
        {
            return current == SeatStatus.Reserved
                ? SeatErrors.AlreadyReserved(Id)
                : SeatErrors.NotSelected(Id);
        }

        if (holdToken != HoldToken)
        {
            return SeatErrors.InvalidHoldToken(Id);
        }

        return next;
    }

    private void ClearHold()
    {
        HoldToken = null;
        ExpiresAt = null;
    }
}
