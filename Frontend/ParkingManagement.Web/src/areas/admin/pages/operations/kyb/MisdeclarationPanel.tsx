import React, { useState } from 'react';
import { adminKybApi } from '@/api/adminKybApi';
import { ParkingLotStatus } from '@/types/adminEnums';
import { MisdeclarationCaseStatus, type LotMisdeclarationCaseDto, type LotMisdeclarationDto } from '@/types/adminKyb';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { CellMono, CellText, CellTwoLine, DataTable } from '../../../components/DataTable';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { QueryState } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import type { StatusTone } from '../../../utils/enumLabels';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatDateTimeIct, orEmpty } from '../../../utils/format';
import { MisdeclarationDialogs, type MisdeclarationDialogType } from './MisdeclarationDialogs';

const adminLabel = (id: number | null) => (id ? `Admin #${id}` : '—');

/** Warned không nằm trong bảng màu chung (enumLabels) → ghi đè; còn lại theo enum (Suspended/Banned đỏ, Resolved xanh). */
const caseTone = (status: string): StatusTone | undefined => (status === MisdeclarationCaseStatus.Warned ? 'warning' : undefined);

const CaseStatus: React.FC<{ c: LotMisdeclarationCaseDto }> = ({ c }) => (
  <span className="inline-flex flex-wrap items-center gap-2">
    <StatusIndicator value={c.status} tone={caseTone(c.status)} />
    {c.isOverdue && <StatusIndicator tone="danger" label="Overdue" />}
  </span>
);

const OpenCase: React.FC<{ view: LotMisdeclarationDto; c: LotMisdeclarationCaseDto; onAction: (t: MisdeclarationDialogType) => void }> = ({
  view,
  c,
  onAction,
}) => {
  const suspended = c.status === MisdeclarationCaseStatus.Suspended;
  return (
    <>
      {c.isOverdue && (
        <Banner tone="danger">
          {suspended ? 'Explanation' : 'Fix'} deadline passed {formatDateTimeIct(c.dueAtUtc)}. Nothing happens automatically – decide the next step.
        </Banner>
      )}
      <DetailList>
        <DetailRow label="Case">#{c.id}</DetailRow>
        <DetailRow label="Status">
          <CaseStatus c={c} />
        </DetailRow>
        <DetailRow label="Severity">{humanizeEnum(c.severity)}</DetailRow>
        <DetailRow label="What was misdeclared">{humanizeEnum(c.category)}</DetailRow>
        <DetailRow label="Finding">{c.finding}</DetailRow>
        <DetailRow label="Opened">{`${formatDateTimeIct(c.openedAtUtc)} · ${adminLabel(c.openedByUserId)}`}</DetailRow>
        {c.escalatedAtUtc && <DetailRow label="Escalated">{`${formatDateTimeIct(c.escalatedAtUtc)} · ${orEmpty(c.escalationNote)}`}</DetailRow>}
        <DetailRow label={suspended ? `Explanation due (${view.responseWindowHours} h)` : 'Fix deadline'}>{formatDateTimeIct(c.dueAtUtc)}</DetailRow>
      </DetailList>
      <div className="mt-4 flex flex-wrap justify-end gap-2">
        {!suspended && (
          <AdminButton variant="destructive" onClick={() => onAction('escalate')}>
            Escalate to suspension
          </AdminButton>
        )}
        <AdminButton variant="destructive" onClick={() => onAction('ban')}>
          Ban owner
        </AdminButton>
        <AdminButton onClick={() => onAction('resolve')}>{suspended ? 'Resolve & reinstate' : 'Resolve'}</AdminButton>
      </div>
    </>
  );
};

const History: React.FC<{ cases: LotMisdeclarationCaseDto[] }> = ({ cases }) => (
  <DataTable
    bare
    rows={cases}
    rowKey={(c) => c.id}
    columns={[
      { key: 'id', header: 'Case', width: 64, render: (c) => <CellMono>#{c.id}</CellMono> },
      { key: 'status', header: 'Status', width: 150, render: (c) => <CaseStatus c={c} /> },
      {
        key: 'what',
        header: 'Finding',
        render: (c) => <CellTwoLine primary={`${humanizeEnum(c.severity)} · ${humanizeEnum(c.category)}`} secondary={c.finding} />,
      },
      {
        key: 'when',
        header: 'Opened / closed',
        width: 190,
        render: (c) => <CellTwoLine primary={formatDateTimeIct(c.openedAtUtc)} secondary={c.closedAtUtc ? formatDateTimeIct(c.closedAtUtc) : `due ${formatDateTimeIct(c.dueAtUtc)}`} />,
      },
      { key: 'note', header: 'Conclusion', render: (c) => <CellText>{orEmpty(c.resolutionNote)}</CellText> },
    ]}
  />
);

/**
 * Operations / KYB Review / Detail – panel Misdeclaration (US-056): case đang mở của bãi đã duyệt (cảnh báo kèm hạn sửa / tạm khóa
 * kèm hạn giải trình 24h), thao tác chuyển cấp – khôi phục – cấm chủ bãi, và lịch sử case. Quá hạn chỉ gắn cờ Overdue.
 */
export const MisdeclarationPanel: React.FC<{
  lotId: number;
  /** Gọi sau mỗi thao tác thành công – trạng thái bãi trong hồ sơ KYB có thể đã đổi. */
  onChanged: () => void;
}> = ({ lotId, onChanged }) => {
  const toast = useAdminToast();
  const [dialog, setDialog] = useState<MisdeclarationDialogType | null>(null);
  const query = useAdminQuery(() => adminKybApi.getMisdeclaration(lotId), [lotId]);
  const view = query.data;

  const lotActive = view?.parkingLot.status === ParkingLotStatus.Active;

  return (
    <Panel title="Misdeclaration" description="LotMisdeclarationCases · warning with fix deadline, suspension with 24 h explanation, owner ban">
      {!view ? (
        <QueryState
          status={query.status}
          error={query.error}
          onRetry={query.refetch}
          copy={{
            emptyTitle: 'No misdeclaration data',
            emptyMessage: 'The lot was not found.',
            errorTitle: 'Could not load misdeclaration cases',
            service: 'ParkingService',
          }}
        />
      ) : (
        <div className="flex flex-col gap-4">
          {view.openCase ? (
            <OpenCase view={view} c={view.openCase} onAction={setDialog} />
          ) : lotActive ? (
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">
                No open case. Report a wrong address, fake photos or inflated capacity found after approval.
              </span>
              <AdminButton variant="destructive" onClick={() => setDialog('report')}>
                Report misdeclaration
              </AdminButton>
            </div>
          ) : (
            <Banner tone={view.parkingLot.status === ParkingLotStatus.Suspended ? 'danger' : 'info'}>
              Lot status {humanizeEnum(view.parkingLot.status)} · misdeclaration cases can only be opened for Active lots.
            </Banner>
          )}
          {view.cases.length > 0 && <History cases={view.cases} />}
          <MisdeclarationDialogs
            view={view}
            open={dialog}
            onClose={() => setDialog(null)}
            onDone={(title, message) => {
              toast.show({ type: 'success', title, message });
              query.refetch();
              onChanged();
            }}
          />
        </div>
      )}
    </Panel>
  );
};
