import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { adminContractTemplatesApi } from '@/api/adminContractTemplatesApi';
import type { ContractTemplateDetailDto, ContractTemplateSummaryDto } from '@/types/adminContractTemplates';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { CellMuted, CellStatus, CellText, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { ListScreen, QueryState } from '../../../components/PageLayout';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { formatDateTime, formatNumber } from '../../../utils/format';
import { CONTRACT_TEMPLATE_BASE, kindLabel } from './contractTemplateFormat';
import { ImportDocxDialog, NewTemplateDialog, RenameTemplateDialog } from './ContractTemplateDialogs';

/**
 * Operations / Contract Templates / List (US-116 v11, T0) – các loại hợp đồng có tên. Mẫu Active chọn được khi lập / sửa hợp đồng bãi
 * (nhiều mẫu cùng lúc). Tạo mẫu mới (form) hoặc nhập từ file DOCX (AI tùy chọn); đổi tên ở mọi trạng thái.
 */
export const ContractTemplateListPage: React.FC = () => {
  const navigate = useNavigate();
  const toast = useAdminToast();
  const [dialog, setDialog] = useState<'new' | 'import' | null>(null);
  const [renaming, setRenaming] = useState<ContractTemplateSummaryDto | null>(null);
  const query = useAdminQuery(() => adminContractTemplatesApi.list(), [], (d) => d.length === 0);
  const rows = query.data ?? [];
  const activeCount = rows.filter((r) => r.status === 'Active').length;

  const opened = (t: ContractTemplateDetailDto, title: string) => {
    toast.show({ type: 'success', title, message: t.name });
    navigate(`${CONTRACT_TEMPLATE_BASE}/${t.id}`);
  };

  const columns: DataTableColumn<ContractTemplateSummaryDto>[] = [
    { key: 'name', header: 'Contract type', render: (r) => <CellTwoLine primary={r.name} secondary={`${kindLabel(r.kind)} · #${r.version}`} /> },
    { key: 'status', header: 'Status', width: 100, render: (r) => <CellStatus value={r.status} /> },
    { key: 'activated', header: 'Activated', width: 140, render: (r) => <CellMuted>{formatDateTime(r.activatedAtUtc)}</CellMuted> },
    { key: 'used', header: 'Used by', width: 110, render: (r) => <CellText>{`${formatNumber(r.usedByVersions)} files`}</CellText> },
    { key: 'note', header: 'Change note', render: (r) => <CellMuted>{r.changeNote ?? '—'}</CellMuted> },
    { key: 'updated', header: 'Updated', width: 140, render: (r) => <CellMuted>{formatDateTime(r.updatedAtUtc ?? r.createdAtUtc)}</CellMuted> },
    {
      key: 'actions',
      header: '',
      width: 100,
      render: (r) => (
        <AdminButton
          variant="ghost"
          onClick={(e) => {
            e.stopPropagation();
            setRenaming(r);
          }}
        >
          Rename
        </AdminButton>
      ),
    },
  ];

  return (
    <ListScreen
      title="Contract Templates"
      subtitle={query.data ? `${formatNumber(rows.length)} templates · ${formatNumber(activeCount)} active` : '—'}
      actions={
        <>
          <AdminButton variant="onNavy" icon="upload" onClick={() => setDialog('import')}>
            Import DOCX
          </AdminButton>
          <AdminButton onClick={() => setDialog('new')}>New template</AdminButton>
        </>
      }
      top={
        <Banner tone="info">
          Every active template is a contract type the admin can choose when creating a lot contract. Edit a draft with the form,
          preview it, then activate. Signed contracts keep their own file. Wording for a single lot goes in Special terms on its KYB page.
        </Banner>
      }
    >
      <DataTable
        columns={columns}
        rows={query.status === 'ready' ? rows : []}
        rowKey={(r) => r.id}
        onRowClick={(r) => navigate(`${CONTRACT_TEMPLATE_BASE}/${r.id}`)}
        placeholder={
          <QueryState
            status={query.status}
            error={query.error}
            onRetry={query.refetch}
            copy={{
              emptyTitle: 'No contract templates',
              emptyMessage: 'Create a template or import a DOCX contract.',
              errorTitle: 'Could not load contract templates',
              service: 'AdminService',
            }}
          />
        }
      />
      {dialog === 'new' && <NewTemplateDialog templates={rows} onClose={() => setDialog(null)} onCreated={(t) => opened(t, 'Template created')} />}
      {dialog === 'import' && <ImportDocxDialog onClose={() => setDialog(null)} onCreated={(t) => opened(t, 'Contract imported')} />}
      {renaming && (
        <RenameTemplateDialog
          template={renaming}
          onClose={() => setRenaming(null)}
          onRenamed={(t) => {
            toast.show({ type: 'success', title: 'Template renamed', message: t.name });
            setRenaming(null);
            query.refetch();
          }}
        />
      )}
    </ListScreen>
  );
};
