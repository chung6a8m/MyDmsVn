using System;
using System.IO;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal enum Web5OriginalFirstStage
{
    ArmStarted, ArmCompleted, WaitStarted, WriterObserved, OperatorObserved,
    WaitCompleted, ReleaseStarted, ReleaseCompleted, CleanupStarted, CleanupCompleted,
    ControlFailed, CleanupFailed, ArmReplySent, WaitReplySent, ReleaseReplySent,
}

// Only fixed stage names are emitted; no frame, operation, SQL, exception or token data.
internal static class Web5OriginalFirstTelemetry
{
    internal static void Write(TextWriter output, Web5OriginalFirstStage stage)
    {
        if (!Enum.IsDefined(typeof(Web5OriginalFirstStage), stage)) throw new InvalidDataException("Unknown fixture stage.");
        output.WriteLine("WEB5_ORIGINAL_FIRST stage=" + stage);
    }
    internal static void Record(Web5OriginalFirstStage stage) => Write(Console.Out, stage);
}
