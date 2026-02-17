-- ============================================================================
-- Veridiana - Database Schema
-- Generated: 2026-02-17 04:18:12 UTC
-- ============================================================================

-- Enable UUID generation
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ============================================================================
-- DROP EXISTING TABLES (reverse dependency order)
-- ============================================================================

DROP TABLE IF EXISTS faq CASCADE;
DROP TABLE IF EXISTS system_updates CASCADE;
DROP TABLE IF EXISTS migration_log CASCADE;
DROP TABLE IF EXISTS audit_logs CASCADE;
DROP TABLE IF EXISTS user_favorites CASCADE;
DROP TABLE IF EXISTS subscription_history CASCADE;
DROP TABLE IF EXISTS plans CASCADE;
DROP TABLE IF EXISTS ai_usage_limits CASCADE;
DROP TABLE IF EXISTS ai_tools_results CASCADE;
DROP TABLE IF EXISTS attachments CASCADE;
DROP TABLE IF EXISTS medical_transcriptions CASCADE;
DROP TABLE IF EXISTS workspace_assistants CASCADE;
DROP TABLE IF EXISTS assistant_tools CASCADE;
DROP TABLE IF EXISTS assistants CASCADE;
DROP TABLE IF EXISTS output_templates CASCADE;
DROP TABLE IF EXISTS workspace_context_presets CASCADE;
DROP TABLE IF EXISTS tool_execution_results CASCADE;
DROP TABLE IF EXISTS workspace_results CASCADE;
DROP TABLE IF EXISTS workspace_tools CASCADE;
DROP TABLE IF EXISTS workspaces CASCADE;
DROP TABLE IF EXISTS calculation_functions CASCADE;
DROP TABLE IF EXISTS calculation_constants CASCADE;
DROP TABLE IF EXISTS intelligent_tools_config CASCADE;
DROP TABLE IF EXISTS auxiliary_tools_config CASCADE;
DROP TABLE IF EXISTS tool_groups CASCADE;
DROP TABLE IF EXISTS groups CASCADE;
DROP TABLE IF EXISTS tools CASCADE;
DROP TABLE IF EXISTS auth_audit_log CASCADE;
DROP TABLE IF EXISTS login_attempts CASCADE;
DROP TABLE IF EXISTS email_verification_tokens CASCADE;
DROP TABLE IF EXISTS password_reset_tokens CASCADE;
DROP TABLE IF EXISTS refresh_tokens CASCADE;
DROP TABLE IF EXISTS user_sessions CASCADE;
DROP TABLE IF EXISTS platform_admins CASCADE;
DROP TABLE IF EXISTS medical_specialties CASCADE;
DROP TABLE IF EXISTS customer_profiles CASCADE;
DROP TABLE IF EXISTS users CASCADE;

-- ============================================================================
-- CREATE TABLES
-- ============================================================================

