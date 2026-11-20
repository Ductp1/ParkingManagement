import React from 'react';
import { AdminTitleBar } from '../layout/AdminTitleBar';
import { StateBlock } from './StateBlock';
import type { AdminQueryIssue, AdminQueryStatus } from '@/types/adminCommon';
import type { AdminApiError } from '@/api/adminClientApi';

interface ListScreenProps {
  title: string;
  subtitle?: string;
  actions?: React.ReactNode;
  /** Banner, sub-tabs… nằm giữa title bar và nội dung. */
  top?: React.ReactNode;
  /** Cột lọc 200px bên trái (FilterColumn); bỏ trống = bảng full chiều rộng. */
  filters?: React.ReactNode;
  toolbar?: React.ReactNode;
  children: React.ReactNode;
}

/** Bố cục màn danh sách (Figma "… / List – Default"): TitleBar → banner → [cột lọc | toolbar + bảng]. */
export const ListScreen: React.FC<ListScreenProps> = ({ title, subtitle, actions, top, filters, toolbar, children }) => (
  <>
    <AdminTitleBar title={title} subtitle={subtitle} actions={actions} />
    {top}
    <div className="flex items-start gap-4">
      {filters}
      <div className="flex min-w-0 flex-1 flex-col gap-4">
        {toolbar}
        {children}
      </div>
    </div>
  </>
);

/** Bố cục màn chi tiết: cột trái 2/3 + cột phải 1/3 (Figma 792 | 380). */
export const DetailColumns: React.FC<{ main: React.ReactNode; side?: React.ReactNode }> = ({ main, side }) => (
  <div className="flex flex-col items-start gap-4 xl:flex-row">
    {/* Panel trong mỗi cột hiện so le lần đầu (tối đa 8, --stagger-step); refetch không chạy lại vì panel không remount. */}
    <div className="adm-stagger-children flex w-full min-w-0 flex-col gap-4 xl:flex-[2]">{main}</div>
    {side && <div className="adm-stagger-children flex w-full min-w-0 flex-col gap-4 xl:flex-1">{side}</div>}
  </div>
);

/** Hàng tóm tắt trạng thái dưới title bar (Figma "Row": nhãn + Status … nút hành động bên phải). */
export const SummaryRow: React.FC<{ items: Array<{ label: string; value: React.ReactNode }>; actions?: React.ReactNode }> = ({ items, actions }) => (
  <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
    {items.map((item) => (
      <div key={item.label} className="flex items-center gap-2">
        <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">{item.label}</span>
        {item.value}
      </div>
    ))}
    {actions && <div className="ml-auto flex items-center gap-2">{actions}</div>}
  </div>
);

interface QueryIssueStateProps {
  status: AdminQueryIssue;
  error: AdminApiError | null;
  onRetry?: () => void;
  bare?: boolean;
  className?: string;
  /** Tiêu đề riêng cho "không tìm thấy" (vd. "Parking lot not found"). */
  notFoundTitle?: string;
}

/**
 * Màn chung cho notFound / offline / sessionExpired để mọi trang hiển thị giống nhau.
 * notFound ưu tiên message của server (ProblemDetails); offline có Retry; sessionExpired chỉ báo đang chuyển trang.
 */
export const QueryIssueState: React.FC<QueryIssueStateProps> = ({ status, error, onRetry, bare, className, notFoundTitle }) => {
  if (status === 'notFound') {
    return <StateBlock type="notFound" title={notFoundTitle} message={error?.problem ? error.message : undefined} bare={bare} className={className} />;
  }
  if (status === 'offline') return <StateBlock type="offline" onRetry={onRetry} bare={bare} className={className} />;
  return <StateBlock type="sessionExpired" bare={bare} className={className} />;
};

export interface QueryCopy {
  emptyTitle: string;
  emptyMessage: string;
  errorTitle: string;
  /** Không hiển thị cho người dùng (giữ để màn cũ không phải sửa); sẽ bỏ khi gom PageHeader (P1-9). */
  service?: string;
}

interface QueryStateProps {
  status: AdminQueryStatus;
  error: AdminApiError | null;
  copy: QueryCopy;
  onRetry: () => void;
  bare?: boolean;
}

/**
 * Chuyển trạng thái truy vấn → State block đúng Figma:
 * loading = skeleton, empty = EmptyState, forbidden = No permission, unavailable = endpoint chưa có, error = ErrorState + Retry.
 * Thông điệp lỗi ưu tiên message tiếng Việt từ API.
 */
export const QueryState: React.FC<QueryStateProps> = ({ status, error, copy, onRetry, bare = true }) => {
  switch (status) {
    case 'loading':
      return <StateBlock type="loading" bare={bare} />;
    case 'empty':
      return <StateBlock type="empty" title={copy.emptyTitle} message={copy.emptyMessage} bare={bare} />;
    case 'forbidden':
      return <StateBlock type="noPermission" message={error?.message} bare={bare} />;
    case 'unavailable':
      return <StateBlock type="unavailable" bare={bare} />;
    case 'notFound':
    case 'offline':
    case 'sessionExpired':
      return <QueryIssueState status={status} error={error} onRetry={onRetry} bare={bare} />;
    case 'error':
      return (
        <StateBlock
          type="error"
          title={copy.errorTitle}
          message={
            error?.problem
              ? error.message
              : 'The server did not respond. Try again.'
          }
          onRetry={onRetry}
          bare={bare}
        />
      );
    default:
      return null;
  }
};
