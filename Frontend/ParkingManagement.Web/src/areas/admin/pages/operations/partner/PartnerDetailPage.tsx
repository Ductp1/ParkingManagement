import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { adminPartnerOnboardingApi } from '@/api/adminPartnerOnboardingApi';
import { PartnerApplicationStatus as S } from '@/types/adminEnums';
import type { ContractDraftDto, PartnerApplicationDetailDto } from '@/types/adminPartnerOnboarding';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { CellStatus, CellText, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { DetailColumns, QueryState, SummaryRow } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { Timeline, type TimelineEntry } from '../../../components/Timeline';
import { ActionDialog } from '../../../components/ActionDialog';
import { DialogSummary } from '../../../components/AdminDialog';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminBreadcrumb } from '../../../hooks/useAdminBreadcrumb';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatDate, formatDateTimeIct, formatNumber, orEmpty } from '../../../utils/format';
import { bankText, percentText, saveFile, termText } from './partnerFormat';
import { ContractTermsDialog, IssueAccountDialog } from './PartnerContractDialogs';

type Dialog = 'startReview' | 'requestInfo' | 'reject' | 'cancel' | 'resend' | 'sendContract' | 'reviseContract' | 'createAccount' | null;

const FLOW: Array<{ status: string; title: string }> = [
  { status: S.Submitted, title: 'Submitted' },
  { status: S.UnderReview, title: 'Under review' },
  { status: S.ContractPending, title: 'Contract pending' },
  { status: S.AccountIssued, title: 'Account issued' },
  { status: S.Activated, title: 'Activated' },
];

const timeline = (app: PartnerApplicationDetailDto): TimelineEntry[] => {
  if (app.status === S.Rejected || app.status === S.Cancelled) {
    return [
      { key: 'submitted', title: 'Submitted', meta: formatDate(app.createdAtUtc), state: 'done' },
      {
        key: 'end',
        title: app.status === S.Rejected ? 'Rejected' : 'Cancelled',
        meta: `${formatDate(app.updatedAtUtc)} · ${orEmpty(app.status === S.Rejected ? app.rejectReason : app.cancelReason)}`,
        state: 'danger',
      },
    ];
  }
  const effective = app.status === S.NeedsMoreInfo ? S.UnderReview : app.status;
  const current = FLOW.findIndex((f) => f.status === effective);
  return FLOW.map((f, i) => ({
    key: f.status,
    title: app.status === S.NeedsMoreInfo && i === current ? 'Needs more info' : f.title,
    meta: i === 0 ? formatDate(app.createdAtUtc) : i === current ? formatDate(app.updatedAtUtc) : undefined,
    state: i < current || (i === current && app.status === S.Activated) ? 'done' : i === current ? 'current' : 'upcoming',
  }));
};

/** Bảng phiên bản hợp đồng (mới nhất trước) + tải file HTML (A8). */
const ContractVersions: React.FC<{ app: PartnerApplicationDetailDto; onError: (message: string) => void }> = ({ app, onError }) => {
  const [downloading, setDownloading] = useState<number | null>(null);

  const download = async (draft: ContractDraftDto) => {
    setDownloading(draft.version);
    try {
      saveFile(await adminPartnerOnboardingApi.contractFile(app.id, draft.version), `${draft.contractNumber}-v${draft.version}.html`);
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : String(err));
    } finally {
      setDownloading(null);
    }
  };

  const columns: DataTableColumn<ContractDraftDto>[] = [
    { key: 'version', header: 'Version', width: 80, render: (d) => <CellText>{`v${d.version}`}</CellText> },
    { key: 'status', header: 'Status', width: 110, render: (d) => <CellStatus value={d.status} /> },
    { key: 'terms', header: 'Terms', render: (d) => <CellTwoLine primary={`${percentText(d.commissionRate)} · ${termText(d)}`} secondary={bankText(d)} /> },
    {
      key: 'sent',
      header: 'Sent',
      width: 150,
      render: (d) => <CellTwoLine primary={formatDateTimeIct(d.sentAtUtc)} secondary={d.revisionReason ?? `Admin #${d.createdByAdminId}`} />,
    },
    {
      key: 'file',
      header: '',
      width: 110,
      render: (d) => (
        <AdminButton variant="ghost" icon="download" onClick={() => download(d)} disabled={downloading !== null}>
          {downloading === d.version ? 'Loading…' : 'Download'}
        </AdminButton>
      ),
    },
  ];

  return (
    <Panel
      title="Contract versions"
      description={app.contractNumber ? `${app.contractNumber} · HTML generated from the active template · SHA-256 checked on download` : 'No contract sent yet'}
      padded={false}
    >
      <DataTable
        bare
        columns={columns}
        rows={app.contractDrafts}
        rowKey={(d) => d.id}
        placeholder={<p className="adm-type-body-default m-0 px-4 py-3 text-(--admin-text-secondary)">Send the contract to create version 1.</p>}
      />
    </Panel>
  );
};

