﻿namespace IdentityServer3.Contrib.RedisStore.Models
{
    public enum TokenType : short
    {
        AuthorizationCode = 1,
        TokenHandle = 2,
        RefreshToken = 3
    }
}
