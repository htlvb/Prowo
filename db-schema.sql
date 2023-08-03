DROP TABLE IF EXISTS registration;
DROP TABLE IF EXISTS project;

CREATE TABLE project(
    id UNIQUEIDENTIFIER PRIMARY KEY NOT NULL DEFAULT(NEWID()),
    title NVARCHAR(255) NOT NULL,
    description NVARCHAR(max) NOT NULL,
    location NVARCHAR(255) NOT NULL,
    organizer NVARCHAR(max) NOT NULL,
    co_organizers NVARCHAR(max) NOT NULL,
    date DATE NOT NULL,
    start_time TIME NOT NULL,
    end_time TIME,
    closing_date DATETIME NOT NULL,
    maxAttendees INT NOT NULL
);

CREATE TABLE registration_event(
    project_id UNIQUEIDENTIFIER NOT NULL,
    [user] NVARCHAR(max) NOT NULL,
    timestamp DATETIME NOT NULL,
    action NVARCHAR(16) NOT NULL CHECK (action IN ('register', 'deregister')),
    FOREIGN KEY(project_id) REFERENCES project(id) ON DELETE CASCADE
);
