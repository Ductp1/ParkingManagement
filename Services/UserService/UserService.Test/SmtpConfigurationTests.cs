using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Infrastructure.Integrations;

namespace UserService.Test;

public class SmtpConfigurationTests
{
    [Fact]
    public void Missing_credentials_prevents_queued_otp()
        => Assert.Throws<DependencyUnavailableException>(() => new SmtpOtpSender(new ConfigurationBuilder().Build()).EnsureAvailable());

    [Fact]
    public void Email_transport_rejects_phone_before_registration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Otp:Transport"]="Smtp" }).Build();
        var delivery = new OtpDeliveryConfiguration(config,new Environment());
        Assert.Throws<ValidationException>(()=>delivery.EnsureDestination("0901234567"));
        delivery.EnsureDestination("a@example.com");
    }

    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
