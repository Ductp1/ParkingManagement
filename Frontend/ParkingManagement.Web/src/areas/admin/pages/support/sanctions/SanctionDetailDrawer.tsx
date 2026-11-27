import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { adminSanctionsApi } from '@/api/adminSanctionsApi';
import type { SanctionDetailDto } from '@/types/adminSanctions';
import { AdminDrawer } from '../../../components/AdminDrawer';
import { AdminButton } from '../../../components/AdminButton';
import { ActionDialog } from '../../../components/ActionDialog';
import { Banner } from '../../../components/Banner';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { QueryState } from '../../../components/PageLayout';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { Timeline } from '../../../components/Timeline';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { formatDateTimeIct, formatVnd, orEmpty } from '../../../utils/format';
import { SYNC_TARGETS, TIMELINE_LABEL, hasSyncProblem, levelLabel, levelTone, scopeText } from './sanctionFormat';

const syncedAt = (detail: SanctionDetailDto, key: (typeof SYNC_TARGETS)[number]['key']) =>
  key === 'userServiceSyncStatus' ? detail.userServiceSyncedAtUtc : key === 'parkingSyncStatus' ? detail.parkingSyncedAtUtc : detail.notificationSentAtUtc;

const evidenceText = (json: string | null): string => {
  if (!json) return '—';
  try {
    const parsed: unknown = JSON.parse(json);
    if (parsed && typeof parsed === 'object' && 'note' in parsed && typeof (parsed as { note: unknown }).note === 'string')
      return (parsed as { note: string }).note;
    return JSON.stringify(parsed);
  } catch {
    return json;
  }
};

