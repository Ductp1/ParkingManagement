using ParkingManagement.SharedKernel.Exceptions;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Application.Features.PlateRecognition;

/// <summary>Một biển số mà dịch vụ ANPR đọc được từ ảnh, kèm độ tin cậy 0..1.</summary>
public sealed record PlateImageCandidate(string Plate, double Confidence);

/// <summary>Kết quả quét ảnh trả cho Gate Console. IsValidFormat = false → staff cần kiểm tra lại trước khi dùng.</summary>
public sealed record PlateScanResultDto(string Plate, string PlateDisplay, double Confidence, bool IsValidFormat);

/// <summary>Port đọc biển số từ ảnh (Infrastructure: Plate Recognizer API).</summary>
public interface IPlateImageReader
{
    Task<IReadOnlyList<PlateImageCandidate>> ReadAsync(Stream image, string fileName, CancellationToken cancellationToken = default);
}

public interface IScanPlateImageUseCase
{
    Task<PlateScanResultDto> ExecuteAsync(Stream image, string fileName, CancellationToken cancellationToken = default);
}

public sealed class ScanPlateImageUseCase(IPlateImageReader reader) : IScanPlateImageUseCase
{
    public async Task<PlateScanResultDto> ExecuteAsync(Stream image, string fileName, CancellationToken cancellationToken = default)
    {
        var candidates = await reader.ReadAsync(image, fileName, cancellationToken);
        if (candidates.Count == 0)
            throw new ValidationException("Không tìm thấy biển số trong ảnh. Hãy chụp gần và rõ biển số hơn.");

        // Ưu tiên ứng viên khớp định dạng TT 01/2021 (sau khi sửa lỗi nhầm ký tự), rồi mới tới độ tin cậy.
        var best = candidates
            .Select(c => (Candidate: c, Corrected: PlateNormalizer.TryCorrect(c.Plate)))
            .OrderByDescending(x => x.Corrected is not null)
            .ThenByDescending(x => x.Candidate.Confidence)
            .First();

        var plate = best.Corrected ?? PlateNormalizer.Normalize(best.Candidate.Plate);
        return new PlateScanResultDto(plate, PlateNormalizer.Format(plate), best.Candidate.Confidence, best.Corrected is not null);
    }
}
