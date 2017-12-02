using IdentityServer3.Contrib.RedisStore.Models;
using IdentityServer3.Core.Models;
using IdentityServer3.Core.Services;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace IdentityServer3.Contrib.RedisStore.Stores
{
    public class AuthorizationCodeStore : BaseTokenStore<AuthorizationCode>, IAuthorizationCodeStore
    {
        public AuthorizationCodeStore(IDatabase database, IScopeStore scopeStore, IClientStore clientStore, RedisKeyGenerator redisKeyGenerator)
            : base(database, TokenType.AuthorizationCode, scopeStore, clientStore, redisKeyGenerator)
        { }

        public override async Task StoreAsync(string key, AuthorizationCode code)
        {
            var json = ConvertToJson(code);
            var expiresIn = new TimeSpan(0, 0, code.Client.AuthorizationCodeLifetime);
            await base.StoreAsync(key, json, code, expiresIn);
        }
    }
}
