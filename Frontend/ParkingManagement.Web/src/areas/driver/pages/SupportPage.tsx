import React, { useCallback, useEffect, useState } from 'react';
import { useAppSelector } from '@/store';
import { supportApi } from '@/api/supportApi';
import type {
  ComplaintCategory,
  ComplaintDetailDto,
  ComplaintSummaryDto,
  ContactChannelDto,
  FaqCategoryDto,
} from '@/types/support';

const CATEGORY_OPTIONS: Array<{ value: ComplaintCategory; label: string }> = [
  { value: 'LotFull', label: 'Bãi đầy / không nhận xe khi đã đặt' },
  { value: 'SlotOccupied', label: 'Ô đỗ đã có xe khác chiếm chỗ' },
  { value: 'Overcharge', label: 'Thu sai / tính thừa phí' },
  { value: 'RefundRequest', label: 'Yêu cầu hoàn tiền' },
  { value: 'Damage', label: 'Sự cố phương tiện tại bãi' },
  { value: 'StaffBehavior', label: 'Thái độ / hỗ trợ nhân viên' },
  { value: 'AppError', label: 'Lỗi ứng dụng / QR / thanh toán' },
  { value: 'Other', label: 'Vấn đề khác' },
];

const STATUS_BADGE: Record<string, string> = {
  Open: 'bg-blue-100 text-blue-700',
  AwaitingOwner: 'bg-amber-100 text-amber-800',
  OwnerResponded: 'bg-indigo-100 text-indigo-700',
  Escalated: 'bg-rose-100 text-rose-700',
  Resolved: 'bg-emerald-100 text-emerald-700',
  Rejected: 'bg-slate-200 text-slate-700',
};

const extractApiError = (err: unknown, fallback: string): string => {
  const axiosErr = err as { response?: { data?: { detail?: string; title?: string; message?: string } } };
  return (
    axiosErr?.response?.data?.detail ||
    axiosErr?.response?.data?.title ||
    axiosErr?.response?.data?.message ||
    (err instanceof Error ? err.message : fallback)
  );
};

