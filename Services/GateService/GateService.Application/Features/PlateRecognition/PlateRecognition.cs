namespace GateService.Application.Features.PlateRecognition;

/// <summary>Nguồn dữ liệu biển số. MVP chỉ có Manual (T-701); Phase 2 bổ sung Ocr/Camera mà không phải sửa Check-in.</summary>
public enum PlateRecognitionSource { Manual = 1 }

/// <summary>Yêu cầu nhận diện biển số: đầu vào thô từ nhân viên (MVP) hoặc camera (Phase 2).</summary>
public sealed record PlateRecognitionRequest(string RawInput, PlateRecognitionSource Source = PlateRecognitionSource.Manual);

/// <summary>Kết quả nhận diện: biển số đã chuẩn hoá ("51F12345") + dạng hiển thị ("51F-123.45").</summary>
public sealed record PlateRecognitionResult(string PlateNumber, string PlateDisplay, PlateRecognitionSource Source, double? Confidence);

/// <summary>
/// T-701: Abstraction nhận diện biển số. MVP dùng ManualPlateRecognizer (nhân viên nhập tay);
/// Phase 2 thay bằng OcrPlateRecognizer mà use case Check-in không cần đổi dòng code nào.
/// </summary>
public interface IPlateRecognizer
{
    Task<PlateRecognitionResult> RecognizeAsync(PlateRecognitionRequest request, CancellationToken cancellationToken = default);
}