/** Operations / Partner Onboarding / Detail (US-116): hồ sơ, hợp đồng tự sinh, cấp tài khoản – thao tác theo nextStatuses của AdminService. */
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
  const can = (status: string) => app.nextStatuses.includes(status);

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
      <>
        <AdminButton variant="onNavy" onClick={() => setDialog('reviseContract')}>
          Revise contract
        </AdminButton>
        <AdminButton onClick={() => setDialog('createAccount')}>Create owner account</AdminButton>
      </>
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
      {app.status === S.NeedsMoreInfo && <Banner tone="warning">More info requested: {orEmpty(app.infoRequestNote)} · waiting for the applicant.</Banner>}
      {app.status === S.Rejected && <Banner tone="danger">Rejected: {orEmpty(app.rejectReason)}</Banner>}
      {app.status === S.Cancelled && <Banner tone="danger">Cancelled: {orEmpty(app.cancelReason)}</Banner>}
      {app.status === S.AccountIssued && (
        <Banner tone="info">Owner account issued (OwnerProfile #{orEmpty(app.createdOwnerProfileId)}) · waiting for the owner to activate.</Banner>
      )}
      <SummaryRow
        items={[
          { label: 'Status', value: <StatusIndicator value={app.status} /> },
          { label: 'Contract', value: <span className="adm-type-body-strong">{orEmpty(app.contractNumber)}</span> },
        ]}
        actions={
          (can(S.Cancelled) || can(S.Rejected)) && (
            <>
              {can(S.Cancelled) && (
                <AdminButton variant="secondary" onClick={() => setDialog('cancel')}>
                  Cancel application
                </AdminButton>
              )}
              {can(S.Rejected) && (
                <AdminButton variant="destructive" onClick={() => setDialog('reject')}>
                  Reject
                </AdminButton>
              )}
            </>
          )
        }
      />
      <DetailColumns
        main={
          <>
            <Panel title="Applicant">
              <DetailList>
                <DetailRow label="Full name">{app.fullName}</DetailRow>
                <DetailRow label="Position">{orEmpty(app.position)}</DetailRow>
                <DetailRow label="Email">{app.email}</DetailRow>
                <DetailRow label="Phone">{app.phone}</DetailRow>
                <DetailRow label="Consent accepted">{formatDateTimeIct(app.consentAcceptedAtUtc)}</DetailRow>
              </DetailList>
            </Panel>
            <Panel title="Business">
              <DetailList>
                <DetailRow label="Business type">{humanizeEnum(app.businessType)}</DetailRow>
                <DetailRow label="Business name">{app.businessName}</DetailRow>
                <DetailRow label="Tax code">{app.taxCode}</DetailRow>
                <DetailRow label="Business address">{app.businessAddress}</DetailRow>
                <DetailRow label="Legal representative">
                  {app.legalRepresentativeName}
                  {app.legalRepresentativePosition ? ` · ${app.legalRepresentativePosition}` : ''}
                </DetailRow>
                <DetailRow label="Business licence">
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
                <DetailRow label="Lot count">{app.lotCount}</DetailRow>
                <DetailRow label="Primary lot address">{app.primaryLotAddress}</DetailRow>
                <DetailRow label="Estimated car slots">{formatNumber(app.estimatedCarSlots)}</DetailRow>
                <DetailRow label="Estimated motorbike slots">{formatNumber(app.estimatedMotorbikeSlots)}</DetailRow>
                <DetailRow label="Operating hours">{orEmpty(app.operatingHours)}</DetailRow>
              </DetailList>
            </Panel>
            <ContractVersions app={app} onError={(message) => toast.show({ type: 'error', title: 'Could not download the contract', message })} />
          </>
        }
        side={
          <>
            <Panel title="Status timeline" description="AuditLogs · Action = PartnerApplication.<Transition>">
              <Timeline entries={timeline(app)} />
            </Panel>
            <Panel title="Allowed next steps" description="From the AdminService transition table">
              <p className="adm-type-body-default m-0 text-(--admin-text-primary)">
                {app.nextStatuses.length > 0 ? app.nextStatuses.map((s) => humanizeEnum(s)).join(' · ') : 'None — terminal'}
              </p>
            </Panel>
            {app.createdOwnerProfileId !== null && (
              <Panel title="Owner account" description="Created in UserService at T6">
                <DetailList>
                  <DetailRow label="User">#{app.createdUserId}</DetailRow>
                  <DetailRow label="OwnerProfile">#{app.createdOwnerProfileId}</DetailRow>
                </DetailList>
              </Panel>
            )}
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
        description="Status changes to Needs more info. The note is stored on the application."
        confirmLabel="Send request"
        field={{ label: 'Note to applicant', required: true, requiredMessage: 'A note is required.', placeholder: 'What should the applicant fix or add?', maxLength: 1000 }}
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
        description="Terminal. A contract version being signed becomes Superseded."
        confirmLabel="Reject application"
        destructive
        field={{ label: 'Reason', required: true, requiredMessage: 'A reason is required.', placeholder: 'Why is this application rejected?', maxLength: 1000 }}
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
        field={{ label: 'Reason', required: true, requiredMessage: 'A reason is required.', placeholder: 'Why is this application cancelled?', maxLength: 1000 }}
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
        description={`UserService revokes the current link and emails a new one to ${app.email}.`}
        confirmLabel="Resend link"
        onClose={close}
        onSubmit={async () => {
          await adminPartnerOnboardingApi.resendActivation(app.id);
          done('Activation resent');
        }}
      >
        {summary}
      </ActionDialog>
      {(dialog === 'sendContract' || dialog === 'reviseContract') && (
        <ContractTermsDialog app={app} mode={dialog === 'sendContract' ? 'send' : 'revise'} open onClose={close} onDone={done} />
      )}
      {dialog === 'createAccount' && <IssueAccountDialog app={app} open onClose={close} onDone={done} />}
    </>
  );
};
