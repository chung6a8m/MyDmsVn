using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.Application;

public enum PermissionCatalogChangeKind
{
    NotCompared,
    Unchanged,
    Added,
    Removed,
    Changed,
}

public sealed class PermissionCatalogComparisonItem
{
    public PermissionCatalogComparisonItem(
        string key,
        PermissionCatalogChangeKind changeKind,
        PermissionCatalogItemDto? current,
        PermissionCatalogItemDto? previous)
    {
        Key = key;
        ChangeKind = changeKind;
        Current = current;
        Previous = previous;
    }

    public string Key { get; }

    public PermissionCatalogChangeKind ChangeKind { get; }

    public PermissionCatalogItemDto? Current { get; }

    public PermissionCatalogItemDto? Previous { get; }
}

public sealed class PermissionCatalogComparisonResult
{
    public PermissionCatalogComparisonResult(
        bool hasPreviousSnapshot,
        IReadOnlyList<PermissionCatalogComparisonItem> items)
    {
        HasPreviousSnapshot = hasPreviousSnapshot;
        Items = items;
    }

    public bool HasPreviousSnapshot { get; }

    public IReadOnlyList<PermissionCatalogComparisonItem> Items { get; }
}

public static class PermissionCatalogSnapshotComparer
{
    public static PermissionCatalogComparisonResult Compare(
        PermissionCatalogDto current,
        PermissionCatalogDto? previous)
    {
        if (current == null)
        {
            throw new ArgumentNullException(nameof(current));
        }

        var currentByKey = current.Items.ToDictionary(item => item.Key, StringComparer.Ordinal);
        if (previous == null)
        {
            return new PermissionCatalogComparisonResult(
                false,
                new ReadOnlyCollection<PermissionCatalogComparisonItem>(current.Items
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new PermissionCatalogComparisonItem(
                        item.Key,
                        PermissionCatalogChangeKind.NotCompared,
                        item,
                        null))
                    .ToArray()));
        }

        var previousByKey = previous.Items.ToDictionary(item => item.Key, StringComparer.Ordinal);
        var keys = currentByKey.Keys.Concat(previousByKey.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal);
        var items = keys.Select(key =>
        {
            currentByKey.TryGetValue(key, out var currentItem);
            previousByKey.TryGetValue(key, out var previousItem);
            var change = currentItem == null
                ? PermissionCatalogChangeKind.Removed
                : previousItem == null
                    ? PermissionCatalogChangeKind.Added
                    : HasChanged(currentItem, previousItem)
                        ? PermissionCatalogChangeKind.Changed
                        : PermissionCatalogChangeKind.Unchanged;
            return new PermissionCatalogComparisonItem(key, change, currentItem, previousItem);
        }).ToArray();
        return new PermissionCatalogComparisonResult(
            true,
            new ReadOnlyCollection<PermissionCatalogComparisonItem>(items));
    }

    private static bool HasChanged(PermissionCatalogItemDto current, PermissionCatalogItemDto previous) =>
        !string.Equals(current.Module, previous.Module, StringComparison.Ordinal) ||
        !string.Equals(current.Description, previous.Description, StringComparison.Ordinal) ||
        !string.Equals(current.Status, previous.Status, StringComparison.Ordinal);
}
