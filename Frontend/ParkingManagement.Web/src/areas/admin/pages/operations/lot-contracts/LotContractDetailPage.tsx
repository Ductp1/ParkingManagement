import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { adminLotContractsApi } from '@/api/adminLotContractsApi';
import { LotContractStatus } from '@/types/adminEnums';
import type { LotContractDetailDto, LotContractVersionDto } from '@/types/adminLotContracts';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { CellStatus, CellText, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { DetailColumns, QueryState, SummaryRow } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { ActionDialog } from '../../../components/ActionDialog';
import { DialogSummary } from '../../../components/AdminDialog';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminBreadcrumb } from '../../../hooks/useAdminBreadcrumb';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { formatDateTimeIct, formatNumber, orEmpty } from '../../../utils/format';
import { KYB_BASE } from '../kyb/kybFormat';
import { ContractTermsDialog, SignContractDialog } from './LotContractDialogs';
import { bankText, percentText, saveFile, termText } from './lotContractFormat';

type Dialog = 'revise' | 'sign' | 'cancel' | null;

/** Bảng phiên bản (mới nhất trước) + tải file HTML (L2f, SHA-256 kiểm tra lại ở server). */
const Versions: React.FC<{ contract: LotContractDetailDto; onError: (message: string) => void }> = ({ contract, onError }) => {
  const [downloading, setDownloading] = useState<number | null>(null);

  const download = async (v: LotContractVersionDto) => {
    setDownloading(v.version);
    try {
      saveFile(await adminLotContractsApi.versionFile(contract.id, v.version), `${contract.contractNumber}-v${v.version}.html`);
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : String(err));
    } finally {
      setDownloading(null);
    }
  };

  const columns: DataTableColumn<LotContractVersionDto>[] = [
    { key: 'version', header: 'Version', width: 80, render: (v) => <CellText>{`v${v.version}`}</CellText> },
    { key: 'status', header: 'Status', width: 110, render: (v) => <CellStatus value={v.status} /> },
    { key: 'terms', header: 'Terms', render: (v) => <CellTwoLine primary={percentText(v.commissionRate)} secondary={termText(v)} /> },
    {
      key: 'created',
      header: 'Created',
      width: 170,
      render: (v) => <CellTwoLine primary={formatDateTimeIct(v.createdAtUtc)} secondary={v.revisionReason ?? `Admin #${v.createdByAdminId}`} />,
    },
    {
      key: 'file',
      header: '',
      width: 110,
      render: (v) => (
        <AdminButton variant="ghost" icon="download" onClick={() => download(v)} disabled={downloading !== null}>
          {downloading === v.version ? 'Loading…' : 'Download'}
        </AdminButton>
      ),
    },
  ];

  return (
    <Panel title="Contract versions" description="HTML generated from the active template · SHA-256 checked on download" padded={false}>
      <DataTable bare columns={columns} rows={contract.versions} rowKey={(v) => v.id} />
    </Panel>
  );
};

