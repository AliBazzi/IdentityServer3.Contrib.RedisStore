using IdentityServer3.Core.Models;
using IdentityServer3.Core.Services;
using System.Threading.Tasks;
using StackExchange.Redis;
using System;
using IdentityServer3.Contrib.RedisStore.Models;

namespace IdentityServer3.Contrib.RedisStore.Stores
{
    public class RefreshTokenStore : BaseTokenStore<RefreshToken>, IRefreshTokenStore
    {
        public RefreshTokenStore(IDatabase database, IScopeStore scopeStore, IClientStore clientStore)
            : base(database, TokenType.RefreshToken, scopeStore, clientStore)
        { }

        public override async Task StoreAsync(string key, RefreshToken refreshToken)
        {
            var json = ConvertToJson(refreshToken);
            var expiresIn = refreshToken.CreationTime.UtcDateTime.AddSeconds(refreshToken.LifeTime) - DateTimeOffset.UtcNow;
            await base.StoreAsync(key, json, refreshToken, expiresIn);
        }
    }
}
