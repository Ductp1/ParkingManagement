import React, { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAppSelector, useAppDispatch } from '@/store';
import { sendBookingConfirmedNotification } from '@/store/slices/notificationSlice';
import { fetchVehicles } from '@/store/slices/vehicleSlice';
import { useParkingHub, type SlotStateChangedEvent } from '@/realtime/useParkingHub';
import { parkingApi } from '@/api/parkingApi';
import { bookingApi } from '@/api/bookingApi';
import { paymentApi } from '@/api/paymentApi';
import { supportApi } from '@/api/supportApi';
import type {
  ParkingLotDto,
  LotOperationalStatusDto,
  FloorLayoutDto,
  LayoutCellDto,
  SlotHierarchyDto,
} from '@/types/parking';
import type { BookingDetailDto } from '@/types/booking';
import type { AppliedPromotionDto, VietQrPaymentDto } from '@/types/payment';
import type { LotReviewsDto } from '@/types/support';

interface FloorOption {
  id: number;
  name: string;
  level: number;
  zoneName: string;
  isClosed: boolean;
  slots: SlotHierarchyDto[];
}

/** Định dạng Date thành value cho input datetime-local (giờ máy). */
const toLocalInput = (d: Date): string => {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

export const LotDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const lotId = id ? parseInt(id, 10) : 1;
  const navigate = useNavigate();
  const dispatch = useAppDispatch();

  const { vehicles } = useAppSelector((state) => state.vehicle);
  const { user } = useAppSelector((state) => state.auth);
  const userId = user?.id || 5;

  useEffect(() => {
    if (vehicles.length === 0) {
      void dispatch(fetchVehicles(userId));
    }
  }, [dispatch, userId, vehicles.length]);

  const [lot, setLot] = useState<ParkingLotDto | null>(null);
  const [availableFloors, setAvailableFloors] = useState<FloorOption[]>([]);
  const [selectedFloorId, setSelectedFloorId] = useState<number | null>(null);
  const [floorLayout, setFloorLayout] = useState<FloorLayoutDto | null>(null);
  const [isLoadingLayout, setIsLoadingLayout] = useState<boolean>(false);
  const [selectedSpotCode, setSelectedSpotCode] = useState<string>('');
  const [slotConflictNotice, setSlotConflictNotice] = useState<string | null>(null);
  const [hasEvService, setHasEvService] = useState<boolean>(false);
  const [hasWashService, setHasWashService] = useState<boolean>(false);
  const [hasInsurance] = useState<boolean>(true);
  const [bookingNote, setBookingNote] = useState<string>('');
  const [paymentMethod, setPaymentMethod] = useState<'gate' | 'vnpay' | 'vietqr'>('gate');
  const [promoCodeInput, setPromoCodeInput] = useState<string>('');
  const [appliedPromo, setAppliedPromo] = useState<AppliedPromotionDto | null>(null);
  const [promoBusy, setPromoBusy] = useState<boolean>(false);
  const [promoError, setPromoError] = useState<string | null>(null);
  const [vietQrData, setVietQrData] = useState<VietQrPaymentDto | null>(null);
  const [paymentInitError, setPaymentInitError] = useState<string | null>(null);
  const [lotReviews, setLotReviews] = useState<LotReviewsDto | null>(null);
  const [showSuccessModal, setShowSuccessModal] = useState<boolean>(false);
  const [notifySent, setNotifySent] = useState<boolean>(false);
  const [showStickyStepper, setShowStickyStepper] = useState<boolean>(false);
  const [countdown, setCountdown] = useState<number>(493); // 8m 13s in seconds

  // US-022/US-069: thời gian gửi & lấy xe dự kiến — người dùng chỉnh được (backend yêu cầu start >= now + 15 phút).
  const defaultStart = new Date(Date.now() + 60 * 60 * 1000); // +1 giờ
  const defaultEnd = new Date(defaultStart.getTime() + 4 * 60 * 60 * 1000); // +4 giờ gửi
  const [startAtLocal, setStartAtLocal] = useState<string>(toLocalInput(defaultStart));
  const [endAtLocal, setEndAtLocal] = useState<string>(toLocalInput(defaultEnd));

  // US-022: tạo booking thật qua POST /api/v1/bookings (BookingService).
  const [bookingBusy, setBookingBusy] = useState(false);
  const [bookingError, setBookingError] = useState<string | null>(null);
  const [bookingResult, setBookingResult] = useState<BookingDetailDto | null>(null);

  // Fetch lot info, hierarchy & reviews from API
  useEffect(() => {
    parkingApi
      .getById(lotId)
      .then((data) => setLot(data))
      .catch(() => {
        // Fallback default
      });
    parkingApi
      .getLotHierarchy(lotId)
      .then((hierarchy) => {
        const floors: FloorOption[] = hierarchy.zones.flatMap((z) =>
          z.floors.map((f) => ({
            id: f.id,
            name: f.name,
            level: f.level,
            zoneName: z.name,
            isClosed: f.isClosed || z.isClosed,
            slots: f.slots ?? [],
          }))
        );
        setAvailableFloors(floors);
        const firstOpen = floors.find((f) => !f.isClosed) || floors[0];
        if (firstOpen) {
          setSelectedFloorId(firstOpen.id);
        }
      })
      .catch(() => setAvailableFloors([]));
    supportApi
      .getLotReviews(lotId, 5)
      .then((rev) => setLotReviews(rev))
      .catch(() => setLotReviews(null));
  }, [lotId]);

  // Fetch published 2D floor layout (fallback to floor.slots if owner hasn't published layout yet)
  useEffect(() => {
    if (!selectedFloorId) return;
    let cancelled = false;
    const loadFloorLayout = async () => {
      setIsLoadingLayout(true);
      try {
        const layout = await parkingApi.getFloorLayout(selectedFloorId);
        if (!cancelled && layout && layout.cells?.length > 0) {
          setFloorLayout(layout);
          const firstAvail = layout.cells.find((c) => c.type === 'slot' && c.state === 'Available' && c.code);
          if (firstAvail?.code) {
            setSelectedSpotCode(firstAvail.code);
            setHasEvService(firstAvail.slotType === 'EVCharging');
          }
          setIsLoadingLayout(false);
          return;
        }
      } catch {
        // Fallback to hierarchy slots below
      }

      if (!cancelled) {
        const floor = availableFloors.find((f) => f.id === selectedFloorId);
        if (floor && floor.slots.length > 0) {
          const maxCol = Math.max(8, ...floor.slots.map((s) => s.gridX + (s.widthCells || 1)));
          const maxRow = Math.max(5, ...floor.slots.map((s) => s.gridY + (s.heightCells || 1)));
          const cells: LayoutCellDto[] = floor.slots.map((s) => ({
            x: s.gridX,
            y: s.gridY,
            type: 'slot',
            code: s.code,
            slotType: s.slotType,
            maxVehicleType: s.maxVehicleType,
            state: s.state,
            widthCells: s.widthCells || 1,
            heightCells: s.heightCells || 1,
          }));
          setFloorLayout({
            floorId: floor.id,
            floorName: floor.name,
            level: floor.level,
            versionNo: 1,
            isPublished: false,
            columns: maxCol,
            rows: maxRow,
            cells,
          });
          const firstAvail = floor.slots.find((s) => s.state === 'Available');
          if (firstAvail) {
            setSelectedSpotCode(firstAvail.code);
            setHasEvService(firstAvail.slotType === 'EVCharging');
          }
        } else {
          setFloorLayout(null);
        }
        setIsLoadingLayout(false);
      }
    };
    void loadFloorLayout();
    return () => {
      cancelled = true;
    };
  }, [selectedFloorId, availableFloors]);

  // US-068: độ tươi dữ liệu bãi (heartbeat & stale) – tự làm mới mỗi 30 giây để cảnh báo kịp thời
  const [opStatus, setOpStatus] = useState<LotOperationalStatusDto | null>(null);
  useEffect(() => {
    let cancelled = false;
    const loadStatus = async () => {
      try {
        const status = await parkingApi.getOperationalStatus(lotId);
        if (!cancelled) setOpStatus(status);
      } catch {
        if (!cancelled) setOpStatus(null);
      }
    };
    void loadStatus();
    const interval = setInterval(() => void loadStatus(), 30_000);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [lotId]);

  // Scroll listener for sticky stepper
  useEffect(() => {
    const handleScroll = () => {
      if (window.scrollY > 300) {
        setShowStickyStepper(true);
      } else {
        setShowStickyStepper(false);
      }
    };
    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  // Countdown timer effect
  useEffect(() => {
    const timer = setInterval(() => {
      setCountdown((prev) => (prev > 0 ? prev - 1 : 600));
    }, 1000);
    return () => clearInterval(timer);
  }, []);

  const formatCountdown = (seconds: number) => {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  };

  const activeFloor = availableFloors.find((f) => f.id === selectedFloorId) || null;
  const selectedFloorLabel = activeFloor ? `${activeFloor.zoneName} · ${activeFloor.name}` : 'Tầng mặc định';

  // Real-time SignalR: khi có khách khác đặt/hủy ô đỗ → cập nhật màu đỏ ngay & đẩy Khách A sang ô xanh gần nhất
  const handleSlotStateChanged = (event: SlotStateChangedEvent) => {
    let affectedCode: string | null = null;
    setAvailableFloors((prev) =>
      prev.map((fl) => ({
        ...fl,
        slots: fl.slots.map((s) => {
          if (s.id === event.slotId) {
            affectedCode = s.code;
            return { ...s, state: event.newState };
          }
          return s;
        }),
      }))
    );

    if (!affectedCode && activeFloor) {
      const found = activeFloor.slots.find((s) => s.id === event.slotId);
      if (found) affectedCode = found.code;
    }
    if (!affectedCode) return;

    setFloorLayout((prev) => {
      if (!prev) return prev;
      const nextCells = prev.cells.map((c) =>
        c.type === 'slot' && c.code === affectedCode ? { ...c, state: event.newState } : c
      );

      if (event.newState !== 'Available' && selectedSpotCode === affectedCode) {
        const takenCell = nextCells.find((c) => c.type === 'slot' && c.code === affectedCode);
        const availCells = nextCells.filter((c) => c.type === 'slot' && c.state === 'Available' && c.code);
        if (availCells.length > 0 && takenCell) {
          availCells.sort((a, b) => {
            const distA = (a.x - takenCell.x) ** 2 + (a.y - takenCell.y) ** 2;
            const distB = (b.x - takenCell.x) ** 2 + (b.y - takenCell.y) ** 2;
            return distA - distB;
          });
          const nearest = availCells[0];
          setSelectedSpotCode(nearest.code!);
          setHasEvService(nearest.slotType === 'EVCharging');
          setSlotConflictNotice(
            `Ô ${affectedCode} vừa được khách khác đặt — hệ thống đã tự động chuyển bạn sang ô trống gần nhất: ${nearest.code}.`
          );
        } else {
          setSelectedSpotCode('');
          setSlotConflictNotice(`Ô ${affectedCode} vừa được khách khác đặt và tầng này hiện không còn ô trống.`);
        }
      }
      return { ...prev, cells: nextCells };
    });
  };

  useParkingHub(lotId, undefined, undefined, handleSlotStateChanged);

  const floorSlotCells = (floorLayout?.cells ?? []).filter(
    (c): c is LayoutCellDto & { code: string } => c.type === 'slot' && Boolean(c.code)
  );
  const availableFloorSpots = floorSlotCells.filter((c) => c.state === 'Available');
  const selectedCell = floorSlotCells.find((c) => c.code === selectedSpotCode) || availableFloorSpots[0] || null;
  const currentSpot = {
    code: selectedCell?.code || selectedSpotCode || 'Tự động',
    isEv: selectedCell?.slotType === 'EVCharging',
    typeText:
      selectedCell?.slotType === 'EVCharging'
        ? 'Sạc EV'
        : selectedCell?.slotType === 'Oversized'
        ? 'Xe lớn / SUV'
        : selectedCell?.maxVehicleType || 'Tiêu chuẩn',
    desc: selectedCell
      ? `${selectedFloorLabel} • Loại ô: ${selectedCell.slotType || 'Standard'} • Phù hợp: ${selectedCell.maxVehicleType || 'Mọi xe'}`
      : `${selectedFloorLabel} • Hệ thống tự động phân bổ ô trống phù hợp khi vào cổng`,
  };

  // Default vehicle selection (from real user vehicles in Redux or fallback)
  const defaultVehicle = vehicles.find((v) => v.isDefault) || vehicles[0];
  const vehiclePlate = defaultVehicle?.plateDisplay || defaultVehicle?.plateNumber || '51K - 889.32';
  const vehicleBrand = defaultVehicle?.brand || 'Mercedes-Benz';
  const vehicleModel = defaultVehicle?.model || 'EQE SUV';
  const vehicleColor = defaultVehicle?.color || 'Xanh Obsidian';
  const vehicleFuel = defaultVehicle?.fuelType || 'Electric';
  const vehicleTypeStr = String(defaultVehicle?.vehicleType || 'Sedan');

  // US-070/071: thời lượng theo khung giờ người dùng chọn
  const durationHours = Math.max(
    0,
    Math.ceil((new Date(endAtLocal).getTime() - new Date(startAtLocal).getTime()) / 3600000)
  );

  // US-070/071: báo giá THẬT từ bảng giá của bãi (GET /api/v1/pricing/quote — PaymentService).
  // Lỗi quote → null → fallback giá client bên dưới (không chặn UI).
  const [quoteFinalAmount, setQuoteFinalAmount] = useState<number | null>(null);
  useEffect(() => {
    if (!startAtLocal || !endAtLocal) return;
    const startUtc = new Date(startAtLocal).toISOString();
    const endUtc = new Date(endAtLocal).toISOString();
    bookingApi
      .getQuote(lotId, vehicleTypeStr, startUtc, endUtc)
      .then((q) => setQuoteFinalAmount(q.finalAmount))
      .catch(() => setQuoteFinalAmount(null));
  }, [lotId, startAtLocal, endAtLocal, vehicleTypeStr]);

  // Pricing calculations
  const hourlyRate = 30000;
  const parkingFee = quoteFinalAmount ?? hourlyRate * durationHours; // giá thật từ PaymentService, fallback theo giờ client
  const techFee = 10000;
  const vipDiscount = 24000; // -20%
  const evFee = hasEvService ? 50000 : 0;
  const washFee = hasWashService ? 80000 : 0;
  const subTotalAmount = Math.max(0, parkingFee + techFee - vipDiscount + evFee + washFee);
  const promoDiscount = appliedPromo?.discountAmount ?? 0;
  const totalAmount = Math.max(0, subTotalAmount - promoDiscount);

  const handleApplyPromo = async () => {
    const code = promoCodeInput.trim();
    if (!code) {
      setPromoError('Vui lòng nhập mã khuyến mãi.');
      return;
    }
    setPromoBusy(true);
    setPromoError(null);
    try {
      const applied = await paymentApi.applyPromotion({
        code,
        parkingLotId: lotId,
        originalAmount: subTotalAmount,
      });
      setAppliedPromo(applied);
    } catch (err) {
      const axiosErr = err as { response?: { data?: { detail?: string; title?: string } } };
      setAppliedPromo(null);
      setPromoError(
        axiosErr?.response?.data?.detail ||
          axiosErr?.response?.data?.title ||
          (err instanceof Error && err.message) ||
          'Mã khuyến mãi không hợp lệ hoặc đã hết hạn.'
      );
    } finally {
      setPromoBusy(false);
    }
  };

  const handleClearPromo = () => {
    setAppliedPromo(null);
    setPromoCodeInput('');
    setPromoError(null);
  };

  const handleSpotSelect = (spotCode: string, slotType?: string | null) => {
    setSelectedSpotCode(spotCode);
    setHasEvService(slotType === 'EVCharging');
  };

  // US-022 + US-032/034: xác nhận đặt chỗ → POST /api/v1/bookings → khởi tạo thanh toán nếu chọn VNPAY/VietQR
  const handleConfirmBooking = async () => {
    setBookingError(null);
    setPaymentInitError(null);
    setVietQrData(null);
    if (!defaultVehicle) {
      setBookingError('Vui lòng thêm xe của bạn trong mục "Xe của tôi" trước khi đặt chỗ.');
      return;
    }
    if (new Date(endAtLocal).getTime() <= new Date(startAtLocal).getTime()) {
      setBookingError('Giờ ra dự kiến phải sau giờ vào dự kiến.');
      return;
    }

    setBookingBusy(true);
    const selectedSlotObj = activeFloor?.slots.find((s) => s.code === selectedSpotCode);
    try {
      const result = await bookingApi.createBooking({
        userId,
        vehicleId: defaultVehicle.id,
        plateNumber: defaultVehicle.plateNumber,
        vehicleType: vehicleTypeStr,
        parkingLotId: lotId,
        ownerProfileId: lot?.ownerProfileId ?? 0,
        parkingLotName: lot?.name || 'Bãi đỗ xe',
        slotId: selectedSlotObj?.id ?? null,
        slotCode: selectedSpotCode || undefined,
        allocationMode: 'Dynamic',
        startAtUtc: new Date(startAtLocal).toISOString(),
        endAtUtc: new Date(endAtLocal).toISOString(),
        totalAmount,
        promotionCode: appliedPromo?.code ?? null,
        requiresOwnerApproval: false,
        payAtGate: paymentMethod === 'gate',
      });
      setBookingResult(result);
      if (selectedSlotObj) {
        setFloorLayout((prev) =>
          prev
            ? {
                ...prev,
                cells: prev.cells.map((c) =>
                  c.type === 'slot' && c.code === selectedSlotObj.code ? { ...c, state: 'Reserved' } : c
                ),
              }
            : prev
        );
      }

      try {
        await dispatch(
          sendBookingConfirmedNotification({
            userId,
            parkingLotName: result.parkingLotName || (lot?.name || ''),
            slotCode: result.slotCode || currentSpot.code,
            floor: selectedFloorLabel,
            bookingCode: result.code,
          })
        ).unwrap();
        setNotifySent(true);
      } catch {
        setNotifySent(false);
      }

      if (paymentMethod === 'vnpay') {
        try {
          const vnpay = await paymentApi.createVnpayPayment({
            bookingId: result.id,
            bookingCode: result.code,
            parkingLotId: lotId,
            ownerProfileId: lot?.ownerProfileId ?? 0,
            userId,
            amount: totalAmount,
          });
          window.location.href = vnpay.paymentUrl;
          return;
        } catch (payErr) {
          const axiosErr = payErr as { response?: { data?: { detail?: string; title?: string } } };
          setPaymentInitError(
            axiosErr?.response?.data?.detail ||
              axiosErr?.response?.data?.title ||
              'Đã tạo booking nhưng chưa khởi tạo được cổng VNPAY — bạn có thể thanh toán lại từ Lịch sử đặt chỗ.'
          );
        }
      } else if (paymentMethod === 'vietqr') {
        try {
          const qr = await paymentApi.createVietQrPayment({
            bookingId: result.id,
            bookingCode: result.code,
            parkingLotId: lotId,
            ownerProfileId: lot?.ownerProfileId ?? 0,
            userId,
            amount: totalAmount,
            bankBin: '970436',
            bankAccountNumber: '1012345678',
            bankAccountName: 'PARKMASTER VIETNAM',
          });
          setVietQrData(qr);
        } catch (payErr) {
          const axiosErr = payErr as { response?: { data?: { detail?: string; title?: string } } };
          setPaymentInitError(
            axiosErr?.response?.data?.detail ||
              axiosErr?.response?.data?.title ||
              'Đã tạo booking nhưng chưa tạo được mã VietQR.'
          );
        }
      }

      setShowSuccessModal(true);
    } catch (err) {
      const axiosErr = err as { response?: { data?: { detail?: string; title?: string } } };
      setBookingError(
        axiosErr?.response?.data?.detail ||
          axiosErr?.response?.data?.title ||
          (err instanceof Error && err.message) ||
          'Không tạo được booking — vui lòng thử lại sau.'
      );
    } finally {
      setBookingBusy(false);
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      {/* US-068: cảnh báo dữ liệu bãi có thể KHÔNG chính xác khi quá hạn heartbeat (stale) */}
      {opStatus?.isDataStale && (
        <div className="bg-amber-50 border-b border-amber-200 px-4 py-3">
          <div className="max-w-4xl mx-auto">
            <p className="text-xs font-bold text-amber-800">
              ⚠️ Dữ liệu số chỗ trống của bãi này có thể KHÔNG chính xác
            </p>
            <p className="text-[11px] text-amber-700 mt-0.5">
              Bãi không gửi cập nhật trong {opStatus.staleAfterMinutes} phút
              {opStatus.lastHeartbeatAtUtc
                ? ` (cập nhật gần nhất: ${new Date(opStatus.lastHeartbeatAtUtc).toLocaleTimeString('vi-VN')})`
                : ' (chưa từng nhận dữ liệu từ thiết bị bãi)'}
              . Vui lòng liên hệ hotline bãi trước khi đặt chỗ.
            </p>
          </div>
        </div>
      )}
      {/* 1. Smart Sticky Stepper (Floats under header on scroll) */}
      <div
        className={`fixed top-16 left-0 right-0 z-40 bg-white/95 backdrop-blur-md border-b border-slate-200 shadow-sm transition-all duration-300 ease-in-out ${
          showStickyStepper ? 'translate-y-0 opacity-100 pointer-events-auto' : '-translate-y-full opacity-0 pointer-events-none'
        }`}
      >
        <div className="max-w-4xl mx-auto px-4 py-3 flex items-center justify-between">
          {/* Step 1 */}
          <div
            className="flex items-center gap-3 cursor-pointer group flex-1"
            onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
          >
            <div className="w-8 h-8 rounded-full bg-blue-600 text-white flex items-center justify-center font-bold text-xs shadow-sm">
              <span className="material-symbols-outlined text-[16px]">check</span>
            </div>
            <div className="flex flex-col min-w-0">
              <span className="text-xs font-bold text-slate-800 truncate">1. Chọn bãi đỗ</span>
              <span className="text-[11px] text-slate-500 truncate">{lot?.name || 'Landmark 81 Garage'}</span>
            </div>
          </div>

          <div className="h-0.5 flex-1 mx-3 bg-blue-600 rounded-full hidden sm:block"></div>

          {/* Step 2 */}
          <div
            className="flex items-center gap-3 cursor-pointer group flex-1 justify-center sm:justify-start"
            onClick={() => document.getElementById('map-section')?.scrollIntoView({ behavior: 'smooth' })}
          >
            <div className="w-8 h-8 rounded-full bg-blue-600 text-white flex items-center justify-center font-bold text-xs shadow-md ring-4 ring-blue-100">
              <span>2</span>
            </div>
            <div className="flex flex-col min-w-0">
              <span className="text-xs font-bold text-blue-600 truncate">2. Chọn vị trí</span>
              <span className="text-[11px] text-blue-600 font-medium truncate">
                {selectedFloorLabel} • Ô {currentSpot.code}
              </span>
            </div>
          </div>

          <div className="h-0.5 flex-1 mx-3 bg-slate-200 rounded-full hidden sm:block"></div>

          {/* Step 3 */}
          <div
            className="flex items-center gap-3 cursor-pointer group flex-1 justify-end sm:justify-start"
            onClick={() => document.getElementById('booking-section')?.scrollIntoView({ behavior: 'smooth' })}
          >
            <div className="w-8 h-8 rounded-full bg-slate-100 text-slate-600 border border-slate-300 flex items-center justify-center font-bold text-xs">
              <span>3</span>
            </div>
            <div className="flex flex-col min-w-0 text-left">
              <span className="text-xs font-semibold text-slate-600 truncate">3. Thanh toán</span>
              <span className="text-[11px] text-slate-400 truncate">{totalAmount.toLocaleString('vi-VN')} đ</span>
            </div>
          </div>
        </div>
      </div>

      {/* 2. Hero Section with Cover Image */}
      <div className="relative w-full h-80 overflow-hidden bg-slate-900">
        <img
          src={lot?.coverImageUrl || "https://images.unsplash.com/photo-1506521781263-d8422e82f27a?auto=format&fit=crop&w=1600&q=80"}
          alt={lot?.name || "Landmark 81 Underground Garage"}
          className="w-full h-full object-cover opacity-80"
        />
        <div className="absolute inset-0 bg-gradient-to-t from-slate-950/90 via-slate-900/40 to-transparent"></div>

        <div className="absolute top-6 left-4 sm:left-8 lg:left-12 flex flex-wrap items-center gap-3">
          <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-white/90 backdrop-blur-md shadow-sm">
            <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse"></span>
            <span className="text-xs text-slate-900 uppercase tracking-wider font-bold">CAMERA AI ANPR LIVE</span>
          </div>
          <div className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-full bg-white/90 backdrop-blur-md shadow-sm">
            <span className="material-symbols-outlined text-[16px] text-blue-600">bolt</span>
            <span className="text-xs text-slate-900 font-bold">Siêu sạc DC 120kW</span>
          </div>
        </div>

        <div className="absolute bottom-6 right-4 sm:right-8 lg:right-12 hidden md:flex items-center gap-2 px-4 py-2 rounded-xl bg-white/90 backdrop-blur-md shadow-md text-slate-900">
          <span className="material-symbols-outlined text-blue-600 text-[20px]">verified</span>
          <span className="text-xs font-bold">Hệ sinh thái ParkMaster Premium</span>
        </div>
      </div>

      {/* Main Content Area */}
      <div className="w-full max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pb-20">
        {/* AC3 Warning Alert if Lot is Suspended/Not Bookable */}
        {lot && (!lot.canBook || lot.status !== 'Active') && (
          <div className="mt-6 p-4 rounded-2xl bg-amber-50 border border-amber-300 text-amber-900 flex items-start gap-3 shadow-sm">
            <span className="material-symbols-outlined text-amber-600 text-[26px] shrink-0">warning</span>
            <div>
              <h4 className="font-bold text-sm">Bãi đỗ này hiện đang tạm ngừng tiếp nhận đặt chỗ online</h4>
              <p className="text-xs text-amber-800 mt-1">
                Trạng thái hiện tại: <strong>{lot.status}</strong>. Hệ thống tạm khóa luồng đặt giữ chỗ trực tuyến để đảm bảo an toàn. Quý khách vui lòng liên hệ hotline chung của bãi: <strong className="underline">{lot.hotlinePhone || '1900 6868'}</strong> để được hỗ trợ.
              </p>
            </div>
          </div>
        )}

        {/* 3. White Overlapping Info Card */}
        <div className="relative z-10 -mt-16 mx-auto max-w-7xl shadow-lg rounded-2xl bg-white p-6 sm:p-8 border border-slate-100 mb-10">
          <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-6">
            <div className="flex flex-col sm:flex-row items-start sm:items-center gap-5">
              <div className="w-20 h-20 sm:w-24 sm:h-24 rounded-2xl bg-gradient-to-br from-blue-600 to-indigo-700 flex items-center justify-center text-white shadow-lg shrink-0">
                <span className="material-symbols-outlined text-[42px]">apartment</span>
              </div>
              <div className="flex flex-col">
                <div className="flex flex-wrap items-center gap-3 mb-1">
                  <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-900 tracking-tight">
                    {lot?.name || 'Landmark 81 Parking Garage'}
                  </h1>
                  <span className="px-2.5 py-1 rounded-full bg-blue-50 text-blue-700 text-[11px] font-bold uppercase tracking-wider flex items-center gap-1">
                    <span className="material-symbols-outlined text-[14px]">stars</span> TÒA THÁP BIỂU TƯỢNG TP.HCM
                  </span>
                </div>
                <p className="text-sm text-slate-500 flex items-center gap-1.5 mb-3">
                  <span className="material-symbols-outlined text-[18px] text-slate-400">location_on</span>
                  {lot?.address || '208 Nguyễn Hữu Cảnh, Phường 22, Bình Thạnh, TP. Hồ Chí Minh'}
                </p>
                <div className="flex flex-wrap items-center gap-2 sm:gap-4">
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg bg-slate-100 text-xs text-slate-800 font-medium">
                    <span className="material-symbols-outlined text-[16px] text-emerald-600">schedule</span>
                    Mở cửa: {lot?.openingHours || '24/7 (Cả ngày lễ)'}
                  </span>
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg bg-emerald-50 text-xs text-emerald-700 font-bold">
                    <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
                    Trống {lot ? `${lot.availableSlots}/${lot.totalSlots}` : '42/120'} chỗ đỗ
                  </span>
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg bg-slate-100 text-xs text-slate-800 font-medium">
                    <span className="material-symbols-outlined text-[16px] text-blue-600">call</span>
                    Hotline: {lot?.hotlinePhone || '1900 6868'}
                  </span>
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg bg-slate-100 text-xs text-slate-800 font-medium">
                    <span className="material-symbols-outlined text-[16px] text-amber-600">height</span>
                    Cao tối đa: {(lot?.maxHeightCm ?? 210) / 100}m
                  </span>
                </div>

                {/* Amenities Badges (US-018 AC1) */}
                {lot?.amenities && lot.amenities.length > 0 && (
                  <div className="flex flex-wrap items-center gap-1.5 mt-3 pt-3 border-t border-slate-100">
                    <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider mr-1">Tiện ích:</span>
                    {lot.amenities.map((item, idx) => (
                      <span key={idx} className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-blue-50 text-blue-700 text-xs font-semibold">
                        <span className="material-symbols-outlined text-[13px]">check_circle</span>
                        {item}
                      </span>
                    ))}
                  </div>
                )}
              </div>
            </div>

            <div className="flex items-center gap-3 pt-4 lg:pt-0">
              <button
                onClick={() => navigate('/driver')}
                className="flex-1 sm:flex-none px-4 py-2.5 rounded-xl bg-blue-50 text-blue-700 hover:bg-blue-100 transition-colors text-sm flex items-center justify-center gap-2 font-semibold shadow-xs"
                type="button"
              >
                <span className="material-symbols-outlined text-[18px]">sync_alt</span>
                <span>Đổi địa điểm</span>
              </button>
              <button
                aria-label="Chia sẻ"
                className="p-2.5 rounded-xl bg-slate-100 text-slate-600 hover:text-slate-900 hover:bg-slate-200 transition-colors flex items-center justify-center"
                type="button"
              >
                <span className="material-symbols-outlined text-[20px]">share</span>
              </button>
              <button
                aria-label="Lưu địa điểm"
                className="p-2.5 rounded-xl bg-slate-100 text-slate-600 hover:text-slate-900 hover:bg-slate-200 transition-colors flex items-center justify-center"
                type="button"
              >
                <span className="material-symbols-outlined text-[20px]">bookmark_border</span>
              </button>
            </div>
          </div>
        </div>

        {/* 4. Floor Selector Tabs (Real Zones & Floors from ParkingService) */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 p-3 bg-white rounded-2xl shadow-xs border border-slate-200 mb-6">
          <div className="flex flex-wrap items-center gap-2 p-1 bg-slate-100 rounded-xl">
            {availableFloors.length === 0 ? (
              <span className="px-3 py-1.5 text-xs text-slate-500 font-medium">
                Đang tải danh sách tầng...
              </span>
            ) : (
              availableFloors.map((fl) => {
                const isActive = fl.id === selectedFloorId;
                const availCount = fl.slots.filter((s) => s.state === 'Available').length;
                return (
                  <button
                    key={fl.id}
                    type="button"
                    disabled={fl.isClosed}
                    onClick={() => setSelectedFloorId(fl.id)}
                    className={`px-3.5 py-2 rounded-lg text-xs sm:text-sm font-semibold transition-colors flex items-center gap-1.5 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed ${
                      isActive ? 'bg-blue-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900'
                    }`}
                  >
                    <span
                      className={`w-2 h-2 rounded-full ${
                        fl.isClosed ? 'bg-rose-400' : availCount > 0 ? 'bg-emerald-400' : 'bg-amber-400'
                      }`}
                    />
                    <span>
                      {fl.zoneName} · {fl.name}
                    </span>
                    <span className={`text-[11px] ${isActive ? 'text-blue-100' : 'text-slate-400'}`}>
                      ({fl.isClosed ? 'Đóng' : `${availCount} trống`})
                    </span>
                  </button>
                );
              })
            )}
          </div>

          <div className="flex flex-wrap items-center gap-4 px-3 py-1">
            <div className="flex items-center gap-1.5 text-xs text-slate-500">
              <span className="w-3 h-3 rounded bg-emerald-500"></span>
              Đang trống
            </div>
            <div className="flex items-center gap-1.5 text-xs text-slate-500">
              <span className="w-3 h-3 rounded bg-blue-600"></span>
              Đang chọn
            </div>
            <div className="flex items-center gap-1.5 text-xs text-slate-500">
              <span className="w-3 h-3 rounded bg-amber-500"></span>
              Trạm sạc EV
            </div>
            <div className="flex items-center gap-1.5 text-xs text-slate-500">
              <span className="w-3 h-3 rounded bg-rose-500"></span>
              Đã đặt / Có xe
            </div>
          </div>
        </div>

        {/* 5. Technical Blueprint / Real 2D Floor Layout Matrix */}
        <section className="rounded-2xl bg-white p-6 shadow-md border border-slate-100 mb-10 scroll-mt-36" id="map-section">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-4 gap-2 border-b border-slate-100">
            <div>
              <h2 className="text-xl font-bold text-slate-900 flex items-center gap-2">
                <span className="material-symbols-outlined text-blue-600">view_quilt</span>
                Sơ đồ 2D · {selectedFloorLabel}
                {floorLayout?.isPublished && (
                  <span className="text-[11px] font-bold px-2 py-0.5 rounded-full bg-blue-50 text-blue-700 border border-blue-200">
                    Bản vẽ v{floorLayout.versionNo}
                  </span>
                )}
              </h2>
              <p className="text-xs text-slate-500 mt-0.5">
                Dữ liệu sơ đồ &amp; trạng thái ô đỗ đồng bộ trực tiếp từ cấu hình bãi của chủ bãi
              </p>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-xs px-3 py-1.5 rounded-lg bg-emerald-50 text-emerald-700 font-bold flex items-center gap-1.5 border border-emerald-200">
                <span className="material-symbols-outlined text-[16px]">sensors</span>
                Trống {availableFloorSpots.length}/{floorSlotCells.length} ô
              </span>
            </div>
          </div>

          {slotConflictNotice && (
            <div className="mt-4 px-4 py-3 rounded-xl bg-amber-50 border border-amber-200 text-amber-800 text-xs font-semibold flex items-center justify-between gap-2">
              <div className="flex items-center gap-2">
                <span className="material-symbols-outlined text-[18px] text-amber-600">swap_horiz</span>
                <span>{slotConflictNotice}</span>
              </div>
              <button
                type="button"
                onClick={() => setSlotConflictNotice(null)}
                className="text-amber-600 hover:text-amber-900 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">close</span>
              </button>
            </div>
          )}

          {/* 2D Matrix Visual Canvas */}
          <div className="w-full rounded-2xl overflow-auto bg-slate-900 min-h-[320px] p-5 flex items-center justify-center mt-5 select-none border border-slate-800">
            {isLoadingLayout ? (
              <div className="py-16 text-center text-xs text-slate-300 space-y-2">
                <span className="material-symbols-outlined text-[32px] text-blue-400 animate-spin">progress_activity</span>
                <p>Đang tải sơ đồ 2D của tầng từ hệ thống...</p>
              </div>
            ) : floorLayout && floorLayout.cells.length > 0 ? (
              <div
                className="grid gap-2 p-2"
                style={{
                  gridTemplateColumns: `repeat(${floorLayout.columns}, minmax(52px, 1fr))`,
                  gridTemplateRows: `repeat(${floorLayout.rows}, minmax(52px, 1fr))`,
                }}
              >
                {floorLayout.cells.map((cell, idx) => {
                  const posStyle: React.CSSProperties = {
                    gridColumn: `${cell.x + 1} / span ${cell.widthCells || 1}`,
                    gridRow: `${cell.y + 1} / span ${cell.heightCells || 1}`,
                  };

                  if (cell.type === 'road') {
                    return (
                      <div
                        key={`${cell.x}-${cell.y}-${idx}`}
                        style={posStyle}
                        className="min-w-12 min-h-12 rounded bg-slate-800/80 border border-dashed border-slate-700 flex items-center justify-center text-[10px] font-mono text-slate-500"
                      >
                        ══
                      </div>
                    );
                  }
                  if (cell.type === 'entry') {
                    return (
                      <div
                        key={`${cell.x}-${cell.y}-${idx}`}
                        style={posStyle}
                        className="min-w-12 min-h-12 rounded bg-blue-900/60 border border-blue-500/60 flex items-center justify-center text-[10px] font-bold text-blue-200 gap-0.5"
                      >
                        <span>VÀO</span>
                        <span className="material-symbols-outlined text-[12px]">east</span>
                      </div>
                    );
                  }
                  if (cell.type === 'exit') {
                    return (
                      <div
                        key={`${cell.x}-${cell.y}-${idx}`}
                        style={posStyle}
                        className="min-w-12 min-h-12 rounded bg-rose-900/60 border border-rose-500/60 flex items-center justify-center text-[10px] font-bold text-rose-200 gap-0.5"
                      >
                        <span className="material-symbols-outlined text-[12px]">east</span>
                        <span>RA</span>
                      </div>
                    );
                  }
                  if (cell.type === 'wall' || cell.type === 'pillar') {
                    return (
                      <div
                        key={`${cell.x}-${cell.y}-${idx}`}
                        style={posStyle}
                        className="min-w-12 min-h-12 rounded bg-slate-700/80 border border-slate-600 flex items-center justify-center text-[9px] text-slate-400 font-bold"
                      >
                        {cell.type === 'pillar' ? 'CỘT' : ''}
                      </div>
                    );
                  }
                  if (cell.type === 'slot' && cell.code) {
                    const isSelected = selectedSpotCode === cell.code;
                    const isAvailable = cell.state === 'Available';
                    const isEv = cell.slotType === 'EVCharging';
                    return (
                      <button
                        key={`${cell.code}-${idx}`}
                        type="button"
                        style={posStyle}
                        disabled={!isAvailable}
                        onClick={() => handleSpotSelect(cell.code!, cell.slotType)}
                        title={
                          isAvailable
                            ? `Chọn ô ${cell.code} (${cell.slotType || 'Standard'})`
                            : `Ô ${cell.code} (${cell.state || 'Không trống'})`
                        }
                        className={`min-w-12 min-h-12 rounded-lg flex flex-col items-center justify-center text-center transition-all border font-bold text-[10.5px] relative ${
                          isSelected
                            ? 'bg-blue-600 text-white border-blue-400 shadow-lg scale-105 ring-2 ring-blue-300 z-10 cursor-pointer'
                            : isAvailable
                            ? 'bg-emerald-500/15 hover:bg-emerald-500/30 text-emerald-200 border-emerald-500/60 cursor-pointer'
                            : 'bg-rose-500/25 text-rose-200 border-rose-500/75 cursor-not-allowed'
                        }`}
                      >
                        <span>{cell.code}</span>
                        {isEv && (
                          <span className="material-symbols-outlined text-[11px] text-amber-400 absolute top-0.5 right-0.5">
                            bolt
                          </span>
                        )}
                        {!isAvailable && (
                          <span className="material-symbols-outlined text-[12px] opacity-70">directions_car</span>
                        )}
                      </button>
                    );
                  }
                  return <div key={`${cell.x}-${cell.y}-${idx}`} style={posStyle} className="min-w-12 min-h-12" />;
                })}
              </div>
            ) : (
              <div className="py-12 text-center text-xs text-slate-400 space-y-1">
                <span className="material-symbols-outlined text-[32px] text-slate-500">grid_off</span>
                <p>Chủ bãi chưa xuất bản sơ đồ 2D cho tầng này.</p>
                <p className="text-[11px] text-slate-500">Bạn vẫn có thể đặt chỗ ở bên dưới (hệ thống tự động phân bổ ô trống).</p>
              </div>
            )}
          </div>

          {/* 6. Quick Selection Cards (Danh sách ô trống gợi ý từ API thật) */}
          <div className="mt-6 flex flex-col gap-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">
                Ô trống gợi ý trên tầng ({availableFloorSpots.length} ô khả dụng)
              </span>
              <span className="text-xs text-blue-600 flex items-center gap-1 font-semibold">
                <span className="material-symbols-outlined text-[15px]">touch_app</span> Chạm vào ô để chọn
              </span>
            </div>

            {availableFloorSpots.length === 0 ? (
              <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 text-xs text-slate-500">
                Hiện chưa có ô trống cụ thể hiển thị trên tầng này — hệ thống sẽ tự động phân bổ ô trống khi bạn xác nhận đặt chỗ.
              </div>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3.5">
                {availableFloorSpots.slice(0, 8).map((spot) => {
                  const isSelected = selectedSpotCode === spot.code;
                  const isEv = spot.slotType === 'EVCharging';
                  return (
                    <div
                      key={spot.code}
                      onClick={() => handleSpotSelect(spot.code, spot.slotType)}
                      className={`p-4 rounded-xl cursor-pointer transition-all duration-200 group shadow-xs flex flex-col justify-between ${
                        isSelected
                          ? 'border-2 border-blue-600 bg-blue-50/60 ring-2 ring-blue-100'
                          : 'border border-slate-200 bg-white hover:border-blue-400 hover:bg-slate-50'
                      }`}
                    >
                      <div>
                        <div className="flex items-center justify-between mb-2">
                          <span
                            className={`px-2.5 py-0.5 rounded font-bold text-sm ${
                              isSelected ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-800'
                            }`}
                          >
                            {spot.code}
                          </span>
                          {isEv ? (
                            <span className="text-emerald-700 font-semibold text-xs flex items-center gap-1">
                              <span className="material-symbols-outlined text-[14px]">bolt</span> Sạc EV
                            </span>
                          ) : (
                            <span className="text-slate-500 font-semibold text-xs flex items-center gap-1">
                              <span className="material-symbols-outlined text-[14px]">check_circle</span> Trống
                            </span>
                          )}
                        </div>
                        <p className="text-xs text-slate-500">
                          {selectedFloorLabel} • {spot.maxVehicleType || 'Tiêu chuẩn'}
                        </p>
                      </div>

                      <div
                        className={`mt-3 pt-2.5 border-t flex items-center justify-between text-xs font-bold ${
                          isSelected
                            ? 'border-blue-200 text-blue-600'
                            : 'border-slate-100 text-slate-500 group-hover:text-blue-600'
                        }`}
                      >
                        <span>{isSelected ? 'Vị trí đang chọn' : 'Chọn & Đặt chỗ'}</span>
                        <span className="material-symbols-outlined text-[16px] group-hover:translate-x-1 transition-transform">
                          south
                        </span>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          {/* Selected Spot Summary Banner */}
          <div className="mt-5 p-4 sm:p-5 rounded-xl bg-blue-50/80 flex flex-col lg:flex-row lg:items-center justify-between gap-4 border border-blue-200">
            <div className="flex items-start sm:items-center gap-3.5">
              <div className="w-12 h-12 rounded-xl bg-blue-600 text-white flex items-center justify-center shrink-0 shadow-sm">
                <span className="material-symbols-outlined text-[26px]">local_parking</span>
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <span className="text-base font-bold text-slate-900">
                    Vị trí {currentSpot.code} đã được chọn ({currentSpot.typeText})
                  </span>
                  <span className="px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-800 text-[10px] font-bold">
                    Tự động giữ chỗ
                  </span>
                </div>
                <p className="text-xs text-slate-600 mt-0.5">{currentSpot.desc}</p>
              </div>
            </div>

            <a
              href="#booking-section"
              className="flex items-center gap-2 text-blue-600 text-xs font-bold bg-white px-4 py-2.5 rounded-xl border border-blue-200 shadow-xs shrink-0 hover:bg-blue-600 hover:text-white transition-all"
            >
              <span>Điền thông tin đặt chỗ</span>
              <span className="material-symbols-outlined text-[18px]">arrow_downward</span>
            </a>
          </div>
        </section>

        {/* 7. Booking Form & Checkout Section */}
        <section className="scroll-mt-24 flex flex-col gap-6" id="booking-section">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-4 rounded-2xl bg-white border border-slate-200 shadow-xs">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center font-bold">
                <span className="material-symbols-outlined text-[22px]">assignment_turned_in</span>
              </div>
              <div>
                <h2 className="text-lg font-bold text-slate-900">
                  Hoàn tất thông tin đặt chỗ & Thanh toán cho vị trí đã chọn
                </h2>
                <p className="text-xs text-slate-500">
                  Vị trí đang chọn: <strong className="text-blue-600 font-bold">{currentSpot.code} ({selectedFloorLabel})</strong> • Hệ thống tự động mở barrier bằng biển số xe
                </p>
              </div>
            </div>

            <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-lg bg-slate-100 text-slate-800 text-xs self-start sm:self-auto font-medium">
              <span className="material-symbols-outlined text-[18px] text-emerald-600 animate-spin">history</span>
              <span>Giữ chỗ trong:</span>
              <span className="text-base font-extrabold text-blue-600">{formatCountdown(countdown)}</span>
            </div>
          </div>

          {/* 2-Column Responsive Layout */}
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
            {/* Left Column: Inputs & Add-on Services (8 Cols) */}
            <div className="lg:col-span-8 flex flex-col gap-6">
              {/* 7a. Biển số xe nhận diện ANPR */}
              <div className="rounded-2xl bg-white p-6 shadow-sm border border-slate-200">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="text-base font-bold text-slate-900 flex items-center gap-2">
                    <span className="material-symbols-outlined text-blue-600 text-[20px]">directions_car</span>
                    Biển số xe nhận diện ANPR
                  </h3>
                  <button
                    onClick={() => navigate('/driver/vehicles')}
                    className="text-blue-600 text-xs font-bold hover:underline flex items-center gap-1"
                    type="button"
                  >
                    <span className="material-symbols-outlined text-[16px]">sync_alt</span> Đổi xe khác
                  </button>
                </div>

                <div className="p-4 rounded-xl bg-slate-50 flex flex-col sm:flex-row sm:items-center justify-between gap-4 border border-slate-200">
                  <div className="flex items-center gap-4">
                    <div className="w-14 h-14 rounded-xl bg-white border border-slate-200 flex items-center justify-center text-blue-600 font-bold text-lg shadow-xs">
                      {vehicleModel.split(' ')[0] || 'EQE'}
                    </div>
                    <div>
                      <div className="flex items-center gap-2.5">
                        <span className="text-xl font-extrabold text-slate-900 tracking-wide">
                          {vehiclePlate}
                        </span>
                        <span className="px-2.5 py-0.5 rounded-full bg-emerald-50 text-emerald-700 text-[10px] font-bold flex items-center gap-1 border border-emerald-200">
                          <span className="material-symbols-outlined text-[12px]">verified</span> Đã xác thực ANPR
                        </span>
                      </div>
                      <p className="text-xs text-slate-500 mt-0.5">
                        {vehicleBrand} {vehicleModel} • Màu {vehicleColor} • Nhiên liệu: {vehicleFuel}
                      </p>
                    </div>
                  </div>

                  <span className="px-3 py-1.5 rounded-lg bg-slate-200 text-slate-700 text-[10px] uppercase tracking-wider font-bold self-start sm:self-auto border border-slate-300">
                    Xe mặc định
                  </span>
                </div>
              </div>

              {/* 7b. Thời gian gửi & lấy xe dự kiến */}
              <div className="rounded-2xl bg-white p-6 shadow-sm border border-slate-200">
                <h3 className="text-base font-bold text-slate-900 flex items-center gap-2 mb-4">
                  <span className="material-symbols-outlined text-blue-600 text-[20px]">schedule</span>
                  Thời gian gửi & lấy xe dự kiến
                </h3>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
                  <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 flex flex-col gap-1.5">
                    <label className="text-[11px] text-slate-500 font-bold uppercase tracking-wider">
                      Giờ vào dự kiến
                    </label>
                    <input
                      type="datetime-local"
                      value={startAtLocal}
                      onChange={(e) => setStartAtLocal(e.target.value)}
                      className="w-full px-3 py-2 rounded-lg bg-white text-slate-900 text-sm font-semibold border border-slate-300 focus:outline-hidden focus:border-blue-600"
                    />
                    <span className="text-xs text-emerald-700 flex items-center gap-1 font-medium">
                      <span className="material-symbols-outlined text-[14px]">sensors</span> Check-in tự động barrier
                    </span>
                  </div>

                  <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 flex flex-col gap-1.5">
                    <label className="text-[11px] text-slate-500 font-bold uppercase tracking-wider">
                      Giờ ra dự kiến
                    </label>
                    <input
                      type="datetime-local"
                      value={endAtLocal}
                      onChange={(e) => setEndAtLocal(e.target.value)}
                      className="w-full px-3 py-2 rounded-lg bg-white text-slate-900 text-sm font-semibold border border-slate-300 focus:outline-hidden focus:border-blue-600"
                    />
                    <span className="text-xs text-slate-500">Tổng thời lượng: {durationHours} giờ (Tính {durationHours} giờ dịch vụ)</span>
                  </div>
                </div>

                <div className="flex items-center gap-2 px-3.5 py-2.5 rounded-lg bg-blue-50/60 text-slate-700 text-xs border border-blue-100">
                  <span className="material-symbols-outlined text-[18px] text-blue-600">info</span>
                  <span>Hệ thống tự động bảo lưu vị trí thêm 15 phút sau giờ vào dự kiến mà không thu thêm phí phụ thu.</span>
                </div>
              </div>

              {/* 7c. Dịch vụ tiện ích cộng thêm */}
              <div className="rounded-2xl bg-white p-6 shadow-sm border border-slate-200">
                <h3 className="text-base font-bold text-slate-900 flex items-center gap-2 mb-4">
                  <span className="material-symbols-outlined text-blue-600 text-[20px]">tune</span>
                  Dịch vụ tiện ích cộng thêm
                </h3>

                <div className="flex flex-col gap-3">
                  {/* Service 1: EV Fast-charge */}
                  <label className="flex items-center justify-between p-4 rounded-xl bg-slate-50 hover:bg-slate-100 cursor-pointer transition-colors border border-slate-200">
                    <div className="flex items-center gap-3.5">
                      <input
                        type="checkbox"
                        checked={hasEvService}
                        onChange={(e) => setHasEvService(e.target.checked)}
                        className="w-5 h-5 rounded text-blue-600 focus:ring-blue-500 border-slate-300 cursor-pointer"
                      />
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="text-sm font-bold text-slate-900">
                            Sạc pin xe điện Fast-charge DC 120kW (Trụ {currentSpot.code})
                          </span>
                          <span className="px-2 py-0.5 rounded bg-emerald-100 text-emerald-800 text-[10px] font-bold">
                            Tự ngắt an toàn
                          </span>
                        </div>
                        <p className="text-xs text-slate-500">Công suất tối đa 120kW, sạc từ 20% lên 80% chỉ trong 35 phút</p>
                      </div>
                    </div>
                    <span className="text-sm font-bold text-blue-600 whitespace-nowrap">+50.000 đ</span>
                  </label>

                  {/* Service 2: Car wash */}
                  <label className="flex items-center justify-between p-4 rounded-xl bg-slate-50 hover:bg-slate-100 cursor-pointer transition-colors border border-slate-200">
                    <div className="flex items-center gap-3.5">
                      <input
                        type="checkbox"
                        checked={hasWashService}
                        onChange={(e) => setHasWashService(e.target.checked)}
                        className="w-5 h-5 rounded text-blue-600 focus:ring-blue-500 border-slate-300 cursor-pointer"
                      />
                      <div>
                        <span className="text-sm font-bold text-slate-900">Rửa xe bọt tuyết & Hút bụi nội thất VIP</span>
                        <p className="text-xs text-slate-500">Kỹ thuật viên phục vụ tận ô đỗ xe {currentSpot.code} trước giờ hẹn</p>
                      </div>
                    </div>
                    <span className="text-sm font-bold text-slate-900 whitespace-nowrap">+80.000 đ</span>
                  </label>

                  {/* Service 3: Insurance */}
                  <label className="flex items-center justify-between p-4 rounded-xl bg-slate-50 border border-slate-200">
                    <div className="flex items-center gap-3.5">
                      <input
                        type="checkbox"
                        checked={hasInsurance}
                        readOnly
                        className="w-5 h-5 rounded text-blue-600 focus:ring-blue-500 border-slate-300 cursor-pointer"
                      />
                      <div>
                        <span className="text-sm font-bold text-slate-900">
                          Bảo hiểm trầy xước & rủi ro bãi đỗ ParkMaster Care
                        </span>
                        <p className="text-xs text-slate-500">Đền bù 100% rủi ro trầy xước với hệ thống AI giám sát 360° liên tục</p>
                      </div>
                    </div>
                    <span className="text-sm font-bold text-emerald-700 whitespace-nowrap">0 đ (VIP Miễn phí)</span>
                  </label>
                </div>
              </div>

              {/* 7d. Ghi chú điều phối */}
              <div className="rounded-2xl bg-white p-6 shadow-sm border border-slate-200">
                <label className="text-base font-bold text-slate-900 flex items-center gap-2 mb-3">
                  <span className="material-symbols-outlined text-blue-600 text-[20px]">sticky_note_2</span>
                  Ghi chú cho bộ phận điều phối (Tùy chọn)
                </label>
                <textarea
                  value={bookingNote}
                  onChange={(e) => setBookingNote(e.target.value)}
                  placeholder="Ví dụ: Xe gầm thấp, vui lòng hỗ trợ góc đánh lái rộng khi vào vị trí..."
                  rows={3}
                  className="w-full px-4 py-3 rounded-xl bg-slate-50 text-slate-900 placeholder:text-slate-400 border border-slate-200 focus:outline-hidden focus:border-blue-600 focus:bg-white transition-colors text-sm resize-none"
                />
              </div>

              {/* 7d. Đánh giá từ tài xế (GET /api/v1/reviews?parkingLotId=...) */}
              <div className="rounded-2xl bg-white p-6 shadow-sm border border-slate-200">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="text-base font-bold text-slate-900 flex items-center gap-2">
                    <span className="material-symbols-outlined text-amber-500 text-[20px]">star</span>
                    Đánh giá từ tài xế
                  </h3>
                  {lotReviews && lotReviews.reviewCount > 0 && (
                    <span className="px-2.5 py-1 rounded-full bg-amber-50 text-amber-800 text-xs font-bold border border-amber-200">
                      {lotReviews.averageRating.toFixed(1)} ★ ({lotReviews.reviewCount} lượt)
                    </span>
                  )}
                </div>
                {!lotReviews || lotReviews.latest.length === 0 ? (
                  <p className="text-xs text-slate-500">Chưa có đánh giá nào cho bãi đỗ này.</p>
                ) : (
                  <div className="space-y-3">
                    {lotReviews.latest.map((rev) => (
                      <div key={rev.id} className="p-3.5 rounded-xl bg-slate-50 border border-slate-200 text-xs space-y-1">
                        <div className="flex items-center justify-between">
                          <span className="font-bold text-slate-800">{rev.reviewerName}</span>
                          <span className="text-amber-500 font-bold">{'★'.repeat(rev.rating)}</span>
                        </div>
                        {rev.comment && <p className="text-slate-600">{rev.comment}</p>}
                        {rev.ownerReply && (
                          <div className="mt-1.5 pl-3 border-l-2 border-blue-400 text-[11px] text-slate-600">
                            <strong className="text-blue-700">Phản hồi từ bãi:</strong> {rev.ownerReply}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* Right Column: Sticky Summary & Checkout Card (4 Cols) */}
            <div className="lg:col-span-4 sticky top-24 flex flex-col gap-6">
              <div className="rounded-2xl bg-white p-6 shadow-xl border border-slate-200 flex flex-col gap-5">
                <div className="flex items-center justify-between">
                  <h3 className="text-lg font-extrabold text-slate-900">Tóm tắt chi phí đặt chỗ</h3>
                  <span className="px-2.5 py-1 rounded bg-emerald-50 text-emerald-700 text-[10px] font-bold uppercase tracking-wider flex items-center gap-1 border border-emerald-200">
                    <span className="material-symbols-outlined text-[13px]">shield</span> Giữ chỗ 100%
                  </span>
                </div>

                {/* Spot snapshot */}
                <div className="p-3.5 rounded-xl bg-slate-50 flex items-center justify-between border border-slate-200">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-lg bg-blue-600 text-white flex items-center justify-center font-bold text-sm shadow-xs">
                      {currentSpot.code}
                    </div>
                    <div>
                      <span className="text-xs font-bold text-slate-900">
                        Vị trí {currentSpot.code} • {selectedFloorLabel}
                      </span>
                      <p className="text-[11px] text-slate-500">{lot?.name || 'Landmark 81 Garage'}</p>
                    </div>
                  </div>
                  <span className="px-2 py-0.5 rounded bg-emerald-100 text-emerald-800 text-[10px] font-bold">
                    Khả dụng
                  </span>
                </div>

                {/* Line item breakdown */}
                <div className="flex flex-col gap-2.5 text-xs pt-2">
                  <div className="flex items-center justify-between text-slate-500">
                    <span>Đơn giá gửi</span>
                    <span className="text-slate-900 font-semibold">{hourlyRate.toLocaleString('vi-VN')} đ/h</span>
                  </div>
                  <div className="flex items-center justify-between text-slate-500">
                    <span>Tạm tính 4 giờ gửi xe</span>
                    <span className="text-slate-900 font-semibold">{parkingFee.toLocaleString('vi-VN')} đ</span>
                  </div>
                  <div className="flex items-center justify-between text-slate-500">
                    <span>Phí công nghệ ANPR & IoT</span>
                    <span className="text-slate-900 font-semibold">{techFee.toLocaleString('vi-VN')} đ</span>
                  </div>
                  <div className="flex items-center justify-between text-emerald-700 font-medium">
                    <span className="flex items-center gap-1">
                      <span className="material-symbols-outlined text-[15px]">stars</span>
                      Chiết khấu VIP (-20%)
                    </span>
                    <span className="font-bold">-{vipDiscount.toLocaleString('vi-VN')} đ</span>
                  </div>
                  {hasEvService && (
                    <div className="flex items-center justify-between text-slate-500">
                      <span>Dịch vụ sạc xe EV Fast (120kW)</span>
                      <span className="text-slate-900 font-semibold">+{evFee.toLocaleString('vi-VN')} đ</span>
                    </div>
                  )}
                  {hasWashService && (
                    <div className="flex items-center justify-between text-slate-500">
                      <span>Rửa xe bọt tuyết & Hút bụi</span>
                      <span className="text-slate-900 font-semibold">+{washFee.toLocaleString('vi-VN')} đ</span>
                    </div>
                  )}
                </div>

                <div className="h-px w-full bg-slate-200 my-1"></div>

                {/* Promotion Code */}
                <div className="flex flex-col gap-1.5">
                  <span className="text-[11px] uppercase font-bold text-slate-400">Mã khuyến mãi</span>
                  <div className="flex gap-2">
                    <input
                      type="text"
                      value={promoCodeInput}
                      onChange={(e) => setPromoCodeInput(e.target.value.toUpperCase())}
                      disabled={Boolean(appliedPromo)}
                      placeholder="Nhập mã giảm giá..."
                      className="flex-1 px-3 py-2 rounded-xl border border-slate-200 text-xs font-semibold text-slate-800 bg-slate-50 focus:outline-hidden focus:ring-2 focus:ring-blue-600/20"
                    />
                    {appliedPromo ? (
                      <button
                        type="button"
                        onClick={handleClearPromo}
                        className="px-3 py-2 rounded-xl bg-slate-200 text-slate-700 text-xs font-bold hover:bg-slate-300 cursor-pointer"
                      >
                        Gỡ
                      </button>
                    ) : (
                      <button
                        type="button"
                        disabled={promoBusy}
                        onClick={handleApplyPromo}
                        className="px-3.5 py-2 rounded-xl bg-blue-600 text-white text-xs font-bold hover:bg-blue-700 disabled:opacity-50 cursor-pointer"
                      >
                        {promoBusy ? '…' : 'Áp dụng'}
                      </button>
                    )}
                  </div>
                  {appliedPromo && (
                    <div className="flex items-center justify-between text-xs text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-xl px-3 py-1.5 font-semibold">
                      <span>{appliedPromo.name} ({appliedPromo.code})</span>
                      <span>-{appliedPromo.discountAmount.toLocaleString('vi-VN')} đ</span>
                    </div>
                  )}
                  {promoError && (
                    <span className="text-[11px] text-rose-600 font-medium">{promoError}</span>
                  )}
                </div>

                {/* Total */}
                <div className="flex items-baseline justify-between">
                  <span className="text-sm font-bold text-slate-900">TỔNG THANH TOÁN:</span>
                  <span className="text-2xl font-extrabold text-rose-600 tracking-tight">
                    {totalAmount.toLocaleString('vi-VN')} đ
                  </span>
                </div>

                {/* Payment Methods */}
                <div className="flex flex-col gap-2 pt-1">
                  <span className="text-[11px] uppercase font-bold text-slate-400">Phương thức thanh toán</span>

                  <label
                    onClick={() => setPaymentMethod('gate')}
                    className={`flex items-center justify-between p-3 rounded-xl cursor-pointer transition-colors border ${
                      paymentMethod === 'gate' ? 'bg-blue-50 border-blue-600 ring-1 ring-blue-600' : 'bg-slate-50 border-slate-200'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <input
                        type="radio"
                        name="paymethod"
                        checked={paymentMethod === 'gate'}
                        onChange={() => setPaymentMethod('gate')}
                        className="w-4 h-4 text-blue-600 focus:ring-blue-500"
                      />
                      <span className="text-xs font-bold text-slate-900">Trả tại cổng khi ra bãi (Pay at Gate)</span>
                    </div>
                    <span className="material-symbols-outlined text-emerald-600 text-[18px]">local_atm</span>
                  </label>

                  <label
                    onClick={() => setPaymentMethod('vnpay')}
                    className={`flex items-center justify-between p-3 rounded-xl cursor-pointer transition-colors border ${
                      paymentMethod === 'vnpay' ? 'bg-blue-50 border-blue-600 ring-1 ring-blue-600' : 'bg-slate-50 border-slate-200'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <input
                        type="radio"
                        name="paymethod"
                        checked={paymentMethod === 'vnpay'}
                        onChange={() => setPaymentMethod('vnpay')}
                        className="w-4 h-4 text-blue-600 focus:ring-blue-500"
                      />
                      <span className="text-xs font-medium text-slate-900">Cổng thanh toán VNPAY-QR</span>
                    </div>
                    <span className="material-symbols-outlined text-blue-600 text-[18px]">payments</span>
                  </label>

                  <label
                    onClick={() => setPaymentMethod('vietqr')}
                    className={`flex items-center justify-between p-3 rounded-xl cursor-pointer transition-colors border ${
                      paymentMethod === 'vietqr' ? 'bg-blue-50 border-blue-600 ring-1 ring-blue-600' : 'bg-slate-50 border-slate-200'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <input
                        type="radio"
                        name="paymethod"
                        checked={paymentMethod === 'vietqr'}
                        onChange={() => setPaymentMethod('vietqr')}
                        className="w-4 h-4 text-blue-600 focus:ring-blue-500"
                      />
                      <span className="text-xs font-medium text-slate-900">Chuyển khoản nhanh VietQR</span>
                    </div>
                    <span className="material-symbols-outlined text-slate-500 text-[18px]">qr_code_2</span>
                  </label>
                </div>

                {/* CTA Button (AC3: Disable khi bãi không khả dụng / đang xử lý) */}
                <button
                  type="button"
                  disabled={bookingBusy || lot?.canBook === false}
                  onClick={handleConfirmBooking}
                  className={`w-full py-4 rounded-xl font-bold text-sm transition-all shadow-lg flex items-center justify-center gap-2 mt-2 ${
                    lot?.canBook === false
                      ? 'bg-slate-300 text-slate-500 cursor-not-allowed shadow-none'
                      : bookingBusy
                        ? 'bg-rose-400 text-white cursor-wait shadow-none'
                        : 'bg-rose-600 text-white hover:bg-rose-700 shadow-rose-500/20 cursor-pointer'
                  }`}
                >
                  <span className="material-symbols-outlined text-[20px]">{lot?.canBook === false ? 'block' : bookingBusy ? 'hourglass_top' : 'lock'}</span>
                  <span>{lot?.canBook === false ? 'Tạm ngừng nhận đặt chỗ' : bookingBusy ? 'Đang tạo booking...' : `Xác nhận & Thanh toán (${totalAmount.toLocaleString('vi-VN')}đ)`}</span>
                </button>

                {bookingError && (
                  <div className="w-full flex items-start gap-2 px-3.5 py-2.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs font-semibold">
                    <span className="material-symbols-outlined text-[18px]">error</span>
                    <span>{bookingError}</span>
                  </div>
                )}

                <div className="flex items-center justify-center gap-2 text-center text-slate-500 text-[11px]">
                  <span className="material-symbols-outlined text-[15px] text-emerald-600">verified_user</span>
                  <span>Miễn phí hủy trước 30 phút • Hoàn tiền 100% trong 30 giây</span>
                </div>
              </div>
            </div>
          </div>
        </section>
      </div>

      {/* Booking Success Modal */}
      {showSuccessModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-2xl shadow-2xl p-6 sm:p-7 w-full max-w-md border border-slate-200 flex flex-col items-center text-center">
            <div className="w-16 h-16 rounded-2xl bg-emerald-100 text-emerald-600 flex items-center justify-center mb-4">
              <span className="material-symbols-outlined text-[36px]">check_circle</span>
            </div>
            <h3 className="text-xl font-bold text-slate-900">Đặt chỗ thành công!</h3>
            <p className="text-xs text-slate-500 mt-1 mb-5">
              Hệ thống ANPR đã kích hoạt nhận diện cho biển số xe <strong className="text-slate-800">{vehiclePlate}</strong>
            </p>

            {/* Ticket Card Preview */}
            <div className="w-full p-4 rounded-xl bg-slate-50 border border-slate-200 text-left flex flex-col gap-2.5 mb-5 text-xs">
              <div className="flex justify-between border-b pb-2">
                <span className="text-slate-500">Mã vé:</span>
                <span className="font-bold text-blue-600">{bookingResult?.code || '—'}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Bãi đỗ:</span>
                <span className="font-semibold text-slate-800">{bookingResult?.parkingLotName || lot?.name || 'Landmark 81 Garage'}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Vị trí đỗ:</span>
                <span className="font-bold text-emerald-600">Ô {bookingResult?.slotCode || currentSpot.code} ({selectedFloorLabel})</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Thời gian:</span>
                <span className="font-semibold text-slate-800">{startAtLocal.replace('T', ' ')} - {endAtLocal.replace('T', ' ')}</span>
              </div>
              <div className="flex justify-between border-t pt-2">
                <span className="text-slate-500">Tổng thanh toán:</span>
                <span className="font-extrabold text-blue-600">{totalAmount.toLocaleString('vi-VN')} đ</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Giữ chỗ đến:</span>
                <span className="font-semibold text-slate-800">
                  {bookingResult?.holdExpiresAtUtc ? new Date(bookingResult.holdExpiresAtUtc).toLocaleString('vi-VN') : '—'}
                </span>
              </div>
            </div>

            {/* Trạng thái gửi thông báo (NotificationService – TV6) */}
            {notifySent ? (
              <div className="w-full flex items-center gap-2 px-3.5 py-2.5 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-700 text-xs font-semibold mb-4">
                <span className="material-symbols-outlined text-[18px]">notifications_active</span>
                <span>Thông báo "Đặt chỗ thành công" đã được gửi – bấm chuông trên Navbar để xem.</span>
              </div>
            ) : (
              <div className="w-full flex items-center gap-2 px-3.5 py-2.5 rounded-xl bg-amber-50 border border-amber-200 text-amber-700 text-xs font-semibold mb-4">
                <span className="material-symbols-outlined text-[18px]">notifications_off</span>
                <span>Không gửi được thông báo – kiểm tra NotificationService :5106 / Gateway :5000.</span>
              </div>
            )}

            {vietQrData && (
              <div className="w-full p-3.5 rounded-xl bg-blue-50 border border-blue-200 mb-4 flex flex-col items-center gap-2 text-xs">
                <span className="font-bold text-blue-900">Quét mã VietQR để hoàn tất thanh toán</span>
                <img src={vietQrData.qrImageUrl} alt="VietQR" className="w-40 h-40 rounded-lg border border-white shadow-xs bg-white" />
                <div className="w-full text-left space-y-0.5 text-[11px] text-slate-700">
                  <div>Ngân hàng: <strong>{vietQrData.bankAccountName}</strong> ({vietQrData.bankAccountNumber})</div>
                  <div>Nội dung CK: <strong className="font-mono text-blue-700">{vietQrData.transferContent}</strong></div>
                  <div>Số tiền: <strong className="text-blue-700">{vietQrData.amount.toLocaleString('vi-VN')} đ</strong></div>
                </div>
              </div>
            )}

            {paymentInitError && (
              <div className="w-full px-3.5 py-2.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs font-semibold mb-4 text-left">
                {paymentInitError}
              </div>
            )}

            <div className="flex flex-col sm:flex-row gap-2.5 w-full">
              <button
                type="button"
                onClick={() =>
                  navigate(
                    `/driver/payment-result?code=${encodeURIComponent(
                      bookingResult?.code || ''
                    )}`
                  )
                }
                className="flex-1 py-3 rounded-xl bg-blue-600 text-white font-bold text-xs hover:bg-blue-700 transition-colors shadow-sm flex items-center justify-center gap-1.5 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[18px]">receipt_long</span>
                <span>Xem Biên nhận & Hóa đơn</span>
              </button>
              <button
                type="button"
                onClick={() => navigate('/driver/history')}
                className="px-4 py-3 rounded-xl bg-slate-100 text-slate-700 font-bold text-xs hover:bg-slate-200 transition-colors cursor-pointer"
              >
                Lịch sử đặt
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
