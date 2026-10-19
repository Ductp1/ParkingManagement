import React, { useState } from 'react';
import { useParams } from 'react-router-dom';
import { adminUsersApi } from '@/api/adminUsersApi';
import { adminOperationsApi } from '@/api/adminOperationsApi';
import { SanctionStatus, SanctionSyncStatus, UserRoleType, UserStatus } from '@/types/adminEnums';
import type { AdminUserDto, SanctionDto } from '@/types/adminUsers';
import type { PagedResult } from '@/types/adminCommon';
import { AdminTitleBar } from '../../../layout/AdminTitleBar';
import { AdminButton } from '../../../components/AdminButton';
import { Banner } from '../../../components/Banner';
import { DetailColumns, QueryState } from '../../../components/PageLayout';
import { Panel } from '../../../components/Panel';
import { DetailList, DetailRow } from '../../../components/DetailRow';
import { StatusIndicator } from '../../../components/StatusIndicator';
import { StateBlock } from '../../../components/StateBlock';
import { CellAmount, CellLink, CellMono, CellMuted, CellStatus, CellText, DataTable } from '../../../components/DataTable';
import { useAdminQuery, type AdminQueryResult } from '../../../hooks/useAdminQuery';
import { useAdminBreadcrumb } from '../../../hooks/useAdminBreadcrumb';
import { useAdminToast } from '../../../hooks/useAdminToast';
import { humanizeEnum } from '../../../utils/enumLabels';
import { formatDateTime, formatShortDateTime, formatVnd, orEmpty } from '../../../utils/format';
import { LockOwnerDialog, RetrySyncDialog, UnlockOwnerDialog } from './UserDialogs';

type UserDialog = 'lock' | 'unlock' | 'retry' | null;

const queryCopy = (title: string, service: string) => ({ emptyTitle: 'Nothing here yet', emptyMessage: 'No records.', errorTitle: title, service });

/** Panel con tự gọi API riêng và hiển thị trạng thái gọn trong panel. */
const PanelQuery = <T,>({ query, service, title, children }: { query: AdminQueryResult<T>; service: string; title: string; children: (data: T) => React.ReactNode }) =>
  query.status === 'ready' && query.data !== null ? (
    <>{children(query.data)}</>
  ) : (
    <QueryState status={query.status} error={query.error} onRetry={query.refetch} copy={queryCopy(title, service)} />
  );

const OwnerSections: React.FC<{ user: AdminUserDto; sanctions: AdminQueryResult<PagedResult<SanctionDto>> }> = ({ user, sanctions }) => {
  const ownerProfileId = user.ownerProfile!.id;
  const lots = useAdminQuery(() => adminOperationsApi.getOwnerParkingLots(ownerProfileId), [ownerProfileId], (d) => d.length === 0);

  return (
    <>
      <Panel title="Owner profile" description="OwnerProfiles">
        <DetailList>
          <DetailRow label="OwnerProfile">#{ownerProfileId}</DetailRow>
          <DetailRow label="Business name">{user.ownerProfile!.businessName}</DetailRow>
          <DetailRow label="Contract" future>
            —
          </DetailRow>
        </DetailList>
      </Panel>
      <Panel title="Lots" description="ParkingService · ParkingLots.OwnerProfileId" padded={false}>
        <PanelQuery query={lots} service="ParkingService" title="Could not load lots">
          {(data) => (
            <DataTable
              bare
              rows={data}
              rowKey={(r) => r.id}
              columns={[
                { key: 'lot', header: 'Lot', render: (r) => <CellLink to={`/admin/operations/parking-lots/${r.id}`}>{r.name}</CellLink> },
                { key: 'city', header: 'City', render: (r) => <CellText>{r.city}</CellText> },
                { key: 'slots', header: 'Slots', render: (r) => <CellText>{r.totalSlots}</CellText> },
                { key: 'status', header: 'Status', render: (r) => <CellStatus value={r.status} /> },
              ]}
            />
          )}
        </PanelQuery>
      </Panel>
      <Panel title="Sanction history" description="AdminService · Sanctions (owner- and lot-level)" padded={false}>
        <PanelQuery query={sanctions} service="AdminService" title="Could not load sanctions">
          {(data) => (
            <DataTable
              bare
              rows={data.items}
              rowKey={(r) => r.id}
              placeholder={<StateBlock type="empty" bare title="No sanctions" message="Issued sanctions appear here." />}
              columns={[
                { key: 'id', header: '#', width: 50, render: (r) => <CellMono>{r.id}</CellMono> },
                { key: 'level', header: 'Level', render: (r) => <CellText>{humanizeEnum(r.level)}</CellText> },
                { key: 'scope', header: 'Scope', render: (r) => <CellText>{r.parkingLotId ? `Lot #${r.parkingLotId}` : 'Owner'}</CellText> },
                { key: 'reason', header: 'Reason', render: (r) => <CellText>{r.reason}</CellText> },
                {
                  key: 'period',
                  header: 'Period',
                  render: (r) => (
                    <CellMuted>
                      {formatDateTime(r.startsAtUtc)} → {r.endsAtUtc ? formatDateTime(r.endsAtUtc) : '—'}
                    </CellMuted>
                  ),
                },
                { key: 'status', header: 'Status', render: (r) => <CellStatus value={r.status} /> },
                { key: 'sync', header: 'UserService sync', render: (r) => <CellStatus value={r.userServiceSyncStatus} /> },
              ]}
            />
          )}
        </PanelQuery>
      </Panel>
    </>
  );
};

