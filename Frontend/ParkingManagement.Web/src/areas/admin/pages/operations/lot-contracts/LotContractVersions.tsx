import React, { useState } from 'react';
import { adminLotContractsApi } from '@/api/adminLotContractsApi';
import type { LotContractDetailDto, LotContractVersionDto } from '@/types/adminLotContracts';
import { AdminButton } from '../../../components/AdminButton';
import { CellStatus, CellText, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { formatDateTimeIct } from '../../../utils/format';
import { ContractViewer } from './ContractViewer';
import { percentText, saveFile, termText } from './lotContractFormat';

/**
 * Bảng phiên bản hợp đồng bãi (mới nhất trước), L2f (SHA-256 kiểm tra lại ở server): bản HTML mở toàn màn hình để xem / lưu PDF,
 * bản DOCX tải file về.
 */
export const LotContractVersions: React.FC<{ contract: LotContractDetailDto; onError: (message: string) => void }> = ({ contract, onError }) => {
  const [downloading, setDownloading] = useState<number | null>(null);
  const [viewing, setViewing] = useState<{ title: string; html: string } | null>(null);
  const isDocx = (v: LotContractVersionDto) => v.fileContentType.includes('wordprocessingml');

  const open = async (v: LotContractVersionDto) => {
    setDownloading(v.version);
    try {
      const file = await adminLotContractsApi.versionFile(contract.id, v.version);
      if (isDocx(v)) saveFile(file, `${contract.contractNumber}-v${v.version}.docx`);
      else setViewing({ title: `${contract.contractNumber} · v${v.version}`, html: await file.blob.text() });
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : String(err));
    } finally {
      setDownloading(null);
    }
  };

  const columns: DataTableColumn<LotContractVersionDto>[] = [
    {
      key: 'version',
      header: 'Version',
      width: 190,
      render: (v) => <CellTwoLine primary={`v${v.version}`} secondary={v.templateName ?? `Template #${v.templateVersion}`} />,
    },
    { key: 'status', header: 'Status', width: 110, render: (v) => <CellStatus value={v.status} /> },
    { key: 'terms', header: 'Terms', render: (v) => <CellTwoLine primary={percentText(v.commissionRate)} secondary={termText(v)} /> },
    {
      key: 'special',
      header: 'Special terms',
      width: 200,
      render: (v) =>
        v.specialTerms ? (
          <span title={v.specialTerms} className="adm-type-body-default line-clamp-2 whitespace-pre-line text-(--admin-text-primary)">
            {v.specialTerms}
          </span>
        ) : (
          <CellText>—</CellText>
        ),
    },
    {
      key: 'created',
      header: 'Created',
      width: 170,
      render: (v) => <CellTwoLine primary={formatDateTimeIct(v.createdAtUtc)} secondary={v.revisionReason ?? `Admin #${v.createdByAdminId}`} />,
    },
    {
      key: 'file',
      header: '',
      width: 130,
      render: (v) => (
        <AdminButton variant="ghost" icon={isDocx(v) ? 'download' : 'file'} onClick={() => open(v)} disabled={downloading !== null}>
          {downloading === v.version ? 'Loading…' : isDocx(v) ? 'DOCX' : 'View / PDF'}
        </AdminButton>
      ),
    },
  ];

  return (
    <>
      <DataTable bare columns={columns} rows={contract.versions} rowKey={(v) => v.id} />
      {viewing && <ContractViewer {...viewing} onClose={() => setViewing(null)} />}
    </>
  );
};
