DROP TABLE IF EXISTS registration;
DROP TABLE IF EXISTS project;

CREATE TABLE project(
    id UUID PRIMARY KEY NOT NULL,
    title VARCHAR NOT NULL,
    description VARCHAR NOT NULL,
    location VARCHAR NOT NULL,
    organizer JSON NOT NULL,
    co_organizers JSON NOT NULL,
    date DATE NOT NULL,
    start_time TIME NOT NULL,
    end_time TIME,
    closing_date TIMESTAMP NOT NULL,
    maxAttendees INT NOT NULL
);

CREATE TYPE registration_action AS ENUM ('register', 'deregister');
CREATE TABLE registration_event(
    project_id UUID NOT NULL,
    "user" JSON NOT NULL,
    timestamp TIMESTAMP NOT NULL,
    action registration_action NOT NULL,
    FOREIGN KEY(project_id) REFERENCES project(id) ON DELETE CASCADE
);

ALTER TABLE project ADD COLUMN payment_info JSON;

CREATE TABLE event (
    id UUID PRIMARY KEY NOT NULL,
    title VARCHAR NOT NULL,
    "start" DATE NOT NULL,
    "end" DATE NOT NULL,
    visible_from TIMESTAMP NOT NULL,
    registration_from TIMESTAMP NOT NULL
);

ALTER TABLE project ADD COLUMN event_id UUID REFERENCES event(id) ON DELETE RESTRICT;

INSERT INTO event (id, title, "start", "end", visible_from, registration_from)
VALUES ('00000000-0000-0000-0000-000000000001', 'Allgemein', '2000-01-01', '9999-12-31', '-infinity', '-infinity');

UPDATE project SET event_id = '00000000-0000-0000-0000-000000000001' WHERE event_id IS NULL;

ALTER TABLE project ALTER COLUMN event_id SET NOT NULL;

INSERT INTO event (id, title, "start", "end", visible_from, registration_from) VALUES
    ('00000000-0000-0000-0000-000000000001', 'Projektwoche 2024', '2024-07-01', '2024-07-03', '-infinity', '-infinity'),
    ('00000000-0000-0000-0000-000000000002', 'Projektwoche 2025', '2025-06-30', '2025-07-03', '-infinity', '-infinity'),
    ('00000000-0000-0000-0000-000000000003', 'Projektwoche 2026', '2026-07-06', '2026-07-09', '2026-05-22 00:00:00', '2026-05-25 04:00:00');