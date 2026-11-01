using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class WebOperationStatusEndpointTests
{
    [Theory]
    [InlineData("operations/context?actor=1", null)]
    [InlineData("operations/context", "{}")]
    [InlineData("operations/context/", null)]
    [InlineData("operations/not-a-guid", null)]
    [InlineData("operations/aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee", null)]
    public async Task Protected_read_rejects_invalid_route_body_and_missing_identity_before_storage(string path, string? body)
    {
        using var host = new AuthFixture(web4Enabled: true);
        using var login = await host.Login();
        var access = (string)JObject.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthFixture.Origin + "/api/v1/" + path);
        request.Headers.Add("Authorization", "Bearer " + access);
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await host.Server.HttpClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ValidationError", (string?)JObject.Parse(await response.Content.ReadAsStringAsync())["error"]!["code"]);
        // No persistence registered: malformed requests cannot touch storage.
    }

    [Fact]
    public async Task Legacy_host_does_not_expose_operation_context()
    {
        using var host = new AuthFixture();
        using var login = await host.Login();
        var access = (string)JObject.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthFixture.Origin + "/api/v1/operations/context");
        request.Headers.Add("Authorization", "Bearer " + access);
        using var response = await host.Server.HttpClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(JObject.Parse(await response.Content.ReadAsStringAsync())["data"]);
    }
}
