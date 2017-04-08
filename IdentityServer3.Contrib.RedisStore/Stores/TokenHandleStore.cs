using IdentityServer3.Contrib.RedisStore.Models;
using IdentityServer3.Core.Services;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace IdentityServer3.Contrib.RedisStore.Stores
{
    public class TokenHandleStore : BaseTokenStore<IdentityServer3.Core.Models.Token>, ITokenHandleStore
    {
        public TokenHandleStore(IDatabaseAsync database, IScopeStore scopeStore, IClientStore clientStore)
            : base(database, TokenType.TokenHandle, scopeStore, clientStore)
        { }

        public override async Task StoreAsync(string key, IdentityServer3.Core.Models.Token tokenHandle)
        {
            var token = new Token
            {
                Key = key,
                CId = tokenHandle.ClientId,
                SId = tokenHandle.SubjectId,
                Content = ConvertToJson(tokenHandle),
                Exp = DateTimeOffset.UtcNow.AddSeconds(tokenHandle.Lifetime)
            };
            var json = TokenToJson(token);
            var expiresIn = new TimeSpan(0, 0, tokenHandle.Lifetime);
            await this.database.StringSetAsync(GetKey(key), json, expiresIn);
            await AddToHashSet(key, tokenHandle, json, expiresIn);
            await AddToSet(key, tokenHandle, expiresIn);
        }
    }
}
