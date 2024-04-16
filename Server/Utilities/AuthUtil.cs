using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using UtilitiesLibrary.Models;

namespace Server.Utilities
{
    internal static class AuthUtil
    {
        internal static async Task<string> GenerateRefreshToken(SqlContext sqlContext)
        {
            using (var randomNumberGenerator = RandomNumberGenerator.Create())
            {
                var randomBytes = new byte[64];
                randomNumberGenerator.GetBytes(randomBytes);
                string refreshToken = Convert.ToBase64String(randomBytes);

                await sqlContext.RefreshTokens.AddAsync(new RefreshToken { Token = refreshToken, ExpiryDate = DateTime.Now.AddDays(3) });
                await sqlContext.SaveChangesAsync();

                return refreshToken;
            }
        }

        internal static bool ValidateRefreshToken(string token, SqlContext sqlContext)
        {
            var refreshToken = sqlContext.RefreshTokens.FirstOrDefault(rt => rt.Token == token && rt.ExpiryDate > DateTime.UtcNow);
            return refreshToken != null;
        }

        internal static async Task UpdateRefreshToken(string oldRefreshToken, string newRefreshToken, SqlContext sqlContext)
        {
            var oldToken = sqlContext.RefreshTokens.FirstOrDefault(rt => rt.Token == oldRefreshToken);
            if (oldToken != null)
            {
                sqlContext.RefreshTokens.Remove(oldToken); // Invalidate the old refresh token
            }
            await sqlContext.SaveChangesAsync();
        }

        internal static string GenerateJwtToken(byte[] secret)
        {
            var jwtKey = new SymmetricSecurityKey(secret);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, "1")
                }),
                Expires = DateTime.UtcNow.AddMinutes(15),
                SigningCredentials = new SigningCredentials(jwtKey, SecurityAlgorithms.HmacSha512)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
