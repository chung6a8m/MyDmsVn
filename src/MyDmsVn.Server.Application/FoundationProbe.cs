using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application
{
    internal sealed class FoundationProbe : IFoundationProbe
    {
        public FoundationStatus GetStatus()
        {
            return new FoundationStatus(true, "Local");
        }
    }
}
