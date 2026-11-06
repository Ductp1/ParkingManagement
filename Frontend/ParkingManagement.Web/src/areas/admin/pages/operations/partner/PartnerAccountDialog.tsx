import React, { useState } from 'react';
import { adminPartnerOnboardingApi } from '@/api/adminPartnerOnboardingApi';
import type { PartnerApplicationDetailDto } from '@/types/adminPartnerOnboarding';
import { AdminDialog, DialogSummary } from '../../../components/AdminDialog';
import { TextField } from '../../../components/FormFields';
import { bankErrors } from './partnerFormat';

interface IssueAccountDialogProps {
  app: PartnerApplicationDetailDto;
  onClose: () => void;
  onDone: (title: string) => void;
}

/**
 * Create owner account (A9, T6): không ký hợp đồng ở bước này – hợp đồng lập theo từng bãi sau khi KYB được duyệt.
 * Admin nhập tài khoản nhận thanh toán (in vào Phụ lục 02 của hợp đồng bãi). UserService lỗi (503) thì hồ sơ giữ nguyên, thử lại an toàn.
 */
export const IssueAccountDialog: React.FC<IssueAccountDialogProps> = ({ app, onClose, onDone }) => {
  const [bin, setBin] = useState('');
  const [bankName, setBankName] = useState('');
  const [accountNumber, setAccountNumber] = useState('');
  const [accountName, setAccountName] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const errors = bankErrors({ bin, bankName, accountNumber, accountName });
  const show = (message: string | null) => (submitted ? message : null);

  const confirm = async () => {
    setSubmitted(true);
    if (Object.values(errors).some(Boolean)) return;
    setSubmitting(true);
    setApiError(null);
    try {
      await adminPartnerOnboardingApi.issueAccount(app.id, {
        bank: { bin: bin.trim(), name: bankName.trim(), accountNumber: accountNumber.trim(), accountName: accountName.trim() },
      });
      onDone('Owner account issued');
      onClose();
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AdminDialog
      open
      size={640}
      title="Create owner account"
      description="Under review → Account issued. UserService creates the owner and emails the activation link. Each lot gets its own contract after its KYB is approved."
      confirmLabel="Create owner account"
      onClose={onClose}
      onConfirm={confirm}
      submitting={submitting}
      error={apiError}
    >
      <DialogSummary title={`${app.code} · ${app.businessName}`} subtitle={`MST ${app.taxCode} · ${app.email}`} />
      <div className="grid grid-cols-2 gap-4">
        <TextField label="Bank BIN" required value={bin} onChange={setBin} maxLength={6} error={show(errors.bin)} />
        <TextField label="Bank name" required value={bankName} onChange={setBankName} maxLength={100} error={show(errors.bankName)} />
        <TextField label="Account number" required value={accountNumber} onChange={setAccountNumber} maxLength={30} error={show(errors.accountNumber)} />
        <TextField label="Account name" required value={accountName} onChange={setAccountName} maxLength={200} error={show(errors.accountName)} />
      </div>
    </AdminDialog>
  );
};
