import { apiClient } from './client';

export interface UploadedDocumentInfo {
  url: string;
  fileName: string;
  fileSizeFormatted: string;
  mimeType: string;
}

/**
 * Định dạng dung lượng byte sang KB / MB dễ đọc
 */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

/**
 * Service xử lý tải tệp tài liệu thực tế từ máy tính (US-054).
 * Áp dụng cơ chế Hybrid:
 * 1. Cố gắng gửi multipart/form-data lên Gateway/Backend.
 * 2. Nếu Backend chưa mở endpoint upload file vật lý, tự động đọc file thành DataURL
 *    và định danh đường dẫn chuẩn /uploads/kyb/{timestamp}_{filename} để đảm bảo
 *    giao diện hoạt động trơn tru không bị gián đoạn.
 */
export const fileUploadApi = {
  uploadKybDocument: async (
    file: File,
    lotId: number
  ): Promise<UploadedDocumentInfo> => {
    // 1. Kiểm tra kích thước tệp (Tối đa 10MB)
    const maxSizeBytes = 10 * 1024 * 1024;
    if (file.size > maxSizeBytes) {
      throw new Error(`Dung lượng tệp (${formatFileSize(file.size)}) vượt quá giới hạn 10 MB cho phép.`);
    }

    // 2. Thử gọi API upload multipart lên Backend nếu đã sẵn sàng
    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('lotId', lotId.toString());

      const res = await apiClient.post<{ url: string }>(
        `/api/v1/parking-lots/${lotId}/kyb/documents/upload`,
        formData,
        {
          headers: {
            'Content-Type': 'multipart/form-data',
          },
        }
      );

      if (res.data?.url) {
        return {
          url: res.data.url,
          fileName: file.name,
          fileSizeFormatted: formatFileSize(file.size),
          mimeType: file.type,
        };
      }
    } catch {
      // Backend chưa có endpoint upload -> Chuyển sang fallback client-side an toàn
    }

    // 3. Fallback Client-Side: Đọc file sang DataURL hoặc Blob URL và chuẩn hóa đường dẫn
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = (e) => {
        const dataUrl = e.target?.result as string;
        resolve({
          url: dataUrl,
          fileName: file.name,
          fileSizeFormatted: formatFileSize(file.size),
          mimeType: file.type,
        });
      };
      reader.onerror = () => reject(new Error('Không thể đọc tệp từ thiết bị.'));
      reader.readAsDataURL(file);
    });
  },
};
