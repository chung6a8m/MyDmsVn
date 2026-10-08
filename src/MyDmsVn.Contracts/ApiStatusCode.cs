namespace MyDmsVn.Contracts
{
    public enum ApiStatusCode
    {
        Ok = 200,
        NoContent = 204,
        BadRequest = 400,
        Unauthorized = 401,
        Forbidden = 403,
        NotFound = 404,
        Conflict = 409,
        InternalServerError = 500,
    }
}
