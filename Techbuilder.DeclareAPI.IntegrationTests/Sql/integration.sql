-- Objects used only by the integration suite.
-- Loaded after Techbuilder.DeclareAPI.Sample/sql/init.sql.

-- One column per filter operator, with typed columns (int, numeric, date, uuid)
CREATE TABLE items (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    sku TEXT NOT NULL,
    supplier TEXT NOT NULL,
    category TEXT NOT NULL,
    qty INT NOT NULL,
    price NUMERIC(10,2) NOT NULL,
    weight INT NOT NULL,
    stock INT NOT NULL,
    status TEXT NOT NULL,
    created_on DATE NOT NULL,
    owner_id UUID NOT NULL
);

INSERT INTO items VALUES
('00000000-0000-0000-0000-000000000001', 'Alpha Widget',  'AW-001', 'Acme Ltd',    'tools',  5,  10.00,  1,  50, 'active',   '2024-01-10', '11111111-1111-1111-1111-111111111111'),
('00000000-0000-0000-0000-000000000002', 'Beta Gadget',   'BG-002', 'Globex Inc',  'toys',  15,  25.50,  3,  20, 'pending',  '2024-02-15', '11111111-1111-1111-1111-111111111111'),
('00000000-0000-0000-0000-000000000003', 'Gamma Widget',  'AW-003', 'Acme Ltd',    'tools', 25,  99.99,  7,   5, 'archived', '2024-03-20', '22222222-2222-2222-2222-222222222222'),
('00000000-0000-0000-0000-000000000004', 'Delta Tool',    'DT-004', 'Initech Inc', 'tools', 35,   5.00, 10,   0, 'active',   '2024-04-25', '22222222-2222-2222-2222-222222222222'),
('00000000-0000-0000-0000-000000000005', 'Epsilon Gizmo', 'EG-005', 'Umbrella Co', 'toys',  45, 150.00, 12, 100, 'pending',  '2024-05-30', '33333333-3333-3333-3333-333333333333');

-- Schema-qualified sources
CREATE SCHEMA clinic;

CREATE TABLE clinic.rooms (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name TEXT NOT NULL,
    floor INT NOT NULL
);

INSERT INTO clinic.rooms (id, name, floor) VALUES
('aaaaaaaa-0000-0000-0000-000000000001', 'Room A', 1),
('aaaaaaaa-0000-0000-0000-000000000002', 'Room B', 2),
('aaaaaaaa-0000-0000-0000-000000000003', 'Room C', 2);

CREATE VIEW clinic.vw_rooms AS
SELECT id, name, floor FROM clinic.rooms;

CREATE FUNCTION clinic.fn_create_room(p_name TEXT, p_floor INT)
RETURNS UUID AS $$
DECLARE
    new_id UUID;
BEGIN
    INSERT INTO clinic.rooms (name, floor) VALUES (p_name, p_floor)
    RETURNING id INTO new_id;
    RETURN new_id;
END;
$$ LANGUAGE plpgsql;

-- Writes without a return value: FUNCTION vs PROCEDURE
CREATE TABLE audit_log (
    id SERIAL PRIMARY KEY,
    message TEXT NOT NULL,
    level TEXT NOT NULL,
    via TEXT NOT NULL
);

CREATE FUNCTION fn_log_event(p_message TEXT, p_level TEXT DEFAULT 'info')
RETURNS VOID AS $$
BEGIN
    INSERT INTO audit_log (message, level, via) VALUES (p_message, p_level, 'function');
END;
$$ LANGUAGE plpgsql;

CREATE PROCEDURE pr_log_event(p_message TEXT, p_level TEXT DEFAULT 'info')
AS $$
BEGIN
    INSERT INTO audit_log (message, level, via) VALUES (p_message, p_level, 'procedure');
END;
$$ LANGUAGE plpgsql;

-- Procedures for the doctor write paths (the sample only has functions)
CREATE PROCEDURE pr_create_doctor(p_name TEXT, p_specialty TEXT, p_crm TEXT, INOUT p_id UUID DEFAULT NULL)
AS $$
BEGIN
    INSERT INTO doctors (name, specialty, crm) VALUES (p_name, p_specialty, p_crm)
    RETURNING id INTO p_id;
END;
$$ LANGUAGE plpgsql;

CREATE PROCEDURE pr_update_doctor(p_id UUID, p_specialty TEXT)
AS $$
BEGIN
    UPDATE doctors SET specialty = p_specialty WHERE id = p_id;
END;
$$ LANGUAGE plpgsql;

CREATE PROCEDURE pr_delete_doctor(p_id UUID)
AS $$
BEGIN
    DELETE FROM doctors WHERE id = p_id;
END;
$$ LANGUAGE plpgsql;
