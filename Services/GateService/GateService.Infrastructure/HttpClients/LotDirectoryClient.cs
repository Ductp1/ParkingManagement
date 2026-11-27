using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GateService.Application.Features.Shifts;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Infrastructure.HttpClients;

/// <summary>ILotDirectory qua ParkingService (API công khai chi tiết bãi + lịch mở cửa). Nhớ kết quả trong phạm vi một request.</summary>
public sealed class LotDirectoryClient(IHttpClientFactory clients) : ILotDirectory
{
    public const string ClientName = "LotDirectory";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly Dictionary<int, LotInfo> cache = [];

    public async Task<LotInfo> GetAsync(int parkingLotId, CancellationToken ct)
    {
        if (cache.TryGetValue(parkingLotId, out var known)) return known;
        var lot = await GetAsync<LotResponse>($"api/v1/parking-lots/{parkingLotId}", parkingLotId, ct);
        return cache[parkingLotId] = new LotInfo(parkingLotId, lot.OwnerProfileId, lot.Name, lot.Status);
    }

    public async Task<IReadOnlyList<TimeRange>> OpeningHoursAsync(int parkingLotId, int ownerProfileId, DateOnly weekStart, CancellationToken ct)
    {
        var date = weekStart.ToDateTime(TimeOnly.MinValue);
        var week = await GetAsync<WeekResponse>(string.Create(CultureInfo.InvariantCulture,
            $"api/v1/parking-lots/{parkingLotId}/operating-hours?ownerProfileId={ownerProfileId}&year={ISOWeek.GetYear(date)}&weekOfYear={ISOWeek.GetWeekOfYear(date)}"),
            parkingLotId, ct);
        List<TimeRange> ranges = [];
        for (var i = 0; i < 7; i++)
        {
            var day = weekStart.AddDays(i);
            var hours = week.Schedule?.FirstOrDefault(h => h.DayOfWeek == day.DayOfWeek);
            if (hours is null || hours.IsClosed) continue;
            var midnight = Shift.StartOfBusinessDateUtc(day);
            if (hours.Is24Hours) { ranges.Add(new TimeRange(midnight, midnight.AddDays(1))); continue; }
            var start = midnight + hours.OpenTime.ToTimeSpan();
            var end = midnight + hours.CloseTime.ToTimeSpan();
            ranges.Add(new TimeRange(start, end > start ? end : end.AddDays(1)));
        }
        return ranges;
    }

    private async Task<T> GetAsync<T>(string path, int parkingLotId, CancellationToken ct)
    {
        try
        {
            using var response = await clients.CreateClient(ClientName).GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new NotFoundException("Bãi đỗ", parkingLotId);
            if (!response.IsSuccessStatusCode)
                throw new DependencyUnavailableException($"ParkingService trả HTTP {(int)response.StatusCode} khi đọc bãi {parkingLotId}.");
            return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                ?? throw new DependencyUnavailableException("ParkingService trả dữ liệu rỗng.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new DependencyUnavailableException("Không gọi được ParkingService, thử lại sau ít phút.");
        }
    }

    private sealed record LotResponse(int OwnerProfileId, string Name, string Status);
    private sealed record WeekResponse(IReadOnlyList<DayResponse>? Schedule);
    private sealed record DayResponse(DayOfWeek DayOfWeek, TimeOnly OpenTime, TimeOnly CloseTime, bool IsClosed, bool Is24Hours);
}
