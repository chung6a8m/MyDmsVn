namespace MyDmsVn.Contracts
{
    public sealed class UnitResponse
    {
        private UnitResponse()
        {
        }

        public static UnitResponse Value { get; } = new UnitResponse();
    }
}
