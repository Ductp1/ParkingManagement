import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Plus } from 'lucide-react';
import { STAFF_PAGE_SIZE, ownerStaffApi, type StaffItemDto } from '@/api/ownerStaffApi';
import { errorCode, errorText, lotStatusLabel } from '@/features/shifts/format';
import { Banner, Button, Field, LinkButton, LoadError, Page, Placeholder, StaffTabs, cardClass, inputClass, useLoad } from '@/features/shifts/ui';
import { AddStaffModal } from '../components/staff/AddStaffModal';
import { RevokeStaffModal } from '../components/staff/RevokeStaffModal';

type Flash = { tone: 'success' | 'info' | 'danger'; title: string; message: string };

export function OwnerStaffPage() {
  const [params, setParams] = useSearchParams();
  const lotId = Number(params.get('lot')) || undefined;
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [search, setSearch] = useState('');
  const [inviting, setInviting] = useState(false);
  const [revoking, setRevoking] = useState<StaffItemDto | null>(null);
  const [sending, setSending] = useState<number | null>(null);
  const [flash, setFlash] = useState<Flash | null>(null);

  const lots = useLoad('lots', () => ownerStaffApi.getOwnerLots());
  const staff = useLoad(`staff:${page}:${lotId ?? ''}`, () => ownerStaffApi.getStaffList(page, lotId));

  const lotName = (id: number) => lots.data?.find((l) => l.id === id)?.name ?? `Bãi #${id}`;
  const items = staff.data ?? [];
  // shortcut: lọc trạng thái / tên trên trang đang tải (20 dòng); API chưa có tham số tìm kiếm.
  const needle = search.trim().toLowerCase();
  const rows = items.filter((i) => (!status || i.activationStatus === status)
    && (!needle || i.fullName.toLowerCase().includes(needle) || (i.email ?? '').toLowerCase().includes(needle)));
  const invited = items.filter((i) => i.activationStatus === 'Invited').length;
  const filtered = !!(needle || status || lotId);

  async function resend(item: StaffItemDto) {
    setSending(item.id);
    setFlash(null);
    try {
      await ownerStaffApi.resendInvite(item.id);
      setFlash({ tone: 'success', title: 'Đã gửi lại lời mời', message: `Link kích hoạt mới đã được gửi đến ${item.email ?? item.fullName}. Link có hiệu lực 72 giờ, link cũ không còn dùng được.` });
    } catch (error) {
      if (errorCode(error) === 'STAFF_ALREADY_ACTIVE') {
        setFlash({ tone: 'info', title: 'Nhân viên đã kích hoạt tài khoản', message: 'Không cần gửi lại lời mời.' });
        staff.reload();
      } else setFlash({ tone: 'danger', title: 'Chưa thể gửi lời mời', message: errorText(error) });
    } finally {
      setSending(null);
    }
  }

  const invite = () => { setFlash(null); setInviting(true); };

  return (
    <Page title="Nhân viên & Phân công" subtitle="Mời nhân viên bằng email riêng, quản lý quyền vận hành theo từng bãi đỗ.">
      {flash && <Banner tone={flash.tone} title={flash.title} onClose={() => setFlash(null)}>{flash.message}</Banner>}
      <StaffTabs />
      <div className="grid gap-4 md:grid-cols-3">
        <Field label="Bãi đỗ">
          <select className={inputClass} value={lotId ?? ''} onChange={(e) => { setPage(1); setParams(e.target.value ? { lot: e.target.value } : {}); }}>
            <option value="">Tất cả bãi đỗ</option>
            {lots.data?.map((l) => <option key={l.id} value={l.id}>{l.name ?? `Bãi #${l.id}`}</option>)}
          </select>
        </Field>
        <Field label="Trạng thái">
          <select className={inputClass} value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">Tất cả trạng thái</option>
            <option value="Active">Đang hoạt động</option>
            <option value="Invited">Chờ kích hoạt</option>
          </select>
        </Field>
        <Field label="Tìm kiếm">
          <input className={inputClass} value={search} placeholder="Tên hoặc email nhân viên" onChange={(e) => setSearch(e.target.value)} />
        </Field>
      </div>
      {lots.error != null && <LoadError title="Không tải được danh sách bãi" error={lots.error} onRetry={lots.reload} />}
      {lots.data?.length === 0 && (
        <Banner tone="warning" title="Chưa có bãi đỗ để phân công">Hãy tạo bãi đỗ tại mục “Bãi đỗ & Sơ đồ” trước khi mời nhân viên.</Banner>
      )}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-[13px] font-semibold text-[#10172a]">
          {!staff.data ? 'Danh sách nhân viên'
            : items.length === 0 ? '0 nhân viên · Chưa có phân công'
              : `${items.length} phân công · ${items.length - invited} đang hoạt động · ${invited} chờ kích hoạt`}
        </p>
        <Button onClick={invite} disabled={!lots.data?.length}><Plus size={16} aria-hidden="true" />Mời nhân viên</Button>
      </div>

      {staff.error != null ? <LoadError title="Không tải được danh sách" error={staff.error} onRetry={staff.reload} />
        : !staff.data ? <Placeholder kind="loading" title="Đang tải nhân viên" />
          : rows.length === 0 ? (
            <Placeholder kind="empty" title={filtered ? 'Không có nhân viên phù hợp' : 'Chưa có nhân viên'}
              message={filtered ? 'Thử đổi bãi, trạng thái hoặc từ khóa.' : 'Mời nhân viên đầu tiên để phân công và xếp ca tại bãi.'}>
              {!filtered && !!lots.data?.length && <Button onClick={invite}><Plus size={16} aria-hidden="true" />Mời nhân viên đầu tiên</Button>}
            </Placeholder>
          ) : (
            <div className={cardClass}>
              <div className="overflow-x-auto rounded-[10px] border border-[#e3e8f0]">
                <table className="w-full min-w-[640px] text-left text-[13px]">
                  <thead className="bg-[#f2f5f9] text-xs font-semibold tracking-[0.6px] text-[#65738c] uppercase">
                    <tr><th className="px-4 py-4">Nhân viên</th><th className="px-4 py-4">Bãi đỗ</th><th className="px-4 py-4">Trạng thái</th><th className="px-4 py-4">Thao tác</th></tr>
                  </thead>
                  <tbody className="divide-y divide-[#e3e8f0]">
                    {rows.map((item) => (
                      <tr key={item.id} className="h-[76px]">
                        <td className="px-4 py-3">
                          <p className="text-xs font-semibold text-[#10172a]">{item.fullName}</p>
                          {item.email && <p className="mt-0.5 text-[11px] break-all text-[#65738c]">{item.email}</p>}
                        </td>
                        <td className="px-4 py-3">
                          {lotName(item.parkingLotId)}
                          {item.lotStatus && item.lotStatus !== 'Active' && <p className="mt-0.5 text-[11px] text-[#d97706]">Bãi {lotStatusLabel(item.lotStatus).toLowerCase()}</p>}
                        </td>
                        <td className="px-4 py-3">{item.activationStatus === 'Invited' ? 'Chờ kích hoạt' : 'Đang hoạt động'}</td>
                        <td className="px-4 py-3">
                          <div className="flex flex-wrap gap-4">
                            {item.activationStatus === 'Invited' && (
                              <LinkButton disabled={sending !== null} onClick={() => void resend(item)}>
                                {sending === item.id ? 'Đang gửi…' : 'Gửi lại lời mời'}
                              </LinkButton>
                            )}
                            <LinkButton onClick={() => { setFlash(null); setRevoking(item); }}>Gỡ khỏi bãi</LinkButton>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {(page > 1 || items.length === STAFF_PAGE_SIZE) && (
                <div className="mt-3 flex items-center justify-between gap-3 text-xs text-[#65738c]">
                  <span>Trang {page}</span>
                  <span className="flex gap-2">
                    <Button variant="secondary" disabled={page === 1} onClick={() => setPage(page - 1)}>Trang trước</Button>
                    <Button variant="secondary" disabled={items.length < STAFF_PAGE_SIZE} onClick={() => setPage(page + 1)}>Trang sau</Button>
                  </span>
                </div>
              )}
            </div>
          )}

      <Banner tone="info" title="Nhân viên có thể làm tại nhiều bãi thuộc cùng một chủ bãi">
        Bãi chưa hoạt động vẫn được phân công nhân viên, nhưng không thể bắt đầu ca hoặc vận hành cổng.
      </Banner>

      {inviting && lots.data && (
        <AddStaffModal lots={lots.data} defaultLotId={lotId} onClose={() => setInviting(false)}
          onInvited={(item) => {
            setInviting(false);
            setFlash(item.activationStatus === 'Invited'
              ? { tone: 'success', title: 'Đã gửi lời mời', message: `Link kích hoạt đã được gửi đến ${item.email ?? item.fullName}.` }
              : { tone: 'success', title: 'Đã phân công nhân viên', message: `${item.fullName} được thêm tại ${lotName(item.parkingLotId)}.` });
            setPage(1);
            staff.reload();
          }} />
      )}
      {revoking && (
        <RevokeStaffModal staff={revoking} lotName={lotName(revoking.parkingLotId)} onClose={() => setRevoking(null)}
          onRevoked={() => {
            setFlash({ tone: 'success', title: 'Đã gỡ nhân viên khỏi bãi', message: `${revoking.fullName} không còn quyền vận hành tại ${lotName(revoking.parkingLotId)}.` });
            setRevoking(null);
            staff.reload();
          }} />
      )}
    </Page>
  );
}
