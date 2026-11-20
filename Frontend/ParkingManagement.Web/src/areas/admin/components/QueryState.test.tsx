import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { AdminQueryStatus } from '@/types/adminCommon';
import { QueryIssueState, QueryState } from './PageLayout';

const copy = { emptyTitle: 'No lots', emptyMessage: 'Nothing yet.', errorTitle: 'Could not load lots', service: 'ParkingService' };

const renderState = (status: AdminQueryStatus, error: AdminApiError | null, onRetry = vi.fn()) => {
  render(<QueryState status={status} error={error} copy={copy} onRetry={onRetry} />);
  return onRetry;
};

afterEach(cleanup);

describe('QueryState – trạng thái mới', () => {
  it('notFound hiện message của server, không có Retry', () => {
    renderState('notFound', new AdminApiError(404, 'Lot 99 does not exist.', { title: 'Not Found', detail: 'Lot 99 does not exist.' }));
    expect(screen.getByText('Not found')).toBeTruthy();
    expect(screen.getByText('Lot 99 does not exist.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Retry' })).toBeNull();
  });

  it('offline báo mất kết nối và Retry gọi lại', () => {
    const onRetry = renderState('offline', new AdminApiError(0, 'Network Error', null, true));
    expect(screen.getByRole('alert').textContent).toContain('You are offline');
    // Không lộ message kỹ thuật của trình duyệt/axios.
    expect(screen.queryByText(/Network Error/)).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('sessionExpired chỉ báo đang chuyển tới trang đăng nhập', () => {
    renderState('sessionExpired', new AdminApiError(401, 'Unauthorized'));
    expect(screen.getByRole('alert').textContent).toContain('Your session has ended');
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('error vẫn có Retry (không đổi)', () => {
    const onRetry = renderState('error', new AdminApiError(500, 'HTTP 500'));
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});

describe('QueryIssueState', () => {
  it('notFoundTitle thay tiêu đề mặc định', () => {
    render(<QueryIssueState status="notFound" error={null} notFoundTitle="Parking lot not found" />);
    expect(screen.getByText('Parking lot not found')).toBeTruthy();
    expect(screen.getByText('This record does not exist or has been removed.')).toBeTruthy();
  });
});