/** Body của drawer – tách riêng để test không cần portal của AdminDrawer. */
export const SanctionDetailBody: React.FC<{ detail: SanctionDetailDto; onChanged: (detail: SanctionDetailDto) => void }> = ({ detail, onChanged }) => {
  const toast = useAdminToast();
  const [retrying, setRetrying] = useState(false);
  const [revokeOpen, setRevokeOpen] = useState(false);
  const s = detail.sanction;
  const syncProblem = hasSyncProblem(s);

  const retry = async () => {
    setRetrying(true);
    try {
      const next = await adminSanctionsApi.retrySync(s.id);
      const stillFailing = hasSyncProblem(next.sanction);
      toast.show({
        type: stillFailing ? 'error' : 'success',
        title: stillFailing ? 'Some services are still not updated' : 'Sanction synced',
        message: stillFailing ? 'Check the service status below and try again later.' : undefined,
      });
      onChanged(next);
    } catch (err: unknown) {
      toast.show({ type: 'error', title: 'Retry failed', message: err instanceof Error ? err.message : String(err) });
    } finally {
      setRetrying(false);
    }
  };

  return (
    <div className="adm-motion-fade flex flex-col gap-4">
      {s.status === 'Active' && syncProblem && (
        <Banner tone="warning">The sanction is saved but not every service has applied it yet. Retry the sync after the service is back.</Banner>
      )}
      <section className="flex flex-col border border-(--admin-border-default) p-3">
        <DetailList>
          <DetailRow label="Level">
            <StatusIndicator label={levelLabel(s.level, s.parkingLotId)} tone={levelTone(s.level)} />
          </DetailRow>
          <DetailRow label="Status">
            <StatusIndicator value={s.status} />
          </DetailRow>
          <DetailRow label="Owner">
            <Link className="adm-type-link-default text-(--admin-brand-navy-900)" to={`/admin/support/sanctions?ownerProfileId=${s.ownerProfileId}`}>
              Owner #{s.ownerProfileId}
            </Link>
            {s.ownerUserId !== null && (
              <>
                {' · '}
                <Link className="adm-type-link-default text-(--admin-brand-navy-900)" to={`/admin/operations/users/${s.ownerUserId}`}>
                  U#{s.ownerUserId}
                </Link>
              </>
            )}
          </DetailRow>
          <DetailRow label="Scope">
            {s.parkingLotId === null ? (
              scopeText(s)
            ) : (
              <Link className="adm-type-link-default text-(--admin-brand-navy-900)" to={`/admin/operations/parking-lots/${s.parkingLotId}`}>
                {scopeText(s)}
              </Link>
            )}
          </DetailRow>
          <DetailRow label="Reason">{s.reason}</DetailRow>
          <DetailRow label="Evidence">{evidenceText(detail.evidenceJson)}</DetailRow>
          <DetailRow label="Starts">{formatDateTimeIct(s.startsAtUtc)}</DetailRow>
          <DetailRow label="Ends">{s.endsAtUtc ? formatDateTimeIct(s.endsAtUtc) : 'No end (permanent)'}</DetailRow>
          {s.correctionDueAtUtc && <DetailRow label="Correction due">{formatDateTimeIct(s.correctionDueAtUtc)}</DetailRow>}
          <DetailRow label="Appeal until">{formatDateTimeIct(s.appealDeadlineUtc)}</DetailRow>
          <DetailRow label="Penalty">{s.penaltyAmount === null ? '—' : formatVnd(s.penaltyAmount)}</DetailRow>
          <DetailRow label="Issued by">U#{s.issuedByUserId}</DetailRow>
          {s.revokedAtUtc && (
            <DetailRow label="Revoked">
              {formatDateTimeIct(s.revokedAtUtc)} · U#{orEmpty(detail.revokedByUserId)} · {orEmpty(detail.revokeReason)}
            </DetailRow>
          )}
          {detail.riskFlagIds.length > 0 && <DetailRow label="Risk flags">{detail.riskFlagIds.map((id) => `#${id}`).join(', ')}</DetailRow>}
        </DetailList>
      </section>

      <section className="flex flex-col gap-2 border border-(--admin-border-default) p-3" aria-label="Service sync">
        <div className="flex items-center justify-between gap-2">
          <h3 className="adm-type-panel-title m-0 uppercase text-(--admin-text-secondary)">Service sync</h3>
          <AdminButton icon="refresh" onClick={retry} disabled={!syncProblem || retrying} loading={retrying} loadingLabel="Retrying…">
            Retry sync
          </AdminButton>
        </div>
        <ul className="m-0 flex list-none flex-col gap-1.5 p-0">
          {SYNC_TARGETS.map((t) => (
            <li key={t.key} data-testid={`sync-${t.short}`} className="flex items-center justify-between gap-3">
              <span className="adm-type-body-default">{t.label}</span>
              <span className="flex items-center gap-2">
                <StatusIndicator value={s[t.key]} />
                <span className="adm-type-body-small text-(--admin-text-secondary)">{formatDateTimeIct(syncedAt(detail, t.key))}</span>
              </span>
            </li>
          ))}
        </ul>
      </section>

      <section className="flex flex-col gap-2 border border-(--admin-border-default) p-3">
        <h3 className="adm-type-panel-title m-0 uppercase text-(--admin-text-secondary)">Timeline</h3>
        {detail.timeline.length === 0 ? (
          <span className="adm-type-body-small text-(--admin-text-secondary)">No audit entries.</span>
        ) : (
          <Timeline
            entries={detail.timeline.map((t, i) => ({
              key: String(t.auditLogId),
              title: TIMELINE_LABEL[t.action] ?? t.action,
              meta: `${formatDateTimeIct(t.atUtc)} · ${t.userId === null ? 'System' : `U#${t.userId}`}${t.reason ? ` · ${t.reason}` : ''}`,
              state: t.action.endsWith('Revoked') || t.action.endsWith('Locked') ? 'danger' : i === detail.timeline.length - 1 ? 'current' : 'done',
            }))}
          />
        )}
        <Link className="adm-type-link-small text-(--admin-brand-navy-900)" to={`/admin/access/audit-log?entityId=${s.id}`}>
          Open in Audit Log
        </Link>
      </section>

      {s.status === 'Active' && (
        <div className="flex justify-end">
          <AdminButton variant="destructive" onClick={() => setRevokeOpen(true)}>
            Revoke sanction
          </AdminButton>
        </div>
      )}

      <ActionDialog
        open={revokeOpen}
        title={`Revoke sanction #${s.id}`}
        description={
          s.parkingLotId === null && (s.level === 'TemporarySuspension' || s.level === 'PermanentBan')
            ? 'Unlocks the owner account (Users › Unlock owner flow) and notifies the owner.'
            : 'Lifts the sanction in ParkingService and notifies the owner.'
        }
        confirmLabel="Revoke sanction"
        destructive
        field={{ label: 'Reason', required: true, requiredMessage: 'A reason is required.', maxLength: 1000, placeholder: 'Why is it revoked (appeal accepted, issued by mistake…)?' }}
        onClose={() => setRevokeOpen(false)}
        onSubmit={async (reason) => {
          const next = await adminSanctionsApi.revoke(s.id, { reason: reason.trim() });
          toast.show({ type: 'success', title: `Sanction #${s.id} revoked` });
          setRevokeOpen(false);
          onChanged(next);
        }}
      />
    </div>
  );
};

/** Slide-over chi tiết chế tài (?sanction= trên URL nên F5 / Back vẫn giữ). */
export const SanctionDetailDrawer: React.FC<{ id: number; onClose: () => void; onChanged: () => void }> = ({ id, onClose, onChanged }) => {
  const query = useAdminQuery(() => adminSanctionsApi.getById(id), [id]);
  const [override, setOverride] = useState<SanctionDetailDto | null>(null);
  const detail = override ?? query.data;
  return (
    <AdminDrawer
      open
      width={560}
      title={`Sanction #${id}`}
      description={detail ? `${levelLabel(detail.sanction.level, detail.sanction.parkingLotId)} · ${scopeText(detail.sanction)}` : undefined}
      onClose={onClose}
    >
      {!detail ? (
        <QueryState
          status={query.status}
          error={query.error}
          onRetry={query.refetch}
          copy={{ emptyTitle: 'Sanction not found', emptyMessage: 'Check the link.', errorTitle: 'Could not load the sanction', service: 'AdminService' }}
        />
      ) : (
        <SanctionDetailBody
          detail={detail}
          onChanged={(next) => {
            setOverride(next);
            onChanged();
          }}
        />
      )}
    </AdminDrawer>
  );
};
