using System.Collections.Generic;
using ErrorOr;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
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
