using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GateService.Application.Features.PlateRecognition;
using Microsoft.Extensions.Configuration;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.Recognition;

/// <summary>Đọc biển số từ ảnh qua Plate Recognizer Snapshot API (https://platerecognizer.com).</summary>
public sealed class PlateRecognizerClient(HttpClient http, IConfiguration configuration) : IPlateImageReader
{
    public const string ApiTokenKey = "PlateRecognizer:ApiToken";

    public async Task<IReadOnlyList<PlateImageCandidate>> ReadAsync(Stream image, string fileName, CancellationToken cancellationToken = default)
    {
        var token = configuration[ApiTokenKey];
        if (string.IsNullOrWhiteSpace(token))
            throw new DependencyUnavailableException($"Chưa cấu hình {ApiTokenKey} – không thể quét biển số bằng ảnh.");

        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(image), "upload", fileName);
        form.Add(new StringContent(configuration["PlateRecognizer:Regions"] ?? "vn"), "regions");

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/plate-reader/") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", token);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new DependencyUnavailableException("Không kết nối được dịch vụ nhận diện biển số. Vui lòng nhập tay.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw response.StatusCode switch
                {
                    HttpStatusCode.RequestEntityTooLarge => new ValidationException("Ảnh quá lớn. Vui lòng dùng ảnh dưới 3MB."),
                    HttpStatusCode.TooManyRequests => new DependencyUnavailableException("Quét quá nhanh, vui lòng thử lại sau 1 giây."),
                    HttpStatusCode.Forbidden => new DependencyUnavailableException("API token nhận diện biển số không hợp lệ hoặc đã hết lượt."),
                    _ => new DependencyUnavailableException($"Dịch vụ nhận diện biển số lỗi ({(int)response.StatusCode}). Vui lòng nhập tay."),
                };
            }

            var body = await response.Content.ReadFromJsonAsync<PlateReaderResponse>(cancellationToken);
            return (body?.Results ?? [])
                .SelectMany(r => r.Candidates is { Count: > 0 } ? r.Candidates : [new PlateReaderCandidate(r.Plate, r.Score)])
                .Where(c => !string.IsNullOrWhiteSpace(c.Plate))
                .Select(c => new PlateImageCandidate(c.Plate!, c.Score))
                .ToList();
        }
    }

    private sealed record PlateReaderResponse([property: JsonPropertyName("results")] List<PlateReaderResult>? Results);

    private sealed record PlateReaderResult(
        [property: JsonPropertyName("plate")] string? Plate,
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("candidates")] List<PlateReaderCandidate>? Candidates);

    private sealed record PlateReaderCandidate(
        [property: JsonPropertyName("plate")] string? Plate,
        [property: JsonPropertyName("score")] double Score);
}
