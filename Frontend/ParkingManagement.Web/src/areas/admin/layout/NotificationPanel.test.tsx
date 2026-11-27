import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { NotificationDto } from '@/types/notification';
import { NotificationPanel } from './NotificationPanel';

const item = (id: number, title: string, dataJson: string | null): NotificationDto => ({
  id,
  channel: 'InApp',
  templateKey: 'KEY',
  title,
  body: 'Body',
  dataJson,
  status: 'Sent',
  isRead: false,
  createdAtUtc: '2026-11-27T00:00:00Z',
});

const renderPanel = (items: NotificationDto[]) => {
  const onRead = vi.fn();
  const onClose = vi.fn();
  render(
    <MemoryRouter initialEntries={['/admin/overview/dashboard']}>
      <Routes>
        <Route
          path="/admin/overview/dashboard"
          element={
            <NotificationPanel
              inbox={{
                data: { unreadCount: items.length, items: { items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1 } },
                status: 'ready',
                error: null,
                isFetching: false,
                refetch: () => undefined,
              }}
              onRead={onRead}
              onReadAll={() => undefined}
              onClose={onClose}
            />
          }
        />
        <Route path="/admin/operations/partner-onboarding/:id" element={<p>Application screen</p>} />
        <Route path="*" element={<p>Outside admin</p>} />
      </Routes>
    </MemoryRouter>,
  );
  return { onRead, onClose };
};

describe('NotificationPanel', () => {
  afterEach(cleanup);

  it('marks the notification read and opens its admin screen', () => {
    const { onRead, onClose } = renderPanel([item(1, 'New application', '{"adminPath":"/admin/operations/partner-onboarding/13"}')]);

    fireEvent.click(screen.getByText('New application'));

    expect(onRead).toHaveBeenCalledTimes(1);
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Application screen')).toBeTruthy();
  });

  it.each([
    ['no data', null],
    ['broken data', '{'],
    ['a path outside the admin portal', '{"adminPath":"https://example.com"}'],
  ])('only marks read when it carries %s', (_label, dataJson) => {
    const { onRead, onClose } = renderPanel([item(1, 'Plain', dataJson)]);

    fireEvent.click(screen.getByText('Plain'));

    expect(onRead).toHaveBeenCalledTimes(1);
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByText('Plain')).toBeTruthy();
  });
});
