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

        public BaseTokenStore(IDatabase database, TokenType tokenType, IScopeStore scopeStore, IClientStore clientStore)
        {
            this.scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
            this.clientStore = clientStore ?? throw new ArgumentNullException(nameof(clientStore));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.tokenType = tokenType;
        }

        protected string GetKey(string key) => $"{(short)this.tokenType}:{key}";

        private string GetSetKey(ITokenMetadata token) => $"{((short)this.tokenType).ToString()}:{token.SubjectId}:{token.ClientId}";

        private string GetSetKey(string subjectId) => $"{((short)this.tokenType).ToString()}:{subjectId}";

        private string GetSetKey(string subjectId, string clientId) => $"{((short)this.tokenType).ToString()}:{subjectId}:{clientId}";

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
            var token = await this.database.StringGetAsync(GetKey(key));
            return token.HasValue ? ConvertFromJson(token) : default(T);
        }

        public async Task RemoveAsync(string key)
        {
            var token = await this.database.StringGetAsync(GetKey(key));
            if (!token.HasValue)
                return;
            var _ = ConvertFromJson(token);
            var transaction = this.database.CreateTransaction();
            transaction.KeyDeleteAsync(GetKey(key));
            transaction.SetRemoveAsync(GetSetKey(_.SubjectId), key);
            transaction.SetRemoveAsync(GetSetKey(_.SubjectId, _.ClientId), key);
            await transaction.ExecuteAsync();
        }

        public async Task<IEnumerable<ITokenMetadata>> GetAllAsync(string subject)
        {
            var setKey = GetSetKey(subject);
            var tokensKeys = await this.database.SetMembersAsync(setKey);
            var tokens = await this.database.StringGetAsync(tokensKeys.Select(_ => (RedisKey)_.ToString()).ToArray());
            var keysToDelete = tokensKeys.Zip(tokens, (key, value) => new KeyValuePair<RedisValue, RedisValue>(key, value)).Where(_ => !_.Value.HasValue).Select(_ => _.Key).ToArray();
            if (keysToDelete.Count() != 0)
                await this.database.SetRemoveAsync(setKey, keysToDelete);
            return tokens.Where(_ => _.HasValue).Select(_ => ConvertFromJson(_)).Cast<ITokenMetadata>();
        }

        public async Task RevokeAsync(string subject, string client)
        {
            var setKey = GetSetKey(subject, client);
            var keys = await this.database.SetMembersAsync(setKey);
            if (keys.Count() == 0)
                return;
            var transaction = this.database.CreateTransaction();
            transaction.KeyDeleteAsync(keys.Select(_ => (RedisKey)_.ToString()).Concat(new RedisKey[] { setKey }).ToArray());
            transaction.SetRemoveAsync(GetSetKey(subject), keys.ToArray());
            await transaction.ExecuteAsync();
        }

        public abstract Task StoreAsync(string key, T value);

        protected async Task StoreAsync(string key, string json, ITokenMetadata token, TimeSpan expiresIn)
        {
            if (!string.IsNullOrEmpty(token.SubjectId))
            {
                var setKey = GetSetKey(token);
                var transaction = this.database.CreateTransaction();
                transaction.StringSetAsync(GetKey(key), json, expiresIn);
                transaction.SetAddAsync(setKey, key);
                transaction.SetAddAsync(GetSetKey(token.SubjectId), key);
                transaction.KeyExpireAsync(setKey, expiresIn);
                await transaction.ExecuteAsync();
            }
            else
            {
                await this.database.StringSetAsync(GetKey(key), json, expiresIn);
            }
        }
    }
}
