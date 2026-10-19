import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { adminPartnerOnboardingApi } from '@/api/adminPartnerOnboardingApi';
import { PartnerApplicationStatus as S } from '@/types/adminEnums';
import type { PartnerApplicationDetailDto } from '@/types/adminPartnerOnboarding';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { DetailColumns, QueryState, SummaryRow } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { Timeline, type TimelineEntry } from '../../../components/Timeline';
import { ActionDialog } from '../../../components/ActionDialog';
import { DialogSummary } from '../../../components/AdminDialog';
import { FormDialog, type FormDialogSpec } from '../../../components/FormDialog';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminBreadcrumb } from '../../../hooks/useAdminBreadcrumb';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { formatDate, formatNumber, orEmpty } from '../../../utils/format';

type Dialog = 'startReview' | 'requestInfo' | 'reject' | 'cancel' | 'resend' | 'sendContract' | 'createAccount' | null;

const FLOW: Array<{ status: string; title: string }> = [
  { status: S.Submitted, title: 'Submitted' },
  { status: S.UnderReview, title: 'Under review' },
  { status: S.ContractPending, title: 'Contract pending' },
  { status: S.AccountIssued, title: 'Account issued' },
  { status: S.Activated, title: 'Activated' },
];

const NEXT_STEPS: Record<string, string> = {
  [S.Submitted]: 'Start review (T2) · Reject (T9) · Cancel (T10)',
  [S.UnderReview]: 'Request info (T3) · Send contract (T5) · Reject (T9) · Cancel (T10)',
  [S.NeedsMoreInfo]: 'Applicant resubmits (T4) · Reject (T9) · Cancel (T10)',
  [S.ContractPending]: 'Create owner account (T6) · Reject (T9) · Cancel (T10)',
  [S.AccountIssued]: 'Resend activation (T7) · owner activates (T8)',
  [S.Activated]: 'None — terminal',
  [S.Rejected]: 'None — terminal',
  [S.Cancelled]: 'None — terminal',
};

const timeline = (app: PartnerApplicationDetailDto): TimelineEntry[] => {
  if (app.status === S.Rejected || app.status === S.Cancelled) {
    return [
      { key: 'submitted', title: 'Submitted', meta: formatDate(app.createdAtUtc), state: 'done' },
      { key: 'end', title: app.status === S.Rejected ? 'Rejected' : 'Cancelled', meta: orEmpty(app.rejectReason ?? app.cancelReason), state: 'danger' },
    ];
  }
  const effective = app.status === S.NeedsMoreInfo ? S.UnderReview : app.status;
  const current = FLOW.findIndex((f) => f.status === effective);
  return FLOW.map((f, i) => ({
    key: f.status,
    title: app.status === S.NeedsMoreInfo && i === current ? 'Needs more info' : f.title,
    meta: i === 0 ? formatDate(app.createdAtUtc) : undefined,
    state: i < current || (i === current && app.status === S.Activated) ? 'done' : i === current ? 'current' : 'upcoming',
  }));
};

const sendContractSpec: FormDialogSpec = {
  title: 'Send contract',
  description: 'Emails the draft contract. Status changes to Contract pending.',
  confirmLabel: 'Send contract',
  fields: [
    {
      key: 'commission',
      kind: 'number',
      label: 'Commission rate (%)',
      required: true,
      helper: 'Default DEFAULT_COMMISSION_RATE = 10%. Allowed 0–100.',
      validate: (v) => (String(v ?? '') !== '' && (Number(v) < 0 || Number(v) > 100) ? 'Rate must be between 0 and 100.' : null),
    },
    { key: 'draft', kind: 'upload', label: 'Draft contract (PDF)', required: true },
  ],
};

const createAccountSpec: FormDialogSpec = {
  title: 'Create owner account',
  description: 'T6 · Contract pending → Account issued',
  size: 640,
  confirmLabel: 'Create owner account',
  fields: [
    { key: 'bankBin', kind: 'text', label: 'Bank BIN', required: true, half: true },
    { key: 'bankName', kind: 'text', label: 'Bank name', required: true, half: true },
    { key: 'accountNumber', kind: 'text', label: 'Account number', required: true, half: true },
    { key: 'accountName', kind: 'text', label: 'Account name', required: true, half: true },
    { key: 'signed', kind: 'upload', label: 'Signed contract (PDF)', required: true },
  ],
};