CREATE TABLE users (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    email VARCHAR(255) NOT NULL UNIQUE,
    first_name VARCHAR(255),
    last_name VARCHAR(255),
    cpf VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255),
    email_verified BOOLEAN,
    provider VARCHAR(255),
    provider_id VARCHAR(255),
    user_type VARCHAR(255),
    is_active BOOLEAN,
    avatar_url VARCHAR(255),
    last_login TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE customer_profiles (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    user_id UUID NOT NULL UNIQUE,
    plan_id UUID,
    specialty_id UUID,
    crm_number VARCHAR(255),
    crm_state VARCHAR(255),
    specialty VARCHAR(255),
    clinic_name VARCHAR(255),
    clinic_address TEXT,
    clinic_phone VARCHAR(255),
    subscription_status VARCHAR(255),
    subscription_expiry DATE,
    subscription_started_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    subscription_ends_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    free_trial_ends_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    free_plan_expires_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    stripe_customer_id VARCHAR(255),
    stripe_subscription_id VARCHAR(255),
    ai_tools_used_today INTEGER,
    ai_tools_reset_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    transcription_minutes_used_this_month INTEGER,
    transcription_reset_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    last_ai_tool_usage TIMESTAMP WITH TIME ZONE,
    preferences JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE medical_specialties (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    key VARCHAR(255) NOT NULL UNIQUE,
    name VARCHAR(255),
    description TEXT,
    is_active BOOLEAN,
    display_order INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE platform_admins (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    user_id UUID NOT NULL UNIQUE,
    can_manage_users BOOLEAN,
    can_manage_workspaces BOOLEAN,
    can_manage_tools BOOLEAN,
    can_export_data BOOLEAN,
    can_view_analytics BOOLEAN,
    department VARCHAR(255),
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE user_sessions (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    user_id UUID,
    session_token VARCHAR(255) NOT NULL UNIQUE,
    refresh_token VARCHAR(255),
    expires_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    ip_address INET,
    user_agent TEXT,
    is_active BOOLEAN,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE refresh_tokens (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    user_id UUID,
    token VARCHAR(255) NOT NULL UNIQUE,
    expires_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    revoked BOOLEAN,
    revoked_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    replaced_by_token VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE password_reset_tokens (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    user_id UUID,
    token VARCHAR(255) NOT NULL UNIQUE,
    expires_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    used BOOLEAN,
    used_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    ip_address INET,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE email_verification_tokens (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    user_id UUID,
    token VARCHAR(255) NOT NULL UNIQUE,
    expires_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    verified BOOLEAN,
    verified_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE login_attempts (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    email VARCHAR(255),
    ip_address INET,
    success BOOLEAN,
    attempted_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    user_agent TEXT,
    failure_reason VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE auth_audit_log (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    user_id VARCHAR(255),
    action VARCHAR(255),
    details JSONB,
    ip_address INET,
    user_agent TEXT,
    success BOOLEAN,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE tools (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE,
    title VARCHAR(255),
    description TEXT,
    key VARCHAR(255),
    category VARCHAR(255),
    tool_role TOOL_ROLE,
    is_enabled BOOLEAN,
    is_public BOOLEAN,
    requires_email BOOLEAN,
    requires_premium BOOLEAN,
    icon VARCHAR(255),
    instructions VARCHAR(255),
    custom_action_button VARCHAR(255),
    form_config JSONB,
    group_id UUID,
    created_by UUID,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE groups (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE,
    description TEXT,
    icon VARCHAR(255),
    color VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE tool_groups (
    tool_id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    group_id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE auxiliary_tools_config (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    tool_id UUID NOT NULL UNIQUE,
    processing_type VARCHAR(255),
    calculation_engine VARCHAR(255),
    uses_dynamic_calculation BOOLEAN,
    processing_config JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE intelligent_tools_config (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    tool_id UUID NOT NULL UNIQUE,
    ai_model VARCHAR(255),
    ai_temperature NUMERIC,
    ai_max_tokens INTEGER,
    system_prompt TEXT,
    user_prompt_template TEXT,
    available_variables TEXT[],
    is_attachment_processor BOOLEAN,
    total_ai_calls INTEGER,
    last_ai_call_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE calculation_constants (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    tool_id UUID,
    constant_name VARCHAR(255),
    constant_type VARCHAR(255),
    constant_value JSONB,
    conditions JSONB,
    description TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE calculation_functions (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    tool_id UUID,
    function_name VARCHAR(255),
    function_type VARCHAR(255),
    function_body TEXT,
    parameters JSONB,
    validation_rules JSONB,
    version INTEGER,
    is_active BOOLEAN,
    created_by VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE workspaces (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    name VARCHAR(255),
    key VARCHAR(255) NOT NULL UNIQUE,
    description TEXT,
    visibility VARCHAR(255),
    created_by UUID,
    owner_id UUID,
    situational_context JSONB,
    max_consultation_minutes INTEGER,
    environment_type VARCHAR(255),
    context_preset_id UUID,
    is_active BOOLEAN,
    display_order INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE workspace_tools (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    workspace_id UUID,
    tool_id UUID,
    display_order INTEGER,
    is_required BOOLEAN,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE workspace_results (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    workspace_id UUID,
    user_id UUID,
    assistant_id UUID,
    transcription_id INTEGER,
    status VARCHAR(255),
    medical_notes TEXT,
    assistant_prompt_used JSONB,
    assistant_response TEXT,
    ai_model_used VARCHAR(255),
    assistant_model_used VARCHAR(255),
    assistant_tokens_used INTEGER,
    assistant_processing_time_ms INTEGER,
    error_message TEXT,
    started_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    completed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE tool_execution_results (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    workspace_result_id UUID,
    tool_id UUID,
    user_id UUID,
    execution_context VARCHAR(255),
    execution_role TOOL_ROLE,
    input_data JSONB,
    output_data JSONB,
    ai_model_used VARCHAR(255),
    tokens_used INTEGER,
    processing_time_ms INTEGER,
    status VARCHAR(255),
    error_message TEXT,
    executed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE workspace_context_presets (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    key VARCHAR(255) NOT NULL UNIQUE,
    name VARCHAR(255),
    description TEXT,
    environment_type VARCHAR(255),
    context JSONB,
    target_specialty VARCHAR(255),
    is_active BOOLEAN,
    display_order INTEGER,
    created_by UUID,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE output_templates (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    name VARCHAR(255),
    key VARCHAR(255) NOT NULL UNIQUE,
    description TEXT,
    domain VARCHAR(255),
    tabs JSONB,
    rendering_config JSONB,
    is_default BOOLEAN,
    created_by UUID,
    is_active BOOLEAN,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE assistants (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    name VARCHAR(255),
    key VARCHAR(255),
    description TEXT,
    visibility VARCHAR(255),
    model VARCHAR(255),
    temperature NUMERIC,
    max_tokens INTEGER,
    system_prompt TEXT,
    user_prompt_template TEXT,
    prompt_config JSONB,
    output_template_id UUID,
    specialty_id UUID,
    avatar_url VARCHAR(255),
    specialty VARCHAR(255),
    created_by UUID,
    owner_id UUID,
    display_order INTEGER,
    is_active BOOLEAN,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE assistant_tools (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    assistant_id UUID,
    tool_id UUID,
    display_order INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE workspace_assistants (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    workspace_id UUID,
    assistant_id UUID,
    display_order INTEGER,
    is_default BOOLEAN,
    output_template_id UUID,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE medical_transcriptions (
    id INTEGER NOT NULL PRIMARY KEY,
    session_id VARCHAR(255),
    full_text TEXT,
    metadata JSONB,
    model VARCHAR(255),
    language VARCHAR(255),
    started_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    completed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    duration_seconds INTEGER,
    average_confidence NUMERIC,
    total_words INTEGER,
    is_edited BOOLEAN,
    edited_text TEXT,
    edited_by UUID,
    edited_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    anonymized BOOLEAN,
    original_hash VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE attachments (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    file_id UUID NOT NULL UNIQUE,
    filename VARCHAR(255),
    original_filename VARCHAR(255),
    mime_type VARCHAR(255),
    file_size INTEGER,
    file_extension VARCHAR(255),
    azure_blob_url TEXT,
    azure_container_name VARCHAR(255),
    azure_blob_name VARCHAR(255),
    extracted_text TEXT,
    minified_content TEXT,
    is_processed BOOLEAN,
    workspace_result_id UUID,
    is_deleted BOOLEAN,
    deleted_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    uploaded_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE ai_tools_results (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    user_id UUID,
    tool_id UUID,
    transcription_id INTEGER,
    workspace_id UUID,
    input_data JSONB,
    output_data JSONB,
    ai_model_used VARCHAR(255),
    tokens_used INTEGER,
    processing_time_ms INTEGER,
    error_message TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE ai_usage_limits (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    subscription_type VARCHAR(255) NOT NULL UNIQUE,
    daily_limit INTEGER,
    monthly_limit INTEGER,
    can_create_tools BOOLEAN,
    max_custom_tools INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE plans (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    key VARCHAR(255) NOT NULL UNIQUE,
    name VARCHAR(255),
    description TEXT,
    price_cents INTEGER,
    billing_interval VARCHAR(255),
    stripe_price_id VARCHAR(255),
    stripe_product_id VARCHAR(255),
    ai_daily_limit INTEGER,
    ai_monthly_limit INTEGER,
    max_tokens_per_request INTEGER,
    allowed_models TEXT[],
    transcription_monthly_minutes INTEGER,
    trial_duration_days INTEGER,
    plan_duration_days INTEGER,
    features JSONB,
    is_active BOOLEAN,
    display_order INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE TABLE subscription_history (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    user_id UUID,
    plan_id UUID,
    previous_status VARCHAR(255),
    new_status VARCHAR(255),
    change_reason VARCHAR(255),
    changed_by VARCHAR(255),
    metadata JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE user_favorites (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    user_id UUID,
    entity_type VARCHAR(255),
    entity_id UUID,
    display_order INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE audit_logs (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    entity_type VARCHAR(255),
    entity_uuid UUID,
    entity_id INTEGER,
    action VARCHAR(255),
    user_id UUID,
    ip_address INET,
    user_agent TEXT,
    old_values JSONB,
    new_values JSONB,
    metadata JSONB,
    contains_personal_data BOOLEAN,
    data_retention_date DATE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE migration_log (
    id INTEGER NOT NULL PRIMARY KEY,
    phase VARCHAR(255),
    step VARCHAR(255),
    status VARCHAR(255),
    message TEXT,
    executed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE system_updates (
    id INTEGER NOT NULL PRIMARY KEY,
    name VARCHAR(255),
    update_type VARCHAR(255),
    release_date TIMESTAMP WITH TIME ZONE,
    release_notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

CREATE TABLE faq (
    id UUID DEFAULT gen_random_uuid() NOT NULL PRIMARY KEY,
    question VARCHAR(255),
    answer TEXT,
    order_number INTEGER,
    is_active BOOLEAN,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW() NOT NULL
);

-- ============================================================================
-- INDEXES
-- ============================================================================

CREATE UNIQUE INDEX idx_users_email ON users(email);
CREATE UNIQUE INDEX idx_users_cpf ON users(cpf);
CREATE INDEX idx_users_email_verified ON users(email_verified);
CREATE INDEX idx_users_provider_id ON users(provider_id);
CREATE INDEX idx_users_user_type ON users(user_type);
CREATE INDEX idx_users_created_at ON users(created_at DESC);
CREATE INDEX idx_customer_profiles_user_id ON customer_profiles(user_id);
CREATE INDEX idx_customer_profiles_plan_id ON customer_profiles(plan_id);
CREATE INDEX idx_customer_profiles_specialty_id ON customer_profiles(specialty_id);
CREATE INDEX idx_customer_profiles_subscription_status ON customer_profiles(subscription_status);
CREATE INDEX idx_customer_profiles_stripe_customer_id ON customer_profiles(stripe_customer_id);
CREATE INDEX idx_customer_profiles_stripe_subscription_id ON customer_profiles(stripe_subscription_id);
CREATE INDEX idx_customer_profiles_created_at ON customer_profiles(created_at DESC);
CREATE UNIQUE INDEX idx_medical_specialties_key ON medical_specialties(key);
CREATE INDEX idx_medical_specialties_name ON medical_specialties(name);
CREATE INDEX idx_medical_specialties_created_at ON medical_specialties(created_at DESC);
CREATE INDEX idx_platform_admins_user_id ON platform_admins(user_id);
CREATE INDEX idx_platform_admins_created_at ON platform_admins(created_at DESC);
CREATE INDEX idx_user_sessions_user_id ON user_sessions(user_id);
CREATE UNIQUE INDEX idx_user_sessions_session_token ON user_sessions(session_token);
CREATE INDEX idx_user_sessions_created_at ON user_sessions(created_at DESC);
CREATE INDEX idx_refresh_tokens_user_id ON refresh_tokens(user_id);
CREATE UNIQUE INDEX idx_refresh_tokens_token ON refresh_tokens(token);
CREATE INDEX idx_refresh_tokens_created_at ON refresh_tokens(created_at DESC);
CREATE INDEX idx_password_reset_tokens_user_id ON password_reset_tokens(user_id);
CREATE UNIQUE INDEX idx_password_reset_tokens_token ON password_reset_tokens(token);
CREATE INDEX idx_password_reset_tokens_created_at ON password_reset_tokens(created_at DESC);
CREATE INDEX idx_email_verification_tokens_user_id ON email_verification_tokens(user_id);
CREATE UNIQUE INDEX idx_email_verification_tokens_token ON email_verification_tokens(token);
CREATE INDEX idx_email_verification_tokens_created_at ON email_verification_tokens(created_at DESC);
CREATE INDEX idx_login_attempts_email ON login_attempts(email);
CREATE INDEX idx_login_attempts_created_at ON login_attempts(created_at DESC);
CREATE INDEX idx_auth_audit_log_user_id ON auth_audit_log(user_id);
CREATE INDEX idx_auth_audit_log_created_at ON auth_audit_log(created_at DESC);
CREATE UNIQUE INDEX idx_tools_name ON tools(name);
CREATE INDEX idx_tools_key ON tools(key);
CREATE INDEX idx_tools_requires_email ON tools(requires_email);
CREATE INDEX idx_tools_group_id ON tools(group_id);
CREATE INDEX idx_tools_created_by ON tools(created_by);
CREATE INDEX idx_tools_created_at ON tools(created_at DESC);
CREATE UNIQUE INDEX idx_groups_name ON groups(name);
CREATE INDEX idx_groups_created_at ON groups(created_at DESC);
CREATE INDEX idx_tool_groups_created_at ON tool_groups(created_at DESC);
CREATE INDEX idx_auxiliary_tools_config_tool_id ON auxiliary_tools_config(tool_id);
CREATE INDEX idx_auxiliary_tools_config_processing_type ON auxiliary_tools_config(processing_type);
CREATE INDEX idx_auxiliary_tools_config_created_at ON auxiliary_tools_config(created_at DESC);
CREATE INDEX idx_intelligent_tools_config_tool_id ON intelligent_tools_config(tool_id);
CREATE INDEX idx_intelligent_tools_config_created_at ON intelligent_tools_config(created_at DESC);
CREATE INDEX idx_calculation_constants_tool_id ON calculation_constants(tool_id);
CREATE INDEX idx_calculation_constants_constant_type ON calculation_constants(constant_type);
CREATE INDEX idx_calculation_constants_created_at ON calculation_constants(created_at DESC);
CREATE INDEX idx_calculation_functions_tool_id ON calculation_functions(tool_id);
CREATE INDEX idx_calculation_functions_function_type ON calculation_functions(function_type);
CREATE INDEX idx_calculation_functions_created_at ON calculation_functions(created_at DESC);
CREATE INDEX idx_workspaces_name ON workspaces(name);
CREATE UNIQUE INDEX idx_workspaces_key ON workspaces(key);
CREATE INDEX idx_workspaces_created_by ON workspaces(created_by);
CREATE INDEX idx_workspaces_owner_id ON workspaces(owner_id);
CREATE INDEX idx_workspaces_environment_type ON workspaces(environment_type);
CREATE INDEX idx_workspaces_context_preset_id ON workspaces(context_preset_id);
CREATE INDEX idx_workspaces_created_at ON workspaces(created_at DESC);
CREATE INDEX idx_workspace_tools_workspace_id ON workspace_tools(workspace_id);
CREATE INDEX idx_workspace_tools_tool_id ON workspace_tools(tool_id);
CREATE INDEX idx_workspace_tools_created_at ON workspace_tools(created_at DESC);
CREATE INDEX idx_workspace_results_workspace_id ON workspace_results(workspace_id);
CREATE INDEX idx_workspace_results_user_id ON workspace_results(user_id);
CREATE INDEX idx_workspace_results_assistant_id ON workspace_results(assistant_id);
CREATE INDEX idx_workspace_results_transcription_id ON workspace_results(transcription_id);
CREATE INDEX idx_workspace_results_status ON workspace_results(status);
CREATE INDEX idx_workspace_results_created_at ON workspace_results(created_at DESC);
CREATE INDEX idx_tool_execution_results_workspace_result_id ON tool_execution_results(workspace_result_id);
CREATE INDEX idx_tool_execution_results_tool_id ON tool_execution_results(tool_id);
CREATE INDEX idx_tool_execution_results_user_id ON tool_execution_results(user_id);
CREATE INDEX idx_tool_execution_results_status ON tool_execution_results(status);
CREATE INDEX idx_tool_execution_results_created_at ON tool_execution_results(created_at DESC);
CREATE UNIQUE INDEX idx_workspace_context_presets_key ON workspace_context_presets(key);
CREATE INDEX idx_workspace_context_presets_name ON workspace_context_presets(name);
CREATE INDEX idx_workspace_context_presets_environment_type ON workspace_context_presets(environment_type);
CREATE INDEX idx_workspace_context_presets_created_by ON workspace_context_presets(created_by);
CREATE INDEX idx_workspace_context_presets_created_at ON workspace_context_presets(created_at DESC);
CREATE INDEX idx_output_templates_name ON output_templates(name);
CREATE UNIQUE INDEX idx_output_templates_key ON output_templates(key);
CREATE INDEX idx_output_templates_created_by ON output_templates(created_by);
CREATE INDEX idx_output_templates_created_at ON output_templates(created_at DESC);
CREATE INDEX idx_assistants_name ON assistants(name);
CREATE INDEX idx_assistants_key ON assistants(key);
CREATE INDEX idx_assistants_output_template_id ON assistants(output_template_id);
CREATE INDEX idx_assistants_specialty_id ON assistants(specialty_id);
CREATE INDEX idx_assistants_created_by ON assistants(created_by);
CREATE INDEX idx_assistants_owner_id ON assistants(owner_id);
CREATE INDEX idx_assistants_created_at ON assistants(created_at DESC);
CREATE INDEX idx_assistant_tools_assistant_id ON assistant_tools(assistant_id);
CREATE INDEX idx_assistant_tools_tool_id ON assistant_tools(tool_id);
CREATE INDEX idx_assistant_tools_created_at ON assistant_tools(created_at DESC);
CREATE INDEX idx_workspace_assistants_workspace_id ON workspace_assistants(workspace_id);
CREATE INDEX idx_workspace_assistants_assistant_id ON workspace_assistants(assistant_id);
CREATE INDEX idx_workspace_assistants_output_template_id ON workspace_assistants(output_template_id);
CREATE INDEX idx_workspace_assistants_created_at ON workspace_assistants(created_at DESC);
CREATE INDEX idx_medical_transcriptions_session_id ON medical_transcriptions(session_id);
CREATE INDEX idx_medical_transcriptions_edited_by ON medical_transcriptions(edited_by);
CREATE INDEX idx_medical_transcriptions_created_at ON medical_transcriptions(created_at DESC);
CREATE INDEX idx_attachments_file_id ON attachments(file_id);
CREATE INDEX idx_attachments_mime_type ON attachments(mime_type);
CREATE INDEX idx_attachments_workspace_result_id ON attachments(workspace_result_id);
CREATE INDEX idx_attachments_created_at ON attachments(created_at DESC);
CREATE INDEX idx_ai_tools_results_user_id ON ai_tools_results(user_id);
CREATE INDEX idx_ai_tools_results_tool_id ON ai_tools_results(tool_id);
CREATE INDEX idx_ai_tools_results_transcription_id ON ai_tools_results(transcription_id);
CREATE INDEX idx_ai_tools_results_workspace_id ON ai_tools_results(workspace_id);
CREATE INDEX idx_ai_tools_results_created_at ON ai_tools_results(created_at DESC);
CREATE UNIQUE INDEX idx_ai_usage_limits_subscription_type ON ai_usage_limits(subscription_type);
CREATE INDEX idx_ai_usage_limits_created_at ON ai_usage_limits(created_at DESC);
CREATE UNIQUE INDEX idx_plans_key ON plans(key);
CREATE INDEX idx_plans_name ON plans(name);
CREATE INDEX idx_plans_stripe_price_id ON plans(stripe_price_id);
CREATE INDEX idx_plans_stripe_product_id ON plans(stripe_product_id);
CREATE INDEX idx_plans_created_at ON plans(created_at DESC);
CREATE INDEX idx_subscription_history_user_id ON subscription_history(user_id);
CREATE INDEX idx_subscription_history_plan_id ON subscription_history(plan_id);
CREATE INDEX idx_subscription_history_previous_status ON subscription_history(previous_status);
CREATE INDEX idx_subscription_history_new_status ON subscription_history(new_status);
CREATE INDEX idx_subscription_history_created_at ON subscription_history(created_at DESC);
CREATE INDEX idx_user_favorites_user_id ON user_favorites(user_id);
CREATE INDEX idx_user_favorites_entity_type ON user_favorites(entity_type);
CREATE INDEX idx_user_favorites_entity_id ON user_favorites(entity_id);
CREATE INDEX idx_user_favorites_created_at ON user_favorites(created_at DESC);
CREATE INDEX idx_audit_logs_entity_type ON audit_logs(entity_type);
CREATE INDEX idx_audit_logs_entity_id ON audit_logs(entity_id);
CREATE INDEX idx_audit_logs_user_id ON audit_logs(user_id);
CREATE INDEX idx_audit_logs_created_at ON audit_logs(created_at DESC);
CREATE INDEX idx_migration_log_status ON migration_log(status);
CREATE INDEX idx_migration_log_created_at ON migration_log(created_at DESC);
CREATE INDEX idx_system_updates_name ON system_updates(name);
CREATE INDEX idx_system_updates_update_type ON system_updates(update_type);
CREATE INDEX idx_system_updates_created_at ON system_updates(created_at DESC);
CREATE INDEX idx_faq_created_at ON faq(created_at DESC);

-- ============================================================================
-- FOREIGN KEY CONSTRAINTS
-- ============================================================================


-- ============================================================================
-- COMMENTS
-- ============================================================================

COMMENT ON COLUMN users.id IS 'Identificador único';
COMMENT ON COLUMN users.email IS 'Email para login (RF-001)';
COMMENT ON COLUMN users.first_name IS 'Nome';
COMMENT ON COLUMN users.last_name IS 'Sobrenome';
COMMENT ON COLUMN users.cpf IS 'CPF (opcional)';
COMMENT ON COLUMN users.password_hash IS 'Bcrypt hash (nullable se OAuth)';
COMMENT ON COLUMN users.email_verified IS 'Email verificado';
COMMENT ON COLUMN users.provider IS 'local|google';
COMMENT ON COLUMN users.provider_id IS 'ID do provider OAuth';
COMMENT ON COLUMN users.user_type IS 'customer|admin|support|master';
COMMENT ON COLUMN users.is_active IS 'Conta ativa';
COMMENT ON COLUMN users.avatar_url IS 'URL do avatar';
COMMENT ON COLUMN users.last_login IS 'Último login';
COMMENT ON COLUMN customer_profiles.user_id IS '1:1 com users';
COMMENT ON COLUMN customer_profiles.plan_id IS 'Plano atual (FK para plans)';
COMMENT ON COLUMN customer_profiles.specialty_id IS 'FK para medical_specialties (normalizado)';
COMMENT ON COLUMN customer_profiles.crm_number IS 'Número CRM';
COMMENT ON COLUMN customer_profiles.crm_state IS 'Estado do CRM';
COMMENT ON COLUMN customer_profiles.specialty IS 'Especialidade médica (deprecated)';
COMMENT ON COLUMN customer_profiles.clinic_name IS 'Nome da clínica';
COMMENT ON COLUMN customer_profiles.clinic_address IS 'Endereço';
COMMENT ON COLUMN customer_profiles.clinic_phone IS 'Telefone';
COMMENT ON COLUMN customer_profiles.subscription_status IS 'free|trial|premium|cancelled (RN-010)';
COMMENT ON COLUMN customer_profiles.subscription_expiry IS 'Expiração da assinatura';
COMMENT ON COLUMN customer_profiles.subscription_started_at IS 'Início da assinatura';
COMMENT ON COLUMN customer_profiles.subscription_ends_at IS 'Fim da assinatura';
COMMENT ON COLUMN customer_profiles.free_trial_ends_at IS 'Fim do trial';
COMMENT ON COLUMN customer_profiles.free_plan_expires_at IS 'Expiração do plano free';
COMMENT ON COLUMN customer_profiles.stripe_customer_id IS 'ID Stripe (RF-011)';
COMMENT ON COLUMN customer_profiles.stripe_subscription_id IS 'Subscription Stripe';
COMMENT ON COLUMN customer_profiles.ai_tools_used_today IS 'Uso diário IA (RN-012)';
COMMENT ON COLUMN customer_profiles.ai_tools_reset_at IS 'Reset do uso diário';
COMMENT ON COLUMN customer_profiles.transcription_minutes_used_this_month IS 'Minutos usados no mês';
COMMENT ON COLUMN customer_profiles.transcription_reset_at IS 'Reset mensal transcrição';
COMMENT ON COLUMN customer_profiles.last_ai_tool_usage IS 'Último uso IA';
COMMENT ON COLUMN customer_profiles.preferences IS 'Preferências do usuário';
COMMENT ON COLUMN medical_specialties.key IS 'Chave única (ex: pediatria)';
COMMENT ON COLUMN medical_specialties.name IS 'Nome para exibição';
COMMENT ON COLUMN medical_specialties.description IS 'Descrição opcional';
COMMENT ON COLUMN medical_specialties.is_active IS 'Especialidade ativa';
COMMENT ON COLUMN medical_specialties.display_order IS 'Ordem de exibição';
COMMENT ON COLUMN platform_admins.user_id IS '1:1 com users';
COMMENT ON COLUMN platform_admins.can_manage_users IS 'Gerenciar usuários';
COMMENT ON COLUMN platform_admins.can_manage_workspaces IS 'Gerenciar workspaces (RF-022)';
COMMENT ON COLUMN platform_admins.can_manage_tools IS 'Gerenciar ferramentas (RF-035)';
COMMENT ON COLUMN platform_admins.can_export_data IS 'Exportar dados';
COMMENT ON COLUMN platform_admins.can_view_analytics IS 'Ver analytics';
COMMENT ON COLUMN platform_admins.department IS 'Departamento';
COMMENT ON COLUMN platform_admins.notes IS 'Observações';
COMMENT ON COLUMN user_sessions.session_token IS 'Token de sessão';
COMMENT ON COLUMN user_sessions.refresh_token IS 'Token de refresh';
COMMENT ON COLUMN user_sessions.expires_at IS 'Expiração (RNF-SEG-001)';
COMMENT ON COLUMN user_sessions.ip_address IS 'IP do cliente';
COMMENT ON COLUMN user_sessions.user_agent IS 'User agent';
COMMENT ON COLUMN user_sessions.is_active IS 'Sessão ativa';
COMMENT ON COLUMN refresh_tokens.token IS 'JWT refresh token';
COMMENT ON COLUMN refresh_tokens.expires_at IS '7 dias (RNF-SEG-001)';
COMMENT ON COLUMN refresh_tokens.revoked IS 'Token revogado';
COMMENT ON COLUMN refresh_tokens.revoked_at IS 'Quando revogado';
COMMENT ON COLUMN refresh_tokens.replaced_by_token IS 'Token substituto';
COMMENT ON COLUMN password_reset_tokens.token IS 'Token de reset (RF-004)';
COMMENT ON COLUMN password_reset_tokens.expires_at IS '1 hora';
COMMENT ON COLUMN password_reset_tokens.used IS 'Token usado';
COMMENT ON COLUMN password_reset_tokens.used_at IS 'Quando usado';
COMMENT ON COLUMN password_reset_tokens.ip_address IS 'IP da solicitação';
COMMENT ON COLUMN email_verification_tokens.token IS 'Token de verificação';
COMMENT ON COLUMN email_verification_tokens.expires_at IS '24 horas';
COMMENT ON COLUMN email_verification_tokens.verified IS 'Email verificado';
COMMENT ON COLUMN email_verification_tokens.verified_at IS 'Quando verificou';
COMMENT ON COLUMN login_attempts.email IS 'Email tentado';
COMMENT ON COLUMN login_attempts.ip_address IS 'IP da tentativa';
COMMENT ON COLUMN login_attempts.success IS 'Sucesso/Falha';
COMMENT ON COLUMN login_attempts.attempted_at IS 'Momento da tentativa';
COMMENT ON COLUMN login_attempts.user_agent IS 'User agent';
COMMENT ON COLUMN login_attempts.failure_reason IS 'Motivo da falha';
COMMENT ON COLUMN auth_audit_log.user_id IS 'ID do usuário (pode ser null)';
COMMENT ON COLUMN auth_audit_log.action IS 'login|logout|password_reset|etc';
COMMENT ON COLUMN auth_audit_log.details IS 'Detalhes da ação';
COMMENT ON COLUMN auth_audit_log.ip_address IS 'IP';
COMMENT ON COLUMN auth_audit_log.user_agent IS 'User agent';
COMMENT ON COLUMN auth_audit_log.success IS 'Sucesso/Falha';
COMMENT ON COLUMN tools.name IS 'Nome único';
COMMENT ON COLUMN tools.title IS 'Título de exibição';
COMMENT ON COLUMN tools.description IS 'Descrição';
COMMENT ON COLUMN tools.key IS 'Chave de identificação';
COMMENT ON COLUMN tools.category IS 'auxiliary|intelligent (RN-041)';
COMMENT ON COLUMN tools.tool_role IS 'input|output (RN-041)';
COMMENT ON COLUMN tools.is_enabled IS 'Ferramenta ativa';
COMMENT ON COLUMN tools.is_public IS 'Sempre true (RN-040)';
COMMENT ON COLUMN tools.requires_email IS 'Permite envio email (RF-033)';
COMMENT ON COLUMN tools.requires_premium IS 'Requer premium (RN-043)';
COMMENT ON COLUMN tools.icon IS 'Ícone';
COMMENT ON COLUMN tools.instructions IS 'Instruções de uso';
COMMENT ON COLUMN tools.custom_action_button IS 'Texto botão personalizado';
COMMENT ON COLUMN tools.form_config IS 'Configuração do formulário (RF-031)';
COMMENT ON COLUMN tools.group_id IS 'Grupo principal';
COMMENT ON COLUMN tools.created_by IS 'Criado por (admin)';
COMMENT ON COLUMN groups.name IS 'Nome do grupo';
COMMENT ON COLUMN groups.description IS 'Descrição';
COMMENT ON COLUMN groups.icon IS 'Ícone';
COMMENT ON COLUMN groups.color IS 'Cor';
COMMENT ON COLUMN tool_groups.tool_id IS 'N:M com tools';
COMMENT ON COLUMN tool_groups.group_id IS 'N:M com groups';
COMMENT ON COLUMN auxiliary_tools_config.tool_id IS '1:1 com tools';
COMMENT ON COLUMN auxiliary_tools_config.processing_type IS 'calculation|form|template|custom';
COMMENT ON COLUMN auxiliary_tools_config.calculation_engine IS 'Motor de cálculo';
COMMENT ON COLUMN auxiliary_tools_config.uses_dynamic_calculation IS 'Usa cálculo dinâmico';
COMMENT ON COLUMN auxiliary_tools_config.processing_config IS 'Configuração de processamento';
COMMENT ON COLUMN intelligent_tools_config.tool_id IS '1:1 com tools';
COMMENT ON COLUMN intelligent_tools_config.ai_model IS 'claude-sonnet|gemini-pro (RN-032)';
COMMENT ON COLUMN intelligent_tools_config.ai_temperature IS 'Temperatura (0-1)';
COMMENT ON COLUMN intelligent_tools_config.ai_max_tokens IS 'Max tokens';
COMMENT ON COLUMN intelligent_tools_config.system_prompt IS 'Prompt de sistema';
COMMENT ON COLUMN intelligent_tools_config.user_prompt_template IS 'Template do prompt';
COMMENT ON COLUMN intelligent_tools_config.available_variables IS 'Variáveis disponíveis';
COMMENT ON COLUMN intelligent_tools_config.is_attachment_processor IS 'Processa anexos';
COMMENT ON COLUMN intelligent_tools_config.total_ai_calls IS 'Total de chamadas';
COMMENT ON COLUMN intelligent_tools_config.last_ai_call_at IS 'Última chamada';
COMMENT ON COLUMN calculation_constants.constant_name IS 'Nome da constante';
COMMENT ON COLUMN calculation_constants.constant_type IS 'Tipo';
COMMENT ON COLUMN calculation_constants.constant_value IS 'Valor';
COMMENT ON COLUMN calculation_constants.conditions IS 'Condições de uso';
COMMENT ON COLUMN calculation_constants.description IS 'Descrição';
COMMENT ON COLUMN calculation_functions.function_name IS 'Nome da função';
COMMENT ON COLUMN calculation_functions.function_type IS 'javascript|formula|lookup_table|conditional|multi_step';
COMMENT ON COLUMN calculation_functions.function_body IS 'Corpo da função';
COMMENT ON COLUMN calculation_functions.parameters IS 'Parâmetros';
COMMENT ON COLUMN calculation_functions.validation_rules IS 'Regras de validação';
COMMENT ON COLUMN calculation_functions.version IS 'Versão';
COMMENT ON COLUMN calculation_functions.is_active IS 'Ativa';
COMMENT ON COLUMN calculation_functions.created_by IS 'Criado por';
COMMENT ON COLUMN workspaces.name IS 'Nome do workspace';
COMMENT ON COLUMN workspaces.key IS 'Chave única';
COMMENT ON COLUMN workspaces.description IS 'Descrição';
COMMENT ON COLUMN workspaces.visibility IS 'public|private (RN-021)';
COMMENT ON COLUMN workspaces.created_by IS 'Criador (admin)';
COMMENT ON COLUMN workspaces.owner_id IS 'Dono do workspace (RN-071)';
COMMENT ON COLUMN workspaces.situational_context IS 'Contexto situacional (RN-073)';
COMMENT ON COLUMN workspaces.max_consultation_minutes IS 'Tempo max consulta';
COMMENT ON COLUMN workspaces.environment_type IS 'Tipo de ambiente (5 tipos)';
COMMENT ON COLUMN workspaces.context_preset_id IS 'Preset de contexto';
COMMENT ON COLUMN workspaces.is_active IS 'Ativo';
COMMENT ON COLUMN workspaces.display_order IS 'Ordem de exibição';
COMMENT ON COLUMN workspace_tools.workspace_id IS 'N:M com workspaces';
COMMENT ON COLUMN workspace_tools.tool_id IS 'N:M com tools';
COMMENT ON COLUMN workspace_tools.display_order IS 'Ordem de exibição';
COMMENT ON COLUMN workspace_tools.is_required IS 'Obrigatória';
COMMENT ON COLUMN workspace_results.workspace_id IS 'Workspace da consulta';
COMMENT ON COLUMN workspace_results.user_id IS 'Usuário (RF-050)';
COMMENT ON COLUMN workspace_results.assistant_id IS 'Assistente usado (RF-027)';
COMMENT ON COLUMN workspace_results.transcription_id IS 'Transcrição vinculada';
COMMENT ON COLUMN workspace_results.status IS 'pending|processing|running|completed|failed|partial';
COMMENT ON COLUMN workspace_results.medical_notes IS 'Notas médicas (RF-044)';
COMMENT ON COLUMN workspace_results.assistant_prompt_used IS 'Prompt utilizado';
COMMENT ON COLUMN workspace_results.assistant_response IS 'Resposta do assistente';
COMMENT ON COLUMN workspace_results.ai_model_used IS 'Modelo de IA usado';
COMMENT ON COLUMN workspace_results.assistant_model_used IS 'Modelo do assistente';
COMMENT ON COLUMN workspace_results.assistant_tokens_used IS 'Tokens consumidos';
COMMENT ON COLUMN workspace_results.assistant_processing_time_ms IS 'Tempo de processamento';
COMMENT ON COLUMN workspace_results.error_message IS 'Mensagem de erro';
COMMENT ON COLUMN workspace_results.started_at IS 'Início';
COMMENT ON COLUMN workspace_results.completed_at IS 'Fim';
COMMENT ON COLUMN tool_execution_results.workspace_result_id IS 'Resultado do workspace';
COMMENT ON COLUMN tool_execution_results.tool_id IS 'Ferramenta executada';
COMMENT ON COLUMN tool_execution_results.user_id IS 'Usuário executor';
COMMENT ON COLUMN tool_execution_results.execution_context IS 'workspace|standalone';
COMMENT ON COLUMN tool_execution_results.execution_role IS 'input|output';
COMMENT ON COLUMN tool_execution_results.input_data IS 'Dados de entrada (RF-031)';
COMMENT ON COLUMN tool_execution_results.output_data IS 'Dados de saída (RF-032)';
COMMENT ON COLUMN tool_execution_results.ai_model_used IS 'Modelo IA (se inteligente)';
COMMENT ON COLUMN tool_execution_results.tokens_used IS 'Tokens usados';
COMMENT ON COLUMN tool_execution_results.processing_time_ms IS 'Tempo de processamento';
COMMENT ON COLUMN tool_execution_results.status IS 'pending|processing|completed|failed';
COMMENT ON COLUMN tool_execution_results.error_message IS 'Mensagem de erro';
COMMENT ON COLUMN workspace_context_presets.key IS 'Chave única (ex: uti-pediatrica)';
COMMENT ON COLUMN workspace_context_presets.name IS 'Nome do preset';
COMMENT ON COLUMN workspace_context_presets.description IS 'Descrição';
COMMENT ON COLUMN workspace_context_presets.environment_type IS 'pronto-socorro|internacao|ambulatorio-sus|consultorio-particular|domiciliar';
COMMENT ON COLUMN workspace_context_presets.context IS 'Configuração completa do contexto';
COMMENT ON COLUMN workspace_context_presets.target_specialty IS 'Especialidade alvo (pediatria, etc)';
COMMENT ON COLUMN workspace_context_presets.is_active IS 'Preset ativo';
COMMENT ON COLUMN workspace_context_presets.display_order IS 'Ordem de exibição';
COMMENT ON COLUMN workspace_context_presets.created_by IS 'Criador (admin)';
COMMENT ON COLUMN output_templates.name IS 'Nome do template';
COMMENT ON COLUMN output_templates.key IS 'Chave única';
COMMENT ON COLUMN output_templates.description IS 'Descrição';
COMMENT ON COLUMN output_templates.domain IS 'Domínio (pediatria, ortopedia, etc)';
COMMENT ON COLUMN output_templates.tabs IS 'Configuração de abas';
COMMENT ON COLUMN output_templates.rendering_config IS 'Configuração de renderização';
COMMENT ON COLUMN output_templates.is_default IS 'Template padrão do domínio';
COMMENT ON COLUMN output_templates.created_by IS 'Criador (admin)';
COMMENT ON COLUMN output_templates.is_active IS 'Ativo';
COMMENT ON COLUMN assistants.name IS 'Nome do assistente';
COMMENT ON COLUMN assistants.key IS 'Chave única';
COMMENT ON COLUMN assistants.description IS 'Descrição';
COMMENT ON COLUMN assistants.visibility IS 'public|private (RN-031)';
COMMENT ON COLUMN assistants.model IS 'claude-sonnet|gemini-pro';
COMMENT ON COLUMN assistants.temperature IS 'Temperatura';
COMMENT ON COLUMN assistants.max_tokens IS 'Max tokens';
COMMENT ON COLUMN assistants.system_prompt IS 'Prompt de sistema (RF-028)';
COMMENT ON COLUMN assistants.user_prompt_template IS 'Template do prompt';
COMMENT ON COLUMN assistants.prompt_config IS 'Configuração adicional';
COMMENT ON COLUMN assistants.output_template_id IS 'Template de saída (RN-070)';
COMMENT ON COLUMN assistants.specialty_id IS 'FK para medical_specialties (normalizado)';
COMMENT ON COLUMN assistants.avatar_url IS 'URL do avatar';
COMMENT ON COLUMN assistants.specialty IS 'Especialidade médica (deprecated)';
COMMENT ON COLUMN assistants.created_by IS 'Criador (admin)';
COMMENT ON COLUMN assistants.owner_id IS 'Dono do assistente (RN-071)';
COMMENT ON COLUMN assistants.display_order IS 'Ordem de exibição';
COMMENT ON COLUMN assistants.is_active IS 'Ativo';
COMMENT ON COLUMN assistant_tools.assistant_id IS 'N:M com assistants';
COMMENT ON COLUMN assistant_tools.tool_id IS 'N:M com tools';
COMMENT ON COLUMN assistant_tools.display_order IS 'Ordem';
COMMENT ON COLUMN workspace_assistants.workspace_id IS 'N:M com workspaces';
COMMENT ON COLUMN workspace_assistants.assistant_id IS 'N:M com assistants';
COMMENT ON COLUMN workspace_assistants.display_order IS 'Ordem de exibição';
COMMENT ON COLUMN workspace_assistants.is_default IS 'Assistente padrão (RF-025)';
COMMENT ON COLUMN workspace_assistants.output_template_id IS 'Override template (RN-070)';
COMMENT ON COLUMN medical_transcriptions.id IS 'SERIAL para performance';
COMMENT ON COLUMN medical_transcriptions.session_id IS 'ID da sessão Azure Speech';
COMMENT ON COLUMN medical_transcriptions.full_text IS 'Texto completo (RF-042)';
COMMENT ON COLUMN medical_transcriptions.metadata IS 'Chunks com timestamps';
COMMENT ON COLUMN medical_transcriptions.model IS 'azure-speech (RN-050)';
COMMENT ON COLUMN medical_transcriptions.language IS 'pt-BR';
COMMENT ON COLUMN medical_transcriptions.started_at IS 'Início gravação (RF-040)';
COMMENT ON COLUMN medical_transcriptions.completed_at IS 'Fim gravação';
COMMENT ON COLUMN medical_transcriptions.duration_seconds IS 'Duração em segundos';
COMMENT ON COLUMN medical_transcriptions.average_confidence IS 'Confiança média';
COMMENT ON COLUMN medical_transcriptions.total_words IS 'Total de palavras';
COMMENT ON COLUMN medical_transcriptions.is_edited IS 'Foi editada (RF-043)';
COMMENT ON COLUMN medical_transcriptions.edited_text IS 'Texto editado';
COMMENT ON COLUMN medical_transcriptions.edited_by IS 'Quem editou';
COMMENT ON COLUMN medical_transcriptions.edited_at IS 'Quando editou';
COMMENT ON COLUMN medical_transcriptions.anonymized IS 'Anonimizada (RF-060)';
COMMENT ON COLUMN medical_transcriptions.original_hash IS 'Hash SHA256 original';
COMMENT ON COLUMN attachments.file_id IS 'ID único do arquivo';
COMMENT ON COLUMN attachments.filename IS 'Nome do arquivo';
COMMENT ON COLUMN attachments.original_filename IS 'Nome original';
COMMENT ON COLUMN attachments.mime_type IS 'Tipo MIME';
COMMENT ON COLUMN attachments.file_size IS 'Tamanho em bytes';
COMMENT ON COLUMN attachments.file_extension IS 'Extensão';
COMMENT ON COLUMN attachments.azure_blob_url IS 'URL Azure Blob';
COMMENT ON COLUMN attachments.azure_container_name IS 'Container Azure';
COMMENT ON COLUMN attachments.azure_blob_name IS 'Nome do blob';
COMMENT ON COLUMN attachments.extracted_text IS 'Texto extraído';
COMMENT ON COLUMN attachments.minified_content IS 'Conteúdo minificado';
COMMENT ON COLUMN attachments.is_processed IS 'Processado';
COMMENT ON COLUMN attachments.workspace_result_id IS 'Resultado vinculado';
COMMENT ON COLUMN attachments.is_deleted IS 'Soft delete';
COMMENT ON COLUMN attachments.deleted_at IS 'Quando deletado';
COMMENT ON COLUMN ai_tools_results.user_id IS 'Usuário';
COMMENT ON COLUMN ai_tools_results.tool_id IS 'Ferramenta';
COMMENT ON COLUMN ai_tools_results.transcription_id IS 'Transcrição';
COMMENT ON COLUMN ai_tools_results.workspace_id IS 'Workspace';
COMMENT ON COLUMN ai_tools_results.input_data IS 'Entrada';
COMMENT ON COLUMN ai_tools_results.output_data IS 'Saída';
COMMENT ON COLUMN ai_tools_results.ai_model_used IS 'Modelo usado';
COMMENT ON COLUMN ai_tools_results.tokens_used IS 'Tokens (RNF-MAN-003)';
COMMENT ON COLUMN ai_tools_results.processing_time_ms IS 'Tempo (RNF-MAN-003)';
COMMENT ON COLUMN ai_tools_results.error_message IS 'Erro';
COMMENT ON COLUMN ai_usage_limits.subscription_type IS 'free|premium (RN-012)';
COMMENT ON COLUMN ai_usage_limits.daily_limit IS 'Limite diário';
COMMENT ON COLUMN ai_usage_limits.monthly_limit IS 'Limite mensal';
COMMENT ON COLUMN ai_usage_limits.can_create_tools IS 'Pode criar ferramentas';
COMMENT ON COLUMN ai_usage_limits.max_custom_tools IS 'Max ferramentas custom';
COMMENT ON COLUMN plans.key IS 'free|premium|canceled';
COMMENT ON COLUMN plans.name IS 'Nome do plano';
COMMENT ON COLUMN plans.description IS 'Descrição';
COMMENT ON COLUMN plans.price_cents IS 'Preço em centavos';
COMMENT ON COLUMN plans.billing_interval IS 'month|year';
COMMENT ON COLUMN plans.stripe_price_id IS 'ID do preço no Stripe';
COMMENT ON COLUMN plans.stripe_product_id IS 'ID do produto no Stripe';
COMMENT ON COLUMN plans.ai_daily_limit IS 'Limite diário de IA (RN-012)';
COMMENT ON COLUMN plans.ai_monthly_limit IS 'Limite mensal de IA';
COMMENT ON COLUMN plans.max_tokens_per_request IS 'Max tokens por requisição';
COMMENT ON COLUMN plans.allowed_models IS 'Modelos de IA permitidos';
COMMENT ON COLUMN plans.transcription_monthly_minutes IS 'Minutos de transcrição/mês';
COMMENT ON COLUMN plans.trial_duration_days IS 'Duração do trial em dias';
COMMENT ON COLUMN plans.plan_duration_days IS 'Duração do plano em dias';
COMMENT ON COLUMN plans.features IS 'Lista de features do plano';
COMMENT ON COLUMN plans.is_active IS 'Plano ativo';
COMMENT ON COLUMN plans.display_order IS 'Ordem de exibição';
COMMENT ON COLUMN subscription_history.user_id IS 'Usuário';
COMMENT ON COLUMN subscription_history.plan_id IS 'Plano';
COMMENT ON COLUMN subscription_history.previous_status IS 'Status anterior';
COMMENT ON COLUMN subscription_history.new_status IS 'Novo status';
COMMENT ON COLUMN subscription_history.change_reason IS 'Motivo';
COMMENT ON COLUMN subscription_history.changed_by IS 'Quem alterou';
COMMENT ON COLUMN subscription_history.metadata IS 'Metadados Stripe';
COMMENT ON COLUMN user_favorites.user_id IS 'Usuário';
COMMENT ON COLUMN user_favorites.entity_type IS 'workspace|tool|assistant';
COMMENT ON COLUMN user_favorites.entity_id IS 'ID da entidade';
COMMENT ON COLUMN user_favorites.display_order IS 'Ordem';
COMMENT ON COLUMN audit_logs.entity_type IS 'Tipo de entidade';
COMMENT ON COLUMN audit_logs.entity_uuid IS 'UUID da entidade';
COMMENT ON COLUMN audit_logs.entity_id IS 'ID da entidade (se int)';
COMMENT ON COLUMN audit_logs.action IS 'create|update|delete|access';
COMMENT ON COLUMN audit_logs.user_id IS 'Quem executou';
COMMENT ON COLUMN audit_logs.ip_address IS 'IP';
COMMENT ON COLUMN audit_logs.user_agent IS 'User agent';
COMMENT ON COLUMN audit_logs.old_values IS 'Valores anteriores';
COMMENT ON COLUMN audit_logs.new_values IS 'Novos valores';
COMMENT ON COLUMN audit_logs.metadata IS 'Metadados';
COMMENT ON COLUMN audit_logs.contains_personal_data IS 'Contém dados pessoais';
COMMENT ON COLUMN audit_logs.data_retention_date IS 'Data de retenção';
COMMENT ON COLUMN migration_log.phase IS 'Fase';
COMMENT ON COLUMN migration_log.step IS 'Passo';
COMMENT ON COLUMN migration_log.status IS 'Status';
COMMENT ON COLUMN migration_log.message IS 'Mensagem';
COMMENT ON COLUMN system_updates.name IS 'Nome da atualização';
COMMENT ON COLUMN system_updates.update_type IS 'feature|hotfix';
COMMENT ON COLUMN system_updates.release_date IS 'Data de release';
COMMENT ON COLUMN system_updates.release_notes IS 'Notas de release';
COMMENT ON COLUMN faq.question IS 'Pergunta';
COMMENT ON COLUMN faq.answer IS 'Resposta';
COMMENT ON COLUMN faq.order_number IS 'Ordem';
COMMENT ON COLUMN faq.is_active IS 'Ativa';
