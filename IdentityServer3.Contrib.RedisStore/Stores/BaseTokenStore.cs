using IdentityServer3.Contrib.RedisStore.Models;
using IdentityServer3.Contrib.RedisStore.Serialization;
using IdentityServer3.Core.Models;
using IdentityServer3.Core.Services;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer3.Contrib.RedisStore.Stores
{
    public abstract class BaseTokenStore<T> where T : ITokenMetadata
    {
        private readonly TokenType tokenType;
        private readonly IScopeStore scopeStore;
        private readonly IClientStore clientStore;
        private readonly IDatabase database;
        private readonly RedisKeyGenerator keyGenerator;

        public BaseTokenStore(IDatabase database, TokenType tokenType, IScopeStore scopeStore, IClientStore clientStore, RedisKeyGenerator redisKeyGenerator)
        {
            this.scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
            this.clientStore = clientStore ?? throw new ArgumentNullException(nameof(clientStore));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.tokenType = tokenType;
            this.keyGenerator = redisKeyGenerator;
        }

        #region Json
        JsonSerializerSettings GetJsonSerializerSettings()
        {
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new ClaimConverter());
            settings.Converters.Add(new ClaimsPrincipalConverter());
            settings.Converters.Add(new ClientConverter(clientStore));
            settings.Converters.Add(new ScopeConverter(scopeStore));
            return settings;
        }

        protected string ConvertToJson(T value)
        {
            return JsonConvert.SerializeObject(value, GetJsonSerializerSettings());
        }

        protected T ConvertFromJson(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, GetJsonSerializerSettings());
        }
        #endregion

        public async Task<T> GetAsync(string key)
        {
            var token = await this.database.StringGetAsync(keyGenerator.GetKey(tokenType, key)).ConfigureAwait(false);
            return token.HasValue ? ConvertFromJson(token) : default(T);
        }

        public async Task RemoveAsync(string key)
        {
            var token = await this.database.StringGetAsync(keyGenerator.GetKey(tokenType, key)).ConfigureAwait(false);
            if (!token.HasValue)
                return;
            var _ = ConvertFromJson(token);
            var transaction = this.database.CreateTransaction();
            transaction.KeyDeleteAsync(keyGenerator.GetKey(tokenType, key));
            transaction.SetRemoveAsync(keyGenerator.GetSetKey(tokenType, _.SubjectId), key);
            transaction.SetRemoveAsync(keyGenerator.GetSetKey(tokenType, _.SubjectId, _.ClientId), key);
            await transaction.ExecuteAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<ITokenMetadata>> GetAllAsync(string subject)
        {
            var setKey = keyGenerator.GetSetKey(tokenType, subject);
            var (tokens, keysToDelete) = await GetTokens(setKey).ConfigureAwait(false);
            if (keysToDelete.Any())
                await this.database.SetRemoveAsync(setKey, keysToDelete.ToArray()).ConfigureAwait(false);
            return tokens.Where(_ => _.HasValue).Select(_ => ConvertFromJson(_)).Cast<ITokenMetadata>();
        }

        private async Task<(IEnumerable<RedisValue> tokens, IEnumerable<RedisValue> keysToDelete)> GetTokens(string setKey)
        {
            var tokensKeys = await this.database.SetMembersAsync(setKey).ConfigureAwait(false);
            if (!tokensKeys.Any())
                return (Enumerable.Empty<RedisValue>(), Enumerable.Empty<RedisValue>());
            var tokens = await this.database.StringGetAsync(tokensKeys.Select(_ => (RedisKey)_.ToString()).ToArray()).ConfigureAwait(false);
            var keysToDelete = tokensKeys.Zip(tokens, (key, value) => new KeyValuePair<RedisValue, RedisValue>(key, value)).Where(_ => !_.Value.HasValue).Select(_ => _.Key).ToArray();
            return (tokens, keysToDelete);
        }

        public async Task RevokeAsync(string subject, string client)
        {
            var setKey = keyGenerator.GetSetKey(tokenType, subject, client);
            var keys = await this.database.SetMembersAsync(setKey).ConfigureAwait(false);
            if (!keys.Any())
                return;
            var transaction = this.database.CreateTransaction();
            transaction.KeyDeleteAsync(keys.Select(_ => (RedisKey)_.ToString()).Concat(new RedisKey[] { setKey }).ToArray());
            transaction.SetRemoveAsync(keyGenerator.GetSetKey(tokenType, subject), keys.ToArray());
            await transaction.ExecuteAsync().ConfigureAwait(false);
        }

        public abstract Task StoreAsync(string key, T value);

        protected async Task StoreAsync(string key, string json, ITokenMetadata token, TimeSpan expiresIn)
        {
            var tokenKey = keyGenerator.GetKey(tokenType, key);
            if (!string.IsNullOrEmpty(token.SubjectId))
            {
                var setKey = keyGenerator.GetSetKey(tokenType, token);
                var setKeyforSubject = keyGenerator.GetSetKey(tokenType, token.SubjectId);

                var ttlOfSet = await this.database.KeyTimeToLiveAsync(setKeyforSubject).ConfigureAwait(false);

                var transaction = this.database.CreateTransaction();
                transaction.StringSetAsync(tokenKey, json, expiresIn);
                transaction.SetAddAsync(setKey, tokenKey);
                transaction.SetAddAsync(setKeyforSubject, tokenKey);
                transaction.KeyExpireAsync(setKey, expiresIn);
                if ((ttlOfSet ?? TimeSpan.Zero) <= expiresIn)
                    transaction.KeyExpireAsync(setKeyforSubject, expiresIn);
                await transaction.ExecuteAsync().ConfigureAwait(false);
            }
            else
            {
                await this.database.StringSetAsync(tokenKey, json, expiresIn).ConfigureAwait(false);
            }
        }
    }
}
