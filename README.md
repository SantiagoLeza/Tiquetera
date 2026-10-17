# Tiquetera

Motor de reservas de asientos para una única función de cine: una API REST en .NET 10 / C# sobre PostgreSQL.

Un asiento puede estar **libre**, **seleccionado** (hay 5 minutos para confirmarlo) o **reservado**. Quien selecciona recibe un *hold token*, el único que permite confirmar o liberar esa selección. La API garantiza que, ante pedidos simultáneos por el mismo asiento, gane exactamente uno.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/) (Docker Desktop en Windows/macOS)

## Levantarlo localmente

```bash
# 1. Base de datos (PostgreSQL 17, publicado en el puerto 5433 del host)
docker compose up -d

# 2. API (al arrancar aplica las migraciones y carga los 80 asientos)
dotnet run --project src/Cinema.Api --launch-profile http
```

| | URL |
|---|---|
| Documentación interactiva (Scalar) | http://localhost:5054/scalar |
| Documento OpenAPI | http://localhost:5054/openapi/v1.json |
| API | http://localhost:5054/seats |

Para volver a la sala vacía: `docker compose down -v` y luego `docker compose up -d`.

### Probarlo con curl

```bash
# Seleccionar el asiento C7: devuelve holdToken, expiresAt y expiresInSeconds
curl -X POST http://localhost:5054/seats/C7/selection

# Confirmar la reserva con el token recibido
curl -X PUT http://localhost:5054/seats/C7/reservation -H "X-Hold-Token: <holdToken>"

# ...o liberarla
curl -X DELETE http://localhost:5054/seats/C7/selection -H "X-Hold-Token: <holdToken>"

# Ver el estado de la sala
curl http://localhost:5054/seats
```

## Tests

```bash
dotnet test
```

| Proyecto | Qué cubre | Requiere |
|---|---|---|
| `Cinema.Domain.Tests` | Transiciones, invariantes y bordes del TTL (reloj falso, sin esperas) | — |
| `Cinema.Application.Tests` | Casos de uso con dobles del repositorio y del publicador de eventos | — |
| `Cinema.Api.IntegrationTests` | API completa contra PostgreSQL real (Testcontainers), incluidos los tests de concurrencia | Docker |

Los tests de concurrencia lanzan 20 pedidos simultáneos sobre el mismo asiento y verifican que exactamente uno gane: sobre un asiento libre, sobre una selección vencida y en confirmaciones con el mismo token. Un test adicional prueba el token de concurrencia de forma determinística.

## Configuración

`src/Cinema.Api/appsettings*.json`, o variables de entorno (`SeatSelection__Ttl`, `ConnectionStrings__Cinema`):

| Clave | Default | Descripción |
|---|---|---|
| `SeatSelection:Ttl` | `00:05:00` | Tiempo para confirmar una selección. Se valida al arrancar. |
| `ConnectionStrings:Cinema` | `Host=localhost;Port=5433;...` (Development) | Cadena de conexión a PostgreSQL. |

## Diseño en una página

```
src/
  Cinema.Domain/          Seat (estados, tabla de transiciones, TTL), Result, errores, eventos
  Cinema.Application/     un handler por caso de uso, puertos (ISeatRepository, ISeatEventPublisher), DTOs
  Cinema.Infrastructure/  EF Core + Npgsql, repositorio, migraciones y seed, publicador de eventos
  Cinema.Api/             Minimal APIs, mapeo Result → ProblemDetails, OpenAPI + Scalar
tests/                    un proyecto por capa + integración
```

Las dependencias apuntan hacia el dominio, que no conoce ningún framework. Las decisiones principales:

- **Concurrencia optimista por asiento**, con `xmin` de PostgreSQL como token de versión. Un `UPDATE` con una versión vieja no afecta filas y se traduce a `409`. No hay locks ni esperas. → [ADR-002](ADR.md#adr-002--concurrencia-optimista-por-asiento)
- **Hold token** emitido por el servidor. Confirmar y liberar lo exigen en `X-Hold-Token`, y nunca se expone en `GET /seats`. → [ADR-003](ADR.md#adr-003--propiedad-de-la-selección-mediante-hold-token)
- **Expiración perezosa**: no hay job de fondo. Se guarda `ExpiresAt` y el estado efectivo se calcula con el reloj del servidor en cada operación. → [ADR-004](ADR.md#adr-004--expiración-perezosa-del-ttl)
- **`Result` para errores de negocio**, traducido a HTTP en un único lugar. Las excepciones quedan para lo inesperado. → [ADR-005](ADR.md#adr-005--errores-de-negocio-con-result)
- **Tabla de transiciones declarativa** `(estado, acción) → estado` dentro de `Seat`. El estado y el token son privados de la entidad. → [ADR-006](ADR.md#adr-006--estados-del-asiento-enum--tabla-de-transiciones)
- **Eventos de dominio** publicados después de persistir, con el punto de extensión preparado para tiempo real por SSE. → [ADR-010](ADR.md#adr-010--eventos-de-dominio-y-extensión-a-tiempo-real)

## Documentación

- [ADR.md](ADR.md): decisiones de arquitectura, con contexto, alternativas descartadas y consecuencias.
- [docs/API.md](docs/API.md): referencia de la API, con ejemplos, códigos de error y precedencia.
- `/scalar`: documentación interactiva generada desde el código.

## Fuera de alcance

- Usuarios, autenticación y reservas de varios asientos a la vez (la consigna no los incluye).
- Notificaciones en tiempo real: el diseño las prevé (ADR-010) pero no están implementadas.
- Las migraciones se aplican al arrancar la API. Es cómodo para desarrollo; en un despliegue con varias instancias se ejecutarían como un paso previo (ADR-001).