export const SupportPage: React.FC = () => {
  const { user } = useAppSelector((state) => state.auth);

  const [activeMainTab, setActiveMainTab] = useState<'faq' | 'create' | 'tracking'>('faq');

  // FAQ State (GET /api/v1/faqs?audience=Driver)
  const [faqCategories, setFaqCategories] = useState<FaqCategoryDto[]>([]);
  const [contacts, setContacts] = useState<ContactChannelDto[]>([]);
  const [faqLoading, setFaqLoading] = useState<boolean>(true);
  const [faqError, setFaqError] = useState<string | null>(null);
  const [activeCategory, setActiveCategory] = useState<string>('all');
  const [openFaqIds, setOpenFaqIds] = useState<number[]>([]);

  // Complaint Form State (POST /api/v1/complaints)
  const [bookingCode, setBookingCode] = useState<string>('');
  const [category, setCategory] = useState<ComplaintCategory>('AppError');
  const [requestedAmount, setRequestedAmount] = useState<string>('');
  const [description, setDescription] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Complaint List & Detail State (GET /api/v1/complaints/my & /{code})
  const [complaints, setComplaints] = useState<ComplaintSummaryDto[]>([]);
  const [complaintsLoading, setComplaintsLoading] = useState<boolean>(false);
  const [complaintsError, setComplaintsError] = useState<string | null>(null);
  const [selectedComplaint, setSelectedComplaint] = useState<ComplaintDetailDto | null>(null);
  const [detailLoading, setDetailLoading] = useState<boolean>(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const loadHelpCenter = useCallback(async () => {
    setFaqLoading(true);
    setFaqError(null);
    try {
      const data = await supportApi.getHelpCenter('Driver');
      setFaqCategories(data.categories);
      setContacts(data.contacts);
      const firstId = data.categories[0]?.items[0]?.id;
      if (firstId) setOpenFaqIds([firstId]);
    } catch (err) {
      setFaqError(extractApiError(err, 'Không thể tải danh sách câu hỏi thường gặp.'));
    } finally {
      setFaqLoading(false);
    }
  }, []);

  const loadMyComplaints = useCallback(async () => {
    if (!user?.id) return;
    setComplaintsLoading(true);
    setComplaintsError(null);
    try {
      const list = await supportApi.listMyComplaints(user.id);
      setComplaints(list);
    } catch (err) {
      setComplaintsError(extractApiError(err, 'Không thể tải danh sách phiếu hỗ trợ.'));
    } finally {
      setComplaintsLoading(false);
    }
  }, [user?.id]);

  useEffect(() => {
    void loadHelpCenter();
    void loadMyComplaints();
  }, [loadHelpCenter, loadMyComplaints]);

  const toggleFaq = (id: number) => {
    setOpenFaqIds((prev) =>
      prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id]
    );
    void supportApi.getFaqById(id).catch(() => {});
  };

  const handleCreateComplaint = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!user?.id) {
      setSubmitError('Vui lòng đăng nhập để gửi phiếu hỗ trợ.');
      return;
    }
    if (!bookingCode.trim() || !description.trim()) {
      setSubmitError('Vui lòng nhập mã đặt chỗ và mô tả chi tiết sự cố.');
      return;
    }

    setIsSubmitting(true);
    setSubmitError(null);
    try {
      const parsedAmount = requestedAmount.trim() ? Number(requestedAmount) : null;
      const created = await supportApi.createComplaint({
        userId: user.id,
        bookingCode: bookingCode.trim(),
        category,
        description: description.trim(),
        requestedAmount: parsedAmount && !Number.isNaN(parsedAmount) ? parsedAmount : null,
      });
      setBookingCode('');
      setRequestedAmount('');
      setDescription('');
      setActiveMainTab('tracking');
      setToastMessage(`Đã gửi yêu cầu hỗ trợ thành công! Mã phiếu: ${created.code}`);
      setTimeout(() => setToastMessage(null), 5000);
      await loadMyComplaints();
    } catch (err) {
      setSubmitError(extractApiError(err, 'Không thể gửi phiếu hỗ trợ lúc này.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenDetail = async (code: string) => {
    if (!user?.id) return;
    setDetailLoading(true);
    try {
      const detail = await supportApi.getComplaintDetail(code, user.id);
      setSelectedComplaint(detail);
    } catch (err) {
      setToastMessage(extractApiError(err, 'Không thể tải chi tiết phiếu hỗ trợ.'));
      setTimeout(() => setToastMessage(null), 4000);
    } finally {
      setDetailLoading(false);
    }
  };

  const visibleFaqs = faqCategories
    .filter((c) => activeCategory === 'all' || c.category === activeCategory)
    .flatMap((c) => c.items);

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      <section className="bg-white border-b border-slate-200">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 py-8 flex flex-col gap-3">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-xs font-bold tracking-wider uppercase">
            <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse" />
            TRUNG TÂM HỖ TRỢ & KHIẾU NẠI (FAQ · COMPLAINTS)
          </div>
          <h1 className="text-3xl font-extrabold text-slate-900 tracking-tight">Trung tâm hỗ trợ tài xế</h1>
          <p className="text-sm text-slate-500 max-w-2xl">
            Tra cứu câu hỏi thường gặp, gửi khiếu nại theo mã vé đặt chỗ hoặc theo dõi tiến độ xử lý trực tiếp từ SupportService.
          </p>

          <div className="flex flex-wrap gap-2 pt-2">
            {[
              { id: 'faq', label: 'Câu hỏi thường gặp (FAQ)', icon: 'help' },
              { id: 'create', label: 'Gửi phiếu hỗ trợ / Khiếu nại', icon: 'edit_note' },
              { id: 'tracking', label: `Theo dõi phiếu (${complaints.length})`, icon: 'confirmation_number' },
            ].map((tab) => (
              <button
                key={tab.id}
                type="button"
                onClick={() => setActiveMainTab(tab.id as 'faq' | 'create' | 'tracking')}
                className={`px-4 py-2 rounded-xl text-xs font-bold flex items-center gap-1.5 transition-all cursor-pointer ${
                  activeMainTab === tab.id
                    ? 'bg-blue-600 text-white shadow-xs'
                    : 'bg-slate-100 text-slate-700 hover:bg-slate-200'
                }`}
              >
                <span className="material-symbols-outlined text-[16px]">{tab.icon}</span>
                <span>{tab.label}</span>
              </button>
            ))}
          </div>
        </div>
      </section>

      <main className="max-w-5xl mx-auto px-4 sm:px-6 py-8 flex-1 w-full space-y-6">
        {toastMessage && (
          <div className="p-3.5 rounded-2xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs font-bold flex items-center justify-between">
            <span>{toastMessage}</span>
            <button type="button" onClick={() => setToastMessage(null)} className="text-emerald-700 cursor-pointer">
              <span className="material-symbols-outlined text-[18px]">close</span>
            </button>
          </div>
        )}

        {activeMainTab === 'faq' && (
          <div className="space-y-6">
            {contacts.length > 0 && (
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                {contacts.map((c, idx) => (
                  <div key={idx} className="bg-white p-4 rounded-2xl border border-slate-200 text-xs">
                    <div className="font-bold text-slate-500 uppercase text-[10px]">{c.channel}</div>
                    <div className="font-extrabold text-blue-600 text-sm mt-0.5">{c.value}</div>
                    {c.note && <div className="text-slate-500 mt-0.5">{c.note}</div>}
                  </div>
                ))}
              </div>
            )}

            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => setActiveCategory('all')}
                className={`px-3.5 py-1.5 rounded-xl text-xs font-bold cursor-pointer ${
                  activeCategory === 'all' ? 'bg-slate-900 text-white' : 'bg-white border border-slate-200 text-slate-700'
                }`}
              >
                Tất cả chủ đề
              </button>
              {faqCategories.map((cat) => (
                <button
                  key={cat.category}
                  type="button"
                  onClick={() => setActiveCategory(cat.category)}
                  className={`px-3.5 py-1.5 rounded-xl text-xs font-bold cursor-pointer ${
                    activeCategory === cat.category
                      ? 'bg-slate-900 text-white'
                      : 'bg-white border border-slate-200 text-slate-700'
                  }`}
                >
                  {cat.category}
                </button>
              ))}
            </div>

            {faqLoading ? (
              <div className="bg-white rounded-2xl border border-slate-200 p-10 text-center text-xs text-slate-500">
                Đang tải câu hỏi thường gặp…
              </div>
            ) : faqError ? (
              <div className="bg-white rounded-2xl border border-rose-200 p-6 text-center text-xs text-rose-600 space-y-2">
                <p>{faqError}</p>
                <button
                  type="button"
                  onClick={() => void loadHelpCenter()}
                  className="px-4 py-2 rounded-xl bg-blue-600 text-white font-bold cursor-pointer"
                >
                  Thử lại
                </button>
              </div>
            ) : visibleFaqs.length === 0 ? (
              <div className="bg-white rounded-2xl border border-slate-200 p-10 text-center text-xs text-slate-500">
                Chưa có câu hỏi nào trong mục này.
              </div>
            ) : (
              <div className="space-y-3">
                {visibleFaqs.map((item) => {
                  const isOpen = openFaqIds.includes(item.id);
                  return (
                    <div key={item.id} className="bg-white rounded-2xl border border-slate-200 overflow-hidden">
                      <button
                        type="button"
                        onClick={() => toggleFaq(item.id)}
                        className="w-full p-4 text-left flex items-center justify-between gap-3 hover:bg-slate-50 cursor-pointer"
                      >
                        <div className="flex items-center gap-2.5">
                          <span className="px-2 py-0.5 rounded bg-blue-50 text-blue-700 text-[10px] font-bold">
                            {item.category}
                          </span>
                          <span className="text-sm font-bold text-slate-900">{item.question}</span>
                        </div>
                        <span className="material-symbols-outlined text-slate-400">
                          {isOpen ? 'expand_less' : 'expand_more'}
                        </span>
                      </button>
                      {isOpen && (
                        <div className="px-4 pb-4 pt-1 text-xs text-slate-600 leading-relaxed border-t border-slate-100">
                          {item.answer}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}

        {activeMainTab === 'create' && (
          <form onSubmit={handleCreateComplaint} className="bg-white rounded-3xl border border-slate-200 p-6 sm:p-8 space-y-5 text-xs">
            <h2 className="text-lg font-bold text-slate-900">Tạo phiếu hỗ trợ / khiếu nại mới</h2>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block font-bold text-slate-700 mb-1.5">Mã đặt chỗ (Booking Code) *</label>
                <input
                  type="text"
                  required
                  value={bookingCode}
                  onChange={(e) => setBookingCode(e.target.value)}
                  placeholder="Ví dụ: BK-0001"
                  className="w-full p-2.5 rounded-xl border border-slate-300 bg-white text-slate-800 focus:ring-2 focus:ring-blue-500 outline-none"
                />
              </div>

              <div>
                <label className="block font-bold text-slate-700 mb-1.5">Loại sự cố *</label>
                <select
                  value={category}
                  onChange={(e) => setCategory(e.target.value as ComplaintCategory)}
                  className="w-full p-2.5 rounded-xl border border-slate-300 bg-white text-slate-800 focus:ring-2 focus:ring-blue-500 outline-none"
                >
                  {CATEGORY_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div>
              <label className="block font-bold text-slate-700 mb-1.5">Số tiền yêu cầu hoàn / bồi thường (VNĐ, nếu có)</label>
              <input
                type="number"
                min={0}
                value={requestedAmount}
                onChange={(e) => setRequestedAmount(e.target.value)}
                placeholder="Để trống nếu không yêu cầu hoàn tiền"
                className="w-full p-2.5 rounded-xl border border-slate-300 bg-white text-slate-800 focus:ring-2 focus:ring-blue-500 outline-none"
              />
            </div>

            <div>
              <label className="block font-bold text-slate-700 mb-1.5">Mô tả chi tiết sự cố *</label>
              <textarea
                required
                rows={4}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Mô tả rõ thời gian, vị trí, vấn đề gặp phải..."
                className="w-full p-3 rounded-xl border border-slate-300 bg-white text-slate-800 focus:ring-2 focus:ring-blue-500 outline-none resize-none"
              />
            </div>

            {submitError && (
              <div className="p-3 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 font-medium">
                {submitError}
              </div>
            )}

            <button
              type="submit"
              disabled={isSubmitting}
              className="px-6 py-3 rounded-xl bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white font-bold cursor-pointer"
            >
              {isSubmitting ? 'Đang gửi…' : 'Gửi phiếu hỗ trợ'}
            </button>
          </form>
        )}

        {activeMainTab === 'tracking' && (
          <div className="space-y-4">
            {complaintsLoading ? (
              <div className="bg-white rounded-2xl border border-slate-200 p-10 text-center text-xs text-slate-500">
                Đang tải danh sách phiếu hỗ trợ…
              </div>
            ) : complaintsError ? (
              <div className="bg-white rounded-2xl border border-rose-200 p-6 text-center text-xs text-rose-600 space-y-2">
                <p>{complaintsError}</p>
                <button
                  type="button"
                  onClick={() => void loadMyComplaints()}
                  className="px-4 py-2 rounded-xl bg-blue-600 text-white font-bold cursor-pointer"
                >
                  Thử lại
                </button>
              </div>
            ) : complaints.length === 0 ? (
              <div className="bg-white rounded-2xl border border-slate-200 p-10 text-center text-xs text-slate-500">
                Bạn chưa gửi phiếu hỗ trợ nào.
              </div>
            ) : (
              <div className="space-y-3">
                {complaints.map((c) => (
                  <div
                    key={c.id}
                    className="bg-white p-4 rounded-2xl border border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs"
                  >
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <span className="font-mono font-bold text-blue-600">{c.code}</span>
                        <span className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${STATUS_BADGE[c.status] || 'bg-slate-100 text-slate-700'}`}>
                          {c.status}
                        </span>
                        <span className="text-slate-500">· {c.category}</span>
                      </div>
                      <div className="text-slate-600">
                        Mã vé: <strong>{c.bookingCode || '—'}</strong> · Tạo lúc:{' '}
                        {new Date(c.createdAtUtc).toLocaleString('vi-VN')}
                      </div>
                    </div>
                    <button
                      type="button"
                      disabled={detailLoading}
                      onClick={() => void handleOpenDetail(c.code)}
                      className="px-4 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-800 font-bold cursor-pointer self-start sm:self-auto"
                    >
                      Xem chi tiết
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </main>

      {selectedComplaint && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-3xl shadow-2xl p-6 w-full max-w-lg border border-slate-200 space-y-4 text-xs max-h-[85vh] overflow-y-auto">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div>
                <h3 className="text-base font-bold text-slate-900">Chi tiết phiếu {selectedComplaint.code}</h3>
                <p className="text-slate-500">{selectedComplaint.category} · {selectedComplaint.status}</p>
              </div>
              <button
                type="button"
                onClick={() => setSelectedComplaint(null)}
                className="text-slate-400 hover:text-slate-700 p-1 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-200 space-y-1.5">
              <div><strong>Mã vé:</strong> {selectedComplaint.bookingCode || '—'}</div>
              <div><strong>Bộ phận xử lý:</strong> {selectedComplaint.assignedTeam}</div>
              <div><strong>Mô tả:</strong> {selectedComplaint.description}</div>
              {selectedComplaint.resolution && (
                <div className="text-emerald-700"><strong>Kết quả xử lý:</strong> {selectedComplaint.resolution}</div>
              )}
            </div>

            {selectedComplaint.messages && selectedComplaint.messages.length > 0 && (
              <div className="space-y-2">
                <div className="font-bold text-slate-700">Trao đổi:</div>
                {selectedComplaint.messages.map((m) => (
                  <div key={m.id} className="p-3 rounded-xl bg-blue-50/60 border border-blue-100">
                    <div className="font-bold text-blue-800">
                      {m.senderParty} · {new Date(m.createdAtUtc).toLocaleString('vi-VN')}
                    </div>
                    <p className="text-slate-700 mt-1">{m.message}</p>
                  </div>
                ))}
              </div>
            )}

            <button
              type="button"
              onClick={() => setSelectedComplaint(null)}
              className="w-full py-2.5 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-800 font-bold cursor-pointer"
            >
              Đóng
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
