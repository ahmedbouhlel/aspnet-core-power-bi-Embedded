using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace PowerBIEmbedded_AppOwnsData.Utils
{
    public static class CustomValidation
    {

        private static readonly string secretKey = ConfigurationManager.AppSettings["secretKey"];

        static byte[] symmetricKey = System.Text.Encoding.UTF8.GetBytes(secretKey);
        internal static bool ValidateToken(string token, out System.IdentityModel.Tokens.Jwt.JwtSecurityToken JwtToken)
        {
            JwtToken = null;
            try
            {
                var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                JwtToken = tokenHandler.ReadToken(token) as System.IdentityModel.Tokens.Jwt.JwtSecurityToken;
                if (JwtToken == null)
                {
                    return false;
                }
                var validationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters()
                {
                    RequireExpirationTime = true,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(symmetricKey)
                };
                Microsoft.IdentityModel.Tokens.SecurityToken securityToken;
                tokenHandler.ValidateToken(token, validationParameters, out securityToken);
                return true;
            }
            catch (Microsoft.IdentityModel.Tokens.SecurityTokenValidationException e)
            {
                Console.WriteLine($"Token Expired!: {e}");
                return false;
            }
        }




    }
}