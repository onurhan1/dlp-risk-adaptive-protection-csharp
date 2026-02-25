-- Add RuleName column to Incidents table to store DLP rule name
ALTER TABLE "Incidents" ADD COLUMN "RuleName" text NULL;
