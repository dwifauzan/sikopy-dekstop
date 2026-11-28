-- =========================================================
-- SIKOPY: Deactivate user accounts instead of deleting them
-- =========================================================

BEGIN;

ALTER TABLE pengguna
    ADD COLUMN IF NOT EXISTS is_aktif BOOLEAN NOT NULL DEFAULT TRUE;

COMMIT;