/** Operations / Lot Contracts / Detail (US-116): hợp đồng một bãi – sửa, ký (kích hoạt bãi), hủy. */
export const LotContractDetailPage: React.FC = () => {
  const { id } = useParams();
  const contractId = Number(id);
  const valid = Number.isInteger(contractId) && contractId > 0;
  const toast = useAdminToast();
  const [dialog, setDialog] = useState<Dialog>(null);
  const query = useAdminQuery(valid ? () => adminLotContractsApi.getById(contractId) : null, [contractId]);
  const contract = query.data;

  useAdminBreadcrumb(contract ? contract.contractNumber : valid ? `#${contractId}` : null);

  if (!contract) {
    return (
      <>
        <AdminTitleBar title="Lot Contracts" subtitle="—" />
        <QueryState
          status={valid ? query.status : 'empty'}
          error={query.error}
          onRetry={query.refetch}
          bare={false}
          copy={{ emptyTitle: 'Contract not found', emptyMessage: 'Check the link or go back to the list.', errorTitle: 'Could not load the contract', service: 'AdminService' }}
        />
      </>
    );
  }

  const done = (_: LotContractDetailDto, title: string) => {
    toast.show({ type: 'success', title, message: `${contract.contractNumber} · ${contract.lotName}` });
    query.refetch();
  };
  const close = () => setDialog(null);
  const pending = contract.status === LotContractStatus.Pending;
  const current = contract.versions.find((v) => v.status === 'Current') ?? contract.versions[0];

  return (
    <>
      <AdminTitleBar
        title={contract.contractNumber}
        subtitle={`${contract.lotName} · ${contract.businessName} · created ${formatDateTimeIct(contract.createdAtUtc)}`}
        actions={
          pending && (
            <>
              <AdminButton variant="onNavy" onClick={() => setDialog('revise')}>
                Revise contract
              </AdminButton>
              <AdminButton onClick={() => setDialog('sign')}>Upload signed contract</AdminButton>
            </>
          )
        }
      />
      {pending && <Banner tone="info">Waiting for signature · the lot stays Pending approval until the signed contract is uploaded.</Banner>}
      {contract.status === LotContractStatus.Signed && (
        <Banner tone="success">Signed {formatDateTimeIct(contract.signedAtUtc)} · the lot was activated in ParkingService.</Banner>
      )}
      {contract.status === LotContractStatus.Cancelled && <Banner tone="danger">Cancelled: {orEmpty(contract.cancelReason)}</Banner>}
      <SummaryRow
        items={[
          { label: 'Status', value: <StatusIndicator value={contract.status} /> },
          { label: 'Platform fee', value: <span className="adm-type-body-strong">{percentText(current?.commissionRate ?? null)}</span> },
          { label: 'Version', value: <span className="adm-type-body-strong">{current ? `v${current.version}` : '—'}</span> },
        ]}
        actions={
          pending && (
            <AdminButton variant="secondary" onClick={() => setDialog('cancel')}>
              Cancel contract
            </AdminButton>
          )
        }
      />
      <DetailColumns
        main={
          <>
            <Versions contract={contract} onError={(message) => toast.show({ type: 'error', title: 'Could not download the contract', message })} />
            <Panel title="Lot (Appendix 01)" description="Snapshot taken when the contract was created" action={{ label: 'Open KYB →', to: `${KYB_BASE}/${contract.kybApplicationId}` }}>
              <DetailList>
                <DetailRow label="Lot">{`${contract.lotName} · Lot #${contract.parkingLotId}`}</DetailRow>
                <DetailRow label="Address">{[contract.lotAddress, contract.lotDistrict, contract.lotCity].filter(Boolean).join(', ')}</DetailRow>
                <DetailRow label="Capacity">{`${formatNumber(contract.lotTotalSlots)} slots`}</DetailRow>
                <DetailRow label="Opening hours">{contract.lotOpeningHours}</DetailRow>
              </DetailList>
            </Panel>
          </>
        }
        side={
          <>
            <Panel title="Party B" description={`Partner application ${contract.applicationCode}`}>
              <DetailList>
                <DetailRow label="Business">{contract.businessName}</DetailRow>
                <DetailRow label="Tax code">{contract.taxCode}</DetailRow>
                <DetailRow label="Address">{contract.businessAddress}</DetailRow>
                <DetailRow label="Representative">
                  {contract.legalRepresentativeName}
                  {contract.legalRepresentativePosition ? ` · ${contract.legalRepresentativePosition}` : ''}
                </DetailRow>
                <DetailRow label="Contact">{`${contract.phone} · ${contract.email}`}</DetailRow>
                <DetailRow label="OwnerProfile">#{contract.ownerProfileId}</DetailRow>
              </DetailList>
            </Panel>
            <Panel title="Payout account (Appendix 02)">
              <p className="adm-type-body-default m-0 text-(--admin-text-primary)">{bankText(contract.bank)}</p>
            </Panel>
            {contract.signedFileSha256 && (
              <Panel title="Signed scan">
                <DetailList>
                  <DetailRow label="Type">{orEmpty(contract.signedFileContentType)}</DetailRow>
                  <DetailRow label="SHA-256">
                    <span className="break-all font-mono text-xs">{contract.signedFileSha256}</span>
                  </DetailRow>
                </DetailList>
              </Panel>
            )}
          </>
        }
      />

      {dialog === 'revise' && <ContractTermsDialog target={{ mode: 'revise', contract }} onClose={close} onDone={done} />}
      {dialog === 'sign' && <SignContractDialog contract={contract} onClose={close} onDone={done} />}
      <ActionDialog
        open={dialog === 'cancel'}
        title="Cancel lot contract"
        description="Use before signing (owner withdraws or terms not agreed). The lot stays Pending approval; a new contract can be created later."
        confirmLabel="Cancel contract"
        cancelLabel="Keep contract"
        destructive
        field={{ label: 'Reason', required: true, requiredMessage: 'A reason is required.', placeholder: 'Why is this contract cancelled?', maxLength: 1000 }}
        onClose={close}
        onSubmit={async (reason) => {
          const saved = await adminLotContractsApi.cancel(contract.id, { reason });
          done(saved, 'Contract cancelled');
        }}
      >
        <DialogSummary title={`${contract.contractNumber} · ${contract.lotName}`} subtitle={contract.businessName} />
      </ActionDialog>
    </>
  );
};
