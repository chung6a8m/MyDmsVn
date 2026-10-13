using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MyDmsVn.Server.DbMigrator.SecurityProvisioning
{
    public enum PermissionCatalogDecision
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
    }

    public enum AdministratorGrantDecision
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
    }

    public enum PermissionSyncChangeKind
    {
        AddDefinition = 0,
        UpdateDefinition = 1,
        ActivateDefinition = 2,
        DeprecateDefinition = 3,
        UpdateApproval = 4,
        StageDefinition = 5,
        ReportUnregisteredDefinition = 6,
        CreateAdministratorRole = 7,
        GrantAdministratorPermission = 8,
        RecordAdministratorRevocation = 9,
        ReportManualAdministratorGrant = 10,
    }

    public sealed class PermissionSyncApproval
    {
        public PermissionSyncApproval(
            string permissionKey,
            PermissionCatalogDecision catalogDecision,
            AdministratorGrantDecision administratorGrantDecision)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                throw new ArgumentException("A permission key is required.", nameof(permissionKey));
            }

            if (!Enum.IsDefined(typeof(PermissionCatalogDecision), catalogDecision))
            {
                throw new ArgumentOutOfRangeException(nameof(catalogDecision));
            }

            if (!Enum.IsDefined(typeof(AdministratorGrantDecision), administratorGrantDecision))
            {
                throw new ArgumentOutOfRangeException(nameof(administratorGrantDecision));
            }

            PermissionKey = permissionKey;
            CatalogDecision = catalogDecision;
            AdministratorGrantDecision = administratorGrantDecision;
        }

        public string PermissionKey { get; }
        public PermissionCatalogDecision CatalogDecision { get; }
        public AdministratorGrantDecision AdministratorGrantDecision { get; }
    }

    public sealed class PermissionSyncApprovalSet
    {
        private readonly IReadOnlyDictionary<string, PermissionSyncApproval> _byKey;

        public PermissionSyncApprovalSet(
            string releaseId,
            string approvedBy,
            DateTime expiresAtUtc,
            IEnumerable<PermissionSyncApproval> approvals)
        {
            if (string.IsNullOrWhiteSpace(releaseId))
            {
                throw new ArgumentException("A release identifier is required.", nameof(releaseId));
            }

            if (releaseId.Length > 64)
            {
                throw new ArgumentException("The release identifier cannot exceed 64 characters.", nameof(releaseId));
            }

            if (string.IsNullOrWhiteSpace(approvedBy))
            {
                throw new ArgumentException("An approval actor is required.", nameof(approvedBy));
            }

            if (approvedBy.Length > 128)
            {
                throw new ArgumentException("The approval actor cannot exceed 128 characters.", nameof(approvedBy));
            }

            if (expiresAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Approval expiry must be UTC.", nameof(expiresAtUtc));
            }

            if (approvals == null)
            {
                throw new ArgumentNullException(nameof(approvals));
            }

            var byKey = new Dictionary<string, PermissionSyncApproval>(StringComparer.Ordinal);
            foreach (var approval in approvals)
            {
                if (approval == null)
                {
                    throw new ArgumentException("Approvals cannot contain null entries.", nameof(approvals));
                }

                if (byKey.ContainsKey(approval.PermissionKey))
                {
                    throw new ArgumentException(
                        $"Duplicate approval for permission '{approval.PermissionKey}'.",
                        nameof(approvals));
                }

                byKey.Add(approval.PermissionKey, approval);
            }

            ReleaseId = releaseId;
            ApprovedBy = approvedBy;
            ExpiresAtUtc = expiresAtUtc;
            _byKey = new ReadOnlyDictionary<string, PermissionSyncApproval>(byKey);
        }

        public string ReleaseId { get; }
        public string ApprovedBy { get; }
        public DateTime ExpiresAtUtc { get; }
        public IReadOnlyCollection<PermissionSyncApproval> Approvals =>
            new ReadOnlyCollection<PermissionSyncApproval>(_byKey.Values.ToArray());

        public bool TryGet(string permissionKey, out PermissionSyncApproval approval)
        {
            return _byKey.TryGetValue(permissionKey, out approval!);
        }
    }

    public sealed class PermissionSyncChange
    {
        internal PermissionSyncChange(PermissionSyncChangeKind kind, string permissionKey)
        {
            Kind = kind;
            PermissionKey = permissionKey;
        }

        public PermissionSyncChangeKind Kind { get; }
        public string PermissionKey { get; }
    }

    public sealed class PermissionSyncConflict
    {
        internal PermissionSyncConflict(string code, string permissionKey)
        {
            Code = code;
            PermissionKey = permissionKey;
        }

        public string Code { get; }
        public string PermissionKey { get; }
    }

    public sealed class PermissionSynchronizationPlan
    {
        internal PermissionSynchronizationPlan(
            string planId,
            string releaseId,
            Guid databaseIdentity,
            string targetDatabase,
            string stateFingerprint,
            string manifestFingerprint,
            string approvalFingerprint,
            IEnumerable<PermissionSyncChange> changes,
            IEnumerable<PermissionSyncConflict> conflicts)
        {
            PlanId = planId;
            ReleaseId = releaseId;
            DatabaseIdentity = databaseIdentity;
            TargetDatabase = targetDatabase;
            StateFingerprint = stateFingerprint;
            ManifestFingerprint = manifestFingerprint;
            ApprovalFingerprint = approvalFingerprint;
            Changes = new ReadOnlyCollection<PermissionSyncChange>(changes.ToArray());
            Conflicts = new ReadOnlyCollection<PermissionSyncConflict>(conflicts.ToArray());
        }

        public string PlanId { get; }
        public string ReleaseId { get; }
        public Guid DatabaseIdentity { get; }
        public string TargetDatabase { get; }
        public string StateFingerprint { get; }
        public string ManifestFingerprint { get; }
        public string ApprovalFingerprint { get; }
        public IReadOnlyList<PermissionSyncChange> Changes { get; }
        public IReadOnlyList<PermissionSyncConflict> Conflicts { get; }
    }

    public sealed class PermissionSynchronizationResult
    {
        internal PermissionSynchronizationResult(string planId)
        {
            PlanId = planId;
        }

        public string PlanId { get; }
    }

    public sealed class PermissionSynchronizationException : Exception
    {
        internal PermissionSynchronizationException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
