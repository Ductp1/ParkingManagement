import { useEffect, useRef, useState } from 'react';

interface Turnstile {
  render: (element: HTMLElement, options: {
    sitekey: string; action: string; size: 'flexible' | 'compact'; callback: (token: string) => void;
    'expired-callback': () => void; 'error-callback': () => void;
  }) => string;
  remove: (id: string) => void;
}
declare global { interface Window { turnstile?: Turnstile } }
let scriptPromise: Promise<void> | undefined;
function loadScript() {
  if (window.turnstile) return Promise.resolve();
  if (!scriptPromise) scriptPromise = new Promise<void>((resolve, reject) => {
    const script = document.createElement('script');
    script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => { script.remove(); scriptPromise = undefined; reject(new Error('CAPTCHA không tải được.')); };
    document.head.appendChild(script);
  });
  return scriptPromise;
}
export function TurnstileWidget({ onToken, attempt }: { onToken: (token: string) => void; attempt: number }) {
  const container = useRef<HTMLDivElement>(null);
  const callback = useRef(onToken);
  useEffect(() => { callback.current = onToken; }, [onToken]);
  const [error, setError] = useState({ message: '', attempt: -1 });
  const [size, setSize] = useState<'flexible' | 'compact'>('flexible');
  const sitekey = import.meta.env.VITE_TURNSTILE_SITE_KEY;
  useEffect(() => {
    if (!container.current) return;
    // Turnstile's flexible widget needs at least 300px; use compact below that.
    const observer = new ResizeObserver(([entry]) => {
      setSize(entry.contentRect.width >= 300 ? 'flexible' : 'compact');
    });
    observer.observe(container.current);
    return () => observer.disconnect();
  }, []);
  useEffect(() => {
    let disposed = false;
    let widget: string | undefined;
    callback.current('');
    if (!sitekey) return;
    loadScript().then(() => {
      if (disposed || !container.current || !window.turnstile) return;
      widget = window.turnstile.render(container.current, {
        sitekey, action: 'login', size,
        callback: token => { if (!disposed) { setError({ message: '', attempt }); callback.current(token); } },
        'expired-callback': () => {
          if (!disposed) { callback.current(''); setError({ message: 'CAPTCHA đã hết hạn. Vui lòng xác thực lại.', attempt }); }
        },
        'error-callback': () => {
          if (!disposed) { callback.current(''); setError({ message: 'CAPTCHA gặp lỗi. Vui lòng tải lại xác thực.', attempt }); }
        },
      });
    }).catch(() => { if (!disposed) setError({ message: 'Không tải được CAPTCHA. Vui lòng thử lại.', attempt }); });
    return () => { disposed = true; if (widget !== undefined) window.turnstile?.remove(widget); };
  }, [attempt, sitekey, size]);
  const message = !sitekey ? 'Chưa cấu hình CAPTCHA. Vui lòng liên hệ quản trị viên.'
    : error.attempt === attempt ? error.message : '';
  return <div className="w-full min-w-0"><div ref={container} className="w-full" />{message && <p role="alert" className="mt-2 text-sm text-red-600">{message}</p>}</div>;
}
