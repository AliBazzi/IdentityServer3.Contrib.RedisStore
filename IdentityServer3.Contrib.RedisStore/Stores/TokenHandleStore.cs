using IdentityServer3.Contrib.RedisStore.Models;
using IdentityServer3.Core.Models;
using IdentityServer3.Core.Services;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace IdentityServer3.Contrib.RedisStore.Stores
{
    public class TokenHandleStore : BaseTokenStore<Token>, ITokenHandleStore
    {
        public TokenHandleStore(IDatabase database, IScopeStore scopeStore, IClientStore clientStore, IRedisKeyFactory redisKeyFactory)
            : base(database, TokenType.TokenHandle, scopeStore, clientStore, redisKeyFactory)
        { }

        public override async Task StoreAsync(string key, Token tokenHandle)
        {
            var json = ConvertToJson(tokenHandle);
            var expiresIn = new TimeSpan(0, 0, tokenHandle.Lifetime);
            await base.StoreAsync(key, json, tokenHandle, expiresIn);
        }
    }
}