const DriverSections: React.FC<{ userId: number }> = ({ userId }) => {
  const vehicles = useAdminQuery(() => adminOperationsApi.getUserVehicles(userId), [userId], (d) => d.length === 0);
  const bookings = useAdminQuery(() => adminOperationsApi.getUserBookings(userId), [userId], (d) => d.length === 0);
  return (
    <>
      <Panel title="Vehicles" description="VehicleService · Vehicles" padded={false}>
        <PanelQuery query={vehicles} service="VehicleService" title="Could not load vehicles">
          {(data) => (
            <DataTable
              bare
              rows={data}
              rowKey={(r) => r.id}
              columns={[
                { key: 'plate', header: 'Plate', render: (r) => <CellMono>{r.plateDisplay || r.plateNumber}</CellMono> },
                { key: 'type', header: 'Type', render: (r) => <CellText>{humanizeEnum(r.vehicleType)}</CellText> },
                { key: 'fuel', header: 'Fuel', render: (r) => <CellText>{humanizeEnum(r.fuelType)}</CellText> },
                { key: 'model', header: 'Brand / model', render: (r) => <CellText>{[r.brand, r.model].filter(Boolean).join(' ') || '—'}</CellText> },
                { key: 'color', header: 'Color', render: (r) => <CellText>{orEmpty(r.color)}</CellText> },
                { key: 'height', header: 'Height', render: (r) => <CellText>{r.heightCm} cm</CellText> },
                { key: 'default', header: 'Default', render: (r) => <CellText>{r.isDefault ? 'Yes' : 'No'}</CellText> },
              ]}
            />
          )}
        </PanelQuery>
      </Panel>
      <Panel title="Recent bookings" description="BookingService · Bookings" padded={false}>
        <PanelQuery query={bookings} service="BookingService" title="Could not load bookings">
          {(data) => (
            <DataTable
              bare
              rows={data.slice(0, 5)}
              rowKey={(r) => r.id}
              columns={[
                { key: 'code', header: 'Booking', render: (r) => <CellLink to={`/admin/operations/bookings/${r.code}`}>{r.code}</CellLink> },
                { key: 'lot', header: 'Lot', render: (r) => <CellText>{r.parkingLotName}</CellText> },
                { key: 'start', header: 'Start', render: (r) => <CellMuted>{formatShortDateTime(r.startAtUtc)}</CellMuted> },
                { key: 'status', header: 'Status', render: (r) => <CellStatus value={r.status} /> },
                { key: 'total', header: 'Total', render: (r) => <CellAmount>{formatVnd(r.totalAmount)}</CellAmount> },
              ]}
            />
          )}
        </PanelQuery>
      </Panel>
    </>
  );
};

