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
        protected readonly TokenType tokenType;
        protected readonly IScopeStore scopeStore;
        protected readonly IClientStore clientStore;
        protected readonly IDatabaseAsync database;

        public BaseTokenStore(IDatabaseAsync database, TokenType tokenType, IScopeStore scopeStore, IClientStore clientStore)
        {
            this.scopeStore = scopeStore ?? throw new ArgumentNullException(nameof(scopeStore));
            this.clientStore = clientStore ?? throw new ArgumentNullException(nameof(clientStore));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.tokenType = tokenType;
        }

        protected string GetKey(string key) => $"{(short)this.tokenType}:{key}";

        private string GetHashSetKey(ITokenMetadata token) => $"{((short)this.tokenType).ToString()}:{token.SubjectId}";

        protected string GetHashSetKey(string subjectId) => $"{((short)this.tokenType).ToString()}:{subjectId}";

        private string GetSetKey(ITokenMetadata token) => $"{((short)this.tokenType).ToString()}:{token.SubjectId}:{token.ClientId}";

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

        protected RedisStuct RedisToToken(string json)
        {
            return JsonConvert.DeserializeObject<RedisStuct>(json);
        }

        protected string TokenToRedis(RedisStuct token)
        {
            return JsonConvert.SerializeObject(token);
        }
        #endregion

        public async Task<T> GetAsync(string key)
        {
            var token = await this.database.StringGetAsync(GetKey(key));
            return token.HasValue ? ConvertFromJson(RedisToToken(token).Content) : default(T);
        }

        public async Task RemoveAsync(string key)
        {
            var token = await this.database.StringGetAsync(GetKey(key));
            if (!token.HasValue)
                return;
            var _ = ConvertFromJson(RedisToToken(token).Content);
            await Task.WhenAll(
                this.database.KeyDeleteAsync(GetKey(key)),
                this.database.HashDeleteAsync(GetHashSetKey(_.SubjectId), key),
                this.database.SetRemoveAsync(GetSetKey(_.SubjectId, _.ClientId), key)
            );
        }

        public async Task<IEnumerable<ITokenMetadata>> GetAllAsync(string subject)
        {
            var tokens = await this.database.HashGetAllAsync(GetHashSetKey(subject));
            return tokens.Select(_ => RedisToToken(_.Value)).Where(_ => _.Exp < DateTimeOffset.UtcNow).Select(_ => ConvertFromJson(_.Content)).Cast<ITokenMetadata>();
        }

        public async Task RevokeAsync(string subject, string client)
        {
            var setKey = GetSetKey(subject, client);
            var set = await this.database.SetMembersAsync(setKey);
            if (set.Count() == 0)
                return;
            await Task.WhenAll(
                this.database.KeyDeleteAsync(set.Select(_ => (RedisKey)_.ToString())
                .Concat(new RedisKey[] { setKey }).ToArray()),
            this.database.HashDeleteAsync(GetHashSetKey(subject), set));
        }

        public abstract Task StoreAsync(string key, T value);

        protected async Task AddToHashSet(string key, ITokenMetadata token, string json, TimeSpan expiresIn)
        {
            var hashKey = GetHashSetKey(token);
            var hashSet = (await this.database.HashGetAllAsync(hashKey)).Select(_ => new { Key = _.Name, Token = RedisToToken(_.Value) });
            await this.database.HashSetAsync(hashKey, key, json);
            if (hashSet.Any())
            {
                var maxExpiry = hashSet.Max(_ => _.Token.Exp - DateTimeOffset.UtcNow);
                if (maxExpiry < expiresIn)
                    await this.database.KeyExpireAsync(hashKey, expiresIn);
            }
            else
                await this.database.KeyExpireAsync(hashKey, expiresIn);

            //clean up the expired hash entries
            var cleanupIds = hashSet.Where(_ => _.Token.Exp - DateTimeOffset.UtcNow < new TimeSpan());
            if (cleanupIds.Any())
                await this.database.HashDeleteAsync(key, cleanupIds.Select(_ => _.Key).ToArray());
        }

        protected async Task AddToSet(string key, ITokenMetadata token, TimeSpan expiresIn)
        {
            var setKey = GetSetKey(token);
            await Task.WhenAll(
                this.database.SetAddAsync(setKey, key),
                this.database.KeyExpireAsync(setKey, expiresIn));
        }
    }
}
