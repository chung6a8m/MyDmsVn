using System;
using System.Collections.Generic;
using MyDmsVn.Contracts;
using Newtonsoft.Json;
using Xunit;

namespace MyDmsVn.Server.Application.Tests
{
    public sealed class ContractEnvelopeTests
    {
        [Fact]
        public void Success_and_failure_factories_create_only_one_meaningful_branch()
        {
            var success = ApiResponse<string>.Success("ready");
            var error = new ApiError(
                ApiStatusCode.BadRequest,
                "ValidationError",
                "The request is invalid.");
            var failure = ApiResponse<string>.Failure(error);

            Assert.True(success.IsSuccess);
            Assert.Equal("ready", success.Data);
            Assert.Null(success.Error);
            Assert.False(failure.IsSuccess);
            Assert.Null(failure.Data);
            Assert.Same(error, failure.Error);
            Assert.Throws<ArgumentNullException>(() => ApiResponse<string>.Success(null!));
            Assert.Throws<ArgumentNullException>(() => ApiResponse<string>.Failure(null!));
        }

        [Fact]
        public void Pagination_and_no_content_have_explicit_non_null_success_values()
        {
            var page = new PagedResult<string>(new[] { "one", "two" }, 2, 25, 27);
            var noContent = ApiResponse<UnitResponse>.Success(UnitResponse.Value);

            Assert.Equal(2, page.PageNumber);
            Assert.Equal(25, page.PageSize);
            Assert.Equal(27, page.TotalCount);
            Assert.Equal(new[] { "one", "two" }, page.Items);
            Assert.True(noContent.IsSuccess);
            Assert.Same(UnitResponse.Value, noContent.Data);
        }

        [Fact]
        public void Serializer_uses_stable_camel_case_shape_and_omits_inactive_branch()
        {
            var response = ApiResponse<string>.Failure(
                new ApiError(
                    ApiStatusCode.BadRequest,
                    "ValidationError",
                    "The request is invalid.",
                    new List<ApiErrorDetail>
                    {
                        new ApiErrorDetail(
                            "Validation.Required",
                            "Quantity is required.",
                            "lines[0].quantity"),
                    }));

            var json = JsonConvert.SerializeObject(response, ApiJson.CreateSerializerSettings());

            Assert.Equal(
                "{\"error\":{\"code\":\"ValidationError\",\"message\":\"The request is invalid.\",\"details\":[{\"code\":\"Validation.Required\",\"message\":\"Quantity is required.\",\"field\":\"lines[0].quantity\"}]}}",
                json);
        }
    }
}
