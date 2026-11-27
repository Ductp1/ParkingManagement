import { useState } from 'react';
import { Clock, Plus, RefreshCw, Zap } from 'lucide-react';
import { shiftApi, type ShiftView } from '@/api/shiftApi';
import { STATUS_LABEL, addDays, dateTime, dayLabel, dayMonth, ictDate, lotStatusLabel, mondayOf, staffName, timeRange, weekLabel } from '@/features/shifts/format';
import { Banner, Button, Field, LoadError, Page, Placeholder, StaffTabs, cardClass, inputClass, useLoad } from '@/features/shifts/ui';
import { useOwnerLot } from '../hooks/useOwnerLot';
import { ShiftEditorModal } from '../components/shifts/ShiftEditorModal';
import { ShiftTemplatesDrawer } from '../components/shifts/ShiftTemplatesDrawer';
import { AdHocShiftModal, CopyWeekModal, ShiftDetailModal } from '../components/shifts/ShiftQuickModals';

type Dialog =
  | { kind: 'editor'; shift?: ShiftView; templateId?: number }
  | { kind: 'detail'; shift: ShiftView }
  | { kind: 'templates' }
  | { kind: 'adhoc' }
  | { kind: 'copy' };

const MAX_WARNINGS = 4;
const DAY_SHORT = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

