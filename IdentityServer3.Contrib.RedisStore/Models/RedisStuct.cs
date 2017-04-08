using System;

namespace IdentityServer3.Contrib.RedisStore.Models
{
    public class RedisStuct
    {
        /// <summary>
        /// the JSON content of the original token
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// the expiration date of the token
        /// </summary>
        public DateTimeOffset Exp { get; set; }
    }
}
