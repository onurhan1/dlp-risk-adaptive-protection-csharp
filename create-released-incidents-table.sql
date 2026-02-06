-- Released Quarantined Incidents Table
-- Bu tablo, DLP sisteminden "Released quarantined message" olarak işaretlenen
-- incident'ları kaydetmek için kullanılır.

-- Table: released_incidents
CREATE TABLE IF NOT EXISTS released_incidents (
    id SERIAL PRIMARY KEY,
    incident_id BIGINT NOT NULL,
    incident_timestamp TIMESTAMP NOT NULL,
    action VARCHAR(50) NOT NULL,
    task_name VARCHAR(255) NOT NULL,
    admin_name VARCHAR(100),
    comments TEXT,
    update_time TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    -- Unique constraint to prevent duplicate entries
    CONSTRAINT uq_released_incident UNIQUE (incident_id, update_time)
);

-- Indexes for better query performance
CREATE INDEX IF NOT EXISTS idx_released_incidents_incident_id ON released_incidents(incident_id);
CREATE INDEX IF NOT EXISTS idx_released_incidents_admin_name ON released_incidents(admin_name);
CREATE INDEX IF NOT EXISTS idx_released_incidents_update_time ON released_incidents(update_time);
CREATE INDEX IF NOT EXISTS idx_released_incidents_incident_timestamp ON released_incidents(incident_timestamp);

-- Add comments to table and columns
COMMENT ON TABLE released_incidents IS 'Stores incidents with Released quarantined message history from DLP';
COMMENT ON COLUMN released_incidents.id IS 'Auto-generated unique ID';
COMMENT ON COLUMN released_incidents.incident_id IS 'Original incident ID from DLP system';
COMMENT ON COLUMN released_incidents.incident_timestamp IS 'When the incident originally occurred';
COMMENT ON COLUMN released_incidents.action IS 'The action taken on the incident (RELEASED)';
COMMENT ON COLUMN released_incidents.task_name IS 'Task name from history (Released quarantined message)';
COMMENT ON COLUMN released_incidents.admin_name IS 'Admin who released the incident';
COMMENT ON COLUMN released_incidents.comments IS 'Comments added during release';
COMMENT ON COLUMN released_incidents.update_time IS 'When the release action was taken';
COMMENT ON COLUMN released_incidents.created_at IS 'When this record was created in our database';
