using IdentityServer3.Contrib.RedisStore.Stores;
using IdentityServer3.Core.Services;
using StackExchange.Redis;
using System;

namespace IdentityServer3.Core.Configuration
{
    public static class IdentityServerRedisStoreExtensions
    {
        /// <summary>
        /// Add Redis operational store services.
        /// Operational stores are: IAuthorizationCodeStore, ITokenHandleStore and IRefreshTokenStore.
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="options">the ConfigurationOptions object.</param>
        public static void ConfigureOperationalRedisStoreServices(this IdentityServerServiceFactory factory, ConfigurationOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            factory.Register(new Registration<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(options)));
            factory.Register(new Registration<IDatabase>(_ => _.Resolve<IConnectionMultiplexer>().GetDatabase()));
            factory.AuthorizationCodeStore = new Registration<IAuthorizationCodeStore, AuthorizationCodeStore>();
            factory.TokenHandleStore = new Registration<ITokenHandleStore, TokenHandleStore>();
            factory.RefreshTokenStore = new Registration<IRefreshTokenStore, RefreshTokenStore>();
        }

        /// <summary>
        /// Add Redis operational store services.
        /// Operational stores are: IAuthorizationCodeStore, ITokenHandleStore and IRefreshTokenStore.
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="redisStoreConnection">the connection string to Redis store</param>
        /// <param name="db">the db number</param>
        public static void ConfigureOperationalRedisStoreServices(this IdentityServerServiceFactory factory, string redisStoreConnection, int db = -1)
        {
            if (string.IsNullOrEmpty(redisStoreConnection)) throw new ArgumentException(nameof(redisStoreConnection));
            factory.Register(new Registration<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisStoreConnection)));
            factory.Register(new Registration<IDatabase>(_ => _.Resolve<IConnectionMultiplexer>().GetDatabase(db)));
            factory.AuthorizationCodeStore = new Registration<IAuthorizationCodeStore, AuthorizationCodeStore>();
            factory.TokenHandleStore = new Registration<ITokenHandleStore, TokenHandleStore>();
            factory.RefreshTokenStore = new Registration<IRefreshTokenStore, RefreshTokenStore>();
        }
    }
}
