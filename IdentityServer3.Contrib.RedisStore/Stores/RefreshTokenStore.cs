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
        public RefreshTokenStore(IDatabaseAsync database, IScopeStore scopeStore, IClientStore clientStore)
            : base(database, TokenType.RefreshToken, scopeStore, clientStore)
        { }

        public override async Task StoreAsync(string key, RefreshToken refreshToken)
        {
            var token = new Models.Token
            {
                Key = key,
                CId = refreshToken.ClientId,
                SId = refreshToken.SubjectId,
                Content = ConvertToJson(refreshToken),
                Exp = refreshToken.CreationTime.AddSeconds(refreshToken.LifeTime)
            };
            var json = TokenToJson(token);
            var expiresIn = refreshToken.CreationTime.UtcDateTime.AddSeconds(refreshToken.LifeTime) - DateTimeOffset.UtcNow;
            await this.database.StringSetAsync(GetKey(key), json, expiresIn);
            await AddToHashSet(key, refreshToken, json, expiresIn);
            await AddToSet(key, refreshToken, expiresIn);
        }
    }
}
