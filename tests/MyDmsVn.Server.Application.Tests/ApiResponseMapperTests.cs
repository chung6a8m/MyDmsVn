using System.Collections.Generic;
using ErrorOr;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using Newtonsoft.Json;
using Xunit;

namespace MyDmsVn.Server.Application.Tests
{
    public sealed class ApiResponseMapperTests
    {
        [Fact]
        public void Success_maps_the_typed_value()
        {
            ErrorOr<string> result = "ready";

            var response = ApiResponseMapper.Map(result);

            Assert.True(response.IsSuccess);
            Assert.Equal("ready", response.Data);
        }

        [Theory]
        [InlineData(ErrorType.Validation, ApiStatusCode.BadRequest, "ValidationError")]
        [InlineData(ErrorType.Unauthorized, ApiStatusCode.Unauthorized, "Auth.Unauthorized")]
        [InlineData(ErrorType.Forbidden, ApiStatusCode.Forbidden, "Auth.Forbidden")]
        [InlineData(ErrorType.NotFound, ApiStatusCode.NotFound, "Product.NotFound")]
        [InlineData(ErrorType.Conflict, ApiStatusCode.Conflict, "Product.CodeAlreadyExists")]
        public void Known_error_types_map_to_transport_neutral_status_metadata(
            ErrorType type,
            ApiStatusCode expectedStatus,
            string expectedCode)
        {
            var error = CreateError(type);
            ErrorOr<string> result = error;

            var response = ApiResponseMapper.Map(result);

            Assert.False(response.IsSuccess);
            Assert.Equal(expectedStatus, response.Error!.Status);
            Assert.Equal(expectedCode, response.Error.Code);
        }

        [Fact]
        public void Validation_field_paths_follow_the_json_naming_policy()
        {
            var metadata = new Dictionary<string, object>
            {
                ["Field"] = "OrderLines[0].UnitCost",
            };
            ErrorOr<string> result = Error.Validation(
                "Validation.NonNegative",
                "Unit cost must be nonnegative.",
                metadata);

            var response = ApiResponseMapper.Map(result);

            Assert.Equal("orderLines[0].unitCost", response.Error!.Details[0].Field);
        }

        [Fact]
        public void Mixed_error_precedence_is_independent_of_enumeration_order()
        {
            var validation = Error.Validation("Validation.Required", "Required.");
            var conflict = Error.Conflict("Product.CodeAlreadyExists", "Duplicate.");

            var first = ApiResponseMapper.Map(
                ErrorOrFactory.From<string>(new List<Error> { validation, conflict }));
            var second = ApiResponseMapper.Map(
                ErrorOrFactory.From<string>(new List<Error> { conflict, validation }));

            Assert.Equal(ApiStatusCode.Conflict, first.Error!.Status);
            Assert.Equal(first.Error.Status, second.Error!.Status);
            Assert.Equal(first.Error.Code, second.Error.Code);
            Assert.Equal(2, first.Error.Details.Count);
            Assert.Equal(2, second.Error.Details.Count);
        }

        [Fact]
        public void Unexpected_errors_do_not_expose_internal_details()
        {
            ErrorOr<string> result = Error.Unexpected(
                "Sql.Exception",
                "Server=production;Password=secret");

            var response = ApiResponseMapper.Map(result);

            Assert.Equal(ApiStatusCode.InternalServerError, response.Error!.Status);
            Assert.Equal("InternalError", response.Error.Code);
            Assert.DoesNotContain("secret", response.Error.Message);
            Assert.Empty(response.Error.Details);
        }

        [Fact]
        public void Unknown_error_types_do_not_expose_internal_details_alone_or_in_mixed_sets()
        {
            var custom = Error.Custom(
                99,
                "Sql.Custom",
                "Server=production;Password=secret");
            var conflict = Error.Conflict("Product.Duplicate", "Duplicate.");

            var alone = ApiResponseMapper.Map<string>(custom);
            var mixed = ApiResponseMapper.Map(
                ErrorOrFactory.From<string>(new List<Error> { conflict, custom }));

            AssertSafeInternalError(alone);
            AssertSafeInternalError(mixed);
        }