export function OwnerShiftSchedulePage() {
  const owner = useOwnerLot();
  const { lotId, lotName, names, selfId } = owner;
  const today = ictDate(Date.now());
  const weekStart = mondayOf(owner.params.get('week') ?? today);
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const [flash, setFlash] = useState<{ title: string; skipped?: string[] } | null>(null);

  const schedule = useLoad(`week:${lotId}:${weekStart}`, async () => (lotId ? shiftApi.week(lotId, weekStart) : { shifts: [], warnings: [] }));
  const templates = useLoad(`templates:${lotId}`, async () => (lotId ? shiftApi.templates(lotId) : []));

  const goWeek = (week: string) => { setFlash(null); owner.setParam('week', week); };
  const thisWeek = mondayOf(today);
  const weeks = [...new Set([...Array.from({ length: 13 }, (_, i) => addDays(thisWeek, (i - 4) * 7)), weekStart])].sort();
  const days = Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));
  const data = schedule.data;
  const who = (id: number) => staffName(id, names, selfId);
  const open = (next: Dialog) => { setFlash(null); setDialog(next); };
  const saved = (title: string) => { setDialog(null); setFlash({ title }); schedule.reload(); };
  /** "Nhân viên #5 2026-11-30T23:00:00Z: …" từ API → tên và giờ Việt Nam. */
  const skippedText = (line: string) => line.replace(/^Nhân viên #(\d+) (\S+?): /, (_, id, iso) => `${who(Number(id))} · ${dateTime(iso)}: `);

  return (
    <Page title="Lịch ca làm việc" subtitle="Xếp ca theo bãi, theo dõi khoảng trống và phân công đúng người.">
      <StaffTabs />
      {owner.lots.error != null && <LoadError title="Không tải được danh sách bãi" error={owner.lots.error} onRetry={owner.lots.reload} />}
      {owner.lots.data?.length === 0 ? (
        <Placeholder kind="empty" title="Chưa có bãi đỗ" message="Tạo bãi đỗ tại mục “Bãi đỗ & Sơ đồ” trước khi xếp ca." />
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Bãi đỗ">
              <select className={inputClass} value={lotId} onChange={(e) => { setFlash(null); owner.setParam('lot', e.target.value); }}>
                {owner.lots.data?.map((l) => <option key={l.id} value={l.id}>{l.name ?? `Bãi #${l.id}`} · {lotStatusLabel(l.status)}</option>)}
              </select>
            </Field>
            <Field label={weekStart === thisWeek ? 'Tuần hiện tại' : 'Tuần đang xem'}>
              <select className={inputClass} value={weekStart} onChange={(e) => goWeek(e.target.value)}>
                {weeks.map((w) => <option key={w} value={w}>{weekLabel(w)}</option>)}
              </select>
            </Field>
          </div>
          <div className="flex flex-wrap gap-2.5">
            <Button variant="secondary" onClick={() => goWeek(addDays(weekStart, -7))}>‹ Tuần trước</Button>
            <Button variant="secondary" onClick={() => goWeek(thisWeek)}>Hôm nay</Button>
            <Button variant="secondary" onClick={() => goWeek(addDays(weekStart, 7))}>Tuần sau ›</Button>
            <Button variant="secondary" disabled={!lotId} onClick={() => open({ kind: 'copy' })}><RefreshCw size={15} aria-hidden="true" />Sao chép tuần trước</Button>
            <Button variant="secondary" disabled={!templates.data} onClick={() => open({ kind: 'templates' })}><Clock size={15} aria-hidden="true" />Mẫu ca</Button>
            <Button variant="secondary" disabled={!owner.staff.data || !lotId} onClick={() => open({ kind: 'adhoc' })}><Zap size={15} aria-hidden="true" />Tạo ca nhanh</Button>
            <Button disabled={!owner.staff.data || !templates.data || !lotId} onClick={() => open({ kind: 'editor' })}><Plus size={16} aria-hidden="true" />Thêm ca</Button>
          </div>
          {flash && (
            <Banner tone={flash.skipped?.length ? 'warning' : 'success'} title={flash.title} onClose={() => setFlash(null)}>
              {!!flash.skipped?.length && <ul className="list-disc pl-4">{flash.skipped.map((s) => <li key={s}>{skippedText(s)}</li>)}</ul>}
            </Banner>
          )}
          {owner.staff.error != null && <LoadError title="Không tải được nhân viên của bãi" error={owner.staff.error} onRetry={owner.staff.reload} />}
          {templates.error != null && <LoadError title="Không tải được mẫu ca" error={templates.error} onRetry={templates.reload} />}

          {schedule.error != null ? <LoadError title="Không tải được lịch tuần" error={schedule.error} onRetry={schedule.reload} />
            : !data ? <Placeholder kind="loading" title="Đang tải lịch tuần" />
              : data.shifts.length === 0 ? (
                <Placeholder kind="empty" title="Tuần này chưa có ca" message="Sao chép tuần trước hoặc dùng mẫu ca để bắt đầu xếp lịch." />
              ) : (
                <>
                  {data.warnings.slice(0, MAX_WARNINGS).map((w) => (
                    <Banner key={w.code + w.fromUtc} tone="warning"
                      title={`${w.code === 'COVERAGE_GAP' ? 'Thiếu người trực' : 'Chưa có người giữ két'} · ${dayLabel(ictDate(w.fromUtc))}`}>
                      {w.code === 'COVERAGE_GAP' ? 'Chưa có người' : 'Chưa có người được thu tiền mặt'} từ {dateTime(w.fromUtc)} đến {dateTime(w.toUtc)}. Bạn vẫn có thể lưu lịch và bổ sung ca sau.
                    </Banner>
                  ))}
                  {data.warnings.length > MAX_WARNINGS && <p className="text-xs text-[#65738c]">Còn {data.warnings.length - MAX_WARNINGS} khoảng khác chưa đủ người.</p>}
                  <div className={cardClass}>
                    <div className="overflow-x-auto">
                      <div className="grid min-w-[900px] grid-cols-7 gap-1">
                        {days.map((day, i) => {
                          const items = [
                            ...data.shifts.filter((s) => s.businessDate === day).map((s) => ({ at: s.scheduledStartUtc, shift: s, gap: null })),
                            ...data.warnings.filter((w) => w.code === 'COVERAGE_GAP' && ictDate(w.fromUtc) === day).map((w) => ({ at: w.fromUtc, shift: null, gap: w })),
                          ].sort((a, b) => a.at.localeCompare(b.at));
                          return (
                            <section key={day} aria-label={dayLabel(day)} className="flex min-w-0 flex-col gap-1">
                              <h2 className={`rounded px-2 py-2.5 text-xs font-bold ${day === today ? 'bg-[#f0f6fe] text-[#2c5cf2]' : 'bg-[#f2f5f9] text-[#10172a]'}`}>
                                {DAY_SHORT[i]} · {dayMonth(day)}
                              </h2>
                              {items.map(({ shift, gap }) => shift ? (
                                <button key={shift.id} type="button"
                                  onClick={() => open(shift.status === 'Scheduled' ? { kind: 'editor', shift } : { kind: 'detail', shift })}
                                  className={`cursor-pointer rounded p-2 text-left text-xs hover:ring-2 hover:ring-[#2c5cf2]/30 ${shift.status === 'Missed' || shift.status === 'AutoClosed' ? 'bg-[#fff7ed]' : shift.isCashHolder ? 'bg-[#f0f6fe]' : 'bg-[#ecfdf3]'}`}>
                                  <span className="block truncate font-bold text-[#2c5cf2]">{templates.data?.find((t) => t.id === shift.templateId)?.name ?? 'Ca trực'}</span>
                                  <span className="block truncate text-[#10172a]">{who(shift.staffUserId)}</span>
                                  <span className="block font-mono text-[11px] text-[#65738c]">
                                    {timeRange(shift.scheduledStartUtc, shift.scheduledEndUtc)}{ictDate(shift.scheduledEndUtc) !== day ? ' (+1)' : ''}
                                  </span>
                                  {shift.isCashHolder && <span className="mt-1 block text-[11px] font-semibold text-[#2c5cf2]">Giữ két</span>}
                                  {shift.status !== 'Scheduled' && <span className="block text-[11px] font-semibold text-[#65738c]">{STATUS_LABEL[shift.status]}</span>}
                                </button>
                              ) : (
                                <div key={gap!.fromUtc} className="rounded bg-[#fdf2f2] p-2 text-xs text-[#dc2626]">
                                  <p className="font-bold">Chưa có người</p>
                                  <p className="font-mono text-[11px]">{timeRange(gap!.fromUtc, gap!.toUtc)}</p>
                                </div>
                              ))}
                            </section>
                          );
                        })}
                      </div>
                    </div>
                    <p className="mt-3 text-[11px] text-[#65738c]">1 ca = 1 nhân viên · Ca đêm được tính theo ngày bắt đầu · Giờ ICT (UTC+7)</p>
                  </div>
                </>
              )}
        </>
      )}

      {dialog?.kind === 'editor' && templates.data && (
        <ShiftEditorModal lotId={lotId} lotName={lotName} staff={owner.options} templates={templates.data} shift={dialog.shift}
          initialDate={today >= weekStart && today <= days[6] ? today : weekStart} initialTemplateId={dialog.templateId}
          onClose={() => setDialog(null)} onSaved={saved} />
      )}
      {dialog?.kind === 'detail' && <ShiftDetailModal shift={dialog.shift} who={who(dialog.shift.staffUserId)} onClose={() => setDialog(null)} />}
      {dialog?.kind === 'templates' && templates.data && (
        <ShiftTemplatesDrawer lotId={lotId} lotName={lotName} templates={templates.data} onClose={() => setDialog(null)}
          onChanged={templates.reload} onUse={(templateId) => setDialog({ kind: 'editor', templateId })} />
      )}
      {dialog?.kind === 'adhoc' && <AdHocShiftModal lotId={lotId} lotName={lotName} staff={owner.options} onClose={() => setDialog(null)} onSaved={saved} />}
      {dialog?.kind === 'copy' && (
        <CopyWeekModal lotId={lotId} lotName={lotName} weekStart={weekStart} weeks={weeks} onClose={() => setDialog(null)}
          onCopied={(result, toWeek) => {
            setDialog(null);
            owner.setParam('week', toWeek);
            setFlash({ title: `Đã sao chép ${result.created} ca${result.skipped.length ? `, bỏ qua ${result.skipped.length} ca` : ''}.`, skipped: result.skipped });
            schedule.reload();
          }} />
      )}
    </Page>
  );
}
