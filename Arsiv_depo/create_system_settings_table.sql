-- Create system_settings table for storing application settings
CREATE TABLE IF NOT EXISTS system_settings (
    key VARCHAR(100) PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Insert default values
INSERT INTO system_settings (key, value) VALUES
    ('risk_threshold_low', '10'),
    ('risk_threshold_medium', '30'),
    ('risk_threshold_high', '50'),
    ('email_notifications', 'true'),
    ('daily_report_time', '06:00'),
    ('admin_email', '')
ON CONFLICT (key) DO NOTHING;

