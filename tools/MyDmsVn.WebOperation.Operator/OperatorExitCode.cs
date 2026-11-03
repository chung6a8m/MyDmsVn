namespace MyDmsVn.WebOperation.Operator;

public enum OperatorExitCode
{
    Completed = 0,
    InvalidArguments = 2,
    Denied = 3,
    Indeterminate = 4,
    EnvironmentFailure = 5,
    Timeout = 6,
    Preview = 7,
    Unknown = 8,
    Cancelled = 130
}
