using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// R1 allowlist: these cases create only managed descriptors/byte arrays, never files or SQL.
public sealed class Web6OwnershipRecordMemoryTests
{
    private static SecurityIdentifier Owner => new SecurityIdentifier("S-1-5-21-100-200-300-1001");

    [Fact]
    public void Restoration_sections_are_exactly_owner_group_access_without_audit() =>
        Assert.Equal(AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access, SqlTestPrivateRecord.Sections);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Initial_descriptor_is_protected_owner_only_and_round_trips_exactly(bool directory)
    {
        var original = directory ? (FileSystemSecurity)SqlTestPrivateRecord.DirectoryAcl(Owner) : SqlTestPrivateRecord.FileAcl(Owner);
        var bytes = original.GetSecurityDescriptorBinaryForm();
        var restored = directory ? (FileSystemSecurity)SqlTestPrivateRecord.RestoreDirectory(bytes) : SqlTestPrivateRecord.RestoreFile(bytes);
        Assert.True(restored.AreAccessRulesProtected);
        Assert.Equal(Owner, restored.GetOwner(typeof(SecurityIdentifier)));
        Assert.Equal(Owner, restored.GetGroup(typeof(SecurityIdentifier)));
        var rule = Assert.Single(restored.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());
        Assert.Equal(Owner, rule.IdentityReference);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.Equal(FileSystemRights.FullControl, rule.FileSystemRights);
        Assert.Equal(bytes, restored.GetSecurityDescriptorBinaryForm());
        Assert.Equal(original.GetSecurityDescriptorSddlForm(SqlTestPrivateRecord.Sections), restored.GetSecurityDescriptorSddlForm(SqlTestPrivateRecord.Sections));
    }

    [Fact]
    public void Restoration_refuses_a_descriptor_containing_audit_sections()
    {
        var descriptor = new RawSecurityDescriptor("O:SYG:SYD:P(A;;FA;;;SY)S:(AU;SA;FA;;;WD)");
        var bytes = new byte[descriptor.BinaryLength]; descriptor.GetBinaryForm(bytes, 0);
        Assert.Throws<InvalidDataException>(() => SqlTestPrivateRecord.RestoreDirectory(bytes));
        Assert.Throws<InvalidDataException>(() => SqlTestPrivateRecord.RestoreFile(bytes));
    }

    [Fact]
    public void Seal_is_deterministic_canonical_and_verifies_all_payload_bytes()
    {
        const string record = "version=1\nkind=memory-probe\nutc=2026-11-09T00:00:00.0000000+00:00\nbytes=AAEC/w==\n";
        var one = SqlTestPrivateRecord.Seal(record);
        Assert.Equal(one, SqlTestPrivateRecord.Seal(record));
        Assert.Equal(record, SqlTestPrivateRecord.Validate(one));
        Assert.DoesNotContain((byte)'\r', one);
        var changed = (byte[])one.Clone(); changed[20] ^= 1;
        Assert.Throws<InvalidDataException>(() => SqlTestPrivateRecord.Validate(changed));
    }

    [Theory]
    [InlineData("version=1\r\nkind=probe\n")]
    [InlineData("version=1\nkind=probe")]
    [InlineData("version=1\nkind=probe\nkind=duplicate\n")]
    [InlineData("version=2\nkind=probe\n")]
    [InlineData("version=1\nsha256=injected\n")]
    public void Serialization_refuses_noncanonical_or_ambiguous_records(string record) =>
        Assert.Throws<InvalidDataException>(() => SqlTestPrivateRecord.Seal(record));

    [Theory]
    [InlineData("truncated")]
    [InlineData("trailing")]
    [InlineData("utf8")]
    public void Validation_refuses_truncated_trailing_or_invalid_UTF8(string corruption)
    {
        var valid = SqlTestPrivateRecord.Seal("version=1\nkind=probe\n");
        var bytes = corruption == "truncated" ? valid.Take(valid.Length - 1).ToArray() :
            corruption == "trailing" ? valid.Concat(Encoding.UTF8.GetBytes("trailing")).ToArray() : new byte[] { 0xff, 0xfe, 0x80 };
        Assert.Throws<InvalidDataException>(() => SqlTestPrivateRecord.Validate(bytes));
    }
}
