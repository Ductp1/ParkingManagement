import React, { useEffect, useRef, useState } from 'react';
import { gateApi } from '@/api/gateApi';

// Plate Recognizer giới hạn ảnh 3MB – thu nhỏ cạnh dài về 1600px là đủ rõ biển số mà vẫn nhẹ.
const MAX_UPLOAD_SIDE = 1600;
// Mỗi khung hình gửi đi tốn 1 lượt Plate Recognizer (free: 1 lượt/giây, 2.500 lượt/tháng)
// → quét tự động ~1,5s/lần và tạm dừng sau ~30s để không đốt hết quota khi camera bị bỏ bật.
const AUTO_SCAN_INTERVAL_MS = 1500;
const MAX_AUTO_SCAN_ATTEMPTS = 20;

/** Đọc 1 file ảnh lên <canvas> (giữ nguyên kích thước gốc) để dùng chung cho jsQR và quét biển số. */
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

const canvasToJpeg = (source: HTMLCanvasElement): Promise<Blob> => {
  const scale = Math.min(1, MAX_UPLOAD_SIDE / Math.max(source.width, source.height));
  let canvas = source;
  if (scale < 1) {
    canvas = document.createElement('canvas');
    canvas.width = Math.round(source.width * scale);
    canvas.height = Math.round(source.height * scale);
    canvas.getContext('2d')?.drawImage(source, 0, 0, canvas.width, canvas.height);
  }
  return new Promise((resolve, reject) =>
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('Không nén được ảnh.'))), 'image/jpeg', 0.9)
  );
};

/** Khung camera của làn khi chưa bật quét. */
export const LaneCameraIdle: React.FC<{ hint: string }> = ({ hint }) => (
  <div className="h-48 bg-slate-900 rounded-xl flex flex-col items-center justify-center text-slate-400 border border-slate-800 text-center px-4">
    <span className="material-symbols-outlined text-[48px] mb-2 text-slate-500">videocam_off</span>
    <span className="text-xs font-medium">{hint}</span>
  </div>
);

interface LaneCameraScannerProps {
  /** 'qr' quét liên tục mã QR (vé đặt chỗ, xử lý tại trình duyệt); 'plate' tự động gửi khung hình cho GateService nhận diện biển số. */
  mode: 'qr' | 'plate';
  onDetected: (value: string) => void;
  onClose: () => void;
}

/**
 * Camera quét ngay trong khung camera của làn (không mở popup). Hỗ trợ cả tải ảnh có sẵn lên.
 * Kết quả chỉ điền sẵn vào ô nhập – staff vẫn xem lại trước khi mở barie.
 */