/** Operations / Users / Detail – Driver / Owner / Owner locked / sync failed / Staff (US-096). */
export const UserDetailPage: React.FC = () => {
  const { id } = useParams();
  const userId = Number(id);
  const valid = Number.isInteger(userId) && userId > 0;
  const toast = useAdminToast();
  const [dialog, setDialog] = useState<UserDialog>(null);

  const query = useAdminQuery(valid ? () => adminUsersApi.getById(userId) : null, [userId]);
  const user = query.data;
  const ownerProfileId = user?.ownerProfile?.id ?? null;
  const sanctions = useAdminQuery(
    ownerProfileId ? () => adminUsersApi.listOwnerSanctions(ownerProfileId, 1, 20) : null,
    [ownerProfileId],
    (d) => d.items.length === 0
  );

  useAdminBreadcrumb(user ? user.fullName : valid ? `#${userId}` : null);

  if (!user) {
    return (
      <>
        <AdminTitleBar title="Users" subtitle="—" />
        <QueryState
          status={valid ? query.status : 'empty'}
          error={query.error}
          onRetry={query.refetch}
          bare={false}
          copy={{ emptyTitle: 'User not found', emptyMessage: 'Check the link or go back to the Users list.', errorTitle: 'Could not load the user', service: 'UserService' }}
        />
      </>
    );
  }

  const isOwner = user.roles.includes(UserRoleType.LotOwner) && user.ownerProfile !== null;
  const isDriver = user.roles.includes(UserRoleType.Driver);
  const isLocked = user.status === UserStatus.Locked;
  const latest = sanctions.data?.items[0] ?? null;
  const activeLock = sanctions.data?.items.find((s) => s.status === SanctionStatus.Active && s.parkingLotId === null) ?? null;
  const syncFailed = latest !== null && latest.userServiceSyncStatus === SanctionSyncStatus.Failed;

  const subtitle = [
    `User #${user.id}`,
    user.roles.map(humanizeEnum).join(', '),
    user.ownerProfile ? `OwnerProfile #${user.ownerProfile.id}` : null,
    isLocked ? 'Locked' : null,
  ]
    .filter(Boolean)
    .join(' · ');

  const titleAction = !isOwner ? undefined : syncFailed ? (
    <AdminButton onClick={() => setDialog('retry')}>Retry sync</AdminButton>
  ) : isLocked ? (
    <AdminButton onClick={() => setDialog('unlock')}>Unlock owner</AdminButton>
  ) : (
    <AdminButton variant="destructive" onClick={() => setDialog('lock')}>
      Lock owner
    </AdminButton>
  );

  const done = (title: string, message: string) => {
    toast.show({ type: 'success', title, message });
    query.refetch();
    sanctions.refetch();
  };

  return (
    <>
      <AdminTitleBar title={user.fullName} subtitle={subtitle} actions={titleAction} />

      {isOwner && isLocked && latest && !syncFailed && (
        <Banner tone="danger">
          Owner locked {formatDateTime(latest.startsAtUtc)} ICT by Admin #{latest.issuedByUserId} · Sanction #{latest.id} {humanizeEnum(latest.level)} · “{latest.reason}”.
        </Banner>
      )}
      {isOwner && syncFailed && latest && (
        <Banner tone="danger">
          Sanction #{latest.id} saved, but UserService did not apply the change (sync Failed). Retry — the original reason and end date are used.
        </Banner>
      )}

      <DetailColumns
        main={
          <>
            <Panel title="Account" description="Users">
              <DetailList>
                <DetailRow label="User ID">{user.id}</DetailRow>
                <DetailRow label="Email">{orEmpty(user.email)}</DetailRow>
                <DetailRow label="Phone">{orEmpty(user.phoneNumber)}</DetailRow>
                <DetailRow label="Status">
                  <StatusIndicator value={user.status} />
                </DetailRow>
              </DetailList>
            </Panel>
            <Panel title="KYC" description="Users.Kyc* (number stored AES-256 encrypted)">
              <DetailList>
                <DetailRow label="KYC status">
                  <StatusIndicator value={user.kycStatus} />
                </DetailRow>
              </DetailList>
            </Panel>
            {isOwner && <OwnerSections user={user} sanctions={sanctions} />}
            {isDriver && <DriverSections userId={user.id} />}
          </>
        }
        side={
          <>
            <Panel title="Roles" description="UserRoles" padded={false}>
              <DataTable
                bare
                rows={user.roles}
                rowKey={(r) => r}
                columns={[{ key: 'role', header: 'Role', render: (r) => <CellText>{humanizeEnum(r)}</CellText> }]}
              />
            </Panel>
            <Panel title="Security events" description="SecurityEvents (latest 3)">
              <StateBlock type="unavailable" bare className="min-h-0 py-4" message="UserService has no endpoint for security events yet." />
            </Panel>
            <Panel title="Data subject requests" description="DataSubjectRequests">
              <StateBlock type="unavailable" bare className="min-h-0 py-4" message="UserService has no endpoint for data requests yet." />
            </Panel>
          </>
        }
      />

      {isOwner && (
        <>
          <LockOwnerDialog
            open={dialog === 'lock'}
            user={user}
            onClose={() => setDialog(null)}
            onDone={() => done('Owner locked', `${user.fullName} can no longer sign in.`)}
          />
          <UnlockOwnerDialog
            open={dialog === 'unlock'}
            user={user}
            activeLock={activeLock}
            onClose={() => setDialog(null)}
            onDone={() => done('Owner unlocked', `${user.fullName} can sign in again.`)}
          />
          <RetrySyncDialog open={dialog === 'retry'} user={user} sanction={latest} onClose={() => setDialog(null)} />
        </>
      )}
    </>
  );
};
