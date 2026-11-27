using GateService.Application.Features.PlateRecognition;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Infrastructure.Recognition;

/// <summary>
/// T-701: MVP KHÔNG dùng OCR – nhân viên cổng nhập tay biển số.
/// Nhiệm vụ: chặn input rỗng → trim → chuẩn hoá theo PlateNormalizer → kiểm tra định dạng
/// Thông tư 01/2021/TT-BCA → trả về biển số đã chuẩn hoá. KHÔNG chứa nghiệp vụ Check-in.
/// Phase 2 thay thế bằng OcrPlateRecognizer mà không phải sửa use case.
/// </summary>
public sealed class ManualPlateRecognizer : IPlateRecognizer
{
    public Task<PlateRecognitionResult> RecognizeAsync(PlateRecognitionRequest request, CancellationToken cancellationToken = default)
    {
        // Trim + bỏ khoảng trắng/gạch ngang/chấm, viết hoa (PlateNormalizer.Normalize).
        var normalized = PlateNormalizer.Normalize(request.RawInput?.Trim());
        if (normalized.Length == 0)
            throw new ValidationException("Biển số không được để trống. Nhân viên vui lòng nhập biển số của xe.");
        if (!PlateNormalizer.IsValid(normalized))
            throw new ValidationException($"Biển số '{request.RawInput}' không đúng định dạng Thông tư 01/2021/TT-BCA (vd: 51F-123.45).");

        PlateRecognitionResult result = new(normalized, PlateNormalizer.Format(normalized), request.Source, Confidence: null);
        return Task.FromResult(result);
    }
}
