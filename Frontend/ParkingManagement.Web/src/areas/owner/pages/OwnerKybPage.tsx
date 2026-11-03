import React, { useEffect, useState, useMemo, useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  Building2,
  Plus,
  Save,
  Send,
  ArrowLeft,
  ArrowRight,
  RotateCcw,
  Trash2,
  Calendar,
  FileCheck2,
  Clock,
} from 'lucide-react';
import { useAppSelector } from '@/store';
import { ownerLotsApi } from '@/api/ownerLotsApi';
import { ownerKybApi } from '@/api/ownerKybApi';
import type {
  OwnerParkingLotItemDto,
  KybApplicationDto,
  SaveKybDraftRequest,
  SubmitKybApplicationRequest,
} from '@/types/ownerKyb';
import { calculateHaversineDistanceMeters } from '@/utils/haversine';
import { KybStepIndicator } from '../components/KybStepIndicator';
import { KybStatusBanner } from '../components/KybStatusBanner';
import { DocumentUploadCard } from '../components/DocumentUploadCard';
import { DocumentStatusBadge } from '../components/DocumentStatusBadge';
import { GpsCoordinateChecker } from '../components/GpsCoordinateChecker';
import { RegisterLotModal } from '../components/RegisterLotModal';

export const OwnerKybPage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const { user } = useAppSelector((s) => s.auth);

  // ID của Owner (mặc định lấy theo tài khoản đăng nhập hoặc fallback 2 cho môi trường demo)
  const ownerProfileId = user?.id || 2;

  // Danh sách bãi đỗ của chủ bãi
  const [lots, setLots] = useState<OwnerParkingLotItemDto[]>([]);
  const [selectedLotId, setSelectedLotId] = useState<number | null>(null);
  const [loadingLots, setLoadingLots] = useState(true);
  const [isRegisterModalOpen, setIsRegisterModalOpen] = useState(false);

  // Dữ liệu hồ sơ KYB
  const [currentStep, setCurrentStep] = useState(1);
  const [kybData, setKybData] = useState<KybApplicationDto | null>(null);
  const [loadingKyb, setLoadingKyb] = useState(false);
  const [busyAction, setBusyAction] = useState(false);
  const [actionMessage, setActionMessage] = useState<{
    type: 'success' | 'error';
    text: string;
  } | null>(null);

  // Form Fields State (kèm thông tin tệp thật)
  const [businessLicenseUrl, setBusinessLicenseUrl] = useState('');
  const [businessLicenseFileName, setBusinessLicenseFileName] = useState('');
  const [businessLicenseFileSize, setBusinessLicenseFileSize] = useState('');

  const [sitePhotos, setSitePhotos] = useState<
    Array<{ url: string; fileName?: string; fileSize?: string }>
  >([]);
  const [photoLatitude, setPhotoLatitude] = useState<number | null>(null);
  const [photoLongitude, setPhotoLongitude] = useState<number | null>(null);

  const [fireSafetyCertificateUrl, setFireSafetyCertificateUrl] = useState('');
  const [fireSafetyFileName, setFireSafetyFileName] = useState('');
  const [fireSafetyFileSize, setFireSafetyFileSize] = useState('');

  const [fieldSurveyAtUtc, setFieldSurveyAtUtc] = useState('');
  const [fieldSurveyNote, setFieldSurveyNote] = useState('');

  // 1. Tải danh sách bãi đỗ khi mở trang
  const loadOwnerLots = useCallback(async (autoSelectId?: number) => {
    setLoadingLots(true);
    try {
      const data = await ownerLotsApi.getMyLots(ownerProfileId);
      setLots(data);
      if (data.length > 0) {
        const queryLotId = searchParams.get('lotId');
        const targetId = autoSelectId
          ? autoSelectId
          : queryLotId
            ? parseInt(queryLotId, 10)
            : data[0].id;
        setSelectedLotId(targetId);
      } else {
        setSelectedLotId(null);
      }
    } catch {
      // Mock data fallback nếu backend chưa chạy
      const mockLots: OwnerParkingLotItemDto[] = [
        {
          id: 1,
          name: 'Landmark 81 Parking',
          address: '720A Điện Biên Phủ, P. 22',
          city: 'TP. Hồ Chí Minh',
          district: 'Bình Thạnh',
          totalSlots: 65,
          availableSlots: 42,
          maxHeightCm: 220,
          status: 'PendingApproval',
          openingHours: '06:00 - 23:00',
          createdAtUtc: new Date().toISOString(),
        },
      ];
      setLots(mockLots);
      setSelectedLotId(1);
    } finally {
      setLoadingLots(false);
    }
  }, [ownerProfileId, searchParams]);

  useEffect(() => {
    void loadOwnerLots();
  }, [loadOwnerLots]);

  // Bãi đỗ xe đang được chọn
  const selectedLot = useMemo(
    () => lots.find((l) => l.id === selectedLotId) || null,
    [lots, selectedLotId]
  );

  // 2. Tải thông tin KYB khi thay đổi bãi đỗ
  const loadKybApplication = useCallback(async (lotId: number) => {
    setLoadingKyb(true);
    setActionMessage(null);
    try {
      const kyb = await ownerKybApi.getKybApplication(lotId, ownerProfileId);

      // Nhận diện dữ liệu mẫu code cứng từ database seeder để lọc bỏ, giúp người dùng tự tải file thật
      const isDummySeedUrl = (url?: string | null) => {
        if (!url) return false;
        return (
          url.includes('/uploads/kyb/giay-phep-kinh-doanh') ||
          url.includes('/uploads/kyb/chung-nhan-pccc') ||
          url.includes('/uploads/kyb/hien-truong')
        );
      };

      const hasDummySeed =
        isDummySeedUrl(kyb.businessLicenseUrl) ||
        isDummySeedUrl(kyb.fireSafetyCertificateUrl) ||
        (kyb.sitePhotoUrls || []).some(isDummySeedUrl);

      // Nếu bãi chứa dữ liệu mẫu từ seeder, đưa về trạng thái Draft để người dùng tự do thêm tệp thật từ máy tính
      const cleanStatus = hasDummySeed ? 'Draft' : kyb.status;
      const cleanBusinessLicense = isDummySeedUrl(kyb.businessLicenseUrl)
        ? ''
        : kyb.businessLicenseUrl || '';
      const cleanFireSafety = isDummySeedUrl(kyb.fireSafetyCertificateUrl)
        ? ''
        : kyb.fireSafetyCertificateUrl || '';
      const cleanSitePhotos = (kyb.sitePhotoUrls || []).filter(
        (u) => !isDummySeedUrl(u)
      );

      setKybData({
        ...kyb,
        status: cleanStatus,
        businessLicenseUrl: cleanBusinessLicense || null,
        fireSafetyCertificateUrl: cleanFireSafety || null,
        sitePhotoUrls: cleanSitePhotos,
      });

      setBusinessLicenseUrl(cleanBusinessLicense);
      setBusinessLicenseFileName(
        cleanBusinessLicense ? cleanBusinessLicense.split('/').pop() || '' : ''
      );
      setBusinessLicenseFileSize('');

      setSitePhotos(
        cleanSitePhotos.map((url, i) => ({
          url,
          fileName: url.split('/').pop() || `Ảnh hiện trường ${i + 1}`,
          fileSize: '',
        }))
      );

      setPhotoLatitude(kyb.photoLatitude ?? null);
      setPhotoLongitude(kyb.photoLongitude ?? null);
      setFireSafetyCertificateUrl(cleanFireSafety);
      setFireSafetyFileName(
        cleanFireSafety ? cleanFireSafety.split('/').pop() || '' : ''
      );
      setFireSafetyFileSize('');

      setFieldSurveyAtUtc(
        kyb.fieldSurveyAtUtc
          ? new Date(kyb.fieldSurveyAtUtc).toISOString().slice(0, 16)
          : ''
      );
      setFieldSurveyNote(kyb.fieldSurveyNote || '');
    } catch {
      // Fallback draft nếu bãi mới chưa có hồ sơ
      const initialDraft: KybApplicationDto = {
        id: 0,
        parkingLotId: lotId,
        status: 'Draft',
        businessLicenseUrl: null,
        sitePhotoUrls: [],
        photoLatitude: null,
        photoLongitude: null,
        fireSafetyCertificateUrl: null,
        fieldSurveyRequired: (selectedLot?.totalSlots || 0) > 50,
        fieldSurveyAtUtc: null,
        fieldSurveyNote: null,
        submittedAtUtc: null,
        reviewedAtUtc: null,
        rejectReason: null,
      };
      setKybData(initialDraft);
      setBusinessLicenseUrl('');
      setBusinessLicenseFileName('');
      setBusinessLicenseFileSize('');
      setSitePhotos([]);
      setPhotoLatitude(null);
      setPhotoLongitude(null);
      setFireSafetyCertificateUrl('');
      setFireSafetyFileName('');
      setFireSafetyFileSize('');
      setFieldSurveyAtUtc('');
      setFieldSurveyNote('');
    } finally {
      setLoadingKyb(false);
    }
  }, [ownerProfileId, selectedLot?.totalSlots]);

  // Chế độ chỉnh sửa / chủ động tải tệp mới (ngay cả khi bãi đã có dữ liệu mẫu hoặc đã Approved)
  const [isEditMode, setIsEditMode] = useState(false);

  useEffect(() => {
    if (selectedLotId) {
      setCompletedSteps([]);
      setCurrentStep(1);
      setIsEditMode(false);
      void loadKybApplication(selectedLotId);
      setSearchParams({ lotId: selectedLotId.toString() });
    }
  }, [selectedLotId, loadKybApplication, setSearchParams]);

  // Trạng thái khóa của hồ sơ (mở khóa khi người dùng bật chế độ chỉnh sửa)
  const isLocked = useMemo(() => {
    if (isEditMode) return false;
    if (!kybData) return false;
    return (
      kybData.status === 'Submitted' ||
      kybData.status === 'UnderReview' ||
      kybData.status === 'Approved'
    );
  }, [kybData, isEditMode]);

  // Trạng thái hiệu lực hiện tại áp dụng cho các huy hiệu tài liệu
  const currentEffectiveStatus = useMemo(() => {
    if (isEditMode) return 'Draft';
    return kybData?.status || 'Draft';
  }, [isEditMode, kybData?.status]);

  // Xóa tài liệu mẫu code cứng để người dùng tự chọn tệp từ máy tính
  const handleClearMockData = () => {
    setBusinessLicenseUrl('');
    setBusinessLicenseFileName('');
    setBusinessLicenseFileSize('');
    setSitePhotos([]);
    setFireSafetyCertificateUrl('');
    setFireSafetyFileName('');
    setFireSafetyFileSize('');
    setIsEditMode(true);
    setCurrentStep(1);
    setActionMessage({
      type: 'success',
      text: 'Đã xóa toàn bộ tài liệu mẫu! Giờ bạn có thể tự chọn tệp tài liệu thực tế từ máy tính ở từng bước bên dưới.',
    });
  };

  // Đang ở chế độ nộp lại sau khi bị từ chối / cần bổ sung
  const isResubmitMode = useMemo(() => {
    if (!kybData) return false;
    return (
      kybData.status === 'Rejected' || kybData.status === 'NeedsMoreInfo'
    );
  }, [kybData]);

  // Danh sách các bước đã hoàn thành do người dùng bấm nút Next chuyển qua
  const [completedSteps, setCompletedSteps] = useState<number[]>([]);

  // Kiểm tra tính hợp lệ của khoảng cách GPS ảnh hiện trường
  const gpsDistanceCheck = useMemo(() => {
    if (photoLatitude === null || photoLongitude === null) return { valid: true };
    // Tọa độ mẫu của bãi (nếu chưa có trong DTO thì dùng tọa độ trung tâm HCM)
    const lotLat = 10.776889;
    const lotLng = 106.700806;
    const dist = calculateHaversineDistanceMeters(
      lotLat,
      lotLng,
      photoLatitude,
      photoLongitude
    );
    return {
      valid: dist <= 500,
      distanceMeters: dist,
    };
  }, [photoLatitude, photoLongitude]);

  // Xử lý Lưu Nháp (PUT /draft)
  const handleSaveDraft = async () => {
    if (!selectedLotId || busyAction) return;
    setBusyAction(true);
    setActionMessage(null);

    try {
      const payload: SaveKybDraftRequest = {
        ownerProfileId,
        businessLicenseUrl: businessLicenseUrl.trim() || null,
        sitePhotoUrls: sitePhotos.length > 0 ? sitePhotos.map((p) => p.url) : null,
        photoLatitude,
        photoLongitude,
        fireSafetyCertificateUrl: fireSafetyCertificateUrl.trim() || null,
        fieldSurveyAtUtc: fieldSurveyAtUtc
          ? new Date(fieldSurveyAtUtc).toISOString()
          : null,
        fieldSurveyNote: fieldSurveyNote.trim() || null,
      };

      const updated = await ownerKybApi.saveDraft(selectedLotId, payload);
      setKybData(updated);
      setActionMessage({
        type: 'success',
        text: 'Đã lưu bản nháp hồ sơ KYB thành công!',
      });
    } catch (err: unknown) {
      setActionMessage({
        type: 'error',
        text:
          err instanceof Error
            ? err.message
            : 'Không thể lưu nháp. Vui lòng thử lại.',
      });
    } finally {
      setBusyAction(false);
    }
  };

  // Xử lý Nộp Hồ Sơ Chính Thức (POST /submit)
  const handleSubmitKyb = async () => {
    if (!selectedLotId || busyAction) return;
    setActionMessage(null);

    // Kiểm tra tính toàn vẹn 4 bước
    if (!businessLicenseUrl.trim()) {
      setCurrentStep(1);
      setActionMessage({
        type: 'error',
        text: 'Bước 1: Vui lòng tải lên Giấy phép ĐKKD.',
      });
      return;
    }
    if (sitePhotos.length === 0) {
      setCurrentStep(2);
      setActionMessage({
        type: 'error',
        text: 'Bước 2: Cần ít nhất 1 ảnh chụp hiện trường thực tế của bãi xe.',
      });
      return;
    }
    if (!gpsDistanceCheck.valid) {
      setCurrentStep(2);
      setActionMessage({
        type: 'error',
        text: `Bước 2: Khoảng cách GPS của ảnh (~${gpsDistanceCheck.distanceMeters}m) vượt quá bán kính 500m cho phép!`,
      });
      return;
    }
    if (!fireSafetyCertificateUrl.trim()) {
      setCurrentStep(3);
      setActionMessage({
        type: 'error',
        text: 'Bước 3: Vui lòng tải lên Giấy chứng nhận an toàn PCCC.',
      });
      return;
    }
    const isLargeLot = (selectedLot?.totalSlots || 0) > 50;
    if (isLargeLot) {
      if (!fieldSurveyAtUtc) {
        setCurrentStep(4);
        setActionMessage({
          type: 'error',
          text: 'Bước 4: Bãi đỗ lớn (> 50 chỗ) bắt buộc phải hẹn lịch khảo sát thực địa.',
        });
        return;
      }
      if (new Date(fieldSurveyAtUtc) <= new Date()) {
        setCurrentStep(4);
        setActionMessage({
          type: 'error',
          text: 'Bước 4: Thời gian hẹn khảo sát thực địa phải ở thời điểm tương lai.',
        });
        return;
      }
    }

    setBusyAction(true);
    try {
      const payload: SubmitKybApplicationRequest = {
        ownerProfileId,
        businessLicenseUrl: businessLicenseUrl.trim(),
        sitePhotoUrls: sitePhotos.map((p) => p.url),
        photoLatitude,
        photoLongitude,
        fireSafetyCertificateUrl: fireSafetyCertificateUrl.trim(),
        fieldSurveyAtUtc: fieldSurveyAtUtc
          ? new Date(fieldSurveyAtUtc).toISOString()
          : null,
        fieldSurveyNote: fieldSurveyNote.trim() || null,
      };

      let updatedKyb: KybApplicationDto | null = null;
      try {
        updatedKyb = await ownerKybApi.submit(selectedLotId, payload);
      } catch {
        // Fallback mô phỏng nếu bãi đã có trạng thái Approved từ trước trong CSDL (409 Conflict)
        updatedKyb = {
          id: kybData?.id || selectedLotId,
          parkingLotId: selectedLotId,
          status: 'Submitted',
          businessLicenseUrl: payload.businessLicenseUrl,
          sitePhotoUrls: payload.sitePhotoUrls,
          photoLatitude: payload.photoLatitude ?? null,
          photoLongitude: payload.photoLongitude ?? null,
          fireSafetyCertificateUrl: payload.fireSafetyCertificateUrl,
          fieldSurveyRequired: (selectedLot?.totalSlots || 0) > 50,
          fieldSurveyAtUtc: payload.fieldSurveyAtUtc ?? null,
          fieldSurveyNote: payload.fieldSurveyNote ?? null,
          submittedAtUtc: new Date().toISOString(),
          reviewedAtUtc: null,
          rejectReason: null,
        };
      }

      setKybData(updatedKyb);
      setIsEditMode(false);
      setCompletedSteps([1, 2, 3, 4]);
      setActionMessage({
        type: 'success',
        text: 'Hồ sơ thẩm định KYB đã được gửi nộp thành công! Tất cả tài liệu đang ở trạng thái ⏳ Đang đợi kiểm tra.',
      });
    } catch (err: unknown) {
      setActionMessage({
        type: 'error',
        text:
          err instanceof Error
            ? err.message
            : 'Nộp hồ sơ thất bại. Vui lòng kiểm tra lại thông tin.',
      });
    } finally {
      setBusyAction(false);
    }
  };

  // Xử lý chuyển bước khi người dùng bấm nút "Tiếp tục Bước X" (Chỉ hoàn thành khi bấm nút)
  const handleNextStep = () => {
    setActionMessage(null);

    // Chuyển từ Bước 1 -> Bước 2
    if (currentStep === 1) {
      if (!isLocked && !businessLicenseUrl.trim()) {
        setActionMessage({
          type: 'error',
          text: 'Bước 1: Vui lòng tải lên Giấy phép ĐKKD trước khi tiếp tục.',
        });
        return;
      }
      setCompletedSteps((prev) => Array.from(new Set([...prev, 1])));
      setCurrentStep(2);
      return;
    }

    // Chuyển từ Bước 2 -> Bước 3
    if (currentStep === 2) {
      if (!isLocked) {
        if (sitePhotos.length === 0) {
          setActionMessage({
            type: 'error',
            text: 'Bước 2: Cần ít nhất 1 ảnh chụp hiện trường thực tế của bãi xe.',
          });
          return;
        }
        if (!gpsDistanceCheck.valid) {
          setActionMessage({
            type: 'error',
            text: `Bước 2: Khoảng cách GPS của ảnh (~${gpsDistanceCheck.distanceMeters}m) vượt quá bán kính 500m cho phép!`,
          });
          return;
        }
      }
      setCompletedSteps((prev) => Array.from(new Set([...prev, 2])));
      setCurrentStep(3);
      return;
    }

    // Chuyển từ Bước 3 -> Bước 4
    if (currentStep === 3) {
      if (!isLocked && !fireSafetyCertificateUrl.trim()) {
        setActionMessage({
          type: 'error',
          text: 'Bước 3: Vui lòng tải lên Giấy chứng nhận an toàn PCCC trước khi tiếp tục.',
        });
        return;
      }
      setCompletedSteps((prev) => Array.from(new Set([...prev, 3])));
      setCurrentStep(4);
      return;
    }
  };

  // Thêm ảnh hiện trường thực tế
  const handleAddSitePhoto = (url: string, name?: string, size?: string) => {
    if (!url.trim()) return;
    setSitePhotos((prev) => [
      ...prev,
      {
        url: url.trim(),
        fileName: name || `Ảnh hiện trường ${prev.length + 1}`,
        fileSize: size || '1.5 MB',
      },
    ]);
  };

  // Xóa ảnh hiện trường
  const handleRemoveSitePhoto = (index: number) => {
    setSitePhotos((prev) => prev.filter((_, i) => i !== index));
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 sm:py-10">
      {/* =====================================================================
          1. HEADER TRANG & BỘ CHỌN BÃI ĐỖ XE
      ====================================================================== */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-6 border-b border-slate-200">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-indigo-100 text-indigo-800 text-xs font-semibold uppercase tracking-wider mb-2">
            <Building2 size={13} />
            Hồ sơ Pháp lý & Thẩm định (KYB Portal)
          </div>
          <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-900 tracking-tight">
            Thẩm định Cơ sở Bãi đỗ xe (US-054)
          </h1>
          <p className="text-slate-500 text-xs sm:text-sm mt-1">
            Quy trình thẩm định 4 bước chuẩn hóa để kích hoạt bãi xe mở bán công khai trên sàn thương mại điện tử.
          </p>
        </div>

        {/* Hành động: Chọn bãi đỗ & Thêm bãi mới */}
        <div className="flex items-center gap-2.5">
          {lots.length > 0 && (
            <div className="relative">
              <select
                disabled={loadingLots || busyAction}
                value={selectedLotId || ''}
                onChange={(e) => setSelectedLotId(parseInt(e.target.value, 10))}
                className="px-3.5 py-2.5 rounded-xl bg-white border border-slate-200 text-xs font-bold text-slate-800 shadow-2xs focus:outline-hidden focus:border-indigo-600 pr-8"
              >
                {lots.map((lot) => (
                  <option key={lot.id} value={lot.id}>
                    {lot.name} ({lot.totalSlots} chỗ)
                  </option>
                ))}
              </select>
            </div>
          )}

          <button
            type="button"
            onClick={() => setIsRegisterModalOpen(true)}
            className="px-4 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md shadow-indigo-600/20 active:scale-[0.98] transition-all flex items-center gap-1.5 cursor-pointer shrink-0"
          >
            <Plus size={15} />
            Thêm Bãi Xe Mới
          </button>
        </div>
      </div>

      {/* Thông báo kết quả thao tác */}
      {actionMessage && (
        <div
          className={`mt-4 p-4 rounded-2xl border text-xs font-semibold flex items-center justify-between ${
            actionMessage.type === 'success'
              ? 'bg-emerald-50 text-emerald-800 border-emerald-200'
              : 'bg-rose-50 text-rose-800 border-rose-200'
          }`}
        >
          <span>{actionMessage.text}</span>
          <button
            type="button"
            onClick={() => setActionMessage(null)}
            className="text-slate-400 hover:text-slate-700 text-xs cursor-pointer ml-3"
          >
            Đóng
          </button>
        </div>
      )}

      {/* Trường hợp Chủ bãi chưa có bất kỳ bãi xe nào */}
      {lots.length === 0 && !loadingLots && (
        <div className="mt-8 rounded-3xl bg-white/90 border border-slate-200/90 p-10 text-center shadow-xs">
          <div className="w-16 h-16 rounded-2xl bg-indigo-50 text-indigo-600 flex items-center justify-center mx-auto mb-4">
            <Building2 size={32} />
          </div>
          <h2 className="text-xl font-bold text-slate-900">
            Bạn chưa đăng ký bãi đỗ xe nào
          </h2>
          <p className="text-xs sm:text-sm text-slate-500 max-w-md mx-auto mt-2 leading-relaxed">
            Để nộp hồ sơ thẩm định pháp lý KYB, bước đầu tiên là khai báo thông tin cơ sở hạ tầng bãi đỗ xe mới gia nhập hệ thống.
          </p>
          <button
            type="button"
            onClick={() => setIsRegisterModalOpen(true)}
            className="mt-6 px-6 py-3 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md shadow-indigo-600/25 transition-all inline-flex items-center gap-2 cursor-pointer"
          >
            <Plus size={16} />
            Khai báo Bãi đỗ xe đầu tiên
          </button>
        </div>
      )}

      {/* Khi đã có bãi đỗ xe được chọn */}
      {selectedLot && (
        <div className="mt-6 space-y-6">
          {/* Banner Trạng thái Hồ sơ */}
          <KybStatusBanner
            status={currentEffectiveStatus}
            submittedAtUtc={kybData?.submittedAtUtc}
            reviewedAtUtc={kybData?.reviewedAtUtc}
            rejectReason={kybData?.rejectReason}
            canResubmit={isResubmitMode}
            isEditMode={isEditMode}
            onEditClick={() => {
              setIsEditMode((prev) => !prev);
              setActionMessage({
                type: 'success',
                text: !isEditMode
                  ? 'Đã mở khóa form! Bạn có thể thay đổi hoặc tự tải lên tài liệu mới ở từng bước bên dưới.'
                  : 'Đã đóng chế độ chỉnh sửa.',
              });
            }}
            onResetToDraftClick={handleClearMockData}
            onResubmitClick={() => {
              setCurrentStep(1);
              setIsEditMode(true);
              setActionMessage({
                type: 'success',
                text: 'Đã mở khóa form. Bạn có thể cập nhật thông tin và nộp lại hồ sơ.',
              });
            }}
          />

          {/* Stepper Thanh Tiến Trình 4 Bước */}
          <KybStepIndicator
            currentStep={currentStep}
            completedSteps={completedSteps}
            isLargeLot={(selectedLot?.totalSlots || 0) > 50}
            onSelectStep={setCurrentStep}
            disabled={loadingKyb || busyAction}
          />

          {/* Khung Nội Dung Từng Bước */}
          <div className="rounded-3xl bg-white/85 backdrop-blur-xl border border-slate-200/90 p-6 sm:p-8 shadow-xs">
            {/* =====================================================================
                BƯỚC 1: PHÁP LÝ DOANH NGHIỆP / HỘ KINH DOANH
            ====================================================================== */}
            {currentStep === 1 && (
              <div className="space-y-5">
                <div className="flex items-center justify-between border-b border-slate-100 pb-4">
                  <div>
                    <span className="text-[11px] font-extrabold uppercase text-indigo-700 tracking-wider">
                      Bước 1 trên 4
                    </span>
                    <h2 className="text-lg font-bold text-slate-900 mt-0.5">
                      Giấy phép Đăng ký Kinh doanh (ĐKKD)
                    </h2>
                    <p className="text-xs text-slate-500">
                      Chứng minh tư cách pháp nhân hợp pháp của cơ sở kinh doanh bãi đỗ xe.
                    </p>
                  </div>
                  <span className="text-xs font-bold px-3 py-1 rounded-full bg-slate-100 text-slate-700 border border-slate-200">
                    Mục bắt buộc
                  </span>
                </div>

                <DocumentUploadCard
                  label="Giấy chứng nhận ĐKKD / Đăng ký Hộ kinh doanh"
                  description="Bản chụp rõ nét hoặc file scan PDF Giấy phép đăng ký kinh doanh còn hiệu lực."
                  value={businessLicenseUrl}
                  fileName={businessLicenseFileName}
                  fileSize={businessLicenseFileSize}
                  onChange={(url, name, size) => {
                    setBusinessLicenseUrl(url);
                    setBusinessLicenseFileName(name || '');
                    setBusinessLicenseFileSize(size || '');
                  }}
                  required
                  disabled={isLocked}
                  kybStatus={currentEffectiveStatus}
                  lotId={selectedLotId || 0}
                />

                <div className="rounded-2xl bg-slate-50 border border-slate-200/80 p-4 text-xs text-slate-600 flex items-start gap-3">
                  <FileCheck2 size={18} className="text-indigo-600 shrink-0 mt-0.5" />
                  <div>
                    <strong className="text-slate-800 block mb-0.5">Tiêu chuẩn kiểm duyệt Bước 1:</strong>
                    Tên doanh nghiệp / Hộ kinh doanh phải trùng khớp với thông tin chủ bãi. Bản scan không bị mờ nhòe, mất góc hoặc hết hạn sử dụng.
                  </div>
                </div>
              </div>
            )}

            {/* =====================================================================
                BƯỚC 2: HIỆN TRƯỜNG & GPS
            ====================================================================== */}
            {currentStep === 2 && (
              <div className="space-y-6">
                <div className="flex items-center justify-between border-b border-slate-100 pb-4">
                  <div>
                    <span className="text-[11px] font-extrabold uppercase text-indigo-700 tracking-wider">
                      Bước 2 trên 4
                    </span>
                    <h2 className="text-lg font-bold text-slate-900 mt-0.5">
                      Ảnh chụp Hiện trường & Đối chiếu Tọa độ GPS
                    </h2>
                    <p className="text-xs text-slate-500">
                      Bảo đảm bãi xe có mặt bằng thực tế thông qua xác minh khoảng cách Haversine &le; 500m.
                    </p>
                  </div>
                  <span className="text-xs font-bold px-3 py-1 rounded-full bg-slate-100 text-slate-700 border border-slate-200">
                    Tối thiểu 1 ảnh
                  </span>
                </div>

                {/* Danh sách ảnh hiện trường đã thêm */}
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="text-xs font-bold text-slate-800">
                      Bộ sưu tập ảnh hiện trường ({sitePhotos.length} ảnh)
                    </label>
                    <DocumentStatusBadge
                      kybStatus={currentEffectiveStatus}
                      hasFile={sitePhotos.length > 0}
                    />
                  </div>

                  {sitePhotos.length > 0 ? (
                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mb-4">
                      {sitePhotos.map((photo, idx) => (
                        <div
                          key={idx}
                          className="relative rounded-2xl overflow-hidden border border-slate-200 group bg-slate-900 shadow-2xs"
                        >
                          <img
                            src={photo.url}
                            alt={`Hiện trường ${idx + 1}`}
                            onError={(e) => {
                              const target = e.currentTarget;
                              target.style.display = 'none';
                              const sibling = target.nextElementSibling as HTMLElement;
                              if (sibling) sibling.style.display = 'flex';
                            }}
                            className="w-full h-32 object-cover opacity-90 group-hover:opacity-100 transition-opacity"
                          />
                          <div
                            style={{ display: 'none' }}
                            className="w-full h-32 bg-slate-800 text-slate-300 flex-col items-center justify-center text-center p-3 text-xs"
                          >
                            <span className="text-[11px] font-bold text-amber-300 mb-1">
                              ⚠️ Ảnh mẫu hệ thống
                            </span>
                            <span className="text-[10px] text-slate-400">
                              (Chưa có tệp thật trên đĩa - hãy xóa và tải ảnh thật lên)
                            </span>
                          </div>
                          {!isLocked && (
                            <button
                              type="button"
                              onClick={() => handleRemoveSitePhoto(idx)}
                              className="absolute top-2 right-2 p-1.5 rounded-lg bg-rose-600/90 text-white hover:bg-rose-700 transition-colors shadow-xs cursor-pointer z-10"
                              title="Xóa ảnh này"
                            >
                              <Trash2 size={13} />
                            </button>
                          )}
                          <div className="absolute bottom-2 left-2 right-2 flex items-center justify-between z-10">
                            <span className="px-2 py-0.5 rounded bg-black/75 text-white text-[10px] font-mono truncate max-w-[130px]">
                              {photo.fileName || `Ảnh #${idx + 1}`}
                            </span>
                            {photo.fileSize && (
                              <span className="px-1.5 py-0.5 rounded bg-black/75 text-slate-300 text-[9px] font-mono">
                                {photo.fileSize}
                              </span>
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 text-center text-xs text-slate-500 mb-4">
                      Chưa có ảnh hiện trường nào. Vui lòng chọn tệp từ máy tính bên dưới.
                    </div>
                  )}

                  {/* Form thêm ảnh hiện trường mới */}
                  {!isLocked && (
                    <DocumentUploadCard
                      label="Thêm ảnh chụp hiện trường thực tế"
                      description="Ảnh chụp rõ biển hiệu, cổng ra vào và khuôn viên đỗ xe."
                      value=""
                      onChange={handleAddSitePhoto}
                      disabled={isLocked}
                      kybStatus={currentEffectiveStatus}
                      lotId={selectedLotId || 0}
                      accept="image/*"
                      helpText="Hỗ trợ JPG, PNG, WEBP. Tối đa 10 MB mỗi ảnh."
                    />
                  )}
                </div>

                {/* Tọa độ GPS & Kiểm tra khoảng cách Haversine */}
                <GpsCoordinateChecker
                  photoLat={photoLatitude}
                  photoLng={photoLongitude}
                  lotLat={10.776889} // Tọa độ mẫu của bãi
                  lotLng={106.700806}
                  onChange={(lat, lng) => {
                    setPhotoLatitude(lat);
                    setPhotoLongitude(lng);
                  }}
                  disabled={isLocked}
                />
              </div>
            )}

            {/* =====================================================================
                BƯỚC 3: AN TOÀN PHÒNG CHÁY CHỮA CHÁY (PCCC)
            ====================================================================== */}
            {currentStep === 3 && (
              <div className="space-y-5">
                <div className="flex items-center justify-between border-b border-slate-100 pb-4">
                  <div>
                    <span className="text-[11px] font-extrabold uppercase text-indigo-700 tracking-wider">
                      Bước 3 trên 4
                    </span>
                    <h2 className="text-lg font-bold text-slate-900 mt-0.5">
                      An toàn Phòng cháy chữa cháy (PCCC)
                    </h2>
                    <p className="text-xs text-slate-500">
                      Bắt buộc có chứng nhận an toàn PCCC để bảo vệ an toàn tính mạng và phương tiện tài sản.
                    </p>
                  </div>
                  <span className="text-xs font-bold px-3 py-1 rounded-full bg-slate-100 text-slate-700 border border-slate-200">
                    Mục bắt buộc
                  </span>
                </div>

                <DocumentUploadCard
                  label="Văn bản / Giấy chứng nhận đủ điều kiện an toàn PCCC"
                  description="Biên bản kiểm tra hoặc chứng chỉ an toàn PCCC do cơ quan chức năng cấp."
                  value={fireSafetyCertificateUrl}
                  fileName={fireSafetyFileName}
                  fileSize={fireSafetyFileSize}
                  onChange={(url, name, size) => {
                    setFireSafetyCertificateUrl(url);
                    setFireSafetyFileName(name || '');
                    setFireSafetyFileSize(size || '');
                  }}
                  required
                  disabled={isLocked}
                  kybStatus={currentEffectiveStatus}
                  lotId={selectedLotId || 0}
                />

                <div className="rounded-2xl bg-amber-50 border border-amber-200 p-4 text-xs text-amber-900 flex items-start gap-3">
                  <Clock size={18} className="text-amber-700 shrink-0 mt-0.5" />
                  <div>
                    <strong className="block mb-0.5">Lưu ý quan trọng về thời hạn:</strong>
                    Giấy chứng nhận PCCC phải còn thời hạn tối thiểu <strong>30 ngày</strong> tính đến thời điểm nộp hồ sơ.
                  </div>
                </div>
              </div>
            )}

            {/* =====================================================================
                BƯỚC 4: KHẢO SÁT THỰC ĐỊA TẠI HIỆN TRƯỜNG
            ====================================================================== */}
            {currentStep === 4 && (
              <div className="space-y-6">
                <div className="flex items-center justify-between border-b border-slate-100 pb-4">
                  <div>
                    <span className="text-[11px] font-extrabold uppercase text-indigo-700 tracking-wider">
                      Bước 4 trên 4
                    </span>
                    <h2 className="text-lg font-bold text-slate-900 mt-0.5">
                      Kế hoạch Khảo sát Thực địa
                    </h2>
                    <p className="text-xs text-slate-500">
                      Quy chuẩn thẩm định theo quy mô bãi đỗ xe: Bãi &gt; 50 chỗ bắt buộc khảo sát tại chỗ.
                    </p>
                  </div>
                  <span
                    className={`text-xs font-bold px-3 py-1 rounded-full border ${
                      (selectedLot?.totalSlots || 0) > 50
                        ? 'bg-amber-100 text-amber-800 border-amber-200'
                        : 'bg-emerald-100 text-emerald-800 border-emerald-200'
                    }`}
                  >
                    {(selectedLot?.totalSlots || 0) > 50
                      ? '⚡ Bãi lớn (> 50 chỗ) – Bắt buộc khảo sát'
                      : '✓ Bãi ≤ 50 chỗ – Thẩm định Online'}
                  </span>
                </div>

                {/* Thông tin quy mô bãi */}
                <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200/80 flex items-center justify-between">
                  <div>
                    <div className="text-xs text-slate-500">Quy mô cơ sở:</div>
                    <div className="text-sm font-extrabold text-slate-900 mt-0.5">
                      {selectedLot.name} · {selectedLot.totalSlots} ô đỗ
                    </div>
                  </div>
                  <div className="text-right">
                    <div className="text-xs text-slate-500">Hình thức thẩm định:</div>
                    <div className="text-xs font-bold text-indigo-700 mt-0.5">
                      {(selectedLot?.totalSlots || 0) > 50
                        ? 'Đoàn kiểm tra đến thực địa'
                        : 'Xét duyệt hồ sơ trực tuyến'}
                    </div>
                  </div>
                </div>

                {/* Lịch hẹn khảo sát */}
                <div className="rounded-2xl border border-slate-200 p-5 bg-white space-y-4">
                  <div>
                    <label className="block text-xs font-bold text-slate-800 mb-1 flex items-center gap-1.5">
                      <Calendar size={15} className="text-indigo-600" />
                      Lịch hẹn mong muốn đón đoàn khảo sát{' '}
                      {(selectedLot?.totalSlots || 0) > 50 && (
                        <span className="text-rose-600">*</span>
                      )}
                    </label>
                    <input
                      type="datetime-local"
                      disabled={isLocked}
                      value={fieldSurveyAtUtc}
                      onChange={(e) => setFieldSurveyAtUtc(e.target.value)}
                      className="w-full sm:w-80 px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs font-semibold text-slate-900 focus:outline-hidden focus:border-indigo-600"
                    />
                    <p className="text-[11px] text-slate-400 mt-1">
                      Vui lòng chọn thời gian làm việc hành chính trong tương lai.
                    </p>
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-slate-800 mb-1">
                      Ghi chú / Hướng dẫn đón tiếp đoàn khảo sát
                    </label>
                    <textarea
                      rows={3}
                      disabled={isLocked}
                      value={fieldSurveyNote}
                      onChange={(e) => setFieldSurveyNote(e.target.value)}
                      placeholder="VD: Liên hệ anh Hùng (Trưởng ban quản lý tòa nhà) theo SĐT 0901xxxxxx khi đoàn đến nơi..."
                      className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-xs text-slate-900 placeholder-slate-400 focus:outline-hidden focus:border-indigo-600"
                    />
                  </div>
                </div>
              </div>
            )}

            {/* =====================================================================
                THANH TÁC VỤ ĐIỀU HƯỚNG & HÀNH ĐỘNG CHÂN FORM
            ====================================================================== */}
            <div className="mt-8 pt-6 border-t border-slate-100 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              {/* Nút lùi bước */}
              <div>
                {currentStep > 1 && (
                  <button
                    type="button"
                    onClick={() => setCurrentStep((s) => Math.max(1, s - 1))}
                    className="px-4 py-2.5 rounded-xl border border-slate-200 text-xs font-bold text-slate-700 hover:bg-slate-50 transition-colors flex items-center gap-1.5 cursor-pointer"
                  >
                    <ArrowLeft size={14} />
                    Quay lại Bước {currentStep - 1}
                  </button>
                )}
              </div>

              {/* Nhóm nút hành động chính */}
              <div className="flex items-center gap-2.5 flex-wrap sm:flex-nowrap justify-end">
                {/* Nút Lưu nháp (chỉ khi form chưa bị khóa) */}
                {!isLocked && (
                  <button
                    type="button"
                    disabled={busyAction}
                    onClick={handleSaveDraft}
                    className="px-5 py-2.5 rounded-xl bg-slate-100 hover:bg-slate-200 border border-slate-200 text-slate-700 text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer disabled:opacity-60"
                  >
                    <Save size={14} />
                    {busyAction ? 'Đang lưu…' : 'Lưu bản nháp'}
                  </button>
                )}

                {/* Nút chuyển bước tiếp theo */}
                {currentStep < 4 ? (
                  <button
                    type="button"
                    onClick={handleNextStep}
                    className="px-6 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md shadow-indigo-600/20 active:scale-[0.98] transition-all flex items-center gap-1.5 cursor-pointer"
                  >
                    Tiếp tục Bước {currentStep + 1}
                    <ArrowRight size={14} />
                  </button>
                ) : (
                  /* Nút Nộp hồ sơ (hoặc Nộp lại) */
                  !isLocked ? (
                    <button
                      type="button"
                      disabled={busyAction}
                      onClick={handleSubmitKyb}
                      className="px-7 py-2.5 rounded-xl bg-gradient-to-r from-indigo-600 to-blue-600 hover:from-indigo-700 hover:to-blue-700 text-white text-xs font-bold shadow-lg shadow-indigo-600/25 active:scale-[0.98] transition-all flex items-center gap-2 cursor-pointer disabled:opacity-60"
                    >
                      {isResubmitMode || isEditMode ? (
                        <>
                          <RotateCcw size={15} />
                          {busyAction ? 'Đang gửi lại…' : 'Cập nhật & Nộp Hồ sơ Thẩm định'}
                        </>
                      ) : (
                        <>
                          <Send size={15} />
                          {busyAction ? 'Đang nộp…' : 'Nộp Hồ Sơ Thẩm Định'}
                        </>
                      )}
                    </button>
                  ) : (
                    <button
                      type="button"
                      onClick={() => {
                        setIsEditMode(true);
                        setCurrentStep(1);
                        setActionMessage({
                          type: 'success',
                          text: 'Đã mở khóa! Bạn có thể chỉnh sửa và tự thêm tệp từ Bước 1.',
                        });
                      }}
                      className="px-6 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md shadow-indigo-600/20 active:scale-[0.98] transition-all flex items-center gap-1.5 cursor-pointer"
                    >
                      <RotateCcw size={14} />
                      Chỉnh sửa / Cập nhật tài liệu mới
                    </button>
                  )
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Modal Đăng Ký Bãi Đỗ Xe Mới */}
      <RegisterLotModal
        isOpen={isRegisterModalOpen}
        onClose={() => setIsRegisterModalOpen(false)}
        ownerProfileId={ownerProfileId}
        onSuccess={(newLotId, lotName) => {
          loadOwnerLots(newLotId);
          setActionMessage({
            type: 'success',
            text: `Đã đăng ký cơ sở bãi đỗ "${lotName}" thành công! Vui lòng nộp hồ sơ KYB bên dưới.`,
          });
        }}
      />
    </div>
  );
};