/** Operations / Partner Onboarding / Detail – các trạng thái T1–T10 theo state machine dự kiến. */
export const PartnerDetailPage: React.FC = () => {
  const { id } = useParams();
  const appId = Number(id);
  const valid = Number.isInteger(appId) && appId > 0;
  const toast = useAdminToast();
  const [dialog, setDialog] = useState<Dialog>(null);
  const query = useAdminQuery(valid ? () => adminPartnerOnboardingApi.getById(appId) : null, [appId]);
  const app = query.data;

  useAdminBreadcrumb(app ? app.code : valid ? `#${appId}` : null);

  if (!app) {
    return (
      <>
        <AdminTitleBar title="Partner Onboarding" subtitle="—" />
        <QueryState
          status={valid ? query.status : 'empty'}
          error={query.error}
          onRetry={query.refetch}
          bare={false}
          copy={{ emptyTitle: 'Application not found', emptyMessage: 'Check the link or go back to the list.', errorTitle: 'Could not load the application', service: 'AdminService' }}
        />
      </>
    );
  }

  const done = (title: string) => {
    toast.show({ type: 'success', title, message: `${app.code} · ${app.businessName}` });
    query.refetch();
  };
  const close = () => setDialog(null);
  const summary = <DialogSummary title={`${app.code} · ${app.businessName}`} subtitle={`MST ${app.taxCode} · ${app.fullName} · ${app.email}`} />;
  const canEnd = ([S.Submitted, S.UnderReview, S.NeedsMoreInfo, S.ContractPending] as string[]).includes(app.status);

  const actions =
    app.status === S.Submitted ? (
      <AdminButton onClick={() => setDialog('startReview')}>Start review</AdminButton>
    ) : app.status === S.UnderReview ? (
      <>
        <AdminButton variant="onNavy" onClick={() => setDialog('requestInfo')}>
          Request info
        </AdminButton>
        <AdminButton onClick={() => setDialog('sendContract')}>Send contract</AdminButton>
      </>
    ) : app.status === S.ContractPending ? (
      <AdminButton onClick={() => setDialog('createAccount')}>Create owner account</AdminButton>
    ) : app.status === S.AccountIssued ? (
      <AdminButton onClick={() => setDialog('resend')}>Resend activation</AdminButton>
    ) : undefined;

  return (
    <>
      <AdminTitleBar
        title={app.businessName}
        subtitle={`${app.code} · submitted ${formatDate(app.createdAtUtc)} · reviewer ${app.reviewerAdminId ? `Admin #${app.reviewerAdminId}` : '—'}`}
        actions={actions}
      />
      <Banner tone="info">
        Future entity: PartnerApplication + OwnerContract exist only in the draft api design (“Partner Onboarding – State Machine &amp; Flow”).
      </Banner>
      <SummaryRow
        items={[{ label: 'Status', value: <StatusIndicator value={app.status} /> }]}
        actions={
          canEnd && (
            <>
              <AdminButton variant="secondary" onClick={() => setDialog('cancel')}>
                Cancel application
              </AdminButton>
              <AdminButton variant="destructive" onClick={() => setDialog('reject')}>
                Reject
              </AdminButton>
            </>
          )
        }
      />
      <DetailColumns
        main={
          <>
            <Panel title="Applicant">
              <DetailList>
                <DetailRow label="Full name" future>{app.fullName}</DetailRow>
                <DetailRow label="Position" future>{orEmpty(app.position)}</DetailRow>
                <DetailRow label="Email" future>{app.email}</DetailRow>
                <DetailRow label="Phone" future>{app.phone}</DetailRow>
              </DetailList>
            </Panel>
            <Panel title="Business">
              <DetailList>
                <DetailRow label="Business type" future>{app.businessType}</DetailRow>
                <DetailRow label="Business name" future>{app.businessName}</DetailRow>
                <DetailRow label="Tax code" future>{app.taxCode}</DetailRow>
                <DetailRow label="Business address" future>{app.businessAddress}</DetailRow>
                <DetailRow label="Business licence" future>
                  {app.businessLicenseUrl ? (
                    <a href={app.businessLicenseUrl} target="_blank" rel="noreferrer" className="adm-type-link-default text-(--admin-brand-navy-900) hover:underline">
                      Open ↗
                    </a>
                  ) : (
                    '—'
                  )}
                </DetailRow>
              </DetailList>
            </Panel>
            <Panel title="Lots declared">
              <DetailList>
                <DetailRow label="Lot count" future>{app.lotCount}</DetailRow>
                <DetailRow label="Primary lot address" future>{app.primaryLotAddress}</DetailRow>
                <DetailRow label="Estimated car slots" future>{formatNumber(app.estimatedCarSlots)}</DetailRow>
                <DetailRow label="Estimated motorbike slots" future>{formatNumber(app.estimatedMotorbikeSlots)}</DetailRow>
                <DetailRow label="Operating hours" future>{orEmpty(app.operatingHours)}</DetailRow>
              </DetailList>
            </Panel>
          </>
        }
        side={
          <>
            <Panel title="Status timeline" description="AuditLogs · Action = PartnerApplication.<Transition>">
              <Timeline entries={timeline(app)} />
            </Panel>
            <Panel title="Allowed next steps">
              <p className="adm-type-body-default m-0 text-(--admin-text-primary)">{NEXT_STEPS[app.status] ?? '—'}</p>
            </Panel>
          </>
        }
      />

      <ActionDialog
        open={dialog === 'startReview'}
        title="Start review"
        description="Status changes Submitted → Under review. You become the reviewer."
        confirmLabel="Start review"
        onClose={close}
        onSubmit={async () => {
          await adminPartnerOnboardingApi.startReview(app.id);
          done('Review started');
        }}
      >
        {summary}
      </ActionDialog>
      <ActionDialog
        open={dialog === 'requestInfo'}
        title="Request more information"
        description="The applicant receives an edit link. Status changes to Needs more info."
        confirmLabel="Send request"
        field={{ label: 'Note to applicant', required: true, requiredMessage: 'A note is required.', placeholder: 'What should the applicant fix or add?' }}
        onClose={close}
        onSubmit={async (note) => {
          await adminPartnerOnboardingApi.requestInfo(app.id, { note });
          done('Info requested');
        }}
      >
        {summary}
      </ActionDialog>
      <ActionDialog
        open={dialog === 'reject'}
        title="Reject application"
        description="Terminal. The applicant is emailed the reason (PARTNER_APPLICATION_REJECTED)."
        confirmLabel="Reject application"
        destructive
        field={{ label: 'Reason', required: true, requiredMessage: 'A reason is required.', placeholder: 'Why is this application rejected?' }}
        onClose={close}
        onSubmit={async (reason) => {
          await adminPartnerOnboardingApi.reject(app.id, { reason });
          done('Application rejected');
        }}
      >
        {summary}
      </ActionDialog>
      <ActionDialog
        open={dialog === 'cancel'}
        title="Cancel application"
        description="Use when the applicant withdraws. Not allowed after Account issued."
        confirmLabel="Cancel application"
        cancelLabel="Keep application"
        destructive
        field={{ label: 'Reason', required: true, requiredMessage: 'A reason is required.', placeholder: 'Why is this application cancelled?' }}
        onClose={close}
        onSubmit={async (reason) => {
          await adminPartnerOnboardingApi.cancel(app.id, { reason });
          done('Application cancelled');
        }}
      >
        {summary}
      </ActionDialog>
      <ActionDialog
        open={dialog === 'resend'}
        title="Resend activation link"
        description={`The current link is revoked and a new 72-hour link is emailed to ${app.email}.`}
        confirmLabel="Resend link"
        onClose={close}
        onSubmit={async () => {
          await adminPartnerOnboardingApi.resendActivation(app.id);
          done('Activation resent');
        }}
      >
        {summary}
      </ActionDialog>
      {dialog === 'sendContract' && <FormDialog spec={sendContractSpec} open onClose={close} unavailableNote="Needs file storage + endpoint A6 (not implemented)." />}
      {dialog === 'createAccount' && <FormDialog spec={createAccountSpec} open onClose={close} unavailableNote="Needs endpoint A9 + UserService U2 (not implemented)." />}
    </>
  );
};
