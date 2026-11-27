import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { adminLotContractsApi } from '@/api/adminLotContractsApi';
import { adminPartnerOnboardingApi } from '@/api/adminPartnerOnboardingApi';
import { PartnerApplicationStatus as S } from '@/types/adminEnums';
import type { LotContractSummaryDto } from '@/types/adminLotContracts';
import type { PartnerApplicationDetailDto } from '@/types/adminPartnerOnboarding';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { CellLink, CellStatus, CellText, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
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
import { KYB_BASE } from '../kyb/kybFormat';
import { bankText, percentText } from '../lot-contracts/lotContractFormat';
import { IssueAccountDialog } from './PartnerAccountDialog';
import { PartnerDocumentsPanel } from './PartnerDocumentsPanel';
import { PartnerEditLinkDialog } from './PartnerEditLinkDialog';
import { DiscardApplicationDialog } from './DiscardApplicationDialog';
import { RequestInfoDialog } from './RequestInfoDialog';
import { AuditTrailLink } from '../../../components/AuditTrailLink';
import { auditLogListLink } from '../../access/auditLogFormat';

type Dialog = 'startReview' | 'requestInfo' | 'reject' | 'cancel' | 'resend' | 'resendInfo' | 'createAccount' | 'editLink' | null;

const FLOW: Array<{ status: string; title: string }> = [
  { status: S.Submitted, title: 'Submitted' },
  { status: S.UnderReview, title: 'Under review' },
  { status: S.AccountIssued, title: 'Account issued' },
  { status: S.Activated, title: 'Activated' },
];

/** Mục timeline cho vòng yêu cầu bổ sung (T3 / T4): chỉ hiện khi đã có. */
const infoEntries = (app: PartnerApplicationDetailDto): TimelineEntry[] => [
  ...(app.infoRequestedAtUtc
    ? [
        {
          key: 'info-requested',
          title: 'More info requested',
          meta: `${formatDate(app.infoRequestedAtUtc)}${app.infoRequestDueAtUtc ? ` · respond by ${formatDate(app.infoRequestDueAtUtc)}` : ''}`,
          state: 'done' as const,
        },
      ]
    : []),
  ...(app.infoProvidedAtUtc
    ? [{ key: 'info-provided', title: 'Info provided', meta: formatDate(app.infoProvidedAtUtc), state: 'done' as const }]
    : []),
];

const timeline = (app: PartnerApplicationDetailDto): TimelineEntry[] => {
  if (app.status === S.Rejected || app.status === S.Cancelled) {
    const cancelled = app.status === S.Cancelled;
    return [
      { key: 'submitted', title: 'Submitted', meta: formatDate(app.createdAtUtc), state: 'done' },
      ...infoEntries(app),
      {
        key: 'end',
        title: cancelled ? 'Discarded' : 'Rejected',
        meta: `${formatDate(cancelled ? app.cancelledAtUtc ?? app.updatedAtUtc : app.updatedAtUtc)} · ${orEmpty(cancelled ? app.cancelReason : app.rejectReason)}`,
        state: 'danger',
      },
    ];
  }
  const effective = app.status === S.NeedsMoreInfo ? S.UnderReview : app.status;
  const current = FLOW.findIndex((f) => f.status === effective);
  const flow: TimelineEntry[] = FLOW.map((f, i) => ({
    key: f.status,
    title: app.status === S.NeedsMoreInfo && i === current ? 'Needs more info' : f.title,
    meta: i === 0 ? formatDate(app.createdAtUtc) : i === current ? formatDate(app.updatedAtUtc) : undefined,
    state: i < current || (i === current && app.status === S.Activated) ? 'done' : i === current ? 'current' : 'upcoming',
  }));
  // Các mục yêu cầu bổ sung chèn ngay sau "Submitted".
  return [flow[0], ...infoEntries(app), ...flow.slice(1)];
};

const lotColumns: DataTableColumn<LotContractSummaryDto>[] = [
  { key: 'number', header: 'Contract', width: 150, render: (c) => <CellLink to={`${KYB_BASE}/${c.kybApplicationId}`}>{c.contractNumber}</CellLink> },
  { key: 'lot', header: 'Lot', render: (c) => <CellTwoLine primary={c.lotName} secondary={`Lot #${c.parkingLotId} · ${c.lotCity}`} /> },
  { key: 'fee', header: 'Fee', width: 80, render: (c) => <CellText>{percentText(c.commissionRate)}</CellText> },
  { key: 'status', header: 'Status', width: 110, render: (c) => <CellStatus value={c.status} /> },
];

/** Hợp đồng của các bãi thuộc chủ bãi này (mỗi bãi một hợp đồng, lập từ KYB Review). */
const OwnerLotContracts: React.FC<{ ownerProfileId: number }> = ({ ownerProfileId }) => {
  const query = useAdminQuery(() => adminLotContractsApi.list({ ownerProfileId, page: 1, pageSize: 50 }), [ownerProfileId], (d) => d.items.length === 0);
  return (
    <Panel title="Lot contracts" description="One contract per lot, negotiated and signed in the lot’s KYB review" padded={false}>
      <DataTable
        bare
        columns={lotColumns}
        rows={query.status === 'ready' ? query.data?.items ?? [] : []}
        rowKey={(c) => c.id}
        placeholder={
          <QueryState
            status={query.status}
            error={query.error}
            onRetry={query.refetch}
            copy={{
              emptyTitle: 'No lot contracts yet',
              emptyMessage: 'The owner creates lots and submits KYB; contracts are created in KYB Review.',
              errorTitle: 'Could not load lot contracts',
              service: 'AdminService',
            }}
          />
        }
      />
    </Panel>
  );
};

/** Operations / Partner Onboarding / Detail (US-116): thẩm định hồ sơ + cấp tài khoản; hợp đồng ký theo từng bãi. */
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
        <AdminTitleBar title="Partner Onboarding" />
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
  const retryCleanup = async () => {
    try {
      await adminPartnerOnboardingApi.retryCleanup(app.id);
      done('Owner account removed');
    } catch (err: unknown) {
      toast.show({ type: 'error', title: 'Cleanup failed', message: err instanceof Error ? err.message : String(err) });
    }
  };
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
        <AdminButton onClick={() => setDialog('createAccount')}>Create owner account</AdminButton>
      </>
    ) : app.status === S.NeedsMoreInfo ? (
      <>
        <AdminButton variant="onNavy" onClick={() => setDialog('editLink')}>
          Link for applicant
        </AdminButton>
        <AdminButton onClick={() => setDialog('resendInfo')}>Resend request email</AdminButton>
      </>
    ) : app.status === S.AccountIssued ? (
      <AdminButton onClick={() => setDialog('resend')}>Resend activation</AdminButton>
    ) : undefined;

  return (
    <>
      <AdminTitleBar
        title={app.businessName}
        subtitle={`${app.code} · submitted ${formatDate(app.createdAtUtc)} · reviewer ${app.reviewerAdminId ? `Admin #${app.reviewerAdminId}` : '—'}`}
        actions={
          <>
            <AuditTrailLink to={auditLogListLink({ entity: 'PartnerApplication', entityId: String(app.id) })} />
            {actions}
          </>
        }
      />
      {app.status === S.NeedsMoreInfo && (
        <Banner tone="warning">
          More info requested: {orEmpty(app.infoRequestNote)} · waiting for the applicant
          {app.infoRequestDueAtUtc ? ` (respond by ${formatDateTimeIct(app.infoRequestDueAtUtc)} ICT)` : ''}. Their update moves the application back to
          Under review.
        </Banner>
      )}
      {app.status === S.NeedsMoreInfo && app.infoRequestEmailStatus === 'Failed' && (
        <Banner tone="danger">
          The request email could not be queued: {orEmpty(app.infoRequestEmailError)}. Use “Resend request email”, or copy “Link for applicant”.
        </Banner>
      )}
      {app.status === S.UnderReview && app.infoProvidedAtUtc && app.infoRequestedAtUtc && (
        <Banner tone="info">The applicant provided the requested information on {formatDateTimeIct(app.infoProvidedAtUtc)} ICT · Updated.</Banner>
      )}
      {app.status === S.Rejected && <Banner tone="danger">Rejected: {orEmpty(app.rejectReason)}</Banner>}
      {app.status === S.Cancelled && <Banner tone="danger">Discarded: {orEmpty(app.cancelReason)}</Banner>}
      {app.status === S.Cancelled && app.cleanupSyncStatus === 'Failed' && (
        <Banner tone="danger">
          The application is discarded, but the owner account has not been removed yet: {orEmpty(app.cleanupError)}.{' '}
          <AdminButton variant="secondary" onClick={() => void retryCleanup()}>
            Retry cleanup
          </AdminButton>
        </Banner>
      )}
      {app.status === S.AccountIssued && (
        <Banner tone="info">Owner account issued (Owner #{orEmpty(app.createdOwnerProfileId)}) · waiting for the owner to activate.</Banner>
      )}
      <SummaryRow
        items={[
          { label: 'Status', value: <StatusIndicator value={app.status} /> },
          { label: 'OwnerProfile', value: <span className="adm-type-body-strong">{app.createdOwnerProfileId ? `#${app.createdOwnerProfileId}` : '—'}</span> },
        ]}
        actions={
          (can(S.Cancelled) || can(S.Rejected)) && (
            <>
              {can(S.Cancelled) && (
                <AdminButton variant="destructive" onClick={() => setDialog('cancel')}>
                  Discard application
                </AdminButton>
              )}
              {can(S.Rejected) && (
                <AdminButton variant="secondary" onClick={() => setDialog('reject')}>
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
            <PartnerDocumentsPanel applicationId={app.id} />
            <Panel title="Lots declared">
              <DetailList>
                <DetailRow label="Lot count">{app.lotCount}</DetailRow>
                <DetailRow label="Primary lot address">{app.primaryLotAddress}</DetailRow>
                <DetailRow label="Estimated car slots">{formatNumber(app.estimatedCarSlots)}</DetailRow>
                <DetailRow label="Estimated motorbike slots">{formatNumber(app.estimatedMotorbikeSlots)}</DetailRow>
                <DetailRow label="Operating hours">{orEmpty(app.operatingHours)}</DetailRow>
              </DetailList>
            </Panel>
            {app.createdOwnerProfileId !== null && <OwnerLotContracts ownerProfileId={app.createdOwnerProfileId} />}
          </>
        }
        side={
          <>
            <Panel title="Status timeline" description="Every status change on this application">
              <Timeline entries={timeline(app)} />
            </Panel>
            <Panel title="Allowed next steps">
              <p className="adm-type-body-default m-0 text-(--admin-text-primary)">
                {app.nextStatuses.length > 0 ? app.nextStatuses.map((s) => humanizeEnum(s)).join(' · ') : 'None — terminal'}
              </p>
            </Panel>
            <Panel title="Payout account" description="Printed in Appendix 02 of every lot contract">
              <p className="adm-type-body-default m-0 text-(--admin-text-primary)">{app.payoutBank ? bankText(app.payoutBank) : 'Set when the owner account is issued.'}</p>
            </Panel>
            {app.createdOwnerProfileId !== null && (
              <Panel title="Owner account" description="Created when the account is issued">
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
        description="The application moves to Under review and you become the reviewer."
        confirmLabel="Start review"
        onClose={close}
        onSubmit={async () => {
          await adminPartnerOnboardingApi.startReview(app.id);
          done('Review started');
        }}
      >
        {summary}
      </ActionDialog>
      <RequestInfoDialog open={dialog === 'requestInfo'} app={app} onClose={close} onDone={() => done('Info requested')} />
      <ActionDialog
        open={dialog === 'resendInfo'}
        title="Resend request email"
        description={`A new link is emailed to ${app.email}; the previous link stops working.`}
        confirmLabel="Resend email"
        onClose={close}
        onSubmit={async () => {
          await adminPartnerOnboardingApi.resendInfoRequest(app.id, { rowVersion: app.rowVersion ?? null });
          done('Request email resent');
        }}
      >
        {summary}
      </ActionDialog>
      <ActionDialog
        open={dialog === 'reject'}
        title="Reject application"
        description="Terminal. No owner account is created."
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
      <DiscardApplicationDialog open={dialog === 'cancel'} app={app} onClose={close} onDone={() => done('Application discarded')} />
      <ActionDialog
        open={dialog === 'resend'}
        title="Resend activation link"
        description={`The current link stops working and a new one is emailed to ${app.email}.`}
        confirmLabel="Resend link"
        onClose={close}
        onSubmit={async () => {
          await adminPartnerOnboardingApi.resendActivation(app.id);
          done('Activation resent');
        }}
      >
        {summary}
      </ActionDialog>
      {dialog === 'createAccount' && <IssueAccountDialog app={app} onClose={close} onDone={done} />}
      {dialog === 'editLink' && <PartnerEditLinkDialog app={app} onClose={close} />}
    </>
  );
};
