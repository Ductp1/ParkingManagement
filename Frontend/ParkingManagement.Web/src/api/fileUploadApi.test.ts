import { afterEach, describe, expect, it, vi } from 'vitest';
import { fileUploadApi } from './fileUploadApi';

const h = vi.hoisted(() => ({ post: vi.fn(), failWith: null as Error | null }));
vi.mock('./client', () => ({
  apiClient: {
    post: async (...args: unknown[]) => {
      if (h.failWith) throw h.failWith;
      return h.post(...args);
    },
  },
}));

const file = (size = 1024, type = 'image/jpeg') => new File([new Uint8Array(size)], 'giay-phep.jpg', { type });

afterEach(() => {
  h.post.mockReset();
  h.failWith = null;
});

describe('fileUploadApi.uploadKybDocument (US-054)', () => {
  it('dùng documentUrl mà ParkingService trả về (đường dẫn ngắn, không phải data URL)', async () => {
    h.post.mockResolvedValue({
      data: { fileId: 'doc_1.jpg', documentUrl: '/api/v1/parking-lots/5/kyb/documents/doc_1.jpg', contentType: 'image/jpeg' },
    });

    const result = await fileUploadApi.uploadKybDocument(file(), 5);

    expect(h.post).toHaveBeenCalledWith('/api/v1/parking-lots/5/kyb/documents/upload', expect.any(FormData), expect.anything());
    expect(result.url).toBe('/api/v1/parking-lots/5/kyb/documents/doc_1.jpg');
    expect(result.url.length).toBeLessThanOrEqual(512);
    expect(result.fileName).toBe('giay-phep.jpg');
  });

  it('vẫn nhận trường url cũ nếu BE trả về như vậy', async () => {
    h.post.mockResolvedValue({ data: { url: '/uploads/kyb/a.pdf' } });
    expect((await fileUploadApi.uploadKybDocument(file(10, 'application/pdf'), 5)).url).toBe('/uploads/kyb/a.pdf');
  });

  it('BE không trả đường dẫn → báo lỗi, không tự dựng data URL', async () => {
    h.post.mockResolvedValue({ data: { fileId: 'doc_2.jpg' } });
    await expect(fileUploadApi.uploadKybDocument(file(), 5)).rejects.toThrow(/không trả về đường dẫn/);
  });

  it('upload lỗi → lỗi được báo lên ô tải tệp, không rơi về data URL base64', async () => {
    h.failWith = new Error('Không có quyền tải tài liệu cho bãi này.');
    await expect(fileUploadApi.uploadKybDocument(file(), 5)).rejects.toThrow('Không có quyền tải tài liệu cho bãi này.');
  });

  it('tệp trên 10 MB bị chặn trước khi gọi API', async () => {
    await expect(fileUploadApi.uploadKybDocument(file(10 * 1024 * 1024 + 1), 5)).rejects.toThrow(/10 MB/);
    expect(h.post).not.toHaveBeenCalled();
  });
});