export const LaneCameraScanner: React.FC<LaneCameraScannerProps> = ({ mode, onDetected, onClose }) => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const rafRef = useRef<number | null>(null);
  const lastFrameUrlRef = useRef<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [suggestion, setSuggestion] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [cameraReady, setCameraReady] = useState(false);
  const [autoScan, setAutoScan] = useState(mode === 'plate');
  const [scanStatus, setScanStatus] = useState<string | null>(null);
  const [lastFrameUrl, setLastFrameUrl] = useState<string | null>(null);

  const showLastFrame = (blob: Blob) => {
    if (lastFrameUrlRef.current) URL.revokeObjectURL(lastFrameUrlRef.current);
    lastFrameUrlRef.current = URL.createObjectURL(blob);
    setLastFrameUrl(lastFrameUrlRef.current);
  };

  useEffect(() => () => {
    if (lastFrameUrlRef.current) URL.revokeObjectURL(lastFrameUrlRef.current);
  }, []);

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
        if (video && canvas && video.readyState >= video.HAVE_CURRENT_DATA && video.videoWidth > 0) {
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

  const recognizePlate = async (canvas: HTMLCanvasElement) => {
    setBusy(true);
    setError(null);
    setSuggestion(null);
    try {
      const frame = await canvasToJpeg(canvas);
      showLastFrame(frame);
      const result = await gateApi.scanPlate(frame);
      if (result.isValidFormat) {
        onDetected(result.plateDisplay);
        return;
      }
      setSuggestion(result.plateDisplay);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  useEffect(() => {
    if (mode !== 'plate' || !cameraReady || !autoScan) return;
    let cancelled = false;
    let timer: number | undefined;
    let attempts = 0;
    let lastMessage = '';

    const scanOnce = async () => {
      const video = videoRef.current;
      const canvas = canvasRef.current;
      // Một số webcam/trình duyệt không bao giờ báo HAVE_ENOUGH_DATA với stream trực tiếp – chỉ cần có khung hình hiện tại.
      if (cancelled || !video || !canvas || video.readyState < video.HAVE_CURRENT_DATA || video.videoWidth === 0) {
        if (!cancelled) {
          setScanStatus('Đang chờ camera trả khung hình…');
          timer = window.setTimeout(scanOnce, AUTO_SCAN_INTERVAL_MS);
        }
        return;
      }
      canvas.width = video.videoWidth;
      canvas.height = video.videoHeight;
      canvas.getContext('2d')?.drawImage(video, 0, 0, canvas.width, canvas.height);
      attempts++;
      setScanStatus(`Đang gửi khung hình ${attempts}/${MAX_AUTO_SCAN_ATTEMPTS}…`);
      try {
        const frame = await canvasToJpeg(canvas);
        showLastFrame(frame);
        const result = await gateApi.scanPlate(frame);
        if (cancelled) return;
        if (result.isValidFormat) {
          onDetected(result.plateDisplay);
          return;
        }
        setSuggestion(result.plateDisplay);
        lastMessage = `Đọc được "${result.plateDisplay}" nhưng sai định dạng.`;
      } catch (e) {
        if (cancelled) return;
        const status = (e as { status?: number }).status;
        lastMessage = `${status ? `[${status}] ` : ''}${(e as Error).message}`;
        // 400 = khung hình chưa thấy biển số → quét tiếp; lỗi khác (chưa cấu hình token, hết quota…) → dừng.
        if (status !== 400) {
          setError(lastMessage);
          setScanStatus(null);
          setAutoScan(false);
          return;
        }
      }
      setScanStatus(`Lần ${attempts}/${MAX_AUTO_SCAN_ATTEMPTS}: ${lastMessage}`);
      if (attempts >= MAX_AUTO_SCAN_ATTEMPTS) {
        setError(`Đã quét ${MAX_AUTO_SCAN_ATTEMPTS} lần nhưng chưa nhận ra biển số. Lần cuối: ${lastMessage}`);
        setScanStatus(null);
        setAutoScan(false);
        return;
      }
      timer = window.setTimeout(scanOnce, AUTO_SCAN_INTERVAL_MS);
    };

    timer = window.setTimeout(scanOnce, 800);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode, cameraReady, autoScan]);

  const restartAutoScan = () => {
    setError(null);
    setSuggestion(null);
    setScanStatus(null);
    setAutoScan(true);
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    setAutoScan(false);

    let canvas: HTMLCanvasElement;
    try {
      canvas = await loadImageFileToCanvas(file);
    } catch (err) {
      setError((err as Error).message);
      return;
    }

    if (mode === 'plate') {
      await recognizePlate(canvas);
      return;
    }

    const jsQR = (await import('jsqr')).default;
    const ctx = canvas.getContext('2d');
    const imageData = ctx?.getImageData(0, 0, canvas.width, canvas.height);
    const code = imageData && jsQR(imageData.data, imageData.width, imageData.height);
    if (!code?.data) {
      setError('Không tìm thấy mã QR trong ảnh đã chọn. Vui lòng chọn ảnh khác hoặc quét trực tiếp bằng camera.');
      return;
    }
    onDetected(code.data);
  };

  const hint =
    mode === 'qr'
      ? 'Đưa mã QR vé đặt chỗ vào khung xanh để quét tự động.'
      : autoScan
        ? scanStatus ?? 'Đang tự động nhận diện – hướng camera vào biển số, giữ yên và đủ sáng.'
        : null;

  return (
    <div className="space-y-2">
      <div className="relative h-48 bg-slate-900 rounded-xl overflow-hidden border border-slate-800">
        <video ref={videoRef} className="w-full h-full object-cover" muted playsInline />
        <div className="absolute top-2 left-2 flex items-center gap-1.5">
          <span className="px-2 py-0.5 rounded bg-red-600 text-[10px] text-white font-bold tracking-wider">LIVE</span>
          <span className="px-2 py-0.5 rounded bg-black/60 text-[10px] text-white font-bold">
            {mode === 'qr' ? 'QUÉT QR VÉ' : 'QUÉT BIỂN SỐ'}
          </span>
        </div>
        <button
          onClick={onClose}
          className="absolute top-2 right-2 w-7 h-7 rounded-full bg-black/60 text-white hover:bg-black/80 flex items-center justify-center cursor-pointer"
          title="Tắt camera"
        >
          <span className="material-symbols-outlined text-[18px]">close</span>
        </button>
        {mode === 'qr' && (
          <div className="absolute inset-y-6 left-1/2 -translate-x-1/2 aspect-square border-2 border-emerald-400/80 rounded-lg pointer-events-none" />
        )}
        {mode === 'plate' && lastFrameUrl && (
          <figure className="absolute bottom-2 right-2 w-24 rounded-md overflow-hidden border-2 border-white/80 shadow-lg pointer-events-none">
            <img src={lastFrameUrl} alt="Khung hình vừa gửi nhận diện" className="w-full block" />
            <figcaption className="bg-black/70 text-white text-[9px] font-semibold text-center py-0.5">Ảnh vừa gửi</figcaption>
          </figure>
        )}
        {!cameraReady && !error && (
          <div className="absolute inset-0 flex items-center justify-center text-slate-300 text-xs font-semibold gap-2">
            <span className="material-symbols-outlined animate-spin">progress_activity</span>
            Đang mở camera…
          </div>
        )}
        {busy && (
          <div className="absolute inset-0 bg-black/60 flex items-center justify-center text-white text-xs font-semibold gap-2">
            <span className="material-symbols-outlined animate-spin">progress_activity</span>
            {mode === 'qr' ? 'Đang xử lý ảnh…' : 'Đang nhận diện biển số…'}
          </div>
        )}
      </div>
      <canvas ref={canvasRef} className="hidden" />

      {cameraReady && hint && (
        <p className="text-[11px] text-slate-500 flex items-center gap-1.5">
          {mode === 'plate' && (
            <span className="material-symbols-outlined text-[14px] text-blue-600 animate-spin">progress_activity</span>
          )}
          {hint}
        </p>
      )}

      {error && (
        <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">{error}</p>
      )}

      {suggestion && (
        <div className="flex items-center justify-between gap-2 text-xs text-amber-800 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
          <span>
            Đọc được <b>{suggestion}</b> – chưa đúng định dạng biển số VN.
          </span>
          <button
            onClick={() => onDetected(suggestion)}
            className="shrink-0 px-3 py-1 bg-white border border-amber-300 rounded-lg font-bold hover:bg-amber-100 cursor-pointer"
          >
            DÙNG & SỬA TAY
          </button>
        </div>
      )}

      <input ref={fileInputRef} type="file" accept="image/*" className="hidden" onChange={(e) => void handleFileUpload(e)} />
      <div className="flex gap-2">
        {mode === 'plate' && !autoScan && cameraReady && (
          <button
            onClick={restartAutoScan}
            disabled={busy}
            className="flex-1 px-3 py-2 bg-blue-600 text-white rounded-xl text-xs font-bold hover:bg-blue-700 disabled:opacity-50 flex items-center justify-center gap-1.5 cursor-pointer"
          >
            <span className="material-symbols-outlined text-[16px]">restart_alt</span>
            QUÉT LẠI
          </button>
        )}
        <button
          onClick={() => fileInputRef.current?.click()}
          disabled={busy}
          className="flex-1 px-3 py-2 bg-slate-100 text-slate-700 rounded-xl text-xs font-bold hover:bg-slate-200 disabled:opacity-50 flex items-center justify-center gap-1.5 cursor-pointer"
        >
          <span className="material-symbols-outlined text-[16px]">upload</span>
          {mode === 'qr' ? 'TẢI ẢNH MÃ QR' : 'TẢI ẢNH BIỂN SỐ'}
        </button>
      </div>
    </div>
  );
};
