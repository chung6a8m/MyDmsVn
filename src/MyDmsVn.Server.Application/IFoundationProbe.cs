using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application
{
    public interface IFoundationProbe
    {
        FoundationStatus GetStatus();
    }
}
