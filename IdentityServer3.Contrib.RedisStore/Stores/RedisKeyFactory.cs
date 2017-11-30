using System;

namespace IdentityServer3.Contrib.RedisStore.Stores
{
    public class RedisKeyFactory : IRedisKeyFactory
    {
        public Func<string> SystemPrefixAction { get; set; }

        public RedisKeyFactory() { }

        public RedisKeyFactory(Func<string> systemPrefixAction)
        {
            SystemPrefixAction = systemPrefixAction;
        }

        public string SystemPrefix()
        {
            var prefix = null == SystemPrefixAction ? String.Empty : SystemPrefixAction();
            if (string.IsNullOrEmpty(prefix))
            {
                return string.Empty;
            }
            return $"{prefix}:";
        }
    }
}
