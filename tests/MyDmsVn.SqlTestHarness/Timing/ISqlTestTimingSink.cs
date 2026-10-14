namespace MyDmsVn.SqlTestHarness.Timing;

public interface ISqlTestTimingSink
{
    void Write(SqlTestTimingEvent timingEvent);
}
