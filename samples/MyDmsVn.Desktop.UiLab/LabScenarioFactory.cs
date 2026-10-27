using System;
using MyDmsVn.Desktop.UiLab.Scenarios;

namespace MyDmsVn.Desktop.UiLab;

// Internal observer is diagnostic only; a listener cannot affect graph creation.
internal static class LabScenarioFactory
{
    internal static event Action<object>? Created;
    internal static SalesScenarioData CreateSales(SalesScenarioId id) => Report(SalesScenarioData.Create(id));
    internal static SecurityScenario CreateSecurity(SecurityScenarioId id) => Report(SecurityScenarioCatalog.Create(id));
    private static T Report<T>(T instance) where T : class
    {
        var listeners = Created;
        if (listeners != null)
            foreach (Action<object> listener in listeners.GetInvocationList())
                try { listener(instance); } catch (Exception) { }
        return instance;
    }
}
