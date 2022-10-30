DROP TABLE IF EXISTS registration;
DROP TABLE IF EXISTS project;

CREATE TABLE project(
    id UUID PRIMARY KEY NOT NULL,
    title VARCHAR NOT NULL,
    description VARCHAR NOT NULL,
    location VARCHAR(50) NOT NULL,
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
