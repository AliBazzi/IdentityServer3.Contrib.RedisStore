﻿using IdentityServer3.Contrib.RedisStore.Models;
using IdentityServer3.Core.Models;
using IdentityServer3.Core.Services;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace IdentityServer3.Contrib.RedisStore.Stores
{
    public class AuthorizationCodeStore : BaseTokenStore<AuthorizationCode>, IAuthorizationCodeStore
    {
        public AuthorizationCodeStore(IDatabaseAsync database, IScopeStore scopeStore, IClientStore clientStore)
            : base(database, TokenType.AuthorizationCode, scopeStore, clientStore)
        { }

        public override async Task StoreAsync(string key, AuthorizationCode code)
        {
            var token = new Models.Token
            {
                Key = key,
                CId = code.ClientId,
                SId = code.SubjectId,
                Content = ConvertToJson(code),
                Exp = DateTimeOffset.UtcNow.AddSeconds(code.Client.AuthorizationCodeLifetime)
            };
            var json = TokenToJson(token);
            var expiresIn = new TimeSpan(0, 0, code.Client.AuthorizationCodeLifetime);
            await this.database.StringSetAsync(GetKey(key), json, expiresIn);
            await AddToHashSet(key, code, json, expiresIn);
            await AddToSet(key, code, expiresIn);
        }
    }
}
