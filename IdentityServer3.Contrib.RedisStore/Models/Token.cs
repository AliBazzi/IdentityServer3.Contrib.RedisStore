using System;

namespace IdentityServer3.Contrib.RedisStore.Models
{
    public class Token
    {
        /// <summary>
        /// the key of the token
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// the subject id
        /// </summary>
        public string SId { get; set; }

        /// <summary>
        /// the client id
        /// </summary>
        public string CId { get; set; }

        /// <summary>
        /// the JSON content of the token
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// the expiration date of the token
        /// </summary>
        public DateTimeOffset Exp { get; set; }
    }
}
