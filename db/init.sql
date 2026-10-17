-- Esquema inicial de Tiquetera (ADR-001).
--
-- PostgreSQL ejecuta los scripts de /docker-entrypoint-initdb.d/ al inicializar una base vacía.
-- Lo usan docker-compose.yml y los tests de integración (Testcontainers): un único esquema para ambos.
-- Solo corre si el volumen está vacío; para recrear la base local: docker compose down -v && docker compose up -d

BEGIN;

CREATE TABLE seats (
    id         varchar(4)  PRIMARY KEY,
    "row"      varchar(1)  NOT NULL,
    number     integer     NOT NULL,
    status     varchar(16) NOT NULL,
    hold_token uuid,
    expires_at timestamptz,
    -- No hay columna de versión: la concurrencia optimista usa la columna de sistema xmin (ADR-002).

    CONSTRAINT uq_seats_row_number UNIQUE ("row", number),
    CONSTRAINT ck_seats_row        CHECK ("row" ~ '^[A-Z]$'),
    CONSTRAINT ck_seats_number     CHECK (number > 0),
    CONSTRAINT ck_seats_id         CHECK (id = "row" || number),
    CONSTRAINT ck_seats_status     CHECK (status IN ('Free', 'Selected', 'Reserved')),
    -- Misma invariante que protege Seat (ADR-006), como segunda línea de defensa:
    -- Selected ⇔ tiene hold token y vencimiento; Free y Reserved no tienen ninguno de los dos.
    CONSTRAINT ck_seats_hold       CHECK (
        (status = 'Selected'  AND hold_token IS NOT NULL AND expires_at IS NOT NULL) OR
        (status <> 'Selected' AND hold_token IS NULL     AND expires_at IS NULL)
    )
);

-- Sala de 8 filas (A–H) × 10 asientos = 80 asientos, todos libres (ADR-012).
INSERT INTO seats (id, "row", number, status)
SELECT r || n, r, n, 'Free'
FROM unnest(string_to_array('ABCDEFGH', NULL)) AS r
CROSS JOIN generate_series(1, 10) AS n;

COMMIT;
