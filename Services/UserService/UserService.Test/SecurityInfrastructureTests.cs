using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Domain.Entities;
using UserService.Infrastructure.Authentication;


namespace UserService.Test;

public class SecurityInfrastructureTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"pm-tv1-tests-"+Guid.NewGuid().ToString("N"));
    private IConfiguration Config => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["Security:EncryptionKey"]=Convert.ToBase64String(Enumerable.Range(0,32).Select(i=>(byte)i).ToArray()) }).Build();
    private Host Environment => new(root);

    [Fact]
    public void Aes256_roundtrip_is_randomized_and_detects_tampering_and_wrong_purpose()
    {
        var cipher=new AesSecretCipher(Config);
        var first=cipher.Encrypt("012345","otp-delivery");
        var second=cipher.Encrypt("012345","otp-delivery");
        Assert.NotEqual(first,second); Assert.Equal("012345",cipher.Decrypt(first,"otp-delivery"));
        Assert.ThrowsAny<CryptographicException>(()=>cipher.Decrypt(first,"wrong-purpose"));
        var raw=Convert.FromBase64String(first[3..]);raw[^1]^=1;
        Assert.ThrowsAny<CryptographicException>(()=>cipher.Decrypt("v1:"+Convert.ToBase64String(raw),"otp-delivery"));
    }
    [Fact]
    public void Missing_aes_key_fails_closed()
    {
        var empty=new ConfigurationBuilder().Build();
        Assert.Throws<DependencyUnavailableException>(()=>new AesSecretCipher(empty).Encrypt("secret","purpose"));
    }
    [Fact]
    public void Password_hash_uses_bcrypt_cost_twelve()
    {
        var hasher=new PasswordHasher();var hash=hasher.Hash("Password123");
        Assert.Contains("$12$",hash);Assert.True(hasher.Verify("Password123",hash));Assert.False(hasher.Verify("Wrong123",hash));
    }
    [Fact]
    public void Jwt_is_signed_rs256_with_multiroles_tenant_session_and_twenty_four_hour_lifetime()
    {
        Directory.CreateDirectory(Path.Combine(root,"Keys"));using var rsa=RSA.Create(2048);
        File.WriteAllText(Path.Combine(root,"Keys","private.key"),rsa.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(Path.Combine(root,"Keys","public.key"),rsa.ExportSubjectPublicKeyInfoPem());
        var user=new User { Email="a@example.com",OwnerProfile=new OwnerProfile() };
        var raw=new JwtTokenGenerator(Config,Environment,TimeProvider.System).GenerateToken(user,["Driver","LotOwner"],"session-hash");
        var jwt=new JwtSecurityTokenHandler().ReadJwtToken(raw);
        Assert.Equal("RS256",jwt.Header.Alg);Assert.Equal(TimeSpan.FromHours(24),jwt.ValidTo-jwt.ValidFrom);
        Assert.Contains(jwt.Claims,c=>c.Type=="role"&&c.Value=="LotOwner");Assert.Contains(jwt.Claims,c=>c.Type=="OwnerProfileId");
        Assert.Contains(jwt.Claims,c=>c.Type=="sid"&&c.Value=="session-hash");
        new JwtSecurityTokenHandler().ValidateToken(raw,new TokenValidationParameters
        {
            ValidIssuer="ParkingManagement",ValidAudience="ParkingManagement.Clients",ValidateLifetime=true,
            IssuerSigningKey=new RsaSecurityKey(rsa.ExportParameters(false)),ValidAlgorithms=[SecurityAlgorithms.RsaSha256]
        },out _);
    }
    [Fact]
    public void Jwt_rejects_mismatched_rsa_keys()
    {
        Directory.CreateDirectory(Path.Combine(root,"Keys"));using var first=RSA.Create(2048);using var second=RSA.Create(2048);
        File.WriteAllText(Path.Combine(root,"Keys","private.key"),first.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(Path.Combine(root,"Keys","public.key"),second.ExportSubjectPublicKeyInfoPem());
        Assert.Throws<InvalidOperationException>(()=>new JwtTokenGenerator(Config,Environment,TimeProvider.System).GenerateToken(new User(),["Driver"]));
    }
    [Fact]
    public void Staff_jwt_contains_tenant_and_assigned_lots()
    {
        Directory.CreateDirectory(Path.Combine(root,"Keys"));using var rsa=RSA.Create(2048);
        File.WriteAllText(Path.Combine(root,"Keys","private.key"),rsa.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(Path.Combine(root,"Keys","public.key"),rsa.ExportSubjectPublicKeyInfoPem());
        var user=new User();user.StaffAssignments.Add(new StaffAssignment
            {OwnerProfileId=7,ParkingLotId=12,IsActive=true,OwnerProfile=new OwnerProfile()});
        var raw=new JwtTokenGenerator(Config,Environment,TimeProvider.System).GenerateToken(user,["Staff"],"session");
        var jwt=new JwtSecurityTokenHandler().ReadJwtToken(raw);
        Assert.Contains(jwt.Claims,c=>c.Type=="OwnerProfileId"&&c.Value=="7");
        Assert.Contains(jwt.Claims,c=>c.Type=="ParkingLotId"&&c.Value=="12");
    }
    [Theory]
    [InlineData("abc")]
    [InlineData("weak123")]
    [InlineData("1234567890")]
    public void Invalid_passwords_are_rejected(string password)=>Assert.Throws<ValidationException>(()=>IdentityRules.Password(password));
    [Theory]
    [InlineData("ABCDEF",false)]
    [InlineData("012345",true)]
    [InlineData("12345",false)]
    [InlineData("１２３４５６",false)]
    public void Otp_requires_six_ascii_digits(string value,bool valid)=>Assert.Equal(valid,IdentityRules.OtpFormat(value));
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
    private sealed class Host(string root):IHostEnvironment
    {
        public string EnvironmentName{get;set;}="Testing";public string ApplicationName{get;set;}="UserService.Tests";
        public string ContentRootPath{get;set;}=root;public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
}
