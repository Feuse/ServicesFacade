using AutoMapper;
using Microsoft.Extensions.Logging;
using ServicesInterfaces;
using ServicesInterfaces.Facades;
using ServicesModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicesFacade
{
    public class UserFacade : IUserFacade
    {
        private readonly IDataAccessManager _dataManager;
        private readonly IMapper _mapper;
        private readonly ILogger<UserFacade> _logger;
        public UserFacade(IDataAccessManager dataManager, IMapper mapper, ILogger<UserFacade> logger)
        {
            _dataManager = dataManager;
            _mapper = mapper;
            _logger = logger;
        }
        public async Task<UserCredentials> GetUserInfo(string id)
        {
            try
            {
                var user = await _dataManager.GetUserById(new Data() { Id = id });
                user.Id = "";
                user.Password = "";

                return user;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);

                return default;
            }
        }
        public async Task SeenTutorial(Data data) // true
        {
            try
            {
                var user = await _dataManager.GetUserById(data); //false
                user.SeenTutorial = data.SeenTutorial;
                data = _mapper.Map(user, data);
                await _dataManager.UpdateUser(data);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);
            }
        }
    }
}
