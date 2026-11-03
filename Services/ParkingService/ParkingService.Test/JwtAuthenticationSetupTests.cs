using System.Reflection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using ParkingService.API.Controllers;

namespace ParkingService.Test;

/// <summary>
/// US-055: ParkingService bật JWT (RS256, public key của UserService) để controller admin dùng policy RequireAdmin.
/// Endpoint hiện có của US-053/US-054 phải giữ nguyên, không yêu cầu token (tương thích ngược).
/// </summary>
public class JwtAuthenticationSetupTests
{
    private static readonly string KeyPath = Path.Combine(AppContext.BaseDirectory, "Keys", "public.key");

    [Fact]
    public void PublicKey_IsCopiedWithApi_AndIsRsa2048OrMore()
    {
        Assert.True(File.Exists(KeyPath), $"Thiếu {KeyPath}");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(KeyPath));
        Assert.True(rsa.KeySize >= 2048);
    }

    [Fact]
    public async Task AddJwtAuth_WithServiceKey_RegistersBearerSchemeAndRequireAdminPolicy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJwtAuth(KeyPath);
        await using var provider = services.BuildServiceProvider();

        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var defaultScheme = await schemes.GetDefaultAuthenticateSchemeAsync();
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, defaultScheme?.Name);

        var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync("RequireAdmin");
        Assert.NotNull(policy);
        var roles = Assert.Single(policy.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Equal(["Admin"], roles.AllowedRoles);
    }

    [Fact]
    public void ExistingPublicControllers_DoNotRequireAuthentication()
    {
        var controllers = typeof(ParkingLotsController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t)
                     && !t.IsAbstract
                     && !t.Name.StartsWith("Admin", StringComparison.Ordinal)
                     && t != typeof(FloorLayoutsController))
            .ToList();

        Assert.Contains(typeof(ParkingLotsController), controllers);
        Assert.Contains(typeof(KybApplicationsController), controllers);
        Assert.Contains(typeof(ParkingLotStructureController), controllers);
        foreach (var controller in controllers)
        {
            Assert.Null(controller.GetCustomAttribute<AuthorizeAttribute>(inherit: true));
            var protectedActions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<AuthorizeAttribute>(inherit: true) is not null)
                .Select(m => m.Name);
            Assert.Empty(protectedActions);
        }
    }

    [Fact]
    public void FloorLayoutsController_GetLayoutIsPublic_AndMutatingActionsRequireAuthorization()
    {
        var controller = typeof(FloorLayoutsController);
        Assert.Null(controller.GetCustomAttribute<AuthorizeAttribute>(inherit: true));

        // Public SVG renderer endpoint for guest drivers and gate kiosks
        var getLayoutMethod = controller.GetMethod(nameof(FloorLayoutsController.GetLayout));
        Assert.NotNull(getLayoutMethod);
        Assert.NotNull(getLayoutMethod.GetCustomAttribute<AllowAnonymousAttribute>());

        // Owner/Admin editor endpoints require authorization
        string[] protectedActionNames = [
            nameof(FloorLayoutsController.GetDraft),
            nameof(FloorLayoutsController.SaveDraft),
            nameof(FloorLayoutsController.Publish),
            nameof(FloorLayoutsController.GetVersions),
            nameof(FloorLayoutsController.GetVersionByNo)
        ];

        foreach (var actionName in protectedActionNames)
        {
            var method = controller.GetMethod(actionName);
            Assert.NotNull(method);
            Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>(inherit: true));
        }
    }
}
