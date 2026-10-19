import React, { useState } from 'react';
import { adminKybApi } from '@/api/adminKybApi';
import type { AdminKybApplicationDetailDto } from '@/types/adminKyb';
import type { AdminParkingLotDto } from '@/types/adminOperations';
import { ActionDialog } from '../../../components/ActionDialog';
import { AdminDialog, DialogSummary } from '../../../components/AdminDialog';
import { Banner } from '../../../components/Banner';
import { DateTimeField, TextAreaField } from '../../../components/FormFields';
import { distanceMeters } from '../../../utils/geo';
import { formatDate, formatNumber, localInputToUtcIso } from '../../../utils/format';
import { kybCode } from './kybFormat';

export type KybDialogType = 'startReview' | 'approve' | 'reject' | 'requestInfo' | 'scheduleSurvey';

interface KybDialogsProps {
  kyb: AdminKybApplicationDetailDto;
  lot: AdminParkingLotDto | null;
  open: KybDialogType | null;
  onClose: () => void;
  /** Gọi sau khi API thành công: bật toast (Figma "Result – …") và tải lại chi tiết. */
  onDone: (title: string, message: string) => void;
}

const ScheduleSurveyDialog: React.FC<{ open: boolean; summary: React.ReactNode; onClose: () => void; onSubmit: (atUtc: string, note: string) => Promise<void> }> = ({
  open,
  summary,
  onClose,
  onSubmit,
}) => {
  const [at, setAt] = useState('');
  const [note, setNote] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);
  const [pastError, setPastError] = useState<string | null>(null);

  const atIso = localInputToUtcIso(at);
  const atError = !at ? 'Survey date & time is required.' : pastError;

  const changeAt = (value: string) => {
    setAt(value);
    setPastError(null);
  };

  const confirm = async () => {
    setSubmitted(true);
    if (!atIso) return;
    if (new Date(atIso).getTime() <= Date.now()) {
      setPastError('Choose a date in the future.');
      return;
    }
    setSubmitting(true);
    setApiError(null);
    try {
      await onSubmit(atIso, note.trim());
      setAt('');
      setNote('');
      setSubmitted(false);
      onClose();
    } catch (err: unknown) {
      setApiError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AdminDialog
      open={open}
      title="Schedule field survey"
      description="Sets FieldSurveyRequired = true and FieldSurveyAtUtc."
      onClose={onClose}
      onConfirm={confirm}
      confirmLabel="Save schedule"
      submitting={submitting}
      error={apiError}
    >
      {summary}
      <DateTimeField label="Survey date & time" required value={at} onChange={changeAt} error={submitted ? atError : null} />
      <TextAreaField label="Survey note" value={note} onChange={setNote} maxLength={2000} />
    </AdminDialog>
  );
};

/** Dialog của màn KYB Detail: Start review / Approve / Reject / Request more info / Schedule field survey. */
export const KybDialogs: React.FC<KybDialogsProps> = ({ kyb, lot, open, onClose, onDone }) => {
  const summary = (
    <DialogSummary
      title={`${kybCode(kyb.id)} · ${kyb.parkingLotName}`}
      subtitle={[kyb.ownerBusinessName ?? `OwnerProfile #${kyb.ownerProfileId}`, lot ? `${formatNumber(lot.totalSlots)} slots` : null]
        .filter(Boolean)
        .join(' · ')}
    />
  );

  const distance =
    kyb.photoLatitude !== null && kyb.photoLongitude !== null && lot
      ? Math.round(distanceMeters(kyb.photoLatitude, kyb.photoLongitude, lot.latitude, lot.longitude))
      : null;
  const approveInfo = [
    kyb.fieldSurveyRequired ? `Field survey ${kyb.fieldSurveyAtUtc ? formatDate(kyb.fieldSurveyAtUtc) : 'not scheduled'}.` : 'No field survey.',
    distance !== null ? `Location distance ${formatNumber(distance)} m.` : 'No GPS on site photos.',
  ].join(' ');

  return (
    <>
      <ActionDialog
        open={open === 'startReview'}
        title="Start review"
        description="Status Submitted → Under review; ReviewedByUserId = you."
        confirmLabel="Start review"
        onClose={onClose}
        onSubmit={async () => {
          await adminKybApi.startReview(kyb.id);
          onDone('Review started', `${kybCode(kyb.id)} is now Under review.`);
        }}
      >
        {summary}
      </ActionDialog>

      <ActionDialog
        open={open === 'approve'}
        title="Approve KYB"
        description="Status → Approved; lot status Pending approval → Active; owner emailed (KYB_APPROVED)."
        confirmLabel="Approve"
        field={{
          label: 'Approval note',
          placeholder: 'Anything the owner should know?',
          helper: 'Saved to AuditLogs.Reason (no note column on KybApplications).',
          maxLength: 1000,
        }}
        onClose={onClose}
        onSubmit={async (note) => {
          await adminKybApi.approve(kyb.id, { note: note || null });
          onDone('KYB approved', `${kyb.parkingLotName} is now Active.`);
        }}
      >
        {summary}
        <Banner tone="info">{approveInfo}</Banner>
      </ActionDialog>

      <ActionDialog
        open={open === 'reject'}
        title="Reject KYB"
        description="Status → Rejected; owner emailed with the reason (KYB_REJECTED)."
        confirmLabel="Reject"
        destructive
        field={{
          label: 'Reject reason',
          required: true,
          requiredMessage: 'A reason is required.',
          placeholder: 'Which document failed, and why?',
          helper: 'Saved to KybApplications.RejectReason',
          maxLength: 1000,
        }}
        onClose={onClose}
        onSubmit={async (reason) => {
          await adminKybApi.reject(kyb.id, { reason });
          onDone('KYB rejected', `${kybCode(kyb.id)} was rejected and the owner was notified.`);
        }}
      >
        {summary}
      </ActionDialog>

      <ActionDialog
        open={open === 'requestInfo'}
        title="Request more information"
        description="Status → Needs more info; owner can edit and resubmit."
        confirmLabel="Send request"
        field={{
          label: 'What is missing',
          required: true,
          requiredMessage: 'Tell the owner what to fix.',
          placeholder: 'List the documents or photos to add.',
          helper: 'Sent by email; kept in AuditLogs.Reason',
          maxLength: 1000,
        }}
        onClose={onClose}
        onSubmit={async (note) => {
          await adminKybApi.requestInfo(kyb.id, { note });
          onDone('Info requested', `${kybCode(kyb.id)} is now Needs more info.`);
        }}
      >
        {summary}
      </ActionDialog>

      <ScheduleSurveyDialog
        open={open === 'scheduleSurvey'}
        summary={summary}
        onClose={onClose}
        onSubmit={async (atUtc, note) => {
          await adminKybApi.scheduleSurvey(kyb.id, { fieldSurveyAtUtc: atUtc, note: note || null });
          onDone('Survey scheduled', `Field survey on ${formatDate(atUtc)}.`);
        }}
      />
    </>
  );
};
