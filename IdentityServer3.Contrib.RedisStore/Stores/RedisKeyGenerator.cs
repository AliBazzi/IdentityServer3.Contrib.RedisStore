using IdentityServer3.Contrib.RedisStore.Models;
using IdentityServer3.Core.Models;

namespace IdentityServer3.Contrib.RedisStore.Stores
{
    public class RedisKeyGenerator
    {
        private readonly string keyPrefix;

        public RedisKeyGenerator(string keyPrefix)
        {
            if (string.IsNullOrEmpty(keyPrefix))
                this.keyPrefix = string.Empty;
            else
                this.keyPrefix = keyPrefix + ":";
        }

        internal string GetKey(TokenType tokenType, string key) => $"{keyPrefix}{(short)tokenType}:{key}";

        internal string GetSetKey(TokenType tokenType, ITokenMetadata token) => $"{keyPrefix}{((short)tokenType).ToString()}:{token.SubjectId}:{token.ClientId}";

        internal string GetSetKey(TokenType tokenType, string subjectId) => $"{keyPrefix}{((short)tokenType).ToString()}:{subjectId}";

        internal string GetSetKey(TokenType tokenType, string subjectId, string clientId) => $"{keyPrefix}{((short)tokenType).ToString()}:{subjectId}:{clientId}";
    }
}
