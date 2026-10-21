import React, { useState } from 'react';
import { adminLotContractsApi } from '@/api/adminLotContractsApi';
import { KybStatus, LotContractStatus } from '@/types/adminEnums';
import type { AdminKybApplicationDetailDto } from '@/types/adminKyb';
import type { LotContractDetailDto, LotContractSummaryDto } from '@/types/adminLotContracts';
import { AdminButton } from '../../../components/AdminButton';
import { ActionDialog } from '../../../components/ActionDialog';
import { DialogSummary } from '../../../components/AdminDialog';
import { Banner } from '../../../components/Banner';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { QueryState } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { formatDateTimeIct, orEmpty } from '../../../utils/format';
import { ownerLabel } from '../kyb/kybFormat';
import { ContractTermsDialog, SignContractDialog } from './LotContractDialogs';
import { LotContractVersions } from './LotContractVersions';
import { bankText, percentText } from './lotContractFormat';

type Dialog = 'create' | 'revise' | 'sign' | 'cancel' | null;

interface LotContractPanelProps {
  kyb: AdminKybApplicationDetailDto;
  /** Hợp đồng chưa hủy của bãi (tìm theo parkingLotId), null nếu chưa lập. */
  summary: LotContractSummaryDto | null;
  /** Gọi sau mỗi thao tác – ký hợp đồng cũng duyệt KYB nên trang KYB phải tải lại. */
  onChanged: () => void;
}

/**
 * Hợp đồng bãi trong KYB review (US-116, design v9): lập v1 khi KYB đang thẩm định, sửa (bàn bạc) ra v2, v3…,
 * tải bản đã ký lên = duyệt KYB + kích hoạt bãi; hủy khi đối tác không đồng ý hoặc KYB bị từ chối.
 */
export const LotContractPanel: React.FC<LotContractPanelProps> = ({ kyb, summary, onChanged }) => {
  const toast = useAdminToast();
  const [dialog, setDialog] = useState<Dialog>(null);
  const detail = useAdminQuery(summary ? () => adminLotContractsApi.getById(summary.id) : null, [summary?.id, summary?.updatedAtUtc]);
  const contract = detail.data;
  const underReview = kyb.status === KybStatus.UnderReview;
  const pending = contract?.status === LotContractStatus.Pending;

  const done = (saved: LotContractDetailDto, title: string) => {
    toast.show({ type: 'success', title, message: `${saved.contractNumber} · ${saved.lotName}` });
    detail.refetch();
    onChanged();
  };
  const close = () => setDialog(null);

  const actions = !summary ? (
    underReview && <AdminButton onClick={() => setDialog('create')}>Create contract</AdminButton>
  ) : (
    pending && (
      <>
        <AdminButton variant="secondary" onClick={() => setDialog('cancel')}>
          Cancel contract
        </AdminButton>
        {underReview && (
          <>
            <AdminButton variant="secondary" onClick={() => setDialog('revise')}>
              Revise
            </AdminButton>
            <AdminButton onClick={() => setDialog('sign')}>Upload signed contract & approve</AdminButton>
          </>
        )}
      </>
    )
  );

  return (
    <Panel
      title="Lot contract"
      description="Created and negotiated during review · uploading the signed contract approves the KYB and activates the lot"
      padded={false}
    >
      <div className="flex flex-col gap-3 px-4 py-3">
        {!summary && (
          <p className="adm-type-body-default m-0 text-(--admin-text-secondary)">
            {underReview
              ? 'No contract yet. Create v1 from the active template, send it to the owner, revise until both sides agree.'
              : kyb.status === KybStatus.Submitted
                ? 'Start the review to create the lot contract.'
                : 'No lot contract.'}
          </p>
        )}
        {summary && !contract && (
          <QueryState
            status={detail.status}
            error={detail.error}
            onRetry={detail.refetch}
            copy={{ emptyTitle: 'Contract not found', emptyMessage: '', errorTitle: 'Could not load the lot contract', service: 'AdminService' }}
          />
        )}
        {contract && (
          <>
            {pending && kyb.status === KybStatus.Rejected && (
              <Banner tone="warning">The KYB was rejected – cancel this pending contract.</Banner>
            )}
            {pending && kyb.status === KybStatus.NeedsMoreInfo && (
              <Banner tone="info">Waiting for the owner to resubmit KYB; the contract stays pending.</Banner>
            )}
            <DetailList>
              <DetailRow label="Contract">
                {contract.contractNumber} <StatusIndicator value={contract.status} />
              </DetailRow>
              <DetailRow label="Platform fee">{percentText(contract.versions[0]?.commissionRate ?? null)}</DetailRow>
              <DetailRow label="Party B">{`${contract.businessName} · MST ${contract.taxCode}`}</DetailRow>
              <DetailRow label="Payout account">{bankText(contract.bank)}</DetailRow>
              {contract.signedAtUtc && <DetailRow label="Signed">{formatDateTimeIct(contract.signedAtUtc)}</DetailRow>}
              {contract.signedFileSha256 && (
                <DetailRow label="Signed scan SHA-256">
                  <span className="break-all font-mono text-xs">{contract.signedFileSha256}</span>
                </DetailRow>
              )}
              {contract.cancelReason && <DetailRow label="Cancel reason">{orEmpty(contract.cancelReason)}</DetailRow>}
            </DetailList>
          </>
        )}
        {actions && <div className="flex flex-wrap justify-end gap-2">{actions}</div>}
      </div>
      {contract && (
        <LotContractVersions contract={contract} onError={(message) => toast.show({ type: 'error', title: 'Could not download the contract', message })} />
      )}

      {dialog === 'create' && (
        <ContractTermsDialog
          target={{ mode: 'create', kybApplicationId: kyb.id, lotName: kyb.parkingLot.name, ownerLabel: ownerLabel(kyb.parkingLot.ownerProfileId) }}
          onClose={close}
          onDone={done}
        />
      )}
      {dialog === 'revise' && contract && <ContractTermsDialog target={{ mode: 'revise', contract }} onClose={close} onDone={done} />}
      {dialog === 'sign' && contract && <SignContractDialog contract={contract} blockers={kyb.approvalBlockers} onClose={close} onDone={done} />}
      {contract && (
        <ActionDialog
          open={dialog === 'cancel'}
          title="Cancel lot contract"
          description="Use when the owner does not agree or the KYB is rejected. A new contract can be created later."
          confirmLabel="Cancel contract"
          cancelLabel="Keep contract"
          destructive
          field={{ label: 'Reason', required: true, requiredMessage: 'A reason is required.', placeholder: 'Why is this contract cancelled?', maxLength: 1000 }}
          onClose={close}
          onSubmit={async (reason) => done(await adminLotContractsApi.cancel(contract.id, { reason }), 'Contract cancelled')}
        >
          <DialogSummary title={`${contract.contractNumber} · ${contract.lotName}`} subtitle={contract.businessName} />
        </ActionDialog>
      )}
    </Panel>
  );
};
