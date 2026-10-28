import React, { useState } from 'react';
import {
  Upload,
  X,
  ExternalLink,
  FileText,
  Loader2,
  FileCheck2,
  RefreshCw,
} from 'lucide-react';
import { fileUploadApi } from '@/api/fileUploadApi';
import { DocumentStatusBadge } from './DocumentStatusBadge';
import type { KybStatus } from '@/types/ownerKyb';

interface DocumentUploadCardProps {
  label: string;
  description: string;
  value: string;
  fileName?: string;
  fileSize?: string;
  onChange: (value: string, fileName?: string, fileSize?: string) => void;
  required?: boolean;
  disabled?: boolean;
  accept?: string;
  helpText?: string;
  kybStatus?: KybStatus | string;
  lotId?: number;
}

export const DocumentUploadCard: React.FC<DocumentUploadCardProps> = ({
  label,
  description,
  value,
  fileName,
  fileSize,
  onChange,
  required = false,
  disabled = false,
  accept = 'image/*,application/pdf',
  helpText = 'Hỗ trợ định dạng PDF, JPG, PNG. Tối đa 10 MB.',
  kybStatus,
  lotId = 0,
}) => {
  const [dragActive, setDragActive] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState('');

  // Tự động nhận diện tên file nếu chưa có
  const displayFileName =
    fileName ||
    (value
      ? value.startsWith('data:')
        ? 'Tệp đã tải lên từ thiết bị'
        : value.split('/').pop() || 'Tài liệu đã đính kèm'
      : '');

  const isPdf =
    displayFileName.toLowerCase().endsWith('.pdf') ||
    value.includes('application/pdf');

  const isImage =
    value &&
    !isPdf &&
    (value.startsWith('data:image') ||
      /\.(jpeg|jpg|png|webp|gif)($|\?)/i.test(value) ||
      value.includes('/images/') ||
      value.includes('/kyb/'));

  // Xử lý khi người dùng chọn hoặc kéo thả tệp thật từ máy tính
  const handleFileSelect = async (file: File) => {
    if (disabled || uploading) return;
    setUploadError('');
    setUploading(true);

    try {
      const result = await fileUploadApi.uploadKybDocument(file, lotId);
      onChange(result.url, result.fileName, result.fileSizeFormatted);
    } catch (err: unknown) {
      setUploadError(
        err instanceof Error
          ? err.message
          : 'Không thể tải tệp lên. Vui lòng thử lại.'
      );
    } finally {
      setUploading(false);
    }
  };

  const onDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(false);
    if (disabled || uploading || !e.dataTransfer.files?.[0]) return;
    void handleFileSelect(e.dataTransfer.files[0]);
  };

  return (
    <div className="rounded-2xl border border-slate-200/90 bg-white p-5 shadow-xs transition-all">
      {/* Tiêu đề & Trạng thái thẩm định */}
      <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-3 mb-3">
        <div>
          <div className="flex items-center gap-2">
            <h4 className="text-sm font-bold text-slate-900">{label}</h4>
            {required ? (
              <span className="text-[10px] font-extrabold uppercase px-1.5 py-0.5 rounded bg-rose-50 text-rose-700 border border-rose-200">
                Bắt buộc
              </span>
            ) : (
              <span className="text-[10px] font-medium px-1.5 py-0.5 rounded bg-slate-100 text-slate-500">
                Tùy chọn
              </span>
            )}
          </div>
          <p className="text-xs text-slate-500 mt-0.5">{description}</p>
        </div>

        {/* Huy hiệu trạng thái kiểm tra tài liệu */}
        <div className="shrink-0">
          <DocumentStatusBadge kybStatus={kybStatus} hasFile={!!value} />
        </div>
      </div>

      {uploadError && (
        <div className="mb-3 p-3 rounded-xl bg-rose-50 border border-rose-200 text-xs text-rose-800">
          {uploadError}
        </div>
      )}

      {/* Hiển thị khi ĐÃ CÓ TỆP */}
      {value ? (
        <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-4 flex flex-col sm:flex-row items-center gap-4">
          {/* Khung thumbnail / icon tài liệu */}
          <div className="w-full sm:w-28 h-24 rounded-xl bg-white overflow-hidden flex items-center justify-center shrink-0 border border-slate-200/90 shadow-2xs">
            {isImage ? (
              <img
                src={value}
                alt={label}
                className="w-full h-full object-cover"
              />
            ) : (
              <div className="flex flex-col items-center text-slate-600 text-xs text-center p-2">
                <FileText size={30} className="text-rose-600 mb-1" />
                <span className="font-bold text-[11px] text-slate-700 uppercase">PDF</span>
              </div>
            )}
          </div>

          {/* Chi tiết file thật: Tên file, dung lượng, hành động */}
          <div className="flex-1 min-w-0 w-full">
            <div className="flex items-center gap-2">
              <FileCheck2 size={16} className="text-indigo-600 shrink-0" />
              <div
                className="text-xs font-bold text-slate-900 truncate"
                title={displayFileName}
              >
                {displayFileName}
              </div>
            </div>

            <div className="text-[11px] text-slate-500 mt-1 flex items-center gap-2">
              {fileSize && (
                <span className="font-mono font-medium px-1.5 py-0.5 rounded bg-white border border-slate-200 text-slate-700">
                  {fileSize}
                </span>
              )}
              <span>Tệp đã sẵn sàng trong hồ sơ</span>
            </div>

            <div className="flex items-center gap-2 mt-3 flex-wrap">
              {/* Nút xem file */}
              {!value.startsWith('data:') && (
                <a
                  href={value}
                  target="_blank"
                  rel="noreferrer"
                  className="px-3 py-1.5 rounded-lg bg-white border border-slate-200 text-xs font-semibold text-slate-700 hover:text-indigo-600 hover:border-indigo-300 flex items-center gap-1.5 shadow-2xs transition-colors"
                >
                  <ExternalLink size={13} />
                  Xem tệp gốc
                </a>
              )}

              {/* Nút chọn lại / thay thế tệp khác */}
              {!disabled && (
                <label className="px-3 py-1.5 rounded-lg bg-white border border-slate-200 text-xs font-semibold text-slate-700 hover:text-indigo-600 hover:border-indigo-300 flex items-center gap-1.5 shadow-2xs transition-colors cursor-pointer">
                  <RefreshCw size={13} />
                  Thay tệp khác
                  <input
                    type="file"
                    className="hidden"
                    accept={accept}
                    disabled={disabled || uploading}
                    onChange={(e) => {
                      if (e.target.files?.[0]) {
                        void handleFileSelect(e.target.files[0]);
                      }
                    }}
                  />
                </label>
              )}

              {/* Nút gỡ bỏ tệp */}
              {!disabled && (
                <button
                  type="button"
                  onClick={() => onChange('', '', '')}
                  className="px-3 py-1.5 rounded-lg bg-rose-50 border border-rose-200 text-xs font-semibold text-rose-700 hover:bg-rose-100 flex items-center gap-1.5 cursor-pointer transition-colors"
                >
                  <X size={13} />
                  Gỡ bỏ
                </button>
              )}
            </div>
          </div>
        </div>
      ) : (
        /* Vùng kéo thả / chọn tệp máy tính khi CHƯA CÓ TỆP */
        <div
          onDragOver={(e) => {
            e.preventDefault();
            setDragActive(true);
          }}
          onDragLeave={() => setDragActive(false)}
          onDrop={onDrop}
          className={`rounded-2xl border-2 border-dashed p-6 text-center transition-all ${
            dragActive
              ? 'border-indigo-600 bg-indigo-50/60 scale-[1.01]'
              : 'border-slate-300 hover:border-indigo-400 bg-slate-50/40 hover:bg-slate-50/80'
          } ${disabled ? 'opacity-60 cursor-not-allowed' : ''}`}
        >
          {uploading ? (
            <div className="py-4 flex flex-col items-center justify-center gap-2">
              <Loader2 size={28} className="text-indigo-600 animate-spin" />
              <span className="text-xs font-bold text-slate-700">
                Đang xử lý tải tệp lên…
              </span>
            </div>
          ) : (
            <>
              <div className="w-12 h-12 rounded-2xl bg-indigo-50 text-indigo-600 flex items-center justify-center mx-auto mb-3 shadow-2xs">
                <Upload size={20} />
              </div>

              <div className="text-xs font-bold text-slate-900">
                Kéo thả tệp vào đây, hoặc{' '}
                <label
                  className={`text-indigo-600 hover:text-indigo-700 underline font-extrabold ${
                    disabled ? '' : 'cursor-pointer'
                  }`}
                >
                  chọn tệp từ máy tính
                  <input
                    type="file"
                    className="hidden"
                    accept={accept}
                    disabled={disabled || uploading}
                    onChange={(e) => {
                      if (e.target.files?.[0]) {
                        void handleFileSelect(e.target.files[0]);
                      }
                    }}
                  />
                </label>
              </div>

              <p className="text-[11px] text-slate-500 mt-1">{helpText}</p>
            </>
          )}
        </div>
      )}
    </div>
  );
};
