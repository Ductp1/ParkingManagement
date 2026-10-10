using System.Text.Json;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Kyb;

public static class KybMapper
{
    public static KybApplicationDto ToDto(this KybApplication app)
    {
        IReadOnlyList<string> photoUrls = [];
        if (!string.IsNullOrWhiteSpace(app.SitePhotoUrlsJson))
        {
            try
            {
                photoUrls = JsonSerializer.Deserialize<List<string>>(app.SitePhotoUrlsJson) ?? [];
            }
            catch
            {
                photoUrls = [];
            }
        }

        return new KybApplicationDto(
            app.Id,
            app.ParkingLotId,
            app.Status.ToString(),
            app.BusinessLicenseUrl,
            photoUrls,
            app.PhotoLatitude,
            app.PhotoLongitude,
            app.FireSafetyCertificateUrl,
            app.FieldSurveyRequired,
            app.FieldSurveyAtUtc,
            app.FieldSurveyNote,
            app.SubmittedAtUtc,
            app.ReviewedAtUtc,
            app.RejectReason);
    }
}
