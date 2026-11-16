import React, { useEffect, useRef, useState } from 'react';
import { adminPartnerOnboardingApi } from '@/api/adminPartnerOnboardingApi';
import type { PartnerDocumentDto } from '@/types/adminPartnerOnboarding';
import { AdminButton } from '../../../components/AdminButton';
import { CellText, CellTwoLine, DataTable, type DataTableColumn } from '../../../components/DataTable';
import { QueryState } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { useAdminQuery } from '../../../hooks/useAdminQuery';
import { formatDateTimeIct } from '../../../utils/format';

const TYPE_LABEL: Record<string, string> = {
  BusinessLicense: 'Business registration',
  RepresentativeId: 'Representative ID',
  LotLegalProof: 'Lot land / lease proof',
  Other: 'Other',
};

const sizeText = (bytes: number) => (bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);

interface Preview {
  documentId: number;
  url: string;
  contentType: string;
  fileName: string;
}

/**
 * US-053: giấy tờ pháp lý đối tác nộp kèm hồ sơ. API chỉ cho Admin nên file được tải qua apiClient (kèm Bearer token)
 * thành Blob rồi xem tại chỗ bằng object URL – link trần <a href>/<img src> không gửi token và sẽ bị 401.
 * Object URL được thu hồi khi đổi file hoặc rời trang.
 */
export const PartnerDocumentsPanel: React.FC<{ applicationId: number }> = ({ applicationId }) => {
  const query = useAdminQuery(() => adminPartnerOnboardingApi.listDocuments(applicationId), [applicationId], (d) => d.length === 0);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [loadingId, setLoadingId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const urlRef = useRef<string | null>(null);

  const replaceUrl = (next: string | null) => {
    if (urlRef.current) URL.revokeObjectURL(urlRef.current);
    urlRef.current = next;
  };

  useEffect(() => () => replaceUrl(null), []);

  const open = async (doc: PartnerDocumentDto) => {
    setError(null);
    setLoadingId(doc.id);
    try {
      const { blob } = await adminPartnerOnboardingApi.getDocumentFile(applicationId, doc.id);
      const typed = blob.type ? blob : new Blob([blob], { type: doc.contentType });
      const url = URL.createObjectURL(typed);
      replaceUrl(url);
      setPreview({ documentId: doc.id, url, contentType: doc.contentType, fileName: doc.originalFileName });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not open the document.');
    } finally {
      setLoadingId(null);
    }
  };

  const close = () => {
    replaceUrl(null);
    setPreview(null);
  };

  const columns: DataTableColumn<PartnerDocumentDto>[] = [
    { key: 'type', header: 'Document', render: (d) => <CellTwoLine primary={TYPE_LABEL[d.documentType] ?? d.documentType} secondary={d.originalFileName} /> },
    { key: 'size', header: 'Size', width: 90, render: (d) => <CellText>{sizeText(d.sizeBytes)}</CellText> },
    { key: 'uploaded', header: 'Uploaded', width: 160, render: (d) => <CellText>{formatDateTimeIct(d.uploadedAtUtc)}</CellText> },
    {
      key: 'open',
      header: '',
      width: 100,
      render: (d) => (
        <AdminButton variant="secondary" onClick={() => void open(d)} disabled={loadingId !== null}>
          {loadingId === d.id ? 'Opening…' : preview?.documentId === d.id ? 'Showing' : 'View'}
        </AdminButton>
      ),
    },
  ];

  return (
    <Panel title="Legal documents" description="Uploaded by the applicant · visible to Admins only" padded={false}>
      <DataTable
        bare
        columns={columns}
        rows={query.status === 'ready' ? query.data ?? [] : []}
        rowKey={(d) => d.id}
        placeholder={
          <QueryState
            status={query.status}
            error={query.error}
            onRetry={query.refetch}
            copy={{
              emptyTitle: 'No documents',
              emptyMessage: 'This application has no uploaded legal documents (older or seeded applications).',
              errorTitle: 'Could not load documents',
              service: 'AdminService',
            }}
          />
        }
      />
      {error && (
        <p role="alert" className="adm-type-body-default m-0 px-4 py-3 text-(--admin-status-danger-text,#b42318)">
          {error}
        </p>
      )}
      {preview && (
        <div className="border-t border-(--admin-border-subtle,#e4e7ec) p-4">
          <div className="mb-3 flex items-center justify-between gap-3">
            <span className="adm-type-body-strong truncate">{preview.fileName}</span>
            <span className="flex shrink-0 gap-2">
              <a href={preview.url} download={preview.fileName} className="adm-type-link-default text-(--admin-brand-navy-900) hover:underline">
                Download
              </a>
              <AdminButton variant="secondary" onClick={close}>
                Close
              </AdminButton>
            </span>
          </div>
          {preview.contentType.startsWith('image/') ? (
            <img src={preview.url} alt={preview.fileName} className="max-h-[600px] w-full rounded object-contain" />
          ) : (
            <object data={preview.url} type={preview.contentType} className="h-[600px] w-full rounded" aria-label={preview.fileName}>
              <p className="adm-type-body-default">
                Preview not supported in this browser —{' '}
                <a href={preview.url} download={preview.fileName}>
                  download the file
                </a>
                .
              </p>
            </object>
          )}
        </div>
      )}
    </Panel>
  );
};
