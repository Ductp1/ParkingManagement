import React, { useEffect, useRef } from 'react';

export interface OtpInputProps {
  length?: number;
  value: string;
  onChange: (value: string) => void;
  onComplete?: (code: string) => void;
  disabled?: boolean;
  hasError?: boolean;
  autoFocus?: boolean;
  className?: string;
}

export const OtpInput: React.FC<OtpInputProps> = ({
  length = 6,
  value = '',
  onChange,
  onComplete,
  disabled = false,
  hasError = false,
  autoFocus = true,
  className = '',
}) => {
  const inputsRef = useRef<(HTMLInputElement | null)[]>([]);

  // Đảm bảo mảng ký tự luôn có đúng số lượng phần tử
  const digits = Array.from({ length }, (_, idx) => value[idx] || '');

  // Tự động focus ô đầu tiên nếu được bật
  useEffect(() => {
    if (autoFocus && !disabled && inputsRef.current[0]) {
      inputsRef.current[0].focus();
    }
  }, [autoFocus, disabled]);

  const setDigitAt = (index: number, digit: string) => {
    const updated = [...digits];
    updated[index] = digit;
    const nextCode = updated.join('').slice(0, length);
    onChange(nextCode);

    if (nextCode.length === length && onComplete) {
      onComplete(nextCode);
    }
  };

  const handleChange = (index: number, e: React.ChangeEvent<HTMLInputElement>) => {
    const rawVal = e.target.value;
    const cleanDigits = rawVal.replace(/\D/g, '');

    if (!cleanDigits) {
      setDigitAt(index, '');
      return;
    }

    if (cleanDigits.length === 1) {
      setDigitAt(index, cleanDigits);
      if (index < length - 1) {
        inputsRef.current[index + 1]?.focus();
      }
    } else {
      // Trường hợp dán hoặc autofill vào ô bất kỳ
      handlePastedContent(cleanDigits, index);
    }
  };

  const handleKeyDown = (index: number, e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Backspace') {
      if (digits[index]) {
        setDigitAt(index, '');
      } else if (index > 0) {
        inputsRef.current[index - 1]?.focus();
        setDigitAt(index - 1, '');
      }
      e.preventDefault();
    } else if (e.key === 'ArrowLeft' && index > 0) {
      inputsRef.current[index - 1]?.focus();
      e.preventDefault();
    } else if (e.key === 'ArrowRight' && index < length - 1) {
      inputsRef.current[index + 1]?.focus();
      e.preventDefault();
    }
  };

  const handlePastedContent = (pasted: string, startIndex = 0) => {
    const clean = pasted.replace(/\D/g, '');
    if (!clean) return;

    const updated = [...digits];
    let writeIdx = startIndex;
    for (let i = 0; i < clean.length && writeIdx < length; i++) {
      updated[writeIdx] = clean[i];
      writeIdx++;
    }

    const nextCode = updated.join('').slice(0, length);
    onChange(nextCode);

    const nextFocusIdx = Math.min(writeIdx, length - 1);
    inputsRef.current[nextFocusIdx]?.focus();

    if (nextCode.length === length && onComplete) {
      onComplete(nextCode);
    }
  };

  const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
    e.preventDefault();
    const pastedData = e.clipboardData.getData('text');
    handlePastedContent(pastedData, 0);
  };

  return (
    <div className={`flex items-center justify-center gap-2 sm:gap-3 ${className}`}>
      {digits.map((digit, idx) => {
        const isMiddle = idx === Math.floor(length / 2) && length % 2 === 0;

        return (
          <React.Fragment key={idx}>
            {isMiddle && (
              <div
                aria-hidden="true"
                className="w-2.5 h-0.5 rounded-full bg-slate-300 shrink-0 mx-0.5 sm:mx-1"
              />
            )}
            <input
              ref={(el) => {
                inputsRef.current[idx] = el;
              }}
              type="text"
              inputMode="numeric"
              pattern="[0-9]*"
              maxLength={1}
              autoComplete={idx === 0 ? 'one-time-code' : 'off'}
              disabled={disabled}
              value={digit}
              onChange={(e) => handleChange(idx, e)}
              onKeyDown={(e) => handleKeyDown(idx, e)}
              onPaste={handlePaste}
              onFocus={(e) => e.target.select()}
              aria-label={`Ký tự OTP thứ ${idx + 1}`}
              className={`w-11 h-14 sm:w-13 sm:h-16 text-center text-xl sm:text-2xl font-black font-mono rounded-2xl border-2 transition-all duration-150 select-none outline-hidden ${
                hasError
                  ? 'border-rose-400 bg-rose-50/60 text-rose-700 ring-2 ring-rose-200'
                  : digit
                    ? 'border-blue-500/80 bg-white text-slate-900 shadow-sm shadow-blue-500/10'
                    : 'border-slate-200 bg-slate-50/80 text-slate-900 hover:border-slate-300'
              } focus:border-blue-600 focus:bg-white focus:ring-4 focus:ring-blue-100 focus:scale-[1.03] disabled:opacity-40 disabled:cursor-not-allowed`}
            />
          </React.Fragment>
        );
      })}
    </div>
  );
};
