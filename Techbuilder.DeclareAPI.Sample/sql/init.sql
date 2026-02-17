-- DeclareAPI Sample Database Schema
-- PostgreSQL 16+

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Tables
CREATE TABLE patients (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(200) NOT NULL,
    birth_date DATE NOT NULL,
    cpf VARCHAR(11) UNIQUE,
    status VARCHAR(20) DEFAULT 'active',
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE doctors (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(200) NOT NULL,
    specialty VARCHAR(100),
    crm VARCHAR(20) UNIQUE NOT NULL
);

CREATE TABLE appointments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    patient_id UUID REFERENCES patients(id),
    doctor_id UUID REFERENCES doctors(id),
    appointment_date TIMESTAMP NOT NULL,
    notes TEXT,
    status VARCHAR(20) DEFAULT 'scheduled',
    created_at TIMESTAMP DEFAULT NOW()
);

-- Views (what DeclareAPI will consume)
CREATE VIEW vw_patients_active AS
SELECT id, name, birth_date, cpf, status, created_at
FROM patients
WHERE status = 'active'
ORDER BY name;

CREATE VIEW vw_patient_detail AS
SELECT p.id, p.name, p.birth_date, p.cpf, p.status,
       p.created_at, p.updated_at,
       COUNT(a.id) as total_appointments,
       MAX(a.appointment_date) as last_appointment
FROM patients p
LEFT JOIN appointments a ON a.patient_id = p.id
GROUP BY p.id;

CREATE VIEW vw_appointments AS
SELECT a.id, a.appointment_date, a.notes, a.status,
       a.patient_id, p.name as patient_name,
       a.doctor_id, d.name as doctor_name, d.specialty
FROM appointments a
JOIN patients p ON p.id = a.patient_id
JOIN doctors d ON d.id = a.doctor_id;

-- Stored Procedures / Functions
CREATE OR REPLACE FUNCTION sp_create_patient(
    p_name VARCHAR(200),
    p_birth_date DATE,
    p_cpf VARCHAR(11),
    p_guardian_id UUID DEFAULT NULL
)
RETURNS UUID AS $$
DECLARE
    new_id UUID;
BEGIN
    INSERT INTO patients (name, birth_date, cpf)
    VALUES (p_name, p_birth_date, p_cpf)
    RETURNING id INTO new_id;

    RETURN new_id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_update_patient(
    p_id UUID,
    p_name VARCHAR(200) DEFAULT NULL,
    p_guardian_id UUID DEFAULT NULL
)
RETURNS VOID AS $$
BEGIN
    UPDATE patients
    SET name = COALESCE(p_name, name),
        updated_at = NOW()
    WHERE id = p_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Patient % not found', p_id;
    END IF;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_delete_patient(p_id UUID)
RETURNS VOID AS $$
BEGIN
    UPDATE patients SET status = 'inactive', updated_at = NOW()
    WHERE id = p_id;
END;
$$ LANGUAGE plpgsql;

-- Seed data
INSERT INTO doctors (name, specialty, crm) VALUES
('Dra. Larissa Silva', 'Neuropediatria', 'CRM-CE 12345'),
('Dr. Carlos Mendes', 'Pediatria', 'CRM-CE 67890'),
('Dra. Ana Costa', 'Cardiologia', 'CRM-CE 11111');

INSERT INTO patients (name, birth_date, cpf) VALUES
('João Pedro Silva', '2020-03-15', '12345678901'),
('Maria Clara Santos', '2019-07-22', '98765432100'),
('Lucas Oliveira', '2021-01-10', '45678912300'),
('Ana Beatriz Lima', '2018-11-05', '78912345600');

INSERT INTO appointments (patient_id, doctor_id, appointment_date, notes)
SELECT p.id, d.id, NOW() + INTERVAL '7 days', 'Consulta de rotina'
FROM patients p, doctors d
WHERE p.name = 'João Pedro Silva' AND d.crm = 'CRM-CE 12345';

INSERT INTO appointments (patient_id, doctor_id, appointment_date, notes, status)
SELECT p.id, d.id, NOW() - INTERVAL '30 days', 'Retorno', 'completed'
FROM patients p, doctors d
WHERE p.name = 'Maria Clara Santos' AND d.crm = 'CRM-CE 67890';
