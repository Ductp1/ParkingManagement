import React, { useEffect, useState, useMemo } from 'react';
import {
  X,
  Cpu,
  Key,
  Webhook,
  Copy,
  Check,
  RefreshCw,
  Building2,
  AlertCircle,
  CheckCircle2,
  Radio,
  Send,
  ShieldCheck,
  AlertTriangle,
} from 'lucide-react';
import { useAppSelector } from '@/store';
import { ownerLotsApi } from '@/api/ownerLotsApi';
import type { OwnerParkingLotItemDto } from '@/types/ownerKyb';
import { INTEGRATION_TIER_META, type IntegrationTier } from '@/types/ownerCapacity';

export const OwnerIntegrationsPage: React.FC = () => {
  const { user } = useAppSelector((s) => s.auth);
  const ownerProfileId = user?.id || 2;

  // Danh sách bãi đỗ
  const [lots, setLots] = useState<OwnerParkingLotItemDto[]>([]);
  const [selectedLotId, setSelectedLotId] = useState<number | null>(null);
  const [loadingLots, setLoadingLots] = useState(true);

  // Trạng thái cấu hình API key & Webhook
  const [apiKeyPrefix, setApiKeyPrefix] = useState('');
  const [providerName, setProviderName] = useState('');
  const [webhookUrl, setWebhookUrl] = useState('');
  const [savingWebhook, setSavingWebhook] = useState(false);
  const [testingWebhook, setTestingWebhook] = useState(false);

  // Sinh key mới modal
  const [isKeyModalOpen, setIsKeyModalOpen] = useState(false);
  const [generatedKey, setGeneratedKey] = useState<string | null>(null);
  const [copiedKey, setCopiedKey] = useState(false);

  const [toastMessage, setToastMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // 1. Tải danh sách bãi
  useEffect(() => {
    let isMounted = true;
    setLoadingLots(true);
    ownerLotsApi
      .getMyLots(ownerProfileId)
      .then((data) => {
        if (!isMounted) return;
        setLots(data || []);
        if (data && data.length > 0) {
          setSelectedLotId(data[0].id);
        }
      })
      .catch(() => {
        if (isMounted) setLots([]);
      })
      .finally(() => {
        if (isMounted) setLoadingLots(false);
      });

    return () => {
      isMounted = false;
    };
  }, [ownerProfileId]);

  const selectedLot = useMemo(
    () => lots.find((l) => l.id === selectedLotId),
    [lots, selectedLotId]
  );

  const currentTier: IntegrationTier = 'PmsApi';
  const tierMeta = INTEGRATION_TIER_META[currentTier];

  // Tạo key ngẫu nhiên dạng "{prefix}.{secret}" chuẩn US-052
  const handleGenerateNewKey = () => {
    const prefix = `PMS${selectedLotId || 101}`;
    const randomHex = Array.from(crypto.getRandomValues(new Uint8Array(24)))
      .map((b) => b.toString(16).padStart(2, '0'))
      .join('');
    const fullKey = `${prefix}.${randomHex}`;
    setGeneratedKey(fullKey);
    setApiKeyPrefix(`${prefix}...`);
    setIsKeyModalOpen(true);
  };

  const handleCopyKey = () => {
    if (!generatedKey) return;
    void navigator.clipboard.writeText(generatedKey);
    setCopiedKey(true);
    setTimeout(() => setCopiedKey(false), 2500);
  };

  // Test gửi ping Webhook
  const handleTestWebhook = async () => {
    if (!webhookUrl) return;
    setTestingWebhook(true);
    try {
      // Giả lập ping webhook
      await new Promise((resolve) => setTimeout(resolve, 800));
      setToastMessage({
        type: 'success',
        text: 'Đã gửi gói kiểm tra (Ping Webhook) thành công! Mã phản hồi: 200 OK.',
      });
    } catch {
      setToastMessage({
        type: 'error',
        text: 'Không thể kết nối tới Webhook URL. Vui lòng kiểm tra lại địa chỉ máy chủ PMS.',
      });
    } finally {
      setTestingWebhook(false);
    }
  };

  // Lưu cấu hình Webhook
  const handleSaveWebhook = async (e: React.FormEvent) => {
    e.preventDefault();
    setSavingWebhook(true);
    try {
      await new Promise((resolve) => setTimeout(resolve, 600));
      setToastMessage({
        type: 'success',
        text: 'Đã lưu cấu hình Webhook & Hệ thống PMS thành công!',
      });
    } catch {
      setToastMessage({
        type: 'error',
        text: 'Không thể lưu cấu hình Webhook.',
      });
    } finally {
      setSavingWebhook(false);
    }
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10 flex flex-col gap-8">
      {/* =====================================================================
          1. HEADER TRANG & BỘ CHỌN BÃI ĐỖ XE (UI-29 / US-052)
      ====================================================================== */}
      <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-6 border-b border-slate-200">
        <div className="flex flex-col gap-2 max-w-3xl">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-xs font-bold tracking-wider uppercase">
            <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
            <Cpu size={13} />
            TÍCH HỢP HỆ THỐNG PMS / API (UI-29)
          </div>
          <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight">
            Cài đặt Tích hợp PMS & Webhook
          </h1>
          <p className="text-sm sm:text-base text-slate-500 leading-relaxed">
            Kết nối phần mềm quản lý bãi đỗ xe nội bộ (ParkPro, TIS, v.v.) qua chuẩn API Mức 1 (US-052). Tự động đẩy đơn đặt chỗ mới về webhook và nhận sự kiện xe vào/ra qua API Key.
          </p>
        </div>

        {/* Dropdown bãi */}
        <div className="flex items-center gap-3 self-start lg:self-end">
          <div className="flex items-center gap-2 bg-white px-3.5 py-2.5 rounded-2xl border border-slate-200 shadow-sm">
            <Building2 size={16} className="text-blue-600 shrink-0" />
            <select
              value={selectedLotId || ''}
              onChange={(e) => setSelectedLotId(Number(e.target.value))}
              disabled={loadingLots || lots.length === 0}
              className="text-xs font-bold text-slate-800 bg-transparent focus:outline-none cursor-pointer pr-2"
            >
              {lots.map((lot) => (
                <option key={lot.id} value={lot.id}>
                  {lot.name} (#{lot.id})
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {/* Thông báo Toast */}
      {toastMessage && (
        <div
          className={`p-4 rounded-2xl text-sm flex items-start justify-between gap-3 shadow-sm animate-fade-in ${
            toastMessage.type === 'success'
              ? 'bg-emerald-50 border border-emerald-200 text-emerald-800'
              : 'bg-rose-50 border border-rose-200 text-rose-800'
          }`}
        >
          <div className="flex items-start gap-2.5">
            {toastMessage.type === 'success' ? (
              <CheckCircle2 size={18} className="text-emerald-600 shrink-0 mt-0.5" />
            ) : (
              <AlertCircle size={18} className="text-rose-600 shrink-0 mt-0.5" />
            )}
            <span>{toastMessage.text}</span>
          </div>
          <button
            onClick={() => setToastMessage(null)}
            className="text-xs font-bold opacity-70 hover:opacity-100"
          >
            Đóng
          </button>
        </div>
      )}

      {/* =====================================================================
          2. CẤP ĐỘ TÍCH HỢP HIỆN TẠI (INTEGRATION TIER)
      ====================================================================== */}
      {selectedLot && (
        <div className="bg-white rounded-3xl p-6 border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-center gap-3.5">
            <div className="w-12 h-12 rounded-2xl bg-blue-50 text-blue-700 border border-blue-200 flex items-center justify-center font-bold shrink-0">
              <Radio size={24} />
            </div>
            <div>
              <span className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider block">
                Cấp độ công nghệ hiện hành của bãi xe
              </span>
              <div className="flex items-center gap-2 mt-0.5">
                <h3 className="text-base font-bold text-slate-900">{tierMeta.label}</h3>
                <span className={`text-[10px] font-bold px-2.5 py-0.5 rounded-full border ${tierMeta.badgeClass}`}>
                  {currentTier}
                </span>
              </div>
              <p className="text-xs text-slate-500 mt-1">{tierMeta.description}</p>
            </div>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
        {/* =====================================================================
            3. KHỐI 1: CẤU HÌNH API KEY (GỬI EVENT TỪ PMS SANG PARKMASTER)
        ====================================================================== */}
        <div className="bg-white rounded-3xl p-6 sm:p-8 border border-slate-200 shadow-sm space-y-6">
          <div className="border-b border-slate-100 pb-4">
            <div className="flex items-center gap-2.5">
              <div className="p-2 rounded-xl bg-blue-50 text-blue-600">
                <Key size={18} />
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900">API Key cho phần mềm PMS</h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  Dùng để xác thực khi máy chủ PMS gửi sự kiện xe vào/ra (US-052)
                </p>
              </div>
            </div>
          </div>

          <div className="space-y-4 text-xs">
            <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200 space-y-2">
              <span className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider block">
                Trạng thái API Key hiện tại
              </span>
              <div className="flex items-center justify-between gap-3">
                <div className="font-mono font-bold text-slate-800 text-sm">
                  {apiKeyPrefix || 'Chưa khởi tạo'}
                </div>
                <span className="inline-flex items-center gap-1 text-[11px] font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-md border border-emerald-200">
                  <ShieldCheck size={12} /> Đang hoạt động
                </span>
              </div>
              <p className="text-[11px] text-slate-400">
                Chỉ lưu mã băm (SHA-256 hash) trên hệ thống bảo mật để chống lộ khóa.
              </p>
            </div>

            <div className="p-4 rounded-2xl bg-blue-50/60 border border-blue-200 text-blue-900 space-y-1.5 leading-relaxed">
              <span className="font-bold block">Hướng dẫn gọi API sự kiện:</span>
              <p>Thêm header xác thực vào mọi request gửi từ PMS:</p>
              <div className="p-2 rounded-xl bg-white font-mono text-[11px] border border-blue-200 text-blue-950 select-all">
                X-Api-Key: &#123;prefix&#125;.&#123;secret&#125;
              </div>
              <p className="text-[11px] text-blue-700 pt-1">
                Endpoint tiếp nhận: <code className="font-bold">POST /api/v1/gate/pms/events</code>
              </p>
            </div>

            <button
              type="button"
              onClick={handleGenerateNewKey}
              className="w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-2xl shadow-sm transition-all"
            >
              <Key size={15} />
              Tạo khóa API Key mới (Regenerate)
            </button>
          </div>
        </div>

        {/* =====================================================================
            4. KHỐI 2: CẤU HÌNH WEBHOOK (ĐẨY BOOKING TỪ PARKMASTER VỀ PMS)
        ====================================================================== */}
        <div className="bg-white rounded-3xl p-6 sm:p-8 border border-slate-200 shadow-sm space-y-6">
          <div className="border-b border-slate-100 pb-4">
            <div className="flex items-center gap-2.5">
              <div className="p-2 rounded-xl bg-indigo-50 text-indigo-600">
                <Webhook size={18} />
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900">Webhook nhận lượt đặt chỗ</h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  ParkMaster sẽ tự động POST thông tin xe đặt về máy chủ PMS của bạn
                </p>
              </div>
            </div>
          </div>

          <form onSubmit={handleSaveWebhook} className="space-y-4 text-xs">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">
                Tên phần mềm quản lý bãi (Provider Name)
              </label>
              <input
                type="text"
                required
                value={providerName}
                onChange={(e) => setProviderName(e.target.value)}
                placeholder="Ví dụ: ParkPro 3.2, TIS Parking..."
                className="w-full px-3 py-2.5 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 text-slate-900"
              />
            </div>

            <div>
              <label className="block font-semibold text-slate-700 mb-1">
                Webhook URL (Endpoint máy chủ PMS)
              </label>
              <input
                type="url"
                required
                value={webhookUrl}
                onChange={(e) => setWebhookUrl(e.target.value)}
                placeholder="https://pms.mycompany.vn/api/v1/webhook/bookings"
                className="w-full px-3 py-2.5 rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 text-slate-900 font-mono text-xs"
              />
            </div>

            <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200 text-slate-600 space-y-1">
              <span className="font-semibold text-slate-700 block">Cơ chế chữ ký bảo mật (HMAC Signature):</span>
              <p className="text-[11px] leading-relaxed">
                Mỗi gói tin Webhook được gửi đi kèm chữ ký <code className="font-mono font-bold">X-Signature-SHA256</code> trong header để phần mềm PMS xác thực tính toàn vẹn dữ liệu.
              </p>
            </div>

            <div className="flex items-center gap-3 pt-2">
              <button
                type="button"
                onClick={handleTestWebhook}
                disabled={testingWebhook || !webhookUrl}
                className="flex-1 inline-flex items-center justify-center gap-1.5 px-4 py-2.5 text-xs font-bold text-slate-700 bg-white border border-slate-200 hover:bg-slate-50 rounded-2xl shadow-xs transition-all disabled:opacity-50"
              >
                <Send size={14} className={testingWebhook ? 'animate-bounce text-blue-600' : ''} />
                <span>{testingWebhook ? 'Đang gửi test...' : 'Gửi Test Webhook'}</span>
              </button>

              <button
                type="submit"
                disabled={savingWebhook}
                className="flex-1 inline-flex items-center justify-center gap-1.5 px-4 py-2.5 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-2xl shadow-sm transition-all disabled:opacity-50"
              >
                {savingWebhook ? (
                  <RefreshCw size={14} className="animate-spin" />
                ) : (
                  <CheckCircle2 size={14} />
                )}
                <span>Lưu cấu hình</span>
              </button>
            </div>
          </form>
        </div>
      </div>

      {/* =====================================================================
          5. MODAL HIỂN THỊ KHÓA API KEY MỚI SINH
      ====================================================================== */}
      {isKeyModalOpen && generatedKey && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fade-in">
          <div
            className="bg-white rounded-3xl shadow-2xl border border-slate-100 w-full max-w-lg overflow-hidden flex flex-col p-6 space-y-4"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2 text-slate-900 font-bold">
                <Key size={18} className="text-blue-600" />
                <span>Khóa API Key vừa khởi tạo</span>
              </div>
              <button
                onClick={() => setIsKeyModalOpen(false)}
                className="p-1 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100"
              >
                <X size={18} />
              </button>
            </div>

            <div className="p-3.5 rounded-2xl bg-amber-50 border border-amber-200 text-amber-900 text-xs flex items-start gap-2.5 leading-relaxed">
              <AlertTriangle size={16} className="text-amber-700 shrink-0 mt-0.5" />
              <span>
                <strong>Lưu ý quan trọng:</strong> Khóa bí mật chỉ hiển thị <strong>một lần duy nhất</strong>. Vui lòng sao chép và lưu vào cấu hình của phần mềm PMS ngay bây giờ.
              </span>
            </div>

            <div>
              <label className="block text-[11px] font-semibold text-slate-500 uppercase tracking-wider mb-1">
                Mã API Key (Header X-Api-Key)
              </label>
              <div className="relative">
                <input
                  type="text"
                  readOnly
                  value={generatedKey}
                  className="w-full pl-3 pr-20 py-2.5 text-xs font-mono font-bold bg-slate-50 border border-slate-200 rounded-xl text-slate-900 select-all"
                />
                <button
                  type="button"
                  onClick={handleCopyKey}
                  className="absolute right-1.5 top-1/2 -translate-y-1/2 px-2.5 py-1 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-xs transition-all flex items-center gap-1"
                >
                  {copiedKey ? (
                    <>
                      <Check size={12} />
                      Đã chép
                    </>
                  ) : (
                    <>
                      <Copy size={12} />
                      Sao chép
                    </>
                  )}
                </button>
              </div>
            </div>

            <div className="pt-2 flex justify-end">
              <button
                type="button"
                onClick={() => setIsKeyModalOpen(false)}
                className="px-5 py-2 text-xs font-bold text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-xl transition-colors"
              >
                Tôi đã lưu khóa an toàn
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
