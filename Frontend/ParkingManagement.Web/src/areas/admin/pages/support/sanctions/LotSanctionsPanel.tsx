import React from 'react';
import { Link } from 'react-router-dom';
import { Panel } from '../../../components/Panel';
import { AdminButton } from '../../../components/AdminButton';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { QueryState } from '../../../components/PageLayout';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { formatDateTimeIct } from '../../../utils/format';
import { SANCTIONS_BASE, levelLabel, levelTone } from './sanctionFormat';
import type { LotSanctions } from './useLotSanctions';

/**
 * Operations / Parking Lots / Detail – panel "Lot-level sanctions" (US-097): hiệu lực đang áp (ParkingService) và chế tài Active
 * (AdminService) của bãi; nút áp chế tài mở dialog với bãi + chủ bãi chọn sẵn.
 */
export const LotSanctionsPanel: React.FC<{ lotId: number; sanctions: LotSanctions; onIssue: () => void }> = ({ lotId, sanctions, onIssue }) => {
  const { state, active } = sanctions;
  const s = state.data;
  return (
    <Panel title="Lot-level sanctions" description="ParkingService enforcement · AdminService sanctions" action={{ label: 'All sanctions →', to: `${SANCTIONS_BASE}?parkingLotId=${lotId}` }}>
      {!s ? (
        <QueryState
          status={state.status}
          error={state.error}
          onRetry={state.refetch}
          copy={{ emptyTitle: 'No sanction state', emptyMessage: '—', errorTitle: 'Could not load sanction state', service: 'ParkingService' }}
        />
      ) : (
        <div className="flex flex-col gap-3">
          <DetailList>
            <DetailRow label="Owner">
              <Link className="adm-type-link-default text-(--admin-brand-navy-900)" to={`${SANCTIONS_BASE}?ownerProfileId=${s.ownerProfileId}`}>
                Owner #{s.ownerProfileId}
              </Link>
            </DetailRow>
            <DetailRow label="New bookings">
              {s.isListingSuspended ? (
                <StatusIndicator
                  tone="danger"
                  label={`Suspended until ${s.listingSuspendedUntilUtc ? formatDateTimeIct(s.listingSuspendedUntilUtc) : 'further notice'}`}
                />
              ) : (
                <StatusIndicator tone="success" label="Accepted" />
              )}
            </DetailRow>
            <DetailRow label="Search ranking">
              {s.isRankingPenalized ? (
                <StatusIndicator tone="warning" label={`Penalized until ${formatDateTimeIct(s.rankingPenaltyUntilUtc)}`} />
              ) : (
                <StatusIndicator tone="success" label="Normal" />
              )}
            </DetailRow>
          </DetailList>
          {active.data && active.data.items.length > 0 && (
            <ul className="m-0 flex list-none flex-col gap-1.5 p-0" aria-label="Active sanctions">
              {active.data.items.map((x) => (
                <li key={x.id} className="flex items-center justify-between gap-2">
                  <Link className="adm-type-link-default text-(--admin-brand-navy-900)" to={`${SANCTIONS_BASE}?sanction=${x.id}&parkingLotId=${lotId}`}>
                    #{x.id}
                  </Link>
                  <StatusIndicator label={levelLabel(x.level, x.parkingLotId)} tone={levelTone(x.level)} />
                  <span className="adm-type-body-small text-(--admin-text-secondary)">{x.endsAtUtc ? `→ ${formatDateTimeIct(x.endsAtUtc)}` : '→ no end'}</span>
                </li>
              ))}
            </ul>
          )}
          <AdminButton variant="secondary" icon="plus" onClick={onIssue}>
            Issue sanction for this lot
          </AdminButton>
        </div>
      )}
    </Panel>
  );
};
