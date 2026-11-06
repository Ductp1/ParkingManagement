using GateService.Application;
using GateService.Application.Features.PlateRecognition;
using GateService.Infrastructure;
using GateService.Infrastructure.Recognition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Test;

// T-701: IPlateRecognizer + ManualPlateRecognizer.
public class ManualPlateRecognizerTests
{
    private readonly ManualPlateRecognizer _recognizer = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---...")]
    public async Task Empty_manual_plate_is_rejected(string input)
        => await Assert.ThrowsAsync<ValidationException>(
            () => _recognizer.RecognizeAsync(new PlateRecognitionRequest(input)));

    [Fact]
    public async Task Valid_manual_plate_is_trimmed_and_normalized()
    {
        var result = await _recognizer.RecognizeAsync(new PlateRecognitionRequest("  51f-123.45  "));

        Assert.Equal("51F12345", result.PlateNumber);   // dạng lưu DB
        Assert.Equal("51F-123.45", result.PlateDisplay); // dạng hiển thị
        Assert.Equal(PlateRecognitionSource.Manual, result.Source);
        Assert.Null(result.Confidence); // nhập tay không có độ tin cậy OCR
    }

    [Fact]
    public async Task Input_not_matching_vietnam_plate_format_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(
            () => _recognizer.RecognizeAsync(new PlateRecognitionRequest("abc123")));

    [Fact]
    public void IPlateRecognizer_is_resolved_from_di()
    {
        var services = new ServiceCollection();
        services.AddGateApplication();
        services.AddGateInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ServiceDb"] = "Host=localhost;Database=pm_gate_test"
            })
            .Build());

        using var provider = services.BuildServiceProvider();
        var recognizer = provider.GetRequiredService<IPlateRecognizer>();

        // MVP: implementation mặc định là ManualPlateRecognizer (Phase 2 sẽ thay bằng OcrPlateRecognizer).
        Assert.IsType<ManualPlateRecognizer>(recognizer);
    }
}
