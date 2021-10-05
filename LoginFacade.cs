using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using ServicesInterfaces;
using ServicesInterfaces.Facades;
using ServicesModels;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ServicesFacade
{
    public class LoginFacade : ILoginFacade
    {
        private readonly IDataAccessManager _dataManager;
        private readonly ILogger<LoginFacade> _logger;
        public LoginFacade(IDataAccessManager dataManager, ILogger<LoginFacade> logger)
        {
            _dataManager = dataManager;
            _logger = logger;
        }
        public async Task Register(Data data, string returnUrl = "/")
        {
            try
            {
                var result = await _dataManager.CheckIfUsernameExists(data);
                if (result is null)
                {
                    await _dataManager.RegisterUser(data);
                    //await Login(new Data() { Username = data.Username, Password = data.Password, Id = data.Id });
                }
                else
                {
                    throw new Exception("Email taken");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);
            }
        }

        public async Task<(ClaimsPrincipal, UserCredentials)> Login(Data data, string returnUrl = "/")
        {
            try
            {
                var result = await _dataManager.AuthenticateUser(data);
                if (result is not null)
                {
                    var claims = new List<Claim>();

                    claims.Add(new Claim("username", result.Username));
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, result.Id));

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                    return (claimsPrincipal, result);
                }
                return (default, default);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);

                return (default, default);
            }
        }
    }
}