        [Theory]
        [InlineData(ErrorType.Unauthorized, ApiStatusCode.Unauthorized, "Auth.Unauthorized")]
        [InlineData(ErrorType.Forbidden, ApiStatusCode.Forbidden, "Auth.Forbidden")]
        public void Authorization_errors_do_not_expose_private_details(
            ErrorType type,
            ApiStatusCode expectedStatus,
            string expectedCode)
        {
            var authorizationError = type == ErrorType.Unauthorized
                ? Error.Unauthorized("Auth.PrivatePermission", "secret authorization detail")
                : Error.Forbidden("Auth.PrivatePermission", "secret authorization detail");
            var validation = Error.Validation("Validation.Required", "Public validation detail.");
            var result = ErrorOrFactory.From<string>(
                new List<Error> { validation, authorizationError });

            var response = ApiResponseMapper.Map(result);
            var json = JsonConvert.SerializeObject(
                response,
                ApiJson.CreateSerializerSettings());

            Assert.Equal(expectedStatus, response.Error!.Status);
            Assert.Equal(expectedCode, response.Error.Code);
            Assert.Empty(response.Error.Details);
            Assert.DoesNotContain("PrivatePermission", json);
            Assert.DoesNotContain("secret", json);
            Assert.DoesNotContain("validation detail", json);
        }

        [Fact]
        public void Invalid_credentials_keep_the_safe_public_login_code_and_message()
        {
            ErrorOr<string> result = Error.Unauthorized(
                "Auth.InvalidCredentials",
                "The username or password is invalid.");

            var response = ApiResponseMapper.Map(result);

            Assert.Equal(ApiStatusCode.Unauthorized, response.Error!.Status);
            Assert.Equal("Auth.InvalidCredentials", response.Error.Code);
            Assert.Equal("The username or password is invalid.", response.Error.Message);
            Assert.Empty(response.Error.Details);
        }

        [Theory]
        [InlineData("Code", "code")]
        [InlineData("Request.Code", "code")]
        [InlineData("code", "code")]
        [InlineData("UnknownField", "unknownField")]
        [InlineData(null, null)]
        public void Conflict_field_is_explicit_and_follows_json_policy(string? field, string? expected)
        {
            var metadata = new Dictionary<string, object>();
            if (field != null) metadata["Field"] = field;
            var response = ApiResponseMapper.Map<string>(
                Error.Conflict("Catalog.Conflict", "Conflict.", metadata), "Request");
            Assert.Equal(ApiStatusCode.Conflict, response.Error!.Status);
            Assert.Equal(expected, Assert.Single(response.Error.Details).Field);
        }

        [Fact]
        public void Authorization_dominates_explicit_duplicate_code_metadata_without_leaking_fields()
        {
            var response = ApiResponseMapper.Map(ErrorOrFactory.From<string>(new List<Error>
            {
                Error.Conflict("Product.DuplicateCode", "Duplicate.",
                    new Dictionary<string, object> { ["Field"] = "Code" }),
                Error.Forbidden("Private.Permission", "Private detail."),
            }));
            Assert.Equal(ApiStatusCode.Forbidden, response.Error!.Status);
            Assert.Empty(response.Error.Details);
            Assert.Equal("The operation is not permitted.", response.Error.Message);
        }

        private static void AssertSafeInternalError(ApiResponse<string> response)
        {
            Assert.Equal(ApiStatusCode.InternalServerError, response.Error!.Status);
            Assert.Equal("InternalError", response.Error.Code);
            Assert.DoesNotContain("secret", response.Error.Message);
            Assert.Empty(response.Error.Details);
        }

        private static Error CreateError(ErrorType type)
        {
            return type switch
            {
                ErrorType.Validation => Error.Validation("Validation.Required", "Required."),
                ErrorType.Unauthorized => Error.Unauthorized("Ignored.Code", "Unauthorized."),
                ErrorType.Forbidden => Error.Forbidden("Ignored.Code", "Forbidden."),
                ErrorType.NotFound => Error.NotFound("Product.NotFound", "Missing."),
                ErrorType.Conflict => Error.Conflict("Product.CodeAlreadyExists", "Duplicate."),
                _ => Error.Unexpected("Internal", "Internal."),
            };
        }
    }
}
