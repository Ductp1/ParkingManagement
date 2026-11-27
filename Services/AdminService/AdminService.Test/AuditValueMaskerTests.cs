using System.Text.Json.Nodes;
using AdminService.Application.Features.AuditLogs;

namespace AdminService.Test;

// US-100: che dữ liệu nhạy cảm trong OldValuesJson / NewValuesJson (hàm thuần, không cần database).
public class AuditValueMaskerTests
{
    private static string Masked(string json) => AuditValueMasker.Mask(JsonNode.Parse(json))!.ToJsonString();

    [Theory]
    [InlineData("password")]
    [InlineData("PasswordHash")]
    [InlineData("new_password")]
    [InlineData("accessToken")]
    [InlineData("refresh-token")]
    [InlineData("bookingToken")]
    [InlineData("clientSecret")]
    [InlineData("apiKey")]
    [InlineData("OTP")]
    [InlineData("pin")]
    [InlineData("cvv")]
    public void Credentials_are_fully_hidden(string key)
    {
        Assert.Equal($"{{\"{key}\":\"***\"}}", Masked($"{{\"{key}\":\"s3cr3t-value-123\"}}"));
    }

    [Theory]
    [InlineData("payoutBankAccount", "\"0123456789\"", "****6789")]     // US-116: PartnerApplicationAudit.Snapshot
    [InlineData("bank_account_number", "\"9704 1234 5678\"", "****5678")]
    [InlineData("accountNumber", "123456789012", "****9012")]           // số JSON cũng bị che
    [InlineData("cardNumber", "\"4111111111111111\"", "****1111")]
    [InlineData("cccd", "\"079203001234\"", "****1234")]
    [InlineData("nationalId", "\"001099012345\"", "****2345")]
    [InlineData("passportNo", "\"C1234567\"", "****4567")]
    [InlineData("cmnd", "\"1234\"", "***")]                             // ≤ 4 ký tự → che hết, không lộ cả giá trị
    [InlineData("bankAccount", "true", "***")]
    public void Account_and_identity_numbers_keep_last_four(string key, string value, string expected)
    {
        Assert.Equal($"{{\"{key}\":\"{expected}\"}}", Masked($"{{\"{key}\":{value}}}"));
    }

    [Fact]
    public void Non_sensitive_fields_are_kept_including_phone_email_and_tax_code()
    {
        const string json = "{\"status\":\"Approved\",\"phoneNumber\":\"0901234567\",\"email\":\"a@b.vn\",\"taxCode\":\"0312345678\","
                            + "\"shippingFee\":15000,\"opinion\":\"ok\",\"tokenCount\":null}";

        Assert.Equal(json, Masked(json));   // "shipping" không bị coi là "pin"; giá trị null giữ nguyên null
    }

    [Fact]
    public void Masking_applies_inside_nested_objects_and_arrays()
    {
        var masked = Masked("{\"owner\":{\"name\":\"An\",\"bank\":{\"accountNumber\":\"0123456789\"}},"
                            + "\"staff\":[{\"userId\":3,\"password\":\"x\"},{\"userId\":4}]}");

        Assert.Equal("{\"owner\":{\"name\":\"An\",\"bank\":{\"accountNumber\":\"****6789\"}},"
                     + "\"staff\":[{\"userId\":3,\"password\":\"***\"},{\"userId\":4}]}", masked);
    }

    [Fact]
    public void Sensitive_key_holding_an_object_is_hidden_as_a_whole()
    {
        Assert.Equal("{\"credentials\":\"***\"}", Masked("{\"credentials\":{\"user\":\"a\",\"pass\":\"b\"}}"));
    }

    [Fact]
    public void Masking_does_not_modify_the_source_node()
    {
        var source = JsonNode.Parse("{\"password\":\"x\",\"nested\":{\"cccd\":\"079203001234\"}}")!;

        _ = AuditValueMasker.Mask(source);

        Assert.Equal("{\"password\":\"x\",\"nested\":{\"cccd\":\"079203001234\"}}", source.ToJsonString());
    }

    [Theory]
    [InlineData("[1,2]", "[1,2]")]
    [InlineData("\"plain\"", "\"plain\"")]
    [InlineData("42", "42")]
    public void Non_object_roots_are_returned_as_is(string json, string expected)
    {
        Assert.Equal(expected, Masked(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Parse_empty_returns_null(string? json)
    {
        Assert.Null(AuditValueMasker.Parse(json));
    }

    [Fact]
    public void Parse_invalid_json_hides_the_value()
    {
        Assert.Equal("\"***\"", AuditValueMasker.Parse("{not json")!.ToJsonString());
    }
}
