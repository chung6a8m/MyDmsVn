using System;
using System.IO;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
internal sealed class Web5OriginalFirstState
{
    private enum Phase { New, Armed, Writer, Operator, Released, Complete }
    private Phase _phase; private Guid _id; private int _writer;
    internal bool IsComplete => _phase == Phase.Complete;
    internal void Arm(Guid id) { if (_phase != Phase.New || id == Guid.Empty) throw Invalid(); _id = id; _phase = Phase.Armed; }
    internal void WriterObserved(Guid id, int session) { Check(id, Phase.Armed); if (session <= 0) throw Invalid(); _writer = session; _phase = Phase.Writer; }
    internal void OperatorObserved(Guid id, int session) { Check(id, Phase.Writer); if (session <= 0 || session == _writer) throw Invalid(); _phase = Phase.Operator; }
    internal void Release(Guid id) { Check(id, Phase.Operator); _phase = Phase.Released; }
    internal void Complete(Guid id) { Check(id, Phase.Released); _phase = Phase.Complete; }
    private void Check(Guid id, Phase phase) { if (_phase != phase || id != _id) throw Invalid(); }
    private static InvalidDataException Invalid() => new InvalidDataException("Invalid original-first fixture state.");
}
