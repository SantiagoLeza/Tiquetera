# Tiquetera — Desafío técnico backend

## Qué es este proyecto

API REST en .NET 10 / C# que funciona como motor de reservas de asientos para una única función de cine, sobre PostgreSQL.

**El diseño está documentado en `ADR.md`. Leelo antes de escribir código y respetalo.** Si algo del ADR resulta inviable o hay que cambiarlo, planteá el cambio antes de desviarte y actualizá el ADR en el mismo commit.

## Cómo trabajar en este repo

- Priorizá código claro e idiomático sobre código ingenioso. Seguí el estilo existente: comentarios que explican el *por qué* y referencian el ADR cuando aplica.
- El dominio (`Cinema.Domain`) no depende de ningún framework. Las reglas de negocio viven ahí, no en handlers ni endpoints.
- Todo cambio de comportamiento lleva tests. `dotnet test` debe pasar completo antes de commitear.
- Un commit por cambio coherente, en `main`.

## Comandos

```bash
docker compose up -d                                       # Postgres 17 en el puerto 5433; la primera vez ejecuta db/init.sql
dotnet run --project src/Cinema.Api --launch-profile http  # API en http://localhost:5054, documentación en /scalar
dotnet test                                                # todos los tests; los de integración requieren Docker
docker compose down -v                                     # borra la base (necesario después de modificar db/init.sql)
```

## Consigna (resumen fiel)

- Una única película, una única función, una única sala. Sin cartelera ni calendario.
- Sin usuarios ni credenciales.
- Reservas de a un asiento por vez.
- Estados del asiento: **Libre**, **Seleccionado** (esperando confirmación o liberación), **Reservado**.
- Requerimientos funcionales:
  1. Consultar qué asientos están libres, seleccionados o reservados.
  2. Seleccionar un asiento; hay hasta 5 minutos para confirmar.
  3. Confirmar la reserva de un asiento seleccionado; no se permite pasados los 5 minutos.
  4. Liberar un asiento seleccionado.
- Restricciones mandatorias: .NET 8 o superior, C#, API RESTful sobre HTTP.
- Entregables: repo de GitHub, README (cómo levantarlo localmente), ADR, documentación de la API (Swagger/Scalar o markdown).
- Se evalúa: diseño y arquitectura (patrones justificados), concurrencia y consistencia, modelado del dominio (invariantes, transiciones, TTL), mantenibilidad, escalabilidad y testabilidad.

## Decisiones clave (detalle y justificación en ADR.md)

- PostgreSQL + EF Core (Npgsql). `docker-compose.yml` para levantar Postgres.
- Esquema y seed en `db/init.sql`, ejecutado por Postgres al inicializar (`docker-entrypoint-initdb.d`) y montado también en Testcontainers. Sin migraciones de EF ni migración al arrancar (ADR-001). El mapeo de EF debe coincidir con ese script.
- Sala: 8 filas (A–H) × 10 asientos = 80. Id = clave natural `"C7"` (PK y ruta). Seed determinístico en `db/init.sql` (ADR-012).
- Concurrencia optimista **por asiento**, con `xmin` como token de concurrencia. `DbUpdateConcurrencyException` → error de negocio (409), traducido en el repositorio, fuera del dominio.
- Hold token (GUID) emitido por el servidor al seleccionar; confirmar y liberar lo exigen en el header `X-Hold-Token`. Nunca se expone en `GET /seats`. Se limpia al reservar.
- Expiración perezosa: se persiste `ExpiresAt`, el estado efectivo se calcula con `Seat.GetStatus(now)`. Sin background job. `now >= ExpiresAt` = vencido. `TimeProvider` inyectado; TTL configurable en `appsettings.json` (`SeatSelection:Ttl`).
- La respuesta de selección incluye `holdToken`, `expiresAt` (UTC ISO 8601) y `expiresInSeconds`.
- `Result` propio para errores de negocio esperados (tipos NotFound, Conflict, Forbidden, Gone), mapeados a ProblemDetails en un único lugar. Excepciones solo para lo inesperado.
- Estados: enum + **tabla de transiciones declarativa** `(estado, acción) → nuevo estado` dentro de `Seat`. NO patrón State. Setters privados; estado persistido y hold token privados; invariantes protegidas (y replicadas con `CHECK` en la base).
- Capas: `Cinema.Domain`, `Cinema.Application`, `Cinema.Infrastructure`, `Cinema.Api`, más proyectos de tests. Dependencias hacia el dominio.
- Un handler por caso de uso (`GetSeats`, `SelectSeat`, `ConfirmReservation`, `ReleaseSeat`), inyectado directo. Sin MediatR.
- Minimal APIs con `MapGroup("/seats")` y `TypedResults`; endpoints en archivos por recurso, no en `Program.cs`.
- Endpoints:
  - `GET /seats` → 200
  - `POST /seats/{id}/selection` → 201 / 404 / 409
  - `PUT /seats/{id}/reservation` → 200 / 400 / 403 / 404 / 409 / 410
  - `DELETE /seats/{id}/selection` → 204 / 400 / 403 / 404 / 409 / 410
- Eventos de dominio (`SeatSelected`, `SeatReserved`, `SeatReleased`) vía `ISeatEventPublisher`; implementación `LoggingSeatEventPublisher`. Se publican **después** de persistir. La extensión a tiempo real por SSE está documentada (ADR-010) pero no implementada.
- Tests: xUnit. Unitarios de dominio con `FakeTimeProvider`; handlers con dobles; integración con `WebApplicationFactory` + Testcontainers (Postgres real); test de concurrencia (N requests paralelos al mismo asiento → exactamente uno gana, incluido el caso de selección vencida).
- Documentación de API con OpenAPI + Scalar (`/scalar`) y `docs/API.md`.
