using System;
using System.Collections.Generic;
using ErrorOr;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
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

        [Fact]
        public void Serializer_round_trips_success_failure_and_value_type_envelopes()
        {
            var settings = ApiJson.CreateSerializerSettings();

            var success = JsonConvert.DeserializeObject<ApiResponse<string>>(
                "{\"data\":\"ready\"}",
                settings)!;
            var failure = JsonConvert.DeserializeObject<ApiResponse<string>>(
                "{\"error\":{\"code\":\"ValidationError\",\"message\":\"Invalid.\",\"details\":[]}}",
                settings)!;
            var valueType = JsonConvert.DeserializeObject<ApiResponse<int>>(
                "{\"data\":0}",
                settings)!;

            Assert.True(success.IsSuccess);
            Assert.Equal("ready", success.Data);
            Assert.False(failure.IsSuccess);
            Assert.Equal("ValidationError", failure.Error!.Code);
            Assert.True(valueType.IsSuccess);
            Assert.Equal(0, valueType.Data);
        }

        [Theory]
        [InlineData(ErrorType.Unauthorized, ApiStatusCode.Unauthorized)]
        [InlineData(ErrorType.Forbidden, ApiStatusCode.Forbidden)]
        [InlineData(ErrorType.NotFound, ApiStatusCode.NotFound)]
        [InlineData(ErrorType.Conflict, ApiStatusCode.Conflict)]
        [InlineData(ErrorType.Unexpected, ApiStatusCode.InternalServerError)]
        public void Http_deserialization_restores_transport_status_metadata(
            ErrorType type,
            ApiStatusCode expectedStatus)
        {
            var local = ApiResponseMapper.Map<string>(CreateError(type));
            var json = JsonConvert.SerializeObject(
                local,
                ApiJson.CreateSerializerSettings());

            var http = ApiJson.DeserializeResponse<string>(json, expectedStatus);

            Assert.Equal(local.IsSuccess, http.IsSuccess);
            Assert.Equal(local.Error!.Code, http.Error!.Code);
            Assert.Equal(local.Error.Status, http.Error.Status);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("{\"data\":\"ready\",\"error\":{\"code\":\"Failure\",\"message\":\"Failed.\"}}")]
        [InlineData("{\"data\":null}")]
        [InlineData("{\"error\":null}")]
        public void Serializer_rejects_ambiguous_or_empty_envelopes(string json)
        {
            Assert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<ApiResponse<string>>(
                    json,
                    ApiJson.CreateSerializerSettings()));
        }

        [Fact]
        public void Pagination_decimal_and_utc_values_have_a_stable_wire_shape()
        {
            var page = new PagedResult<StockBalanceRow>(
                new[]
                {
                    new StockBalanceRow(2, 7, "P-007", "Product 7", 12.3400m),
                },
                1,
                25,
                1);
            var response = ApiResponse<PagedResult<StockBalanceRow>>.Success(page);

            var json = JsonConvert.SerializeObject(response, ApiJson.CreateSerializerSettings());

            Assert.Equal(
                "{\"data\":{\"items\":[{\"warehouseId\":2,\"productId\":7,\"productCode\":\"P-007\",\"productName\":\"Product 7\",\"quantity\":12.3400}],\"pageNumber\":1,\"pageSize\":25,\"totalCount\":1}}",
                json);
        }

        [Fact]
        public void Planned_p5_command_dtos_have_stable_golden_json()
        {
            var settings = ApiJson.CreateSerializerSettings();

            Assert.Equal(
                "{\"code\":\"P-001\",\"name\":\"Product\",\"unit\":\"EA\"}",
                JsonConvert.SerializeObject(
                    new SaveProductRequest("P-001", "Product", "EA"),
                    settings));
            Assert.Equal(
                "{\"code\":\"W-001\",\"name\":\"Main\",\"address\":\"Bangkok\"}",
                JsonConvert.SerializeObject(
                    new SaveWarehouseRequest("W-001", "Main", "Bangkok"),
                    settings));
            Assert.Equal(
                "{\"code\":\"E-001\",\"name\":\"Operator\",\"phone\":\"0123\",\"userId\":9}",
                JsonConvert.SerializeObject(
                    new SaveEmployeeRequest("E-001", "Operator", "0123", 9),
                    settings));
            Assert.Equal(
                "{\"code\":\"C-001\",\"name\":\"Customer\",\"address\":\"Hanoi\",\"phone\":\"0456\",\"taxCode\":\"TAX-1\"}",
                JsonConvert.SerializeObject(
                    new SaveCustomerRequest("C-001", "Customer", "Hanoi", "0456", "TAX-1"),
                    settings));

            var receipt = new SaveGoodsReceiptRequest(
                new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
                2,
                3,
                "Initial stock",
                new[] { new SaveGoodsReceiptLineRequest(7, 2.5000m, 19.9900m) });
            Assert.Equal(
                "{\"receiptDate\":\"2026-10-08\",\"warehouseId\":2,\"employeeId\":3,\"note\":\"Initial stock\",\"lines\":[{\"productId\":7,\"quantity\":2.5000,\"unitCost\":19.9900}]}",
                JsonConvert.SerializeObject(receipt, settings));
        }

        [Theory]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void Receipt_date_preserves_the_calendar_day_for_every_datetime_kind(DateTimeKind kind)
        {
            var request = new SaveGoodsReceiptRequest(
                new DateTime(2026, 10, 8, 0, 0, 0, kind),
                2,
                3,
                null,
                Array.Empty<SaveGoodsReceiptLineRequest>());

            var json = JsonConvert.SerializeObject(request, ApiJson.CreateSerializerSettings());
            var roundTrip = JsonConvert.DeserializeObject<SaveGoodsReceiptRequest>(
                json,
                ApiJson.CreateSerializerSettings())!;

            Assert.Contains("\"receiptDate\":\"2026-10-08\"", json);
            Assert.Equal(new DateTime(2026, 10, 8), roundTrip.ReceiptDate);
            Assert.Equal(DateTimeKind.Unspecified, roundTrip.ReceiptDate.Kind);
        }

        [Fact]
        public void Posted_timestamp_is_serialized_as_utc_iso_8601()
        {
            var response = new PostGoodsReceiptResponse(
                42,
                "Posted",
                new DateTime(2026, 10, 8, 1, 2, 3, DateTimeKind.Utc));

            var json = JsonConvert.SerializeObject(response, ApiJson.CreateSerializerSettings());

            Assert.Equal(
                "{\"receiptId\":42,\"status\":\"Posted\",\"postedAtUtc\":\"2026-10-08T01:02:03Z\"}",
                json);
        }

        private static Error CreateError(ErrorType type)
        {
            return type switch
            {
                ErrorType.Unauthorized => Error.Unauthorized("Auth.Private", "Private."),
                ErrorType.Forbidden => Error.Forbidden("Auth.Private", "Private."),
                ErrorType.NotFound => Error.NotFound("Product.NotFound", "Missing."),
                ErrorType.Conflict => Error.Conflict("Product.Duplicate", "Duplicate."),
                _ => Error.Unexpected("Internal.Private", "Private."),
            };
        }
    }
}
