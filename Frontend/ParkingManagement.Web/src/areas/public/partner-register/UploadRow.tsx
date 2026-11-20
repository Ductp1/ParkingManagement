import { useEffect, useRef, useState, type DragEvent } from 'react';
import { FileText, Plus, Upload } from 'lucide-react';
import { PARTNER_DOCUMENT_LIMITS as LIMITS } from '@/api/partnerRegistrationApi';
import { fileTypeLabel, formatBytes, isImageFile, type DocumentSpec } from './registrationForm';
import { FieldMessage } from './ui';
import { messageId } from './styles';

/**
 * Ảnh xem trước: object URL tạo và thu hồi trong cùng một effect (an toàn với StrictMode mount 2 lần), gán thẳng vào <img>.
 * jsdom không có createObjectURL, PDF, hoặc ảnh không giải mã được → hiện icon.
 */
function Thumbnail({ file }: { file: File }) {
  const imgRef = useRef<HTMLImageElement>(null);
  const [broken, setBroken] = useState(false);
  const canPreview = isImageFile(file) && typeof URL.createObjectURL === 'function' && !broken;
  useEffect(() => {
    if (!canPreview) return undefined;
    const url = URL.createObjectURL(file);
    if (imgRef.current) imgRef.current.src = url;
    return () => URL.revokeObjectURL(url);
  }, [file, canPreview]);
  return canPreview ? (
    <img ref={imgRef} alt="" onError={() => setBroken(true)} className="size-10 shrink-0 rounded-md border border-(--admin-border-default) bg-(--admin-surface-row-alt) object-cover" />
  ) : (
    <span className="flex size-10 shrink-0 items-center justify-center rounded-md border border-(--admin-border-default) bg-(--admin-surface-row-alt)" aria-hidden="true">
      <FileText size={18} className="text-(--admin-text-secondary)" />
    </span>
  );
}

interface UploadRowProps {
  spec: DocumentSpec;
  files: File[];
  error?: string | null;
  disabled?: boolean;
  /** Parent kiểm tra (checkFile) rồi thêm / thay; `replacing` = file bị thay. */
  onPick: (picked: File[], replacing?: File) => void;
  onRemove: (file: File) => void;
}

const actionClass =
  'rounded-md px-2 py-1 text-[13px] font-semibold text-(--admin-brand-navy-900) outline-none enabled:hover:bg-(--admin-surface-selected) focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900) disabled:opacity-50';

/**
 * Một loại giấy tờ (Figma 5 mixed uploads): rỗng → vùng "Kéo thả hoặc Chọn file"; có file → thumbnail + "Thay file" / "Xóa";
 * lỗi → viền đỏ + message. Vùng chọn là <button> nên dùng được bằng bàn phím; input file ẩn.
 */
export function UploadRow({ spec, files, error, disabled, onPick, onRemove }: UploadRowProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const replacingRef = useRef<File | undefined>(undefined);
  const [dragging, setDragging] = useState(false);
  const id = `pr-doc-${spec.kind}`;
  const titleId = `${id}-title`;
  const multi = !spec.imageOnly;

  const open = (replacing?: File) => {
    replacingRef.current = replacing;
    if (inputRef.current) inputRef.current.multiple = multi && !replacing;
    inputRef.current?.click();
  };

  const handleDrop = (e: DragEvent<HTMLElement>) => {
    e.preventDefault();
    setDragging(false);
    if (disabled) return;
    const picked = Array.from(e.dataTransfer.files);
    if (picked.length) onPick(multi ? picked : picked.slice(0, 1));
  };

  const dragProps = {
    onDragOver: (e: DragEvent<HTMLElement>) => {
      e.preventDefault();
      if (!disabled) setDragging(true);
    },
    onDragLeave: () => setDragging(false),
    onDrop: handleDrop,
  };

  return (
    <section
      id={id}
      tabIndex={-1}
      aria-labelledby={titleId}
      className={`rounded-xl outline-none focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900) border p-4 ${error ? 'border-(--admin-status-danger)' : 'border-(--admin-border-default)'} bg-(--admin-surface-card)`}
      {...(files.length > 0 ? dragProps : {})}
    >
      <h3 id={titleId} className="text-[13px] font-semibold text-(--admin-text-primary)">
        {spec.label}
        {spec.required && (
          <span className="text-(--admin-status-danger)" aria-hidden="true">
            {' '}
            *
          </span>
        )}
        {spec.required && <span className="sr-only"> (bắt buộc)</span>}
      </h3>
      <p className="mt-0.5 text-xs text-(--admin-text-secondary)">{spec.hint}</p>

      <input
        ref={inputRef}
        type="file"
        className="hidden"
        tabIndex={-1}
        accept={spec.imageOnly ? LIMITS.acceptImage : LIMITS.accept}
        multiple={multi}
        disabled={disabled}
        aria-label={`Tải lên ${spec.label}`}
        onChange={(e) => {
          const picked = Array.from(e.target.files ?? []);
          e.target.value = '';
          if (picked.length) onPick(multi && !replacingRef.current ? picked : picked.slice(0, 1), replacingRef.current);
          replacingRef.current = undefined;
        }}
      />

      {files.length > 0 && (
        <ul className="mt-3 flex flex-col gap-3">
          {files.map((file) => (
            <li key={`${file.name}-${file.size}-${file.lastModified}`} className="flex flex-col gap-1.5">
              <div className="flex items-center gap-3">
                <Thumbnail file={file} />
                <div className="min-w-0">
                  <p className="truncate text-[13px] font-medium text-(--admin-text-primary)">{file.name}</p>
                  <p className="text-xs text-(--admin-text-secondary)">
                    {fileTypeLabel(file)} · {formatBytes(file.size)}
                  </p>
                </div>
              </div>
              <div className="flex gap-1">
                <button type="button" className={actionClass} disabled={disabled} onClick={() => open(file)} aria-label={`Thay file ${file.name}`}>
                  Thay file
                </button>
                <button type="button" className={actionClass} disabled={disabled} onClick={() => onRemove(file)} aria-label={`Xóa ${file.name}`}>
                  Xóa
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}

      {files.length === 0 ? (
        <button
          type="button"
          disabled={disabled}
          onClick={() => open()}
          aria-describedby={error ? messageId(id) : undefined}
          aria-label={`${spec.label}: kéo thả hoặc chọn file`}
          className={`mt-3 flex h-11 w-full items-center gap-2 rounded-lg border border-dashed px-3 text-[13px] font-semibold text-(--admin-text-primary) outline-none transition-colors focus-visible:ring-2 focus-visible:ring-(--admin-brand-navy-900) disabled:cursor-not-allowed disabled:opacity-50 ${
            dragging ? 'border-(--admin-brand-navy-900) bg-(--admin-surface-selected)' : 'border-(--admin-border-default) bg-(--admin-surface-row-alt) enabled:hover:bg-(--admin-surface-selected)'
          }`}
          {...dragProps}
        >
          <Upload size={16} aria-hidden="true" />
          Kéo thả hoặc Chọn file
        </button>
      ) : (
        multi && (
          <button type="button" className={`${actionClass} mt-2 inline-flex items-center gap-1`} disabled={disabled} onClick={() => open()}>
            <Plus size={14} aria-hidden="true" /> Thêm file
          </button>
        )
      )}

      {error && (
        <div className="mt-2">
          <FieldMessage id={id} error={error} />
        </div>
      )}
    </section>
  );
}
