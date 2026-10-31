using System.Net;
using System.Net.Http;
using System.Text;
using MyDmsVn.Contracts;
using Newtonsoft.Json;

namespace MyDmsVn.Server.HttpHost;

public static class HttpJson
{
    public static HttpResponseMessage Success<T>(T data) => From(ApiResponse<T>.Success(data));
    public static HttpResponseMessage From<T>(ApiResponse<T> result)
    {
        return new ApiHttpResponseMessage(result.IsSuccess ? HttpStatusCode.OK : (HttpStatusCode)result.Error!.Status)
        {
            Content = new StringContent(JsonConvert.SerializeObject(result, ApiJson.CreateSerializerSettings()), Encoding.UTF8, "application/json"),
        };
    }
    public static HttpResponseMessage Error(int status, string code, string message, string correlationId) =>
        From(ApiResponse<object>.Failure(new ApiError((ApiStatusCode)status, code, message, correlationId: correlationId)));
}
internal sealed class ApiHttpResponseMessage : HttpResponseMessage
{
    public ApiHttpResponseMessage(HttpStatusCode status) : base(status) { }
}
