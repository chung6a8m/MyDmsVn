namespace MyDmsVn.Contracts
{
    public sealed class FoundationStatus
    {
        public FoundationStatus(bool isReady, string runtime)
        {
            IsReady = isReady;
            Runtime = runtime;
        }

        public bool IsReady { get; }

        public string Runtime { get; }
    }
}
