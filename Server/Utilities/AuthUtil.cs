using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using UtilitiesLibrary.Models;

namespace Server.Utilities
{
    internal static class AuthUtil
    {
        internal static string GenerateRefreshToken(SqlContext sqlContext)
        {
            using (var randomNumberGenerator = RandomNumberGenerator.Create())
            {
                var randomBytes = new byte[64];
                randomNumberGenerator.GetBytes(randomBytes);
                return Convert.ToBase64String(randomBytes);
            }
        }

        internal static bool ValidateRefreshToken(string token, SqlContext sqlContext)
        {
            var refreshToken = sqlContext.RefreshTokens.FirstOrDefault(rt => rt.Token == token && rt.ExpiryDate > DateTime.UtcNow);
            return refreshToken != null;
        }

        internal static async Task SaveRefreshToken(string refreshToken, SqlContext sqlContext)
        {
            sqlContext.RefreshTokens.Add(new RefreshToken { Token = refreshToken, ExpiryDate = DateTime.UtcNow.AddDays(7) });
            await sqlContext.SaveChangesAsync();
        }

        internal static async Task UpdateRefreshToken(string oldRefreshToken, string newRefreshToken, SqlContext sqlContext)
        {
            var oldToken = sqlContext.RefreshTokens.FirstOrDefault(rt => rt.Token == oldRefreshToken);
            if (oldToken != null)
            {
                oldToken.ExpiryDate = DateTime.UtcNow; // Invalidate the old refresh token
            }
            sqlContext.RefreshTokens.Add(new RefreshToken { Token = newRefreshToken, ExpiryDate = DateTime.UtcNow.AddDays(7) });
            await sqlContext.SaveChangesAsync();
        }

        internal static string GenerateJwtToken()
        {
            var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("gADOPGFjaGija3i23jI@#!#SfjiaVJSJIVJSBBS$#$#$!#$b34153afgdgsgsfgagasdfgasfgafgafs3@!q315135"));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, "1")
                }),
                Expires = DateTime.UtcNow.AddMinutes(1),
                SigningCredentials = new SigningCredentials(jwtKey, SecurityAlgorithms.HmacSha512)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        internal static ClaimsPrincipal ValidateJwtToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("gADOPGFjaGija3i23jI@#!#SfjiaVJSJIVJSBBS$#$#$!#$b34153afgdgsgsfgagasdfgasfgafgafs3@!q315135"));

            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = jwtKey,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            if (jwtToken == null || !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha512, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;
        }
    }
}
