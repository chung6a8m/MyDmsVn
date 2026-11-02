using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class WebOperationResolutionEndpointTests
{
    [Theory]
    [InlineData("context?actor=1", null)]
    [InlineData("context/", null)]
    [InlineData("context", "{}")]
    [InlineData("not-a-guid/resolution", null)]
    [InlineData("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee/resolution", null)]
    public async Task Versioned_reads_reject_malformed_input_before_storage(string path, string? body)
    {
        using var host = new AuthFixture(web5Enabled: true);
        using var login = await host.Login();
        var access = (string)JObject.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthFixture.Origin + "/api/v2/operations/" + path);
        request.Headers.Add("Authorization", "Bearer " + access);
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await host.Server.HttpClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ValidationError", (string?)JObject.Parse(await response.Content.ReadAsStringAsync())["error"]!["code"]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Existing_modes_do_not_expose_the_extension(bool web4)
    {
        using var host = new AuthFixture(web4Enabled: web4);
        using var login = await host.Login();
        var access = (string)JObject.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthFixture.Origin + "/api/v2/operations/context");
        request.Headers.Add("Authorization", "Bearer " + access);
        using var response = await host.Server.HttpClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
