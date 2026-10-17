# Tiquetera — Desafío técnico backend

## Qué es este proyecto

Desafío técnico para un proceso de ingreso: una API .NET / C# que funciona como motor de reservas de asientos de cine. Plazo: 3 días. Después hay una entrevista donde se defienden las decisiones técnicas.

**El diseño ya está cerrado y documentado en `ADR.md`. Leelo antes de escribir código y respetalo.** Si algo del ADR resulta inviable o hay que cambiarlo, avisame antes de desviarte, y actualizá el ADR en el mismo cambio.

## Sobre mí y cómo trabajar conmigo

- No conozco C# ni .NET. Vengo de Python (últimamente FastAPI), sé Java y estudié C.
- Tengo que poder explicar y defender cada línea en la entrevista. Por eso:
  - Trabajá **paso a paso**, siguiendo el plan de abajo. Al terminar cada paso, frená y explicame lo que hiciste.
  - Explicá los conceptos de C#/.NET nuevos con **equivalencias en Java o Python** (Spring, JPA/Hibernate, FastAPI, SQLAlchemy, pytest, etc.).
  - Priorizá código claro e idiomático sobre código ingenioso.
- Estoy en Windows, con Docker Desktop corriendo.

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
- Concurrencia optimista **por asiento**, con `xmin` como token de concurrencia. `DbUpdateConcurrencyException` → error de negocio (409), traducido fuera del dominio.
- Hold token (GUID) emitido por el servidor al seleccionar; confirmar y liberar lo exigen en el header `X-Hold-Token`. Nunca se expone en `GET /seats`. Se limpia al reservar.
- Expiración perezosa: se persiste `ExpiresAt`, el estado efectivo se calcula con `Seat.GetStatus(now)`. Sin background job. `now >= ExpiresAt` = vencido. `TimeProvider` inyectado; TTL configurable en `appsettings.json`.
- La respuesta de selección incluye `holdToken`, `expiresAt` (UTC ISO 8601) y `expiresInSeconds`.
- `Result` propio para errores de negocio esperados (tipos NotFound, Conflict, Forbidden, Gone), mapeados a ProblemDetails en un único lugar. Excepciones solo para lo inesperado.
- Estados: enum + **tabla de transiciones declarativa** `(estado, acción) → nuevo estado` dentro de `Seat`. NO patrón State. Setters privados; invariantes protegidas.
- Capas: `Cinema.Domain`, `Cinema.Application`, `Cinema.Infrastructure`, `Cinema.Api`, más proyectos de tests. Dependencias hacia el dominio.
- Un handler por caso de uso (`GetSeats`, `SelectSeat`, `ConfirmReservation`, `ReleaseSeat`), inyectado directo. Sin MediatR.
- Minimal APIs con `MapGroup("/seats")` y `TypedResults`; endpoints en archivos por recurso, no en `Program.cs`.
- Endpoints:
  - `GET /seats` → 200
  - `POST /seats/{id}/selection` → 201 / 404 / 409
  - `PUT /seats/{id}/reservation` → 200 / 403 / 404 / 409 / 410
  - `DELETE /seats/{id}/selection` → 204 / 403 / 404 / 409 / 410
- Eventos de dominio (`SeatSelected`, `SeatReserved`, `SeatReleased`) vía `ISeatEventPublisher`; implementación `LoggingSeatEventPublisher`. Se publican **después** de persistir. Dejar comentarios en el código sobre la extensión futura a SSE (ver ADR-010). No implementar SSE.
- Tests: xUnit. Unitarios de dominio con `FakeTimeProvider`; handlers con dobles; integración con `WebApplicationFactory` + Testcontainers (Postgres real); test de concurrencia (N requests paralelos al mismo asiento → exactamente uno gana, incluido el caso de selección vencida).
- Documentación de API con OpenAPI + Scalar.

- Sala: 8 filas (A–H) × 10 asientos = 80. Id = clave natural `"C7"` (PK y ruta). Seed determinístico en la migración (ADR-012).

## Plan de trabajo (en este orden, frenando al final de cada paso)

1. Estructura de la solución: `.sln`, proyectos y referencias entre ellos.
2. Dominio: `Seat`, estados, transiciones, `Result`, errores + tests unitarios.
3. Application: handlers, `ISeatRepository`, `ISeatEventPublisher`, DTOs + tests.
4. Infrastructure: DbContext, mapeo con `xmin`, migraciones, seed, `docker-compose.yml`.
5. API: endpoints, mapeo Result → HTTP, Scalar.
6. Tests de integración y de concurrencia con Testcontainers.
7. Docs: README, revisar/actualizar ADR, documentación de la API.
