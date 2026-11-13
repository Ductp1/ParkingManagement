import React, { useState } from 'react';
import { adminPartnerOnboardingApi } from '@/api/adminPartnerOnboardingApi';
import type { PartnerApplicationDetailDto } from '@/types/adminPartnerOnboarding';
import { AdminDialog } from '../../../components/AdminDialog';
import { SelectField, TextAreaField, TextField, ToggleField } from '../../../components/FormFields';
import {
  BUSINESS_TYPE_OPTIONS,
  MAX_INTAKE_NOTE,
  emptyIntake,
  intakeErrors,
  toCreateRequest,
  type PartnerIntakeForm,
} from './partnerIntake';

const Section: React.FC<{ title: string; children: React.ReactNode }> = ({ title, children }) => (
  <fieldset className="m-0 flex flex-col gap-3 border-0 p-0">
    <legend className="adm-type-panel-title mb-1 p-0 uppercase text-(--admin-text-secondary)">{title}</legend>
    {children}
  </fieldset>
);

const Row: React.FC<{ children: React.ReactNode }> = ({ children }) => <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">{children}</div>;

interface NewPartnerApplicationDialogProps {
  open: boolean;
  onClose: () => void;
  onCreated: (application: PartnerApplicationDetailDto) => void;
}

/**
 * New partner application (US-116 T1): Admin nhập hồ sơ nhận qua điện thoại – cùng 4 phần với form public
 * (Contact · Business · Parking lots · Consent). Hồ sơ vào trạng thái Submitted, thẩm định như hồ sơ tự nộp.
 * Lỗi kiểm tra hiện theo từng ô sau khi rời ô hoặc bấm tạo; 400 / 409 từ server hiện nguyên văn ở cuối dialog.
 */
export const NewPartnerApplicationDialog: React.FC<NewPartnerApplicationDialogProps> = ({ open, onClose, onCreated }) => {
  const [form, setForm] = useState<PartnerIntakeForm>(emptyIntake);
  const [touched, setTouched] = useState<Partial<Record<keyof PartnerIntakeForm, boolean>>>({});
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const errors = intakeErrors(form);
  const error = (key: keyof PartnerIntakeForm) => (submitted || touched[key] ? errors[key] ?? null : null);
  const set = <K extends keyof PartnerIntakeForm>(key: K) => (value: PartnerIntakeForm[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
    setTouched((prev) => ({ ...prev, [key]: true }));
  };

  const close = () => {
    setForm(emptyIntake());
    setTouched({});
    setSubmitted(false);
    setApiError(null);
    onClose();
  };

  const confirm = async () => {
    setSubmitted(true);
    if (Object.keys(errors).length > 0) return;
    setSubmitting(true);
    setApiError(null);
    try {
      const created = await adminPartnerOnboardingApi.create(toCreateRequest(form));
      close();
      onCreated(created);
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  const text = (key: keyof PartnerIntakeForm, label: string, opts: { required?: boolean; maxLength?: number; placeholder?: string; helper?: string; type?: 'text' | 'number' | 'email'; suffix?: string } = {}) => (
    <TextField
      label={label}
      value={String(form[key])}
      onChange={set(key) as (v: string) => void}
      error={error(key)}
      {...opts}
    />
  );

  return (
    <AdminDialog
      open={open}
      size={640}
      title="New partner application"
      description="Enter an application received by phone. It starts as Submitted and is reviewed like one sent from the public form."
      confirmLabel="Create application"
      onClose={close}
      onConfirm={confirm}
      submitting={submitting}
      error={apiError}
    >
      <Section title="Contact">
        <Row>
          {text('fullName', 'Contact name', { required: true, maxLength: 200, placeholder: 'Trần Văn Minh' })}
          {text('position', 'Position', { maxLength: 100, placeholder: 'Director' })}
        </Row>
        <Row>
          {text('email', 'Email', { required: true, type: 'email', maxLength: 254, placeholder: 'minh@example.vn' })}
          {text('phone', 'Phone', { required: true, maxLength: 20, placeholder: '0901 234 567' })}
        </Row>
      </Section>

      <Section title="Business">
        <Row>
          <SelectField
            label="Business type"
            required
            value={form.businessType}
            options={BUSINESS_TYPE_OPTIONS}
            onChange={(v) => set('businessType')(v as PartnerIntakeForm['businessType'])}
            error={error('businessType')}
          />
          {text('taxCode', 'Tax code', { required: true, maxLength: 20, placeholder: '0312345678', helper: '10 digits, 10-3 for a branch, or 12 for an individual.' })}
        </Row>
        {text('businessName', 'Business name', { required: true, maxLength: 300, placeholder: 'Công ty TNHH Bãi xe Thủ Đức' })}
        {text('businessAddress', 'Business address', { required: true, maxLength: 300 })}
        <Row>
          {text('legalRepresentativeName', 'Legal representative', { required: true, maxLength: 200 })}
          {text('legalRepresentativePosition', 'Representative position', { maxLength: 100 })}
        </Row>
      </Section>

      <Section title="Parking lots">
        <Row>
          {text('lotCount', 'Number of lots', { required: true, type: 'number', suffix: 'lots' })}
          {text('operatingHours', 'Opening hours', { maxLength: 100, placeholder: '06:00–22:00' })}
        </Row>
        {text('primaryLotAddress', 'Main lot address', { required: true, maxLength: 300 })}
        <Row>
          {text('estimatedCarSlots', 'Car spots (estimate)', { type: 'number', suffix: 'spots' })}
          {text('estimatedMotorbikeSlots', 'Motorbike spots (estimate)', { type: 'number', suffix: 'spots' })}
        </Row>
      </Section>

      <Section title="Consent and call note">
        <div className="flex flex-col gap-1">
          <ToggleField label="The partner agreed on the call to ParkMaster processing this data (Decree 13/2023)" checked={form.consentAccepted} onChange={set('consentAccepted')} />
          {error('consentAccepted') && (
            <span role="alert" className="adm-type-body-small text-(--admin-status-danger)">
              {error('consentAccepted')}
            </span>
          )}
        </div>
        <TextAreaField
          label="Call note"
          value={form.intakeNote}
          onChange={set('intakeNote')}
          maxLength={MAX_INTAKE_NOTE}
          rows={3}
          placeholder="For example: hotline call on 13/11, asked for a site visit next week."
          helper="Saved in the audit log with your name."
          error={error('intakeNote')}
        />
      </Section>
    </AdminDialog>
  );
};
