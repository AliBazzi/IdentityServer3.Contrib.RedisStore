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
        public TokenHandleStore(IDatabase database, IScopeStore scopeStore, IClientStore clientStore)
            : base(database, TokenType.TokenHandle, scopeStore, clientStore)
        { }

        public override async Task StoreAsync(string key, Token tokenHandle)
        {
            var token = new RedisStuct
            {
                Content = ConvertToJson(tokenHandle),
                Exp = DateTimeOffset.UtcNow.AddSeconds(tokenHandle.Lifetime)
            };
            var json = TokenToRedis(token);
            var expiresIn = new TimeSpan(0, 0, tokenHandle.Lifetime);
            await base.StoreAsync(key, json, tokenHandle, expiresIn);
        }
    }
}
