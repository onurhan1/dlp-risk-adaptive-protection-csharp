-- RADAR Security Agent key-uniqueness repair
-- Run once against PostgreSQL after taking a database backup.
-- It archives and removes duplicate Isolation Forest score rows, then prevents
-- another duplicate canonical mailbox within the same scoring job.
--
-- Do NOT automatically rename duplicate workflow node IDs here. An edge that
-- references a duplicated ID cannot be assigned to one of the nodes safely by SQL.
-- Use the diagnostic query at the bottom and repair those workflows in the editor.

BEGIN;

CREATE SCHEMA IF NOT EXISTS dlp;

-- Keep deleted derived-score duplicates recoverable.
CREATE TABLE IF NOT EXISTS dlp.isolation_forest_scores_duplicate_archive AS
SELECT scores.*, CURRENT_TIMESTAMP AS archived_at
FROM dlp.isolation_forest_scores AS scores
WHERE FALSE;

ALTER TABLE dlp.isolation_forest_scores_duplicate_archive
    ADD COLUMN IF NOT EXISTS archived_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP;

-- For each job and case-insensitive mailbox, keep the strongest signal.
-- Ties keep the newest physical record (largest id).
WITH ranked AS (
    SELECT
        scores.id,
        ROW_NUMBER() OVER (
            PARTITION BY scores.job_id, LOWER(BTRIM(scores.user_email))
            ORDER BY scores.if_score DESC,
                     scores.baseline_incident_count DESC,
                     scores.id DESC
        ) AS row_number
    FROM dlp.isolation_forest_scores AS scores
    WHERE BTRIM(scores.user_email) <> ''
), duplicate_rows AS (
    SELECT scores.*
    FROM dlp.isolation_forest_scores AS scores
    INNER JOIN ranked ON ranked.id = scores.id
    WHERE ranked.row_number > 1
)
INSERT INTO dlp.isolation_forest_scores_duplicate_archive
SELECT duplicate_rows.*, CURRENT_TIMESTAMP
FROM duplicate_rows;

WITH ranked AS (
    SELECT
        scores.id,
        ROW_NUMBER() OVER (
            PARTITION BY scores.job_id, LOWER(BTRIM(scores.user_email))
            ORDER BY scores.if_score DESC,
                     scores.baseline_incident_count DESC,
                     scores.id DESC
        ) AS row_number
    FROM dlp.isolation_forest_scores AS scores
    WHERE BTRIM(scores.user_email) <> ''
)
DELETE FROM dlp.isolation_forest_scores AS scores
USING ranked
WHERE scores.id = ranked.id
  AND ranked.row_number > 1;

-- Future rows must be unique inside one Isolation Forest job, ignoring casing and whitespace.
CREATE UNIQUE INDEX IF NOT EXISTS ux_isolation_forest_scores_job_user_canonical
    ON dlp.isolation_forest_scores (job_id, LOWER(BTRIM(user_email)))
    WHERE BTRIM(user_email) <> '';

COMMIT;

-- Diagnostic only: lists workflows with unsafe repeated node IDs.
-- Repair these in the workflow editor: SQL cannot know which duplicated node a
-- source/target edge was intended to reference.
SELECT
    playbook.id AS playbook_id,
    playbook.name AS playbook_name,
    node ->> 'id' AS duplicate_node_id,
    COUNT(*) AS duplicate_count
FROM dlp.playbooks AS playbook
CROSS JOIN LATERAL jsonb_array_elements(
    COALESCE(playbook.graph_json::jsonb -> 'nodes', '[]'::jsonb)
) AS node
GROUP BY playbook.id, playbook.name, node ->> 'id'
HAVING COUNT(*) > 1
ORDER BY playbook.name, duplicate_node_id;
