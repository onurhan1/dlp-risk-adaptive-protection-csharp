-- ═══════════════════════════════════════════════════════
-- KATMAN 1: POLİTİKA
-- ═══════════════════════════════════════════════════════
CREATE TABLE dlp.pi_policies (
    id SERIAL PRIMARY KEY,
    policy_name VARCHAR(500) NOT NULL UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ═══════════════════════════════════════════════════════
-- KATMAN 2: KURAL
-- ═══════════════════════════════════════════════════════
CREATE TABLE dlp.pi_rules (
    id SERIAL PRIMARY KEY,
    policy_id INT NOT NULL REFERENCES dlp.pi_policies(id) ON DELETE CASCADE,
    rule_name VARCHAR(500) NOT NULL,
    parts_count_type VARCHAR(100),
    condition_relation_type VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(policy_id, rule_name)
);

-- Kural Classifier'ları (type=policy, Col 6-9)
CREATE TABLE dlp.pi_rule_classifiers (
    id SERIAL PRIMARY KEY,
    rule_id INT NOT NULL REFERENCES dlp.pi_rules(id) ON DELETE CASCADE,
    classifier_name VARCHAR(500),
    threshold_type VARCHAR(100),
    threshold_value_from INT,
    threshold_calculate_type VARCHAR(100)
);

-- Kural Severity Action (type=severity_action, Col 38-44)
CREATE TABLE dlp.pi_rule_severity_actions (
    id SERIAL PRIMARY KEY,
    rule_id INT NOT NULL REFERENCES dlp.pi_rules(id) ON DELETE CASCADE,
    type VARCHAR(100),
    max_matches VARCHAR(100),
    selected VARCHAR(10),
    number_of_matches INT,
    severity_type VARCHAR(50),
    dup_severity_type VARCHAR(50),
    action_plan VARCHAR(200)
);

-- Kural Source Resources (type=source_destination, Col 45-47)
CREATE TABLE dlp.pi_rule_sources (
    id SERIAL PRIMARY KEY,
    rule_id INT NOT NULL REFERENCES dlp.pi_rules(id) ON DELETE CASCADE,
    resource_name VARCHAR(500),
    resource_type VARCHAR(100),
    include VARCHAR(10)
);

-- Kural Destination Channels (type=source_destination, Col 48-50)
CREATE TABLE dlp.pi_rule_destinations (
    id SERIAL PRIMARY KEY,
    rule_id INT NOT NULL REFERENCES dlp.pi_rules(id) ON DELETE CASCADE,
    email_monitor_directions VARCHAR(200),
    channel_type VARCHAR(100),
    channel_enabled VARCHAR(10)
);

-- Kural Channel Resources (type=source_destination, Col 51-53)
CREATE TABLE dlp.pi_rule_channel_resources (
    id SERIAL PRIMARY KEY,
    destination_id INT NOT NULL REFERENCES dlp.pi_rule_destinations(id) ON DELETE CASCADE,
    resource_name VARCHAR(500),
    resource_type VARCHAR(100),
    include VARCHAR(10)
);

-- ═══════════════════════════════════════════════════════
-- KATMAN 3: EXCEPTION (type=policy satırlarından)
-- ═══════════════════════════════════════════════════════
CREATE TABLE dlp.pi_exceptions (
    id SERIAL PRIMARY KEY,
    rule_id INT NOT NULL REFERENCES dlp.pi_rules(id) ON DELETE CASCADE,
    exception_rule_name VARCHAR(500) NOT NULL,
    enabled VARCHAR(10) DEFAULT 'true',
    description TEXT,
    condition_enabled VARCHAR(10) DEFAULT 'false',
    source_enabled VARCHAR(10) DEFAULT 'false',
    destination_enabled VARCHAR(10) DEFAULT 'false',
    parts_count_type VARCHAR(100),
    condition_relation_type VARCHAR(50),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Exception Classifier'ları (type=policy, Col 18-23)
CREATE TABLE dlp.pi_exception_classifiers (
    id SERIAL PRIMARY KEY,
    exception_id INT NOT NULL REFERENCES dlp.pi_exceptions(id) ON DELETE CASCADE,
    classifier_name VARCHAR(500),
    position INT,
    threshold_type VARCHAR(100),
    threshold_value_from INT,
    threshold_calculate_type VARCHAR(100),
    analyzed_specific_fields TEXT
);

-- Exception Severity Action (type=policy, Col 24-28)
CREATE TABLE dlp.pi_exception_severity_actions (
    id SERIAL PRIMARY KEY,
    exception_id INT NOT NULL REFERENCES dlp.pi_exceptions(id) ON DELETE CASCADE,
    selected VARCHAR(10),
    number_of_matches INT,
    severity_type VARCHAR(50),
    dup_severity_type VARCHAR(50),
    action_plan VARCHAR(200)
);

-- Exception Source Resources (type=policy, Col 29-31)
CREATE TABLE dlp.pi_exception_sources (
    id SERIAL PRIMARY KEY,
    exception_id INT NOT NULL REFERENCES dlp.pi_exceptions(id) ON DELETE CASCADE,
    resource_name VARCHAR(500),
    resource_type VARCHAR(100),
    include VARCHAR(10)
);

-- Exception Destination Channels (type=policy, Col 32-34)
CREATE TABLE dlp.pi_exception_destinations (
    id SERIAL PRIMARY KEY,
    exception_id INT NOT NULL REFERENCES dlp.pi_exceptions(id) ON DELETE CASCADE,
    email_monitor_directions VARCHAR(200),
    channel_type VARCHAR(100),
    channel_enabled VARCHAR(10)
);

-- Exception Channel Resources (type=policy, Col 35-37)
CREATE TABLE dlp.pi_exception_channel_resources (
    id SERIAL PRIMARY KEY,
    destination_id INT NOT NULL REFERENCES dlp.pi_exception_destinations(id) ON DELETE CASCADE,
    resource_name VARCHAR(500),
    resource_type VARCHAR(100),
    include VARCHAR(10)
);
