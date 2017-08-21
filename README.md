# IdentityServer3.Contrib.RedisStore

IdentityServer3.Contrib.RedisStore is a persistance layer using [Redis](https://redis.io) DB for operational data of Identity Server 3. Specifically, this store provides implementation for AuthorizationCodeStore, RefreshTokenStore and TokenHandleStore.


## How to use

You need to install the [nuget package](https://www.nuget.org/packages/IdentityServer3.Contrib.RedisStore)

then you can inject the stores in the Identity Server [service Factory](https://identityserver.github.io/Documentation/docsv2/configuration/serviceFactory.html)

like the following:

```csharp
var factory = new IdentityServerServiceFactory();

factory.Register(new Registration<RedisStoreMultiplexer>(new RedisStoreMultiplexer("Connection string"));

factory.Register(new Registration<IDatabaseAsync>(resolver => resolver.Resolve<RedisStoreMultiplexer>().GetDatabase()));

factory.AuthorizationCodeStore = new Registration<IAuthorizationCodeStore, AuthorizationCodeStore>();

factory.TokenHandleStore = new Registration<ITokenHandleStore, TokenHandleStore>();

factory.RefreshTokenStore = new Registration<IRefreshTokenStore, RefreshTokenStore>();
```

you can define your own strategy for connecting to Redis DB, in this example, you can create your own strategy to resolve the IDatabase using RedisStoreMultiplexer:

```csharp
public class RedisStoreMultiplexer
    {
        private readonly IConnectionMultiplexer multiplexer;

        private readonly int DB;

        public RedisStoreMultiplexer(string connectionString, int DB = 0)
        {
            this.DB = DB;
            this.multiplexer = ConnectionMultiplexer.Connect(connectionString);
        }

        public IDatabase GetDatabase()
        {
            return this.multiplexer.GetDatabase(this.DB);
        }
    }
```

## the solution approach

the solution was approached based on how the [SQL Store](https://github.com/IdentityServer/IdentityServer3.EntityFramework) storing the operational data, but the concept of Redis as a NoSQL db is totally different than relational db concepts, all the operational data stores implement the following [ITransientDataRepository](https://github.com/IdentityServer/IdentityServer3/blob/master/source%2FCore%2FServices%2FITransientDataRepository.cs) interface:

```csharp
    public interface ITransientDataRepository<T>
        where T : ITokenMetadata
    {

        Task StoreAsync(string key, T value);

        Task<T> GetAsync(string key);

        Task RemoveAsync(string key);

        Task<IEnumerable<ITokenMetadata>> GetAllAsync(string subject);

        Task RevokeAsync(string subject, string client);
    }
```

and the [ITokenMetadata](https://github.com/IdentityServer/IdentityServer3/blob/93bc6bc9b536146b9e3fa0bed21d77283d07f788/source/Core/Models/ITokenMetadata.cs) defines the following contract:

```csharp
    public interface ITokenMetadata
    {
        string SubjectId { get; }
        string ClientId { get; }
        IEnumerable<string> Scopes { get; }
    }
```

with the ITransientDataRepository contract, we notice that the GetAllAsync(subject) and RevokeAsync(subject,client) defines a contract to read based on subject id and revoke all the tokens in the store based on subject and client ids.

this brings trouble to Redis store since redis as a reliable dictionary is not designed for relational queries, so the trick is to store multiple entries for the same token, and can be reached using key, subject and client ids.

so the StoreAsync operation stores the following entries in Redis:

1. Key(TokenType:Key) -> RedisStruct: stored as key string value pairs, used to retrieve the Token based on the key, if the token exists or not expired.

1. Key(TokenType:SubjectId) -> Key* : stored in a redis Set, used on the GetAllAsync, to retrieve all the tokens related to a given subject id.

1. Key(TokenType:SubjectId:ClientId) -> Key* : stored in a redis set, used to retrieve all the keys that are related to a subject and client ids, to revoke them while calling RevokeAsync.

for more information on data structures used to store the token please refer to [Redis data types documentation](https://redis.io/topics/data-types)

now it's time to look at the RedisStruct which is used as mediator data type to be stored in redis instead of storing the json of the token directly:

```csharp
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
```

since Redis has a [key Expiration](https://redis.io/commands/expire) feature based on a defined date time or time span, and to not implement a logic similar to SQL store implementation for [cleaning up the store](https://identityserver.github.io/Documentation/docsv2/ef/operational.html) periodically from dangling tokens, the store uses the key expiration of redis while storing based on the following criteria:

1. for Key(TokenType:Key) the expiration is straight forward, it's set on the StringSet Redis operation as defined by identity server on the token object.

1. for Key(TokenType:SubjectId:ClientId) set the expiration also set as the lifetime of the token passed by the identity server, since the [Client](https://identityserver.github.io/Documentation/docsv2/configuration/clients.html) has unified lifetime for the token defined in the configuration.

1. for Key(TokenType:SubjectId) HashSet, the expiration is a little bit complicated since the subject id is involved, and it may happen that the subject has valid tokens from multiple clients, each client define it's own lifetime of the token in-place. so the expiration set for the HashSet is based on the longest lifetime configuration of the clients the user already using.

## Feedback

feedbacks are always welcomed, please open an issue for any problem or bug found, and the suggestions are also welcomed.


