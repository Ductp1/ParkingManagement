import React, { useState } from 'react';
import { adminLotContractsApi } from '@/api/adminLotContractsApi';
import type { LotContractDetailDto, LotContractVersionDto } from '@/types/adminLotContracts';
import { AdminButton } from '../../../components/AdminButton';
import { CellStatus, CellText, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { formatDateTimeIct } from '../../../utils/format';
import { percentText, saveFile, termText } from './lotContractFormat';

/** Bảng phiên bản hợp đồng bãi (mới nhất trước) + tải file HTML (L2f, SHA-256 kiểm tra lại ở server). */
export const LotContractVersions: React.FC<{ contract: LotContractDetailDto; onError: (message: string) => void }> = ({ contract, onError }) => {
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

  return <DataTable bare columns={columns} rows={contract.versions} rowKey={(v) => v.id} />;
};
