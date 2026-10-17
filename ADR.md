# Architecture Decision Records — Tiquetera

Motor de reservas de asientos para una única función de cine. API REST en .NET 10 (LTS) / C#.

Este documento reúne las decisiones técnicas tomadas durante el diseño, con su contexto, las alternativas evaluadas y sus consecuencias.

## Índice

1. [Persistencia en PostgreSQL](#adr-001--persistencia-en-postgresql)
2. [Concurrencia optimista por asiento](#adr-002--concurrencia-optimista-por-asiento)
3. [Propiedad de la selección mediante hold token](#adr-003--propiedad-de-la-selección-mediante-hold-token)
4. [Expiración perezosa del TTL](#adr-004--expiración-perezosa-del-ttl)
5. [Errores de negocio con Result](#adr-005--errores-de-negocio-con-result)
6. [Estados del asiento: enum + tabla de transiciones](#adr-006--estados-del-asiento-enum--tabla-de-transiciones)
7. [Arquitectura en capas](#adr-007--arquitectura-en-capas)
8. [Minimal APIs con TypedResults](#adr-008--minimal-apis-con-typedresults)
9. [Diseño de los contratos REST](#adr-009--diseño-de-los-contratos-rest)
10. [Eventos de dominio y extensión a tiempo real](#adr-010--eventos-de-dominio-y-extensión-a-tiempo-real)
11. [Estrategia de testing](#adr-011--estrategia-de-testing)
12. [Composición de la sala e identificador del asiento](#adr-012--composición-de-la-sala-e-identificador-del-asiento)

---

## ADR-001 — Persistencia en PostgreSQL

**Contexto.** La consigna no exige una persistencia en memoria. El sistema debe garantizar integridad de datos bajo concurrencia y poder escalar.

**Decisión.** Usar PostgreSQL con EF Core (proveedor Npgsql).

**Alternativas descartadas.**
- *Almacenamiento en memoria* (`ConcurrentDictionary` + locks). Más simple, pero el estado se pierde al reiniciar y no permite correr más de una instancia de la API: cada instancia tendría su propia copia de los asientos.

**Consecuencias.**
- La consistencia la garantiza la base de datos, no el proceso. La API queda stateless y puede escalar horizontalmente.
- Se requiere Docker para levantar Postgres localmente (se provee `docker-compose.yml`).
- El `docker-compose.yml` publica Postgres en el puerto **5433** del host, para no chocar con una instalación local en el 5432.

**Esquema: script SQL, fuera del arranque de la API.** El esquema y el seed viven en un único script, `db/init.sql`. PostgreSQL lo ejecuta al inicializar una base vacía (`/docker-entrypoint-initdb.d/`). Lo montan tanto `docker-compose.yml` como los tests de integración (Testcontainers): hay una sola fuente de verdad y los tests corren contra el mismo esquema que el entorno local.

*Alternativas descartadas para el esquema:*
- *Migraciones de EF Core aplicadas al arrancar la API.* Fue la primera implementación. Con varias instancias, todas intentan migrar a la vez, y además la API necesita permisos de DDL. El esquema queda repartido entre la migración, el *designer* y el *snapshot* generados.
- *Migraciones de EF Core + script generado para ejecutar aparte.* Conserva toda esa maquinaria y suma el riesgo de olvidarse de regenerar el script. No se justifica para un esquema inicial único.

*Consecuencias:*
- La API solo lee y escribe datos: no crea ni modifica tablas.
- El script declara restricciones `CHECK` que replican las invariantes de `Seat` (ADR-006) como segunda línea de defensa.
- El modelo de EF y el script ya no se generan uno del otro y podrían desincronizarse. Los tests de integración corren contra el script, así que una diferencia los rompe.
- Postgres ejecuta los scripts de `initdb.d` **solo cuando el volumen está vacío**. Cambiar el esquema en local requiere `docker compose down -v`. Si el esquema tuviera que evolucionar con datos existentes, se adoptaría una herramienta de migraciones versionadas (Flyway, DbUp o *bundles* de EF) ejecutada como paso de despliegue.

---

## ADR-002 — Concurrencia optimista por asiento

**Contexto.** Dos usuarios pueden intentar seleccionar el mismo asiento al mismo tiempo. Solo uno debe tener éxito.

**Decisión.** Concurrencia optimista con un token de versión por asiento. Se usa la columna de sistema `xmin` de PostgreSQL como token de concurrencia en EF Core.

Cada actualización incluye la versión leída en la condición:

```sql
UPDATE seats SET ... WHERE id = @id AND xmin = @versionLeida;
```

Si otra operación modificó el asiento antes, la condición no coincide, se actualizan 0 filas y EF Core lanza `DbUpdateConcurrencyException`. Esa excepción se traduce a un error de negocio (409 Conflict, código `seat.concurrent_modification`).

**Implementación.** La versión es una *shadow property* `uint Version` marcada con `IsRowVersion()`. Npgsql la mapea a `xmin`, una columna de sistema que PostgreSQL cambia en cada `UPDATE`, así que no se crea ninguna columna. La clase `Seat` no conoce la versión: solo existe en el modelo de EF.

Funciona aun si los dos requests llegan exactamente al mismo tiempo. Con el aislamiento por defecto de PostgreSQL (READ COMMITTED), el segundo `UPDATE` espera el lock de fila del primero y vuelve a evaluar el `WHERE` sobre la fila ya confirmada. Como `xmin` cambió, no afecta ninguna fila.

**Granularidad.** La versión pertenece a cada asiento. Cada `Seat` es su propio agregado y unidad de consistencia: operaciones concurrentes sobre asientos distintos nunca entran en conflicto. Como las reservas son de a un asiento por vez, nunca se necesita coordinar varias filas en una misma transacción.

**Alternativas descartadas.**
- *Concurrencia pesimista* (`SELECT ... FOR UPDATE`). Bloquea la fila durante la operación. Genera esperas y riesgo de deadlocks, y no aporta valor: ante un conflicto no tiene sentido esperar, porque el asiento ya es de otro. Lo correcto es fallar rápido.
- *Agregado `Sala` con una única versión.* Toda reserva concurrente chocaría aunque fuera de asientos distintos, degradando el throughput sin ninguna regla de negocio que lo justifique.

**Consecuencias.**
- No hay bloqueos; las operaciones sobre asientos distintos escalan en paralelo.
- El caso de un asiento con selección vencida que dos usuarios intentan tomar a la vez también queda protegido por la versión (ver ADR-004).

---

## ADR-003 — Propiedad de la selección mediante hold token

**Contexto.** El sistema no tiene usuarios ni credenciales. Sin un mecanismo de propiedad, cualquier cliente podría confirmar o liberar la selección de otro.

**Decisión.** Al seleccionar un asiento, el servidor genera un **hold token** (GUID aleatorio) y lo devuelve al cliente. Confirmar y liberar exigen ese token (header `X-Hold-Token`). El token se persiste en la fila del asiento.

**Alternativas descartadas.**
- *Sin control de propiedad.* Rompe la regla implícita de que solo quien selecciona puede confirmar.
- *Identificador generado por el cliente* (ej. `X-Client-Id`). El servidor confiaría en un valor que el cliente inventa y que otro podría copiar o adivinar.

**Consecuencias.**
- El token nunca se expone en `GET /seats`; de lo contrario cualquiera podría apropiarse de selecciones ajenas.
- Al confirmar la reserva el token se limpia: ya no tiene uso.
- Si una selección vence y otro usuario selecciona el asiento, se genera un token nuevo y el anterior queda invalidado sin lógica adicional.
- El front puede guardarlo en local storage. El riesgo ante XSS es acotado: el token vive como máximo el TTL y solo da control sobre ese asiento.
- Fuera de alcance: un token que agrupe varios asientos (la consigna define reservas de a un asiento).

---

## ADR-004 — Expiración perezosa del TTL

**Contexto.** Un asiento seleccionado debe confirmarse dentro de los 5 minutos; pasado ese tiempo no puede confirmarse y vuelve a estar disponible.

**Decisión.** Expiración perezosa (*lazy expiration*). No se persiste un estado "vencido": se guarda `ExpiresAt` y el **estado efectivo** se calcula al momento de usarlo. Un asiento `Selected` con `now >= ExpiresAt` se comporta como `Free`.

La regla vive en el dominio (`Seat.GetStatus(now)`) y la usan todas las operaciones y la consulta. El tiempo se obtiene de `TimeProvider`, inyectado.

**Alternativas descartadas.**
- *Job de fondo* (`BackgroundService`) que libera los asientos vencidos periódicamente. Entre corridas existe una ventana en la que un asiento vencido figura como seleccionado, y con varias instancias de la API todas ejecutarían el job. Agrega complejidad sin aportar corrección.
- *Expiración perezosa + job de limpieza.* La limpieza no cambia ningún comportamiento observable; se descartó por no aportar valor.

**Consecuencias.**
- Nunca hay inconsistencia temporal: el estado es siempre exacto respecto del reloj.
- Borde definido: a los 5:00 exactos la selección ya está vencida (`now >= ExpiresAt`).
- Siempre se usa el reloj del servidor, nunca uno provisto por el cliente.
- El TTL es configurable (`appsettings.json`), no está hardcodeado.
- `TimeProvider` permite testear los bordes del TTL con `FakeTimeProvider`, sin esperas reales.
- La respuesta de la selección incluye `expiresAt` (UTC, ISO 8601) y `expiresInSeconds`. El front usa la duración relativa para la cuenta regresiva, evitando errores por desfase del reloj del navegador. La autoridad sigue siendo el servidor: una confirmación con la selección vencida responde 410 aunque el front no haya limpiado el token.

---

## ADR-005 — Errores de negocio con Result

**Contexto.** Las operaciones pueden fallar por motivos de negocio esperados: asiento no disponible, token inválido, selección vencida, asiento inexistente.

**Decisión.** Usar un tipo `Result` propio para los errores de negocio esperados, y excepciones solo para lo verdaderamente excepcional (falla de infraestructura, bugs).

Cada error de dominio tiene un código y un tipo (`NotFound`, `Conflict`, `Forbidden`, `Gone`). Un único punto en la capa API los traduce a respuestas HTTP con formato **ProblemDetails** (RFC 9457).

**Alternativas descartadas.**
- *Excepciones de dominio + manejador global.* Usaría excepciones como control de flujo para situaciones que son parte normal del negocio (dos usuarios queriendo el mismo asiento), y la firma de los métodos no expresa qué puede fallar.
- *Librerías* (ErrorOr, FluentResults). Válidas, pero la implementación necesaria es mínima y una propia evita sumar dependencias.

**Consecuencias.**
- Los fallos posibles son explícitos en las firmas y fáciles de testear.
- `DbUpdateConcurrencyException` es de infraestructura pero su significado es de negocio ("otro usuario tomó el asiento"). Se captura fuera del dominio y se convierte en el error correspondiente; el dominio no conoce a EF Core. Concretamente, la traduce la implementación del repositorio: `ISeatRepository.SaveAsync` devuelve un `Result` que falla con `seat.concurrent_modification`, así que tampoco Application conoce a EF Core.

---

## ADR-006 — Estados del asiento: enum + tabla de transiciones

**Contexto.** Un asiento tiene tres estados (Libre, Seleccionado, Reservado) y transiciones con reglas. El diseño debe poder extenderse si el dominio crece.

**Decisión.** Representar el estado con un enum y declarar las transiciones permitidas en una **tabla única** dentro de la entidad (`(estado, acción) → nuevo estado`). Las reglas adicionales (validación del token, TTL) viven en los métodos de transición de la entidad (`Select`, `Confirm`, `Release`).

La entidad protege sus invariantes: setters privados, todas las transiciones pasan por métodos con nombre de negocio, y si el asiento está libre no tiene token ni vencimiento.

El estado persistido y el hold token son propiedades **privadas** de `Seat`:
- El estado solo se consulta con `GetStatus(now)`, así nadie puede leer por error un `Selected` ya vencido.
- El token solo sale de la entidad en el `SeatHold` que devuelve `Select`. Esto refuerza el ADR-003 a nivel de compilación.

La base replica la invariante principal con un `CHECK` (`Selected` ⇔ token y vencimiento presentes; ver ADR-001). La autoridad es el dominio; la restricción protege ante escrituras que no pasen por él.

**Alternativas descartadas.**
- *Enum con validaciones dispersas en cada método.* Agregar un estado obliga a revisar varios métodos; las transiciones válidas no se leen en un solo lugar.
- *Patrón State* (una clase por estado). Se descartó porque:
  - Con tres estados, uno de ellos terminal, y reglas de pocas líneas, la cantidad de clases y la indirección no se justifican.
  - El estado efectivo depende del tiempo (un `Selected` vencido se comporta como `Free`), lo que obligaría a recalcular el objeto de estado en cada operación.
  - EF Core persiste un enum de forma directa; una jerarquía de estados requiere mapeo adicional.

**Evolución prevista.** Estados como *Cancelado* o *Reembolsado* no pertenecen al asiento: el asiento modela **disponibilidad**, mientras que pagos, cancelaciones y reembolsos son el ciclo de vida de una **reserva** como transacción comercial. Si el dominio crece, se introduciría un concepto `Reservation` separado con su propio ciclo (Pendiente, Confirmada, Cancelada, Reembolsada), y el asiento volvería a `Free` cuando corresponda. Si ese ciclo resultara complejo, ahí sí se reevaluaría el patrón State para `Reservation`.

---

## ADR-007 — Arquitectura en capas

**Decisión.** Arquitectura en capas al estilo Clean Architecture, en una versión liviana:

```
src/
  Cinema.Domain/          Seat, estados, transiciones, Result, errores
  Cinema.Application/     casos de uso, interfaces (repositorio, eventos), DTOs
  Cinema.Infrastructure/  EF Core, DbContext, mapeo, repositorio, publicador de eventos
db/
  init.sql                esquema y seed (ADR-001)
  Cinema.Api/             endpoints, mapeo Result → HTTP, documentación
tests/
  Cinema.Domain.Tests/
  Cinema.Application.Tests/
  Cinema.Api.IntegrationTests/
```

Todas las dependencias apuntan hacia el dominio. `Domain` no depende de ningún framework. `Application` define las interfaces que `Infrastructure` implementa. `Api` compone todo mediante inyección de dependencias.

**Casos de uso.** Una clase handler por caso de uso (`GetSeats`, `SelectSeat`, `ConfirmReservation`, `ReleaseSeat`), inyectada directamente en los endpoints.

**Alternativas descartadas.**
- *MediatR.* Agrega una dependencia e indirección que no aportan valor con cuatro casos de uso.
- *Un único `SeatService`.* Concentra responsabilidades y crece mal al agregar operaciones.

**Consecuencias.** El dominio se testea sin base de datos ni HTTP, y la persistencia puede reemplazarse sin tocar reglas de negocio.

---

## ADR-008 — Minimal APIs con TypedResults

**Decisión.** Exponer los endpoints con Minimal APIs, agrupados con `MapGroup("/seats")`, devolviendo `TypedResults`.

**Alternativas descartadas.**
- *Controllers.* Válidos, pero agregan ceremonia (clase base, atributos, convenciones). Con handlers por caso de uso, el controller sería una capa que solo delega.

**Motivos.**
- Encaje directo con los handlers: el endpoint recibe el request, invoca el handler y traduce el `Result` a HTTP.
- `TypedResults` con `Results<...>` hace que el tipo de retorno declare las respuestas posibles: el compilador impide devolver otra cosa y OpenAPI infiere las respuestas exitosas (`200`, `201`, `204`) sin atributos. Los errores salen como `ProblemHttpResult`, que no indica un status concreto, así que sus códigos se declaran con `.ProducesProblem(...)` en cada endpoint.
- El rendimiento no fue un criterio: la diferencia con Controllers es irrelevante frente al costo de acceso a la base.
- La escalabilidad horizontal es equivalente en ambos enfoques (la API es stateless).

**Consecuencias.** Para que el código escale en cantidad de endpoints, se organizan en archivos por recurso en lugar de declararlos en `Program.cs`.

---

## ADR-009 — Diseño de los contratos REST

**Decisión.** La selección y la reserva se modelan como **subrecursos** del asiento, en lugar de endpoints con verbos (`/select`, `/confirm`).

| Método | Ruta | Descripción | Respuestas |
|---|---|---|---|
| GET | `/seats` | Lista los asientos con su estado efectivo | 200 |
| POST | `/seats/{id}/selection` | Selecciona un asiento | 201, 404, 409 |
| PUT | `/seats/{id}/reservation` | Confirma la reserva | 200, 400, 403, 404, 409, 410 |
| DELETE | `/seats/{id}/selection` | Libera la selección | 204, 400, 403, 404, 409, 410 |

Semántica de los errores:
- **400** falta el header `X-Hold-Token` o no es un GUID válido. Es un request mal formado, no un problema de propiedad, y se valida antes de llegar al caso de uso.
- **404** el asiento no existe.
- **409** el asiento no está en un estado que permita la operación (ej. ya está seleccionado por otro), o se perdió una carrera de concurrencia.
- **403** el hold token no corresponde a la selección vigente.
- **410** la selección venció. Permite al cliente distinguir "llegaste tarde" de "lo tiene otro".

El `DELETE` también puede responder 409: al liberar un asiento libre o reservado, o al perder una carrera contra un `PUT` con el mismo token. Una versión inicial de esta tabla lo omitía; se agregó al implementar el dominio para que ambos comandos usen el mismo criterio.

**Precedencia.** Si aplica más de un error, se informa en este orden: 410 (vencida) → 409 (el estado no lo permite) → 403 (token incorrecto). El vencimiento se informa sin validar el token, porque que una selección venció ya es público en `GET /seats`.

**Identificador.** El id del asiento distingue mayúsculas: `/seats/c7/selection` responde 404. No se normaliza para que exista un único nombre canónico por asiento.

**Hold token por header.** `PUT` y `DELETE` reciben el token en el header `X-Hold-Token`. Se descartó enviarlo en el body porque, aunque HTTP lo permite, algunos clientes y proxies ignoran el body en `DELETE`; el header funciona igual en cualquier verbo.

**Consecuencias.** Los errores siguen el formato ProblemDetails. Las fechas se expresan en UTC (ISO 8601).

---

## ADR-010 — Eventos de dominio y extensión a tiempo real

**Contexto.** Una mejora de UX deseable es que un cliente que está viendo la sala reciba en tiempo real los cambios de estado, sin polling.

**Decisión.** Los handlers publican eventos de dominio (`SeatSelected`, `SeatReserved`, `SeatReleased`) a través de una interfaz `ISeatEventPublisher`. La implementación inicial, `LoggingSeatEventPublisher`, registra los eventos en el log. El canal en tiempo real **no se implementa** en esta versión; la extensión queda documentada aquí y en comentarios en el código.

Los eventos se publican **después** de persistir con éxito. Publicar antes podría informar una selección que luego pierde la carrera de concurrencia.

Los eventos son records del dominio y los crean los handlers después del `SaveAsync`. No los acumula la entidad: con cuatro casos de uso, una colección de eventos dentro de `Seat` agregaría ceremonia sin beneficio. Ningún evento transporta el hold token.

El contrato de `ISeatEventPublisher` prohíbe lanzar excepciones por fallas al notificar. Cuando se publica, el cambio ya está confirmado, y fallar el request le diría al cliente que la operación no ocurrió. Si en el futuro hiciera falta garantía de entrega, se usaría el patrón *transactional outbox*: guardar el evento en la misma transacción que el asiento y despacharlo después.

**Extensión prevista.** Agregar una implementación que envíe los eventos por **Server-Sent Events** (`GET /seats/events`), sin modificar los handlers.

Puntos considerados para esa extensión:
- **SSE en lugar de WebSocket.** La consigna exige HTTP/REST. SSE es HTTP puro y unidireccional (servidor → cliente), que es lo necesario: los comandos ya viajan por REST. WebSocket (o SignalR) cambia de protocolo y su bidireccionalidad no aporta.
- **Interacción con la expiración perezosa.** Un vencimiento no produce escritura ni evento. El front lo resuelve localmente usando el `expiresAt` de cada asiento seleccionado, evitando reintroducir un job en el servidor.
- **Múltiples instancias.** Un evento ocurrido en una instancia debe llegar a clientes conectados a otra. Se resolvería con un canal compartido, como `LISTEN/NOTIFY` de PostgreSQL (ya presente en la infraestructura) o Redis.

**Motivo para no implementarlo ahora.** No es un requerimiento de la consigna y el esfuerzo se priorizó en concurrencia, dominio y TTL. El diseño deja el punto de extensión preparado (principio abierto/cerrado).

---

## ADR-011 — Estrategia de testing

**Decisión.**
- **Tests unitarios del dominio:** transiciones válidas e inválidas, invariantes, y bordes del TTL con `FakeTimeProvider`.
- **Tests de casos de uso:** handlers con dobles del repositorio y del publicador de eventos.
- **Tests de integración:** API completa con `WebApplicationFactory` contra un **PostgreSQL real** levantado con **Testcontainers**.
- **Test de concurrencia:** N solicitudes en paralelo sobre el mismo asiento; exactamente una debe tener éxito. Incluye el caso de un asiento con selección vencida disputado por varios clientes.
- **Test determinístico del token de concurrencia:** dos repositorios leen la misma versión del asiento, ambos lo modifican y ambos guardan; el segundo debe recibir `seat.concurrent_modification`. No depende de que los requests se superpongan en el tiempo.

Los tests de integración reemplazan `TimeProvider` por un `FakeTimeProvider`, así que el vencimiento (410) se prueba de punta a punta sin esperas.

**Alternativa descartada.** *Proveedor en memoria de EF Core o SQLite* para integración. No reproducen el comportamiento de PostgreSQL (en particular `xmin`), por lo que el test de concurrencia no probaría nada real.

**Consecuencias.**
- Correr los tests de integración requiere Docker.
- Se verificó que los tests protegen lo que dicen proteger: al quitar el token de concurrencia, fallan el test determinístico y las carreras de selección paralela. Las carreras paralelas dependen del timing: muestran que hay superposición real (la mayoría de los perdedores recibe `seat.concurrent_modification`), pero la garantía la da el test determinístico.

---

## ADR-012 — Composición de la sala e identificador del asiento

**Contexto.** La consigna define una única sala sin especificar su forma.

**Decisión.** Sala fija de **8 filas (A–H) × 10 asientos (1–10)**, es decir, 80 asientos. El identificador es la **clave natural** fila + número (`"C7"`), usada como clave primaria y en las rutas (`/seats/C7/selection`). `Seat` expone además `Row` y `Number` por separado. Los asientos se cargan con un seed determinístico en `db/init.sql`.

**Alternativas descartadas.**
- *Clave surrogate numérica (1..80).* Es más convencional en bases relacionales, pero `/seats/23` no dice qué asiento es y obliga al front a mapear. La clave natural acá es estable: un asiento no cambia de fila ni de número.
- *Sala configurable* (filas y columnas en `appsettings.json`). Obliga a definir qué pasa si cambia la configuración con datos existentes, y la consigna no lo pide.

**Consecuencias.**
- `Seat` valida que la fila sea una letra mayúscula y el número sea positivo, y la base lo refuerza con `CHECK`s (incluido que el id sea fila + número). El tamaño concreto de la sala vive en el seed, no en el dominio.
- Cambiar la sala requiere modificar el seed y recrear la base. Es aceptable para una sala única.
