using MyDmsVn.Server.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Identity;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests
{
    public sealed class BcryptPasswordHasherTests
    {
        [Fact]
        public void Hash_round_trip_accepts_only_the_original_password()
        {
            var hasher = new BcryptPasswordHasher(10);

            var passwordHash = hasher.Hash("A-strong-password-123!");

            Assert.Equal("BCrypt", hasher.Algorithm);
            Assert.NotEqual("A-strong-password-123!", passwordHash);
            Assert.True(hasher.Verify("A-strong-password-123!", passwordHash));
            Assert.False(hasher.Verify("wrong-password", passwordHash));
        }

        [Fact]
        public void Malformed_hash_is_rejected_without_throwing()
        {
            var hasher = new BcryptPasswordHasher(10);

            Assert.False(hasher.Verify("A-strong-password-123!", "not-a-bcrypt-hash"));
        }

        [Fact]
        public void Hash_from_lower_work_factor_is_marked_for_rehash()
        {
            var oldHasher = new BcryptPasswordHasher(10);
            var currentHasher = new BcryptPasswordHasher(11);
            var oldHash = oldHasher.Hash("A-strong-password-123!");

            Assert.True(currentHasher.NeedsRehash(oldHash));
            Assert.False(oldHasher.NeedsRehash(oldHash));
        }

        [Fact]
        public void Sql_persistence_registers_the_current_password_hasher()
        {
            var services = new ServiceCollection();
            services.AddSqlPersistence(
                "Server=invalid.invalid;Database=MyDmsVn;User ID=test;Password=test;Encrypt=false");

            using var provider = services.BuildServiceProvider();

            Assert.IsType<BcryptPasswordHasher>(provider.GetRequiredService<IPasswordHasher>());
        }

        [Fact]
        public void Passwords_past_the_bcrypt_utf8_boundary_are_rejected_instead_of_truncated()
        {
            var hasher = new BcryptPasswordHasher(10);
            var seventyTwoBytes = new string('a', 72);
            var hash = hasher.Hash(seventyTwoBytes);

            Assert.True(hasher.Verify(seventyTwoBytes, hash));
            Assert.False(hasher.Verify(seventyTwoBytes + "different-suffix", hash));
            Assert.Throws<System.ArgumentException>(() => hasher.Hash(seventyTwoBytes + "x"));
        }

        [Fact]
        public void Multibyte_passwords_use_utf8_byte_count_for_the_bcrypt_boundary()
        {
            var hasher = new BcryptPasswordHasher(10);
            var seventyTwoBytes = new string('\u00e9', 36);
            var hash = hasher.Hash(seventyTwoBytes);

            Assert.True(hasher.Verify(seventyTwoBytes, hash));
            Assert.False(hasher.Verify(seventyTwoBytes + "x", hash));
            Assert.Throws<System.ArgumentException>(() => hasher.Hash(new string('\u00e9', 37)));
        }
    }
}
