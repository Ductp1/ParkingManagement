import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { AlertCircle, ReceiptText } from 'lucide-react';
import { getMyHistory } from '@/api/accountApi';
import type { HistoryFilter, ParkingHistoryItem, ParkingHistoryPage } from '@/types/account';
import { bookingLabels, dateTime, methodLabels, money, paymentLabels, time } from './accountFormat';

const filters: { id: HistoryFilter; label: string }[] = [
  { id: 'all', label: 'Tất cả' },
  { id: 'upcoming', label: 'Sắp tới' },
  { id: 'active', label: 'Đang gửi' },
  { id: 'completed', label: 'Đã xong' },
  { id: 'cancelled', label: 'Đã hủy' },
];

function duration(start: string, end: string) {
  const minutes = Math.max(0, Math.round((new Date(end).getTime() - new Date(start).getTime()) / 60000));
  if (minutes < 60) return `${minutes} phút`;
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  const rest = minutes % 60;
  return [days && `${days} ngày`, hours && `${hours} giờ`, !days && rest && `${rest} phút`].filter(Boolean).join(' ');
}

export function HistoryPage() {
  const [filter, setFilter] = useState<HistoryFilter>('all');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<ParkingHistoryPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [reload, setReload] = useState(0);
  const [open, setOpen] = useState<number | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    getMyHistory(filter, page, controller.signal)
      .then(setData)
      .catch(err => { if (!controller.signal.aborted) setError(err instanceof Error ? err.message : 'Không tải được lịch sử.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [filter, page, reload]);

  // Nhóm theo tháng của giờ bắt đầu; danh sách đã sắp mới nhất trước từ máy chủ.
  const months = useMemo(() => {
    const groups: { key: string; label: string; items: ParkingHistoryItem[] }[] = [];
    for (const item of data?.items ?? []) {
      const d = new Date(item.startAtUtc);
      const key = `${d.getFullYear()}-${d.getMonth()}`;
      let group = groups.find(g => g.key === key);
      if (!group) groups.push(group = { key, label: `Tháng ${d.getMonth() + 1}, ${d.getFullYear()}`, items: [] });
      group.items.push(item);
    }
    return groups;
  }, [data]);

  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  const s = data?.summary;

  return <>
    <header className="acct-head">
      <h1 className="acct-title">Lịch sử gửi xe</h1>
      <p className="acct-lede">Mọi lượt đặt chỗ, gửi xe và khoản thanh toán của bạn, mới nhất ở trên cùng.</p>
    </header>

    {s && s.totalBookings > 0 && <p className="ledger-summary">
      Bạn đã đặt <strong>{s.totalBookings}</strong> lượt, hoàn tất <strong>{s.completedBookings}</strong> lượt với tổng chi <strong>{money(s.totalSpent)}</strong>.
    </p>}

    <div className="acct-segment" role="group" aria-label="Lọc theo trạng thái">
      {filters.map(f => <button key={f.id} type="button" aria-pressed={filter === f.id} disabled={loading && filter === f.id}
        onClick={() => { setFilter(f.id); setPage(1); setOpen(null); }}>{f.label}</button>)}
    </div>

    {error && <div role="alert" className="acct-note error mt-6"><AlertCircle size={18} /><span>{error} <button onClick={() => setReload(n => n + 1)}>Thử lại</button></span></div>}
    {data && !data.paymentsAvailable && <div role="status" className="acct-note warn mt-6"><AlertCircle size={18} /><span>Chưa lấy được chi tiết thanh toán lúc này. Lượt gửi vẫn hiển thị đầy đủ; thử tải lại sau ít phút.</span></div>}
    {loading && !data && <p className="acct-muted mt-6" role="status">Đang tải lịch sử…</p>}

    {data && data.items.length === 0 && !loading && <div className="acct-empty">
      <ReceiptText size={34} className="text-slate-400" aria-hidden="true" />
      <p className="acct-muted">{filter === 'all' ? 'Bạn chưa có lượt gửi xe nào.' : 'Không có lượt nào trong mục này.'}</p>
      {filter === 'all' && <Link to="/driver/parking-lots" className="acct-btn">Tìm bãi đỗ gần bạn</Link>}
    </div>}

    <div aria-busy={loading}>
      {months.map(m => <section key={m.key} className="ledger-month" aria-labelledby={`m-${m.key}`}>
        <h3 id={`m-${m.key}`}>{m.label}</h3>
        {m.items.map(item => <LedgerRow key={item.bookingId} item={item} open={open === item.bookingId}
          onToggle={() => setOpen(v => v === item.bookingId ? null : item.bookingId)} />)}
      </section>)}
    </div>

    {data && pages > 1 && <nav className="acct-pager" aria-label="Phân trang">
      <button className="acct-btn ghost" disabled={page === 1 || loading} onClick={() => { setPage(p => p - 1); setOpen(null); }}>Trang trước</button>
      <span className="acct-faint">Trang {page} / {pages}</span>
      <button className="acct-btn ghost" disabled={page >= pages || loading} onClick={() => { setPage(p => p + 1); setOpen(null); }}>Trang sau</button>
    </nav>}
  </>;
}

function LedgerRow({ item, open, onToggle }: { item: ParkingHistoryItem; open: boolean; onToggle: () => void }) {
  const start = new Date(item.startAtUtc);
  const state = bookingLabels[item.status] ?? { text: item.status, tone: 'idle' as const };
  const paid = item.payments.filter(p => p.status === 'Succeeded' || p.status.includes('Refunded'));
  const refunded = item.payments.reduce((sum, p) => sum + p.refundedAmount, 0);
  const detailId = `booking-${item.bookingId}`;
  return <div className="ledger-row">
    <button type="button" className="ledger-trigger" aria-expanded={open} aria-controls={detailId} onClick={onToggle}>
      <span className="ledger-date"><b>{String(start.getDate()).padStart(2, '0')}</b><span>{start.toLocaleDateString('vi-VN', { weekday: 'short' })}</span></span>
      <span className="ledger-what">
        <span className="ledger-lot block">{item.parkingLotName}</span>
        <span className="ledger-meta">
          <span className="plate">{item.plateNumber}</span>
          <span>{time(item.startAtUtc)} đến {time(item.endAtUtc)}, {duration(item.startAtUtc, item.endAtUtc)}</span>
          {item.slotCode && <span>Ô {item.slotCode}</span>}
        </span>
      </span>
      <span className="ledger-money">
        <b>{money(item.totalAmount)}</b>
        <span className={`acct-state ${state.tone}`}>{state.text}</span>
      </span>
    </button>
    {open && <div className="ledger-detail" id={detailId}>
      <dl>
        <dt>Mã đặt chỗ</dt><dd className="acct-num" style={{ fontSize: 16 }}>{item.code}</dd>
        <dt>Thời gian</dt><dd>{dateTime(item.startAtUtc)} đến {dateTime(item.endAtUtc)}</dd>
        {refunded > 0 && <><dt>Đã hoàn</dt><dd>{money(refunded)}</dd></>}
      </dl>
      {item.payments.length === 0
        ? <p className="acct-faint">Chưa có giao dịch thanh toán cho lượt này.</p>
        : item.payments.map(p => <div key={p.code} className="ledger-pay">
          <span><strong>{methodLabels[p.method] ?? p.method}</strong> <span className="acct-faint">{p.paidAtUtc ? dateTime(p.paidAtUtc) : p.code}</span></span>
          <span className="flex gap-3 items-center"><span className="acct-num" style={{ fontSize: 16 }}>{money(p.amount)}</span><span className="acct-faint">{paymentLabels[p.status] ?? p.status}</span></span>
          {p.invoiceNumber && <span className="acct-faint w-full">Hóa đơn {p.invoiceNumber}</span>}
        </div>)}
      {paid.length === 0 && item.payments.length > 0 && <p className="acct-faint">Chưa có khoản thanh toán thành công.</p>}
      <Link className="acct-btn quiet justify-self-start" to={`/driver/history`}>Xem trong mục quản lý đặt chỗ</Link>
    </div>}
  </div>;
}
