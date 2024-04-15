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
        internal static string GenerateRefreshToken()
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

        internal static string GenerateJwtToken(byte[] secret)
        {
            var jwtKey = new SymmetricSecurityKey(secret);

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
    }
}
