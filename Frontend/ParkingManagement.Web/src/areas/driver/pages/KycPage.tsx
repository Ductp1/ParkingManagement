import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAppSelector } from '@/store';

type KycStatus = 'unverified' | 'pending' | 'verified' | 'rejected';
type DocumentType = 'cccd' | 'gplx' | 'passport';

interface KycFormData {
  docType: DocumentType;
  idNumber: string;
  fullName: string;
  dateOfBirth: string;
  gender: 'male' | 'female' | 'other';
  issueDate: string;
  issuePlace: string;
  licenseClass?: string;
  frontImage: string | null;
  backImage: string | null;
  selfieImage: string | null;
}

export const KycPage: React.FC = () => {
  const { user } = useAppSelector((state) => state.auth);

  // Status for demo / testing
  const [kycStatus, setKycStatus] = useState<KycStatus>('unverified');
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [agreementChecked, setAgreementChecked] = useState(false);

  // Form State
  const [formData, setFormData] = useState<KycFormData>({
    docType: 'cccd',
    idNumber: '079201008921',
    fullName: (user?.fullName || 'BÙI PHẠM CHÍ NHÂN').toUpperCase(),
    dateOfBirth: '2001-08-15',
    gender: 'male',
    issueDate: '2022-04-10',
    issuePlace: 'Cục Cảnh sát QLHC về TTXH',
    licenseClass: 'B2',
    frontImage: null,
    backImage: null,
    selfieImage: null,
  });

  const [previewZoom, setPreviewZoom] = useState<string | null>(null);

  // Mock sample image URLs for quick testing without uploading real files
  const SAMPLE_FRONT = 'https://images.unsplash.com/photo-1628155930542-3c7a64e2c833?auto=format&fit=crop&w=800&q=80';
  const SAMPLE_BACK = 'https://images.unsplash.com/photo-1589829545856-d10d557cf95f?auto=format&fit=crop&w=800&q=80';
  const SAMPLE_SELFIE = 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=800&q=80';

  const handleFileUpload = (
    field: 'frontImage' | 'backImage' | 'selfieImage',
    e: React.ChangeEvent<HTMLInputElement>
  ) => {
    const file = e.target.files?.[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (event) => {
        setFormData((prev) => ({
          ...prev,
          [field]: event.target?.result as string,
        }));
      };
      reader.readAsDataURL(file);
    }
  };

  const handleUseDemoImages = () => {
    setFormData((prev) => ({
      ...prev,
      frontImage: SAMPLE_FRONT,
      backImage: SAMPLE_BACK,
      selfieImage: SAMPLE_SELFIE,
    }));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.frontImage || !formData.backImage) {
      alert('Vui lòng tải lên đầy đủ ảnh mặt trước và mặt sau của giấy tờ.');
      return;
    }
    if (!agreementChecked) {
      alert('Vui lòng đồng ý với điều khoản xử lý dữ liệu định danh.');
      return;
    }

    setIsSubmitting(true);
    setTimeout(() => {
      setIsSubmitting(false);
      setKycStatus('pending');
    }, 1200);
  };

  const resetForm = () => {
    setKycStatus('unverified');
    setStep(1);
    setFormData((prev) => ({
      ...prev,
      frontImage: null,
      backImage: null,
      selfieImage: null,
    }));
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10 flex flex-col gap-8">
      {/* 1. Header & Test Mode Switcher */}
      <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-6 border-b border-slate-200">
        <div className="flex flex-col gap-2 max-w-3xl">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-xs font-bold tracking-wider uppercase">
            <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
            E-KYC VERIFICATION • XÁC MINH DANH TÍNH TÀI XẾ (UI-04)
          </div>
          <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight">
            Xác minh danh tính (eKYC)
          </h1>
          <p className="text-slate-500 text-sm leading-relaxed">
            Xác thực danh tính bằng CCCD gắn chip hoặc Giấy phép lái xe để mở khóa đặt chỗ ưu tiên, thuê bãi dài hạn và thanh toán trả sau trên toàn hệ thống ParkMaster.
          </p>
        </div>

        {/* Demo State Switcher for Reviewing all UI States */}
        <div className="flex flex-col gap-1.5 bg-slate-100 p-2 rounded-2xl border border-slate-200 self-start lg:self-auto">
          <div className="text-[11px] font-bold text-slate-500 uppercase tracking-wider px-2 flex items-center justify-between">
            <span>Giả lập trạng thái duyệt (Demo):</span>
          </div>
          <div className="flex items-center gap-1">
            <button
              onClick={() => { setKycStatus('unverified'); setStep(1); }}
              className={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-all cursor-pointer ${
                kycStatus === 'unverified'
                  ? 'bg-white text-slate-800 shadow-xs'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Chưa nộp
            </button>
            <button
              onClick={() => setKycStatus('pending')}
              className={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-all cursor-pointer ${
                kycStatus === 'pending'
                  ? 'bg-amber-500 text-white shadow-xs'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Chờ duyệt
            </button>
            <button
              onClick={() => setKycStatus('verified')}
              className={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-all cursor-pointer ${
                kycStatus === 'verified'
                  ? 'bg-emerald-600 text-white shadow-xs'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Đã xác thực
            </button>
            <button
              onClick={() => setKycStatus('rejected')}
              className={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-all cursor-pointer ${
                kycStatus === 'rejected'
                  ? 'bg-rose-600 text-white shadow-xs'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Bị từ chối
            </button>
          </div>
        </div>
      </div>

      {/* 2. STATE VIEW: APPROVED (VERIFIED) */}
      {kycStatus === 'verified' && (
        <div className="bg-white rounded-3xl border border-emerald-200 shadow-sm p-6 sm:p-10 flex flex-col gap-8">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-5 rounded-2xl bg-emerald-50/70 border border-emerald-100">
            <div className="flex items-center gap-4">
              <div className="w-14 h-14 rounded-2xl bg-emerald-500 text-white flex items-center justify-center shadow-sm">
                <span className="material-symbols-outlined text-[32px]">verified</span>
              </div>
              <div>
                <div className="inline-flex items-center gap-1.5 text-xs font-bold text-emerald-700 uppercase tracking-wider">
                  <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
                  Hồ sơ định danh đã được phê duyệt
                </div>
                <h2 className="text-xl font-bold text-slate-900 mt-0.5">
                  Tài xế định danh cấp độ 2 (Full Verified)
                </h2>
                <p className="text-slate-500 text-xs mt-1">
                  Đã xác thực vào lúc 14:32 • 24/10/2026 qua CSDL Định danh điện tử Quốc gia
                </p>
              </div>
            </div>
            <span className="px-4 py-2 rounded-xl bg-emerald-100 text-emerald-800 text-xs font-extrabold uppercase tracking-wider self-start sm:self-auto border border-emerald-200">
              Đang hiệu lực
            </span>
          </div>

          {/* Identity Card Details */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            <div className="p-5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-col gap-1">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Họ và tên</span>
              <span className="text-base font-bold text-slate-800">{formData.fullName}</span>
            </div>

            <div className="p-5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-col gap-1">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Loại giấy tờ & Số</span>
              <span className="text-base font-bold text-slate-800">
                {formData.docType === 'cccd' ? 'CCCD Gắn chip' : 'GPLX Hạng B2'}: {formData.idNumber}
              </span>
            </div>

            <div className="p-5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-col gap-1">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Ngày sinh & Giới tính</span>
              <span className="text-base font-bold text-slate-800">
                15/08/2001 • {formData.gender === 'male' ? 'Nam' : 'Nữ'}
              </span>
            </div>

            <div className="p-5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-col gap-1">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Ngày cấp & Nơi cấp</span>
              <span className="text-base font-bold text-slate-800">10/04/2022 • {formData.issuePlace}</span>
            </div>

            <div className="p-5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-col gap-1">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Hạn mức đỗ xe</span>
              <span className="text-base font-bold text-blue-600">Không giới hạn • Ưu tiên đặt chỗ</span>
            </div>

            <div className="p-5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-col gap-1">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Mã hồ sơ KYC</span>
              <span className="text-base font-bold text-slate-800 font-mono">KYC-2026-9812-VN</span>
            </div>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-4 pt-6 border-t border-slate-200">
            <div className="flex items-center gap-2 text-xs text-slate-500">
              <span className="material-symbols-outlined text-[18px] text-emerald-600">lock</span>
              Thông tin của bạn được mã hóa an toàn theo tiêu chuẩn AES-256 & Nghị định 13/2023/NĐ-CP.
            </div>
            <div className="flex items-center gap-3">
              <button
                onClick={resetForm}
                className="px-4 py-2 rounded-xl border border-slate-200 hover:bg-slate-50 text-slate-700 text-xs font-semibold transition-all cursor-pointer"
              >
                Cập nhật lại giấy tờ
              </button>
              <Link
                to="/driver"
                className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold transition-all shadow-sm flex items-center gap-2"
              >
                <span>Về trang chủ tìm bãi</span>
                <span className="material-symbols-outlined text-[16px]">arrow_forward</span>
              </Link>
            </div>
          </div>
        </div>
      )}

      {/* 3. STATE VIEW: PENDING REVIEW */}
      {kycStatus === 'pending' && (
        <div className="bg-white rounded-3xl border border-amber-200 shadow-sm p-6 sm:p-10 flex flex-col gap-8">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-5 rounded-2xl bg-amber-50/80 border border-amber-100">
            <div className="flex items-center gap-4">
              <div className="w-14 h-14 rounded-2xl bg-amber-500 text-white flex items-center justify-center shadow-sm">
                <span className="material-symbols-outlined text-[32px] animate-spin">sync</span>
              </div>
              <div>
                <div className="inline-flex items-center gap-1.5 text-xs font-bold text-amber-700 uppercase tracking-wider">
                  <span className="w-2 h-2 rounded-full bg-amber-500 animate-ping"></span>
                  Hồ sơ đang trong quá trình thẩm duyệt
                </div>
                <h2 className="text-xl font-bold text-slate-900 mt-0.5">
                  Đang xử lý hồ sơ định danh điện tử
                </h2>
                <p className="text-slate-500 text-xs mt-1">
                  Mã hồ sơ: <span className="font-mono font-bold text-slate-800">KYC-2026-9812</span> • Nộp lúc: 25/10/2026 21:55
                </p>
              </div>
            </div>
            <span className="px-4 py-2 rounded-xl bg-amber-100 text-amber-800 text-xs font-extrabold uppercase tracking-wider self-start sm:self-auto border border-amber-200">
              Đang xử lý (SLA 15 Phút)
            </span>
          </div>

          {/* Processing Timeline */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="p-4 rounded-2xl bg-emerald-50 border border-emerald-100 flex items-start gap-3">
              <span className="material-symbols-outlined text-emerald-600 text-[24px]">check_circle</span>
              <div>
                <h4 className="text-xs font-bold text-emerald-900">1. Tiếp nhận hồ sơ</h4>
                <p className="text-[11px] text-emerald-700 mt-0.5">Hệ thống đã nhận ảnh CCCD và dữ liệu khai báo.</p>
              </div>
            </div>
            <div className="p-4 rounded-2xl bg-amber-50 border border-amber-200 flex items-start gap-3">
              <span className="material-symbols-outlined text-amber-600 text-[24px] animate-spin">refresh</span>
              <div>
                <h4 className="text-xs font-bold text-amber-900">2. Đối soát AI & OCR</h4>
                <p className="text-[11px] text-amber-700 mt-0.5">Đang quét số ID, độ sắc nét và tính toàn vẹn 4 góc.</p>
              </div>
            </div>
            <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200 flex items-start gap-3 opacity-60">
              <span className="material-symbols-outlined text-slate-400 text-[24px]">schedule</span>
              <div>
                <h4 className="text-xs font-bold text-slate-800">3. Phê duyệt & Kích hoạt</h4>
                <p className="text-[11px] text-slate-500 mt-0.5">Thông báo qua chuông in-app & Email khi hoàn tất.</p>
              </div>
            </div>
          </div>

          <div className="p-5 rounded-2xl bg-slate-50 border border-slate-200 text-xs text-slate-600 flex flex-col gap-2">
            <div className="font-bold text-slate-800 flex items-center gap-2">
              <span className="material-symbols-outlined text-[18px] text-blue-600">info</span>
              Bạn vẫn có thể tiếp tục sử dụng hệ thống bình thường
            </div>
            <p className="leading-relaxed">
              Trong thời gian chờ duyệt, bạn vẫn có thể tìm kiếm bãi đỗ và đặt chỗ cho các lượt đỗ xe ngắn hạn thông thường. Kết quả thẩm duyệt sẽ tự động thông báo qua chuông trên thanh menu.
            </p>
          </div>

          <div className="flex items-center justify-between pt-4 border-t border-slate-200">
            <button
              onClick={() => setKycStatus('rejected')}
              className="text-xs text-slate-400 hover:text-slate-600 cursor-pointer"
            >
              [Mô phỏng trường hợp từ chối]
            </button>
            <Link
              to="/driver/vehicles"
              className="px-5 py-2.5 rounded-xl bg-slate-900 hover:bg-slate-800 text-white text-xs font-bold transition-all shadow-sm flex items-center gap-2"
            >
              <span>Xem danh sách xe của tôi</span>
              <span className="material-symbols-outlined text-[16px]">arrow_forward</span>
            </Link>
          </div>
        </div>
      )}

      {/* 4. STATE VIEW: REJECTED */}
      {kycStatus === 'rejected' && (
        <div className="bg-white rounded-3xl border border-rose-200 shadow-sm p-6 sm:p-10 flex flex-col gap-8">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-5 rounded-2xl bg-rose-50/80 border border-rose-100">
            <div className="flex items-center gap-4">
              <div className="w-14 h-14 rounded-2xl bg-rose-600 text-white flex items-center justify-center shadow-sm">
                <span className="material-symbols-outlined text-[32px]">cancel</span>
              </div>
              <div>
                <div className="inline-flex items-center gap-1.5 text-xs font-bold text-rose-700 uppercase tracking-wider">
                  <span className="w-2 h-2 rounded-full bg-rose-600"></span>
                  Hồ sơ chưa được phê duyệt
                </div>
                <h2 className="text-xl font-bold text-slate-900 mt-0.5">
                  Xác minh không thành công
                </h2>
                <p className="text-slate-500 text-xs mt-1">
                  Hệ thống kiểm duyệt phát hiện giấy tờ chưa đáp ứng tiêu chuẩn hình ảnh.
                </p>
              </div>
            </div>
            <span className="px-4 py-2 rounded-xl bg-rose-100 text-rose-800 text-xs font-extrabold uppercase tracking-wider self-start sm:self-auto border border-rose-200">
              Yêu cầu nộp lại
            </span>
          </div>

          <div className="p-5 rounded-2xl bg-rose-50 border border-rose-200 flex flex-col gap-3">
            <h4 className="text-xs font-bold text-rose-900 flex items-center gap-2">
              <span className="material-symbols-outlined text-[18px]">error</span>
              Lý do từ chối:
            </h4>
            <ul className="text-xs text-rose-800 space-y-1.5 pl-6 list-disc">
              <li>Ảnh chụp mặt trước bị lóa đèn flash, che khuất số định danh cá nhân (CCCD).</li>
              <li>Ảnh chụp bị mất góc dưới bên phải, không đọc được mã QR bảo mật.</li>
            </ul>
          </div>

          <div className="flex items-center justify-between pt-4 border-t border-slate-200">
            <span className="text-xs text-slate-500">Vui lòng chụp lại ảnh rõ nét trong điều kiện đủ sáng.</span>
            <button
              onClick={resetForm}
              className="px-6 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold transition-all shadow-sm flex items-center gap-2 cursor-pointer"
            >
              <span className="material-symbols-outlined text-[18px]">replay</span>
              <span>Tải lại giấy tờ mới</span>
            </button>
          </div>
        </div>
      )}

      {/* 5. STATE VIEW: FORM UPLOAD (UNVERIFIED) */}
      {kycStatus === 'unverified' && (
        <div className="bg-white rounded-3xl border border-slate-200/80 shadow-sm overflow-hidden">
          {/* Stepper Header */}
          <div className="grid grid-cols-3 border-b border-slate-200 bg-slate-50/60 text-xs font-bold">
            <button
              type="button"
              onClick={() => setStep(1)}
              className={`py-4 px-3 sm:px-6 flex items-center justify-center gap-2 border-b-2 transition-all cursor-pointer ${
                step === 1
                  ? 'border-blue-600 text-blue-700 bg-white'
                  : 'border-transparent text-slate-500 hover:text-slate-800'
              }`}
            >
              <span className={`w-5 h-5 rounded-full flex items-center justify-center text-[10px] ${
                step === 1 ? 'bg-blue-600 text-white' : 'bg-slate-200 text-slate-600'
              }`}>
                1
              </span>
              <span className="hidden sm:inline">Thông tin giấy tờ</span>
            </button>

            <button
              type="button"
              onClick={() => setStep(2)}
              className={`py-4 px-3 sm:px-6 flex items-center justify-center gap-2 border-b-2 transition-all cursor-pointer ${
                step === 2
                  ? 'border-blue-600 text-blue-700 bg-white'
                  : 'border-transparent text-slate-500 hover:text-slate-800'
              }`}
            >
              <span className={`w-5 h-5 rounded-full flex items-center justify-center text-[10px] ${
                step === 2 ? 'bg-blue-600 text-white' : 'bg-slate-200 text-slate-600'
              }`}>
                2
              </span>
              <span className="hidden sm:inline">Tải lên hình ảnh</span>
            </button>

            <button
              type="button"
              onClick={() => setStep(3)}
              className={`py-4 px-3 sm:px-6 flex items-center justify-center gap-2 border-b-2 transition-all cursor-pointer ${
                step === 3
                  ? 'border-blue-600 text-blue-700 bg-white'
                  : 'border-transparent text-slate-500 hover:text-slate-800'
              }`}
            >
              <span className={`w-5 h-5 rounded-full flex items-center justify-center text-[10px] ${
                step === 3 ? 'bg-blue-600 text-white' : 'bg-slate-200 text-slate-600'
              }`}>
                3
              </span>
              <span className="hidden sm:inline">Xác nhận & Nộp</span>
            </button>
          </div>

          <form onSubmit={handleSubmit} className="p-6 sm:p-10 flex flex-col gap-8">
            {/* STEP 1: CHỌN LOẠI GIẤY TỜ & THÔNG TIN CƠ BẢN */}
            {step === 1 && (
              <div className="flex flex-col gap-6">
                <div>
                  <h3 className="text-lg font-bold text-slate-900">1. Chọn loại giấy tờ xác minh</h3>
                  <p className="text-slate-500 text-xs mt-1">
                    Chọn loại giấy tờ còn hiệu lực theo quy định pháp luật Việt Nam.
                  </p>
                </div>

                {/* Document Type Selector Cards */}
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                  <div
                    onClick={() => setFormData({ ...formData, docType: 'cccd' })}
                    className={`p-4 rounded-2xl border-2 transition-all cursor-pointer flex items-start gap-3.5 ${
                      formData.docType === 'cccd'
                        ? 'border-blue-600 bg-blue-50/50 shadow-xs'
                        : 'border-slate-200 hover:border-slate-300 bg-white'
                    }`}
                  >
                    <div className={`p-2.5 rounded-xl ${formData.docType === 'cccd' ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-600'}`}>
                      <span className="material-symbols-outlined text-[22px]">badge</span>
                    </div>
                    <div>
                      <h4 className="text-sm font-bold text-slate-900">CCCD gắn chip</h4>
                      <p className="text-[11px] text-slate-500 mt-0.5">Khuyên dùng, duyệt tự động nhanh nhất</p>
                    </div>
                  </div>

                  <div
                    onClick={() => setFormData({ ...formData, docType: 'gplx' })}
                    className={`p-4 rounded-2xl border-2 transition-all cursor-pointer flex items-start gap-3.5 ${
                      formData.docType === 'gplx'
                        ? 'border-blue-600 bg-blue-50/50 shadow-xs'
                        : 'border-slate-200 hover:border-slate-300 bg-white'
                    }`}
                  >
                    <div className={`p-2.5 rounded-xl ${formData.docType === 'gplx' ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-600'}`}>
                      <span className="material-symbols-outlined text-[22px]">directions_car</span>
                    </div>
                    <div>
                      <h4 className="text-sm font-bold text-slate-900">Giấy phép lái xe</h4>
                      <p className="text-[11px] text-slate-500 mt-0.5">Hạng B1, B2, C, D...</p>
                    </div>
                  </div>

                  <div
                    onClick={() => setFormData({ ...formData, docType: 'passport' })}
                    className={`p-4 rounded-2xl border-2 transition-all cursor-pointer flex items-start gap-3.5 ${
                      formData.docType === 'passport'
                        ? 'border-blue-600 bg-blue-50/50 shadow-xs'
                        : 'border-slate-200 hover:border-slate-300 bg-white'
                    }`}
                  >
                    <div className={`p-2.5 rounded-xl ${formData.docType === 'passport' ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-600'}`}>
                      <span className="material-symbols-outlined text-[22px]">public</span>
                    </div>
                    <div>
                      <h4 className="text-sm font-bold text-slate-900">Hộ chiếu (Passport)</h4>
                      <p className="text-[11px] text-slate-500 mt-0.5">Dành cho công dân quốc tế</p>
                    </div>
                  </div>
                </div>

                {/* Input Fields */}
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6 pt-2">
                  <div className="flex flex-col gap-1.5">
                    <label className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                      Số {formData.docType === 'cccd' ? 'CCCD (12 số)' : formData.docType === 'gplx' ? 'GPLX' : 'Hộ chiếu'} *
                    </label>
                    <input
                      type="text"
                      required
                      value={formData.idNumber}
                      onChange={(e) => setFormData({ ...formData, idNumber: e.target.value })}
                      placeholder="VD: 079201008921"
                      className="px-4 py-2.5 rounded-xl border border-slate-200 text-sm font-medium focus:outline-none focus:ring-2 focus:ring-blue-600/30 focus:border-blue-600"
                    />
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <label className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                      Họ và tên (in hoa theo giấy tờ) *
                    </label>
                    <input
                      type="text"
                      required
                      value={formData.fullName}
                      onChange={(e) => setFormData({ ...formData, fullName: e.target.value.toUpperCase() })}
                      placeholder="VD: NGUYỄN VĂN AN"
                      className="px-4 py-2.5 rounded-xl border border-slate-200 text-sm font-medium uppercase focus:outline-none focus:ring-2 focus:ring-blue-600/30 focus:border-blue-600"
                    />
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <label className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                      Ngày sinh *
                    </label>
                    <input
                      type="date"
                      required
                      value={formData.dateOfBirth}
                      onChange={(e) => setFormData({ ...formData, dateOfBirth: e.target.value })}
                      className="px-4 py-2.5 rounded-xl border border-slate-200 text-sm font-medium focus:outline-none focus:ring-2 focus:ring-blue-600/30 focus:border-blue-600"
                    />
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <label className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                      Giới tính *
                    </label>
                    <select
                      value={formData.gender}
                      onChange={(e) => setFormData({ ...formData, gender: e.target.value as any })}
                      className="px-4 py-2.5 rounded-xl border border-slate-200 text-sm font-medium bg-white focus:outline-none focus:ring-2 focus:ring-blue-600/30 focus:border-blue-600"
                    >
                      <option value="male">Nam</option>
                      <option value="female">Nữ</option>
                      <option value="other">Khác</option>
                    </select>
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <label className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                      Ngày cấp *
                    </label>
                    <input
                      type="date"
                      required
                      value={formData.issueDate}
                      onChange={(e) => setFormData({ ...formData, issueDate: e.target.value })}
                      className="px-4 py-2.5 rounded-xl border border-slate-200 text-sm font-medium focus:outline-none focus:ring-2 focus:ring-blue-600/30 focus:border-blue-600"
                    />
                  </div>

                  <div className="flex flex-col gap-1.5">
                    <label className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                      Nơi cấp *
                    </label>
                    <input
                      type="text"
                      required
                      value={formData.issuePlace}
                      onChange={(e) => setFormData({ ...formData, issuePlace: e.target.value })}
                      placeholder="VD: Cục Cảnh sát QLHC về TTXH"
                      className="px-4 py-2.5 rounded-xl border border-slate-200 text-sm font-medium focus:outline-none focus:ring-2 focus:ring-blue-600/30 focus:border-blue-600"
                    />
                  </div>

                  {formData.docType === 'gplx' && (
                    <div className="flex flex-col gap-1.5">
                      <label className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                        Hạng GPLX
                      </label>
                      <select
                        value={formData.licenseClass}
                        onChange={(e) => setFormData({ ...formData, licenseClass: e.target.value })}
                        className="px-4 py-2.5 rounded-xl border border-slate-200 text-sm font-medium bg-white focus:outline-none focus:ring-2 focus:ring-blue-600/30 focus:border-blue-600"
                      >
                        <option value="B1">Hạng B1 (Số tự động)</option>
                        <option value="B2">Hạng B2 (Ô tô dưới 9 chỗ)</option>
                        <option value="C">Hạng C (Xe tải)</option>
                        <option value="D">Hạng D</option>
                      </select>
                    </div>
                  )}
                </div>

                <div className="flex justify-end pt-4 border-t border-slate-100">
                  <button
                    type="button"
                    onClick={() => setStep(2)}
                    className="px-6 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold transition-all shadow-sm flex items-center gap-2 cursor-pointer"
                  >
                    <span>Tiếp tục: Tải lên hình ảnh</span>
                    <span className="material-symbols-outlined text-[16px]">arrow_forward</span>
                  </button>
                </div>
              </div>
            )}

            {/* STEP 2: TẢI LÊN HÌNH ẢNH GIẤY TỜ */}
            {step === 2 && (
              <div className="flex flex-col gap-6">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                  <div>
                    <h3 className="text-lg font-bold text-slate-900">2. Tải lên hình ảnh giấy tờ</h3>
                    <p className="text-slate-500 text-xs mt-1">
                      Chụp rõ 4 góc, đủ sáng, không bị bóng đèn lóa sáng hoặc nhòe mờ.
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={handleUseDemoImages}
                    className="px-4 py-2 rounded-xl border border-blue-200 bg-blue-50 text-blue-700 hover:bg-blue-100 text-xs font-bold transition-all flex items-center gap-2 cursor-pointer self-start sm:self-auto"
                  >
                    <span className="material-symbols-outlined text-[18px]">auto_fix_high</span>
                    <span>Tải ảnh mẫu (Test nhanh)</span>
                  </button>
                </div>

                {/* Upload Zones */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {/* Front Image */}
                  <div className="flex flex-col gap-2">
                    <label className="text-xs font-bold text-slate-700 flex items-center justify-between">
                      <span>Mặt trước giấy tờ *</span>
                      {formData.frontImage && (
                        <span className="text-[11px] text-emerald-600 font-semibold flex items-center gap-1">
                          <span className="material-symbols-outlined text-[14px]">check</span> Đã tải lên
                        </span>
                      )}
                    </label>

                    {formData.frontImage ? (
                      <div className="relative rounded-2xl overflow-hidden border border-slate-200 bg-slate-900 group aspect-4/3 flex items-center justify-center">
                        <img
                          src={formData.frontImage}
                          alt="Front"
                          className="w-full h-full object-cover"
                        />
                        <div className="absolute inset-0 bg-slate-900/60 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-3">
                          <button
                            type="button"
                            onClick={() => setPreviewZoom(formData.frontImage)}
                            className="p-2 rounded-xl bg-white/20 hover:bg-white/40 text-white transition-all cursor-pointer"
                            title="Xem phóng to"
                          >
                            <span className="material-symbols-outlined text-[20px]">zoom_in</span>
                          </button>
                          <button
                            type="button"
                            onClick={() => setFormData({ ...formData, frontImage: null })}
                            className="p-2 rounded-xl bg-rose-600/80 hover:bg-rose-600 text-white transition-all cursor-pointer"
                            title="Xóa ảnh"
                          >
                            <span className="material-symbols-outlined text-[20px]">delete</span>
                          </button>
                        </div>
                      </div>
                    ) : (
                      <label className="border-2 border-dashed border-slate-300 hover:border-blue-500 rounded-2xl p-6 flex flex-col items-center justify-center text-center cursor-pointer transition-all bg-slate-50 hover:bg-blue-50/30 aspect-4/3">
                        <div className="w-12 h-12 rounded-2xl bg-blue-100 text-blue-600 flex items-center justify-center mb-3">
                          <span className="material-symbols-outlined text-[26px]">upload_file</span>
                        </div>
                        <span className="text-sm font-bold text-slate-800">Chọn hoặc kéo thả ảnh mặt trước</span>
                        <span className="text-xs text-slate-400 mt-1">Hỗ trợ JPG, PNG (Tối đa 5MB)</span>
                        <input
                          type="file"
                          accept="image/*"
                          className="hidden"
                          onChange={(e) => handleFileUpload('frontImage', e)}
                        />
                      </label>
                    )}
                  </div>

                  {/* Back Image */}
                  <div className="flex flex-col gap-2">
                    <label className="text-xs font-bold text-slate-700 flex items-center justify-between">
                      <span>Mặt sau giấy tờ *</span>
                      {formData.backImage && (
                        <span className="text-[11px] text-emerald-600 font-semibold flex items-center gap-1">
                          <span className="material-symbols-outlined text-[14px]">check</span> Đã tải lên
                        </span>
                      )}
                    </label>

                    {formData.backImage ? (
                      <div className="relative rounded-2xl overflow-hidden border border-slate-200 bg-slate-900 group aspect-4/3 flex items-center justify-center">
                        <img
                          src={formData.backImage}
                          alt="Back"
                          className="w-full h-full object-cover"
                        />
                        <div className="absolute inset-0 bg-slate-900/60 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-3">
                          <button
                            type="button"
                            onClick={() => setPreviewZoom(formData.backImage)}
                            className="p-2 rounded-xl bg-white/20 hover:bg-white/40 text-white transition-all cursor-pointer"
                            title="Xem phóng to"
                          >
                            <span className="material-symbols-outlined text-[20px]">zoom_in</span>
                          </button>
                          <button
                            type="button"
                            onClick={() => setFormData({ ...formData, backImage: null })}
                            className="p-2 rounded-xl bg-rose-600/80 hover:bg-rose-600 text-white transition-all cursor-pointer"
                            title="Xóa ảnh"
                          >
                            <span className="material-symbols-outlined text-[20px]">delete</span>
                          </button>
                        </div>
                      </div>
                    ) : (
                      <label className="border-2 border-dashed border-slate-300 hover:border-blue-500 rounded-2xl p-6 flex flex-col items-center justify-center text-center cursor-pointer transition-all bg-slate-50 hover:bg-blue-50/30 aspect-4/3">
                        <div className="w-12 h-12 rounded-2xl bg-blue-100 text-blue-600 flex items-center justify-center mb-3">
                          <span className="material-symbols-outlined text-[26px]">upload_file</span>
                        </div>
                        <span className="text-sm font-bold text-slate-800">Chọn hoặc kéo thả ảnh mặt sau</span>
                        <span className="text-xs text-slate-400 mt-1">Hỗ trợ JPG, PNG (Tối đa 5MB)</span>
                        <input
                          type="file"
                          accept="image/*"
                          className="hidden"
                          onChange={(e) => handleFileUpload('backImage', e)}
                        />
                      </label>
                    )}
                  </div>
                </div>

                {/* Instructions Box */}
                <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200 text-xs text-slate-600 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
                  <div className="flex items-center gap-3">
                    <span className="material-symbols-outlined text-[22px] text-blue-600">lightbulb</span>
                    <span>Quy chuẩn ảnh: Giấy tờ gốc, nhìn rõ số thẻ và họ tên, không bị cắt góc, không chụp qua màn hình máy tính.</span>
                  </div>
                </div>

                <div className="flex items-center justify-between pt-4 border-t border-slate-100">
                  <button
                    type="button"
                    onClick={() => setStep(1)}
                    className="px-5 py-2.5 rounded-xl border border-slate-200 hover:bg-slate-50 text-slate-700 text-xs font-bold transition-all cursor-pointer"
                  >
                    Quay lại Bước 1
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      if (!formData.frontImage || !formData.backImage) {
                        alert('Vui lòng tải lên ảnh mặt trước và mặt sau (hoặc bấm Tải ảnh mẫu để test nhanh).');
                        return;
                      }
                      setStep(3);
                    }}
                    className="px-6 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold transition-all shadow-sm flex items-center gap-2 cursor-pointer"
                  >
                    <span>Tiếp tục: Xem lại & Nộp hồ sơ</span>
                    <span className="material-symbols-outlined text-[16px]">arrow_forward</span>
                  </button>
                </div>
              </div>
            )}

            {/* STEP 3: XEM LẠI & NỘP HỒ SƠ */}
            {step === 3 && (
              <div className="flex flex-col gap-6">
                <div>
                  <h3 className="text-lg font-bold text-slate-900">3. Kiểm tra lại thông tin & Xác nhận</h3>
                  <p className="text-slate-500 text-xs mt-1">
                    Vui lòng đối chiếu kỹ thông tin khai báo trước khi gửi hồ sơ cho bộ phận thẩm định ParkMaster.
                  </p>
                </div>

                {/* Summary Table */}
                <div className="rounded-2xl border border-slate-200 divide-y divide-slate-100 overflow-hidden text-xs">
                  <div className="grid grid-cols-2 p-3.5 bg-slate-50 font-bold text-slate-700">
                    <span>Họ và tên</span>
                    <span className="text-slate-900">{formData.fullName}</span>
                  </div>
                  <div className="grid grid-cols-2 p-3.5">
                    <span className="text-slate-500">Loại giấy tờ</span>
                    <span className="font-semibold text-slate-800">
                      {formData.docType === 'cccd' ? 'Căn cước công dân gắn chip' : formData.docType === 'gplx' ? 'Giấy phép lái xe' : 'Hộ chiếu'}
                    </span>
                  </div>
                  <div className="grid grid-cols-2 p-3.5 bg-slate-50">
                    <span className="text-slate-500">Số định danh</span>
                    <span className="font-mono font-bold text-slate-900">{formData.idNumber}</span>
                  </div>
                  <div className="grid grid-cols-2 p-3.5">
                    <span className="text-slate-500">Ngày sinh & Giới tính</span>
                    <span className="text-slate-800">{formData.dateOfBirth} ({formData.gender === 'male' ? 'Nam' : 'Nữ'})</span>
                  </div>
                  <div className="grid grid-cols-2 p-3.5 bg-slate-50">
                    <span className="text-slate-500">Ngày cấp & Nơi cấp</span>
                    <span className="text-slate-800">{formData.issueDate} • {formData.issuePlace}</span>
                  </div>
                </div>

                {/* Attached Images Mini Preview */}
                <div className="flex flex-col gap-2">
                  <span className="text-xs font-bold text-slate-700">Hình ảnh giấy tờ đã đính kèm</span>
                  <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
                    {formData.frontImage && (
                      <div className="rounded-xl overflow-hidden border border-slate-200 aspect-4/3 relative">
                        <img src={formData.frontImage} alt="Mặt trước" className="w-full h-full object-cover" />
                        <span className="absolute bottom-2 left-2 px-2 py-0.5 rounded bg-slate-900/80 text-white text-[10px] font-bold">
                          Mặt trước
                        </span>
                      </div>
                    )}
                    {formData.backImage && (
                      <div className="rounded-xl overflow-hidden border border-slate-200 aspect-4/3 relative">
                        <img src={formData.backImage} alt="Mặt sau" className="w-full h-full object-cover" />
                        <span className="absolute bottom-2 left-2 px-2 py-0.5 rounded bg-slate-900/80 text-white text-[10px] font-bold">
                          Mặt sau
                        </span>
                      </div>
                    )}
                  </div>
                </div>

                {/* Policy Agreement Checkbox */}
                <div className="p-4 rounded-2xl bg-blue-50/60 border border-blue-200 flex items-start gap-3">
                  <input
                    type="checkbox"
                    id="kyc-agreement"
                    checked={agreementChecked}
                    onChange={(e) => setAgreementChecked(e.target.checked)}
                    className="mt-0.5 w-4 h-4 rounded text-blue-600 focus:ring-blue-500 border-slate-300 cursor-pointer"
                  />
                  <label htmlFor="kyc-agreement" className="text-xs text-slate-700 leading-relaxed cursor-pointer select-none">
                    Tôi cam kết các thông tin và hình ảnh giấy tờ tải lên là chính chủ và hoàn toàn xác thực. Tôi đồng ý cho phép ParkMaster đối soát dữ liệu với CSDL Định danh điện tử Quốc gia phục vụ công tác đối chiếu an ninh bãi đỗ xe theo Nghị định 13/2023/NĐ-CP.
                  </label>
                </div>

                <div className="flex items-center justify-between pt-4 border-t border-slate-100">
                  <button
                    type="button"
                    onClick={() => setStep(2)}
                    className="px-5 py-2.5 rounded-xl border border-slate-200 hover:bg-slate-50 text-slate-700 text-xs font-bold transition-all cursor-pointer"
                  >
                    Quay lại sửa ảnh
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmitting}
                    className="px-8 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white text-xs font-bold transition-all shadow-sm flex items-center gap-2 cursor-pointer"
                  >
                    {isSubmitting ? (
                      <>
                        <span className="material-symbols-outlined text-[18px] animate-spin">refresh</span>
                        <span>Đang gửi hồ sơ thẩm duyệt…</span>
                      </>
                    ) : (
                      <>
                        <span className="material-symbols-outlined text-[18px]">verified</span>
                        <span>Nộp hồ sơ thẩm định ngay</span>
                      </>
                    )}
                  </button>
                </div>
              </div>
            )}
          </form>
        </div>
      )}

      {/* Image Zoom Modal */}
      {previewZoom && (
        <div
          className="fixed inset-0 z-50 bg-slate-900/80 backdrop-blur-xs flex items-center justify-center p-4"
          onClick={() => setPreviewZoom(null)}
        >
          <div className="relative max-w-3xl max-h-[90vh] rounded-2xl overflow-hidden bg-black shadow-2xl">
            <img src={previewZoom} alt="Zoom" className="w-full h-full object-contain" />
            <button
              onClick={() => setPreviewZoom(null)}
              className="absolute top-3 right-3 p-2 rounded-full bg-slate-800/80 hover:bg-slate-800 text-white text-xs cursor-pointer"
            >
              <span className="material-symbols-outlined text-[20px]">close</span>
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
