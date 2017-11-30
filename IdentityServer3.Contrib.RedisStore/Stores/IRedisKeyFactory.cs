namespace IdentityServer3.Contrib.RedisStore.Stores
{
    public interface IRedisKeyFactory
    {
        string SystemPrefix();
    }
}
