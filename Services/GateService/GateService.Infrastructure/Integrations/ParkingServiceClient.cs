using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GateService.Application.Features.GateConsole;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.Integrations;

/// <summary>
/// Port IParkingSlotClient cài bằng HttpClient qua Gateway (route /api/v1/parking-lots → ParkingService).
/// US-049: POST api/v1/parking-lots/slots/{slotId}/state-override – ParkingService cập nhật Slot.State
/// + SlotStateLog (Source, ChangedByUserId, Reason); US-039 dùng để nhả chỗ (slot → Available).
/// </summary>
public sealed class ParkingServiceClient(HttpClient http) : IParkingSlotClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SlotStateOverrideResult> OverrideSlotStateAsync(int parkingLotId, int slotId, SlotState newState,
        SlotStateSource source, int? staffUserId, string reason, CancellationToken cancellationToken)
    {
        var body = new { parkingLotId, newState, source, staffUserId, reason };
        var (found, dto) = await SendAsync<OverrideResultJson>(HttpMethod.Post, $"api/v1/parking-lots/slots/{slotId}/state-override",
            body, cancellationToken);
        if (!found || dto is null)
            throw new NotFoundException("Slot", slotId);

        return new SlotStateOverrideResult(dto.SlotId, dto.SlotCode, dto.FromState, dto.ToState, dto.Source,
            dto.ChangedByUserId, dto.ChangedAtUtc);
    }

    private async Task<(bool Found, T? Value)> SendAsync<T>(HttpMethod method, string path, object? body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
            using var response = await http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return (IsJson(response), default);
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
                throw new ConflictException(await DetailAsync(response, cancellationToken)
                                            ?? "ParkingService từ chối ghi đè trạng thái slot.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new ForbiddenException(await DetailAsync(response, cancellationToken)
                                             ?? "Ô đỗ không thuộc bãi đang thao tác.");
            if (!response.IsSuccessStatusCode)
                throw new DependencyUnavailableException($"ParkingService trả HTTP {(int)response.StatusCode} khi gọi {method} {path}.");

            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return (true, value);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new DependencyUnavailableException($"Không gọi được ParkingService ({method} {path}). {ex.Message}");
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

    private sealed record OverrideResultJson(int SlotId, string SlotCode, string FromState, string ToState,
        string Source, int? ChangedByUserId, DateTime ChangedAtUtc);
}