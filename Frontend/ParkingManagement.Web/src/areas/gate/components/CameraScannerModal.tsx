import React, { useEffect, useRef, useState } from 'react';

// Vùng khung hình gửi cho OCR (tỉ lệ % so với khung hình camera) – chỉ đọc đúng vùng biển số,
// bỏ qua nền/vật thể khác xung quanh để tránh OCR đọc lẫn chữ ngoài biển số.
const PLATE_CROP = { leftPct: 0.08, topPct: 0.28, widthPct: 0.84, heightPct: 0.44 };
// Biển số VN (TT 01/2021/TT-BCA) không dùng I, O, Q, R, W, J để tránh nhầm với số – giới hạn
// bộ ký tự OCR được phép đọc giúp giảm hẳn nhiễu/ký tự rác ngoài biển số.
const PLATE_CHAR_WHITELIST = '0123456789ABCDEFGHKLMNPSTUVXYZ.-';

interface Rectangle {
  left: number;
  top: number;
  width: number;
  height: number;
}

interface PlateOcrResult {
  raw: string;
  plausible: boolean;
}

/** Chạy OCR biển số trên 1 canvas (ảnh từ camera hoặc ảnh tải lên), dùng chung whitelist + PSM. */
const recognizePlateFromCanvas = async (canvas: HTMLCanvasElement, rectangle?: Rectangle): Promise<PlateOcrResult> => {
  const Tesseract = await import('tesseract.js');
  const worker = await Tesseract.createWorker('eng');
  try {
    // PSM 6 (SINGLE_BLOCK): chịu được cả biển 1 dòng (ô tô) và 2 dòng (xe máy).
    await worker.setParameters({
      tessedit_pageseg_mode: Tesseract.PSM.SINGLE_BLOCK,
      tessedit_char_whitelist: PLATE_CHAR_WHITELIST,
    });
    const result = await worker.recognize(canvas, rectangle ? { rectangle } : {});
    const raw = result.data.text.replace(/[^A-Z0-9.-]/g, '').toUpperCase();
    const plausible = raw.length >= 6 && raw.length <= 11 && /\d/.test(raw) && /[A-Z]/.test(raw);
    return { raw, plausible };
  } finally {
    await worker.terminate();
  }
};

/** Đọc 1 file ảnh lên <canvas> (giữ nguyên kích thước gốc) để dùng chung cho jsQR/Tesseract. */
const loadImageFileToCanvas = (file: File): Promise<HTMLCanvasElement> =>
  new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      const canvas = document.createElement('canvas');
      canvas.width = img.naturalWidth;
      canvas.height = img.naturalHeight;
      const ctx = canvas.getContext('2d');
      URL.revokeObjectURL(url);
      if (!ctx) {
        reject(new Error('canvas 2d context không khả dụng'));
        return;
      }
      ctx.drawImage(img, 0, 0);
      resolve(canvas);
    };
    img.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error('Không đọc được file ảnh.'));
    };
    img.src = url;
  });

interface CameraScannerModalProps {
  /** 'qr' quét liên tục mã QR (vé đặt chỗ); 'plate' chụp 1 khung hình rồi nhận diện biển số bằng OCR. */
  mode: 'qr' | 'plate';
  title: string;
  onDetected: (value: string) => void;
  onClose: () => void;
}

/**
 * Modal camera dùng chung cho quét QR (jsQR, liên tục) và quét biển số (chụp ảnh + OCR tesseract.js,
 * chỉ gợi ý – nhân viên luôn xem lại/sửa trước khi xác nhận vì GateService chưa hỗ trợ OCR phía server).
 * Ngoài camera còn hỗ trợ tải ảnh có sẵn lên (ảnh chụp sẵn, ảnh từ Zalo/máy tính...) cho cả 2 chế độ.
 */
export const CameraScannerModal: React.FC<CameraScannerModalProps> = ({ mode, title, onDetected, onClose }) => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const rafRef = useRef<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [cameraReady, setCameraReady] = useState(false);

  useEffect(() => {
    let stopped = false;

    const start = async () => {
      try {
        const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
        if (stopped) {
          stream.getTracks().forEach((t) => t.stop());
          return;
        }
        streamRef.current = stream;
        if (videoRef.current) {
          videoRef.current.srcObject = stream;
          await videoRef.current.play();
        }
        setCameraReady(true);
        if (mode === 'qr') scanQrLoop();
      } catch {
        setError('Không truy cập được camera. Bạn có thể tải ảnh lên hoặc nhập tay thay thế.');
      }
    };

    const scanQrLoop = async () => {
      const jsQR = (await import('jsqr')).default;
      const tick = () => {
        const video = videoRef.current;
        const canvas = canvasRef.current;
        if (video && canvas && video.readyState === video.HAVE_ENOUGH_DATA) {
          canvas.width = video.videoWidth;
          canvas.height = video.videoHeight;
          const ctx = canvas.getContext('2d');
          if (ctx) {
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
            const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            const code = jsQR(imageData.data, imageData.width, imageData.height);
            if (code?.data) {
              onDetected(code.data);
              return;
            }
          }
        }
        rafRef.current = requestAnimationFrame(tick);
      };
      rafRef.current = requestAnimationFrame(tick);
    };

    void start();

    return () => {
      stopped = true;
      if (rafRef.current) cancelAnimationFrame(rafRef.current);
      streamRef.current?.getTracks().forEach((t) => t.stop());
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode]);

  const capturePlate = async () => {
    const video = videoRef.current;
    const canvas = canvasRef.current;
    if (!video || !canvas) return;
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

    setBusy(true);
    setError(null);
    try {
      const rectangle: Rectangle = {
        left: Math.round(canvas.width * PLATE_CROP.leftPct),
        top: Math.round(canvas.height * PLATE_CROP.topPct),
        width: Math.round(canvas.width * PLATE_CROP.widthPct),
        height: Math.round(canvas.height * PLATE_CROP.heightPct),
      };
      const { raw, plausible } = await recognizePlateFromCanvas(canvas, rectangle);
      if (!plausible) {
        setError(
          raw
            ? `Không đọc rõ biển số (nhận được "${raw}"). Vui lòng đưa camera gần/thẳng biển số hơn rồi chụp lại, hoặc nhập tay.`
            : 'Không đọc được biển số từ ảnh. Vui lòng chụp lại hoặc nhập tay.'
        );
        return;
      }
      onDetected(raw);
    } catch {
      setError('Nhận diện biển số thất bại. Vui lòng chụp lại hoặc nhập tay.');
    } finally {
      setBusy(false);
    }
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;

    setBusy(true);
    setError(null);
    try {
      const canvas = await loadImageFileToCanvas(file);
      if (mode === 'qr') {
        const jsQR = (await import('jsqr')).default;
        const ctx = canvas.getContext('2d');
        if (!ctx) throw new Error('canvas 2d context không khả dụng');
        const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
        const code = jsQR(imageData.data, imageData.width, imageData.height);
        if (!code?.data) {
          setError('Không tìm thấy mã QR trong ảnh đã chọn. Vui lòng chọn ảnh khác hoặc quét trực tiếp bằng camera.');
          return;
        }
        onDetected(code.data);
      } else {
        const { raw, plausible } = await recognizePlateFromCanvas(canvas);
        if (!plausible) {
          setError(
            raw
              ? `Không đọc rõ biển số trong ảnh (nhận được "${raw}"). Vui lòng chọn ảnh rõ hơn hoặc nhập tay.`
              : 'Không đọc được biển số từ ảnh đã chọn. Vui lòng chọn ảnh khác hoặc nhập tay.'
          );
          return;
        }
        onDetected(raw);
      }
    } catch {
      setError('Xử lý ảnh thất bại. Vui lòng thử lại hoặc nhập tay.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/70 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md p-5 space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="font-bold text-slate-900 text-sm flex items-center gap-2">
            <span className="material-symbols-outlined text-blue-600 text-[20px]">
              {mode === 'qr' ? 'qr_code_scanner' : 'directions_car'}
            </span>
            {title}
          </h3>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-700 cursor-pointer">
            <span className="material-symbols-outlined">close</span>
          </button>
        </div>

        <div className="relative rounded-xl overflow-hidden bg-slate-900 aspect-video">
          <video ref={videoRef} className="w-full h-full object-cover" muted playsInline />
          {mode === 'qr' && (
            <div className="absolute inset-8 border-2 border-emerald-400/80 rounded-lg pointer-events-none" />
          )}
          {mode === 'plate' && (
            <div
              className="absolute border-2 border-dashed border-amber-400/90 rounded-lg pointer-events-none"
              style={{
                left: `${PLATE_CROP.leftPct * 100}%`,
                top: `${PLATE_CROP.topPct * 100}%`,
                width: `${PLATE_CROP.widthPct * 100}%`,
                height: `${PLATE_CROP.heightPct * 100}%`,
              }}
            />
          )}
          {busy && (
            <div className="absolute inset-0 bg-black/60 flex items-center justify-center text-white text-xs font-semibold gap-2">
              <span className="material-symbols-outlined animate-spin">progress_activity</span>
              {mode === 'qr' ? 'Đang xử lý ảnh…' : 'Đang nhận diện biển số…'}
            </div>
          )}
        </div>
        <canvas ref={canvasRef} className="hidden" />

        {error && (
          <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">{error}</p>
        )}

        {mode === 'qr' ? (
          <p className="text-xs text-slate-500 text-center">
            {cameraReady ? 'Đưa mã QR vé đặt chỗ vào khung hình để quét tự động.' : 'Đang mở camera…'}
          </p>
        ) : (
          <>
            <p className="text-xs text-slate-500 text-center">
              Căn biển số lấp đầy khung nét đứt, chụp thẳng góc và đủ sáng để nhận diện chính xác hơn.
            </p>
            <button
              onClick={capturePlate}
              disabled={busy || !cameraReady}
              className="w-full px-4 py-2.5 bg-blue-600 text-white rounded-xl text-xs font-bold hover:bg-blue-700 disabled:opacity-50 shadow-xs transition-colors flex items-center justify-center gap-1.5 cursor-pointer"
            >
              <span className="material-symbols-outlined text-[18px]">photo_camera</span>
              CHỤP & NHẬN DIỆN BIỂN SỐ
            </button>
          </>
        )}

        <div className="flex items-center gap-2">
          <div className="flex-1 h-px bg-slate-200" />
          <span className="text-[10px] font-bold text-slate-400 uppercase">Hoặc</span>
          <div className="flex-1 h-px bg-slate-200" />
        </div>

        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          className="hidden"
          onChange={(e) => void handleFileUpload(e)}
        />
        <button
          onClick={() => fileInputRef.current?.click()}
          disabled={busy}
          className="w-full px-4 py-2.5 bg-slate-100 text-slate-700 rounded-xl text-xs font-bold hover:bg-slate-200 disabled:opacity-50 transition-colors flex items-center justify-center gap-1.5 cursor-pointer"
        >
          <span className="material-symbols-outlined text-[18px]">upload</span>
          {mode === 'qr' ? 'TẢI ẢNH MÃ QR LÊN' : 'TẢI ẢNH BIỂN SỐ LÊN'}
        </button>
      </div>
    </div>
  );
};
