using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GateService.Application.Features.GateConsole;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.Integrations;

/// <summary>
/// Port IGateBookingClient cài bằng HttpClient qua Gateway (route /api/v1/bookings → BookingService).
/// 404 kèm ProblemDetails → null; 409/400 → ConflictException với lý do của BookingService; 401/403 → lỗi xác thực.
/// </summary>
public sealed class BookingServiceClient(HttpClient http) : IGateBookingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<GateNoShowCandidate>> ListNoShowCandidatesAsync(
        int parkingLotId, int graceMinutes, CancellationToken cancellationToken)
    {
        var path = $"api/v1/bookings/no-show-candidates?parkingLotId={parkingLotId}&graceMinutes={graceMinutes}";
        var rows = await SendAsync<GateNoShowCandidateJson[]>(HttpMethod.Get, path, body: null, cancellationToken)
            ?? [];
        return rows.Select(r => new GateNoShowCandidate(r.Code, r.UserId, r.PlateNumber, r.ParkingLotName,
            r.SlotCode, r.StartAtUtc, r.EndAtUtc, r.MinutesOverdue)).ToList();
    }

    public async Task<GateBookingSnapshot?> GetBookingAsync(string bookingCode, CancellationToken cancellationToken)
    {
        var (found, dto) = await SendAsync<BookingDetailJson>(HttpMethod.Get, $"api/v1/bookings/{bookingCode}", null, cancellationToken);
        if (!found || dto is null) return null;

        return new GateBookingSnapshot(dto.Code, dto.ParkingLotId, dto.UserId, dto.Status, dto.PlateNumber,
            dto.SlotId, dto.SlotCode, dto.StartAtUtc, dto.EndAtUtc, dto.CheckedInAtUtc);
    }

    public Task MarkNoShowAsync(string bookingCode, int staffUserId, CancellationToken cancellationToken)
        => SendActionAsync(bookingCode, "no-show", staffUserId, cancellationToken);

    public Task MarkCheckedInAsync(string bookingCode, string plate, CancellationToken cancellationToken)
        => SendActionAsync(bookingCode, "check-in", plate, cancellationToken);

    public Task MarkCheckedOutAsync(string bookingCode, string plate, CancellationToken cancellationToken)
        => SendActionAsync(bookingCode, "check-out", plate, cancellationToken);

    // GateActionCommand của BookingService: { gateDeviceId, staffUserId, plateNumber } – action nội bộ giữa TV3 ↔ TV7.
    private Task SendActionAsync(string bookingCode, string action, object value, CancellationToken cancellationToken)
        => SendAsync<BookingDetailJson>(HttpMethod.Post, $"api/v1/bookings/{bookingCode}/{action}",
            value is int staff ? new { gateDeviceId = 0, staffUserId = staff } : new { gateDeviceId = 0, staffUserId = 0, plateNumber = value },
            cancellationToken, readBody: false);

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken,
        bool readBody = true)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
            using var response = await http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                if (IsJson(response)) return default;
                throw new DependencyUnavailableException($"BookingService chưa có endpoint {method} {path}.");
            }
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
                throw new ConflictException(await DetailAsync(response, cancellationToken)
                                            ?? "BookingService từ chối thao tác trên booking này.");
            if (!response.IsSuccessStatusCode)
                throw new DependencyUnavailableException($"BookingService trả HTTP {(int)response.StatusCode} khi gọi {method} {path}.");

            if (!readBody) return default;
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new DependencyUnavailableException($"Không gọi được BookingService ({method} {path}).", ex);
        }
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!IsJson(response)) return null;
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions, cancellationToken);
        return string.IsNullOrWhiteSpace(problem?.Detail) ? null : problem.Detail;
    }

    private static bool IsJson(HttpResponseMessage response)
        => response.Content.Headers.ContentType?.MediaType is { } mediaType
           && mediaType.EndsWith("json", StringComparison.OrdinalIgnoreCase);

    private sealed record ProblemJson(string? Detail);

    private sealed record GateNoShowCandidateJson(string Code, int UserId, string PlateNumber, string ParkingLotName,
        string? SlotCode, DateTime StartAtUtc, DateTime EndAtUtc, int MinutesOverdue);

    private sealed record BookingDetailJson(string Code, string Status, int UserId, string PlateNumber,
        int ParkingLotId, int? SlotId, string? SlotCode, DateTime StartAtUtc, DateTime EndAtUtc, DateTime? CheckedInAtUtc);
}