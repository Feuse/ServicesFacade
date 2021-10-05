using AutoMapper;
using BadooAPI.Factories;
using Microsoft.Extensions.Logging;
using ServicesInterfaces;
using ServicesInterfaces.Facades;
using ServicesInterfaces.Scheduler;
using ServicesModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicesFacade
{
    public class UserServicesFacade : IUserServicesFacade
    {
        private readonly IServicesFactory _factory;
        private readonly IDataAccessManager _dataManager;
        private readonly IMapper _mapper;
        private readonly ILogger<UserServicesFacade> _logger;

        public UserServicesFacade(IServicesFactory factory, IDataAccessManager dataManager, IMapper mapper, ILogger<UserServicesFacade> logger)
        {
            _factory = factory;
            _dataManager = dataManager;
            _mapper = mapper;
            _logger = logger;
        }
        public async Task<List<UserServiceCredentials>> AuthenticateUserServices(string id)
        {
            var servicesList = new List<UserServiceCredentials>();

            try
            {
                var data = new Data() { Id = id };

                //check cache for services
                var UserServices = await _dataManager.GetAllUserServicesById(data);

                if (UserServices.Count == 0)
                {
                    return servicesList;
                }

                foreach (var singleService in UserServices)
                {
                    IService service = _factory.GetService(singleService.Service);
                    data = _mapper.Map(singleService, data);

                    await GetServicesSessions(servicesList, data, singleService, service);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);

                servicesList.Clear();
                return servicesList;
            }

            return servicesList;
        }
        public async Task<Data> LoginToService(Data data)
        {
            try
            {
                IService service = _factory.GetService(data.Service);

                var UserServices = await _dataManager.GetAllUserServicesById(data);

                if (UserServices.Count == 0)
                {
                    service = _factory.GetService(data.Service);
                    var serviceDetails = await service.AppStartUp(data); // age name
                    if (serviceDetails.Result == Result.Success)
                    {
                        await _dataManager.RegisterService(serviceDetails);

                        return serviceDetails;
                    }
                    return serviceDetails;
                }
                //user logs into service without premium
                //saved into db
                //every nexy request is from db
                //user gets premium
                //need to update db

                var singleService = UserServices.Where(a => a.Service == data.Service).FirstOrDefault();

                service = _factory.GetService(data.Service);
                var user = await _dataManager.GetUserById(data);
                //if (string.IsNullOrEmpty(data.About))
                //{
                //    data = _mapper.Map(user, data);

                //}
                data = _mapper.Map(singleService, data);

                await GetSingleServiceSession(data, singleService, new ServiceSessions(), service);

                return data;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);
                return data;
            }
        }
        private async Task GetServicesSessions(List<UserServiceCredentials> servicesList, Data data, UserServiceCredentials singleService, IService service)
        {
            try
            {
                var chachedSession = await _dataManager.GetServiceSession(data);

                if (chachedSession is null)
                {
                    // No session, log in to user and update session.
                    data = await service.AppStartUp(data);
                    if ((!singleService.Premium) && data.Premium)
                    {
                        singleService.Premium = data.Premium;
                    }
                    await TryGetAndUpdateSession(data);
                    servicesList.Add(new UserServiceCredentials() { Premium = singleService.Premium, Service = singleService.Service });
                }
                else
                {
                    servicesList.Add(new UserServiceCredentials() { Premium = singleService.Premium, Service = singleService.Service });
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);
            }
        }
        private async Task GetSingleServiceSession(Data data, UserServiceCredentials singleService, ServiceSessions singleSession, IService service)
        {
            try
            {
                var chachedSession = await _dataManager.GetServiceSession(data);

                if (chachedSession is null)
                {
                    // No session, log in to user and update session.
                    var result = await service.AppStartUp(data);
                    await TryGetAndUpdateSession(result);
                }
                else
                {
                    data = _mapper.Map(chachedSession, data);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);
            }
        }
        private async Task TryGetAndUpdateSession(Data data)
        {
            try
            {
                if (data.Result == Result.Success)
                {
                    await _dataManager.UpdateServiceSession(data);
                }
                else
                {
                    //if unable to log into service, remove service and session.
                    await _dataManager.RemoveServiceFromUser(data);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);
            }
        }
    }
}
