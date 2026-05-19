-- Admin, user management, and audit logging support for the C# migration.
-- Apply this in Supabase PostgreSQL after reviewing it against your existing schema.

CREATE TABLE IF NOT EXISTS app_users (
    id BIGSERIAL PRIMARY KEY,
    username TEXT NOT NULL UNIQUE,
    full_name TEXT,
    email TEXT UNIQUE,
    password_hash TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_admin BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by BIGINT NULL REFERENCES app_users(id),
    deleted_at TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS app_user_privileges (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    privilege_code TEXT NOT NULL,
    granted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    granted_by BIGINT NULL REFERENCES app_users(id),
    UNIQUE (user_id, privilege_code)
);

CREATE TABLE IF NOT EXISTS tariff_change_audit (
    id BIGSERIAL PRIMARY KEY,
    actor_user_id BIGINT NULL REFERENCES app_users(id),
    action_type TEXT NOT NULL CHECK (action_type IN ('INSERT', 'UPDATE', 'DELETE')),
    table_name TEXT NOT NULL,
    record_key TEXT NOT NULL,
    old_value JSONB,
    new_value JSONB,
    changed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    changed_from_host TEXT,
    remarks TEXT
);

CREATE OR REPLACE FUNCTION log_tariff_change()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    actor_user BIGINT;
    record_identity TEXT;
BEGIN
    actor_user := NULLIF(current_setting('app.current_user_id', TRUE), '')::BIGINT;

    IF TG_OP = 'DELETE' THEN
        record_identity := COALESCE(OLD.id::TEXT, 'unknown');

        INSERT INTO tariff_change_audit (
            actor_user_id,
            action_type,
            table_name,
            record_key,
            old_value,
            new_value
        )
        VALUES (
            actor_user,
            TG_OP,
            TG_TABLE_NAME,
            record_identity,
            to_jsonb(OLD),
            NULL
        );

        RETURN OLD;
    END IF;

    record_identity := COALESCE(NEW.id::TEXT, 'unknown');

    INSERT INTO tariff_change_audit (
        actor_user_id,
        action_type,
        table_name,
        record_key,
        old_value,
        new_value
    )
    VALUES (
        actor_user,
        TG_OP,
        TG_TABLE_NAME,
        record_identity,
        CASE WHEN TG_OP = 'UPDATE' THEN to_jsonb(OLD) ELSE NULL END,
        to_jsonb(NEW)
    );

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_tariff_components_audit ON tariff_components;
CREATE TRIGGER trg_tariff_components_audit
AFTER INSERT OR UPDATE OR DELETE ON tariff_components
FOR EACH ROW EXECUTE FUNCTION log_tariff_change();

DROP TRIGGER IF EXISTS trg_taxes_audit ON taxes;
CREATE TRIGGER trg_taxes_audit
AFTER INSERT OR UPDATE OR DELETE ON taxes
FOR EACH ROW EXECUTE FUNCTION log_tariff_change();

DROP TRIGGER IF EXISTS trg_levies_audit ON levies;
CREATE TRIGGER trg_levies_audit
AFTER INSERT OR UPDATE OR DELETE ON levies
FOR EACH ROW EXECUTE FUNCTION log_tariff_change();
