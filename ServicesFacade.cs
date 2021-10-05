using AutoMapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


using ServicesInterfaces;
using ServicesInterfaces.Global;
using ServicesInterfaces.Scheduler;
using ServicesModels;
using ServicesModels.Global;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ServicesFacade
{
    public class ServicesFacade : IServicesFacade
    {
        private readonly IServicesFactory _factory;
        private readonly IDataAccessManager _dataManager;
        private readonly IMapper _mapper;
        private readonly ILogger<ServicesFacade> _logger;
        private readonly IScheduler _scheduler;

        public ServicesFacade(IServicesFactory factory, IDataAccessManager dataManager, IMapper mapper, ILogger<ServicesFacade> logger, IScheduler scheduler)
        {
            _factory = factory;
            _dataManager = dataManager;
            _mapper = mapper;
            _logger = logger;
            _scheduler = scheduler;
        }

        public async Task<List<Service>> AuthenticateUserServices(string id)
        {
            var servicesList = new List<Service>();

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

        public async Task<IDictionary<string, string>> GetImages(Data data)
        {
            IDictionary<string, string> images = new Dictionary<string, string>();
            try
            {

                IService service = _factory.GetService(data.Service);

                var cachedImages = await _dataManager.GetUserImages(data);
                if (cachedImages is not null)
                {
                    return cachedImages;
                }

                var userService = await _dataManager.GetUserServiceByServiceNameAndId(data);

                data = _mapper.Map(userService, data);

                await LoginToService(data);

                images = await service.GetImages(data);
                if (images.Count > 0)
                {
                    await _dataManager.SetUserImages(data, images);
                    return images;
                }
                return images;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);
                return images;
            }
        }

        public async Task<UserCredentials> GetUserInfo(string id)
        {
            var user = await _dataManager.GetUserById(new Data() { Id = id });
            user.Id = "";
            user.Password = "";

            return user;
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

                var singleService = UserServices.Where(a => a.Service == data.Service).FirstOrDefault();

                service = _factory.GetService(data.Service);

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

        public async Task Register(Data data, string returnUrl = "/")
        {
            try
            {
                var result = await _dataManager.CheckIfUsernameExists(data);
                if (result is null)
                {
                    await _dataManager.RegisterUser(data);
                    await Login(new Data() { Username = data.Username, Password = data.Password, Id = data.Id });
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

        public async Task<IDictionary<string, string>> RemoveImage(Data data)
        {
            IDictionary<string, string> result = new Dictionary<string, string>();
            IService service = default;
            try
            {
                var userService = await _dataManager.GetUserServiceByServiceNameAndId(data);

                service = _factory.GetService(data.Service);
                if (userService is null)
                {
                    await service.AppStartUp(data);
                    await LoginToService(data);
                }

                var session = await _dataManager.GetServiceSession(new Data() { UserServiceId = userService.UserServiceId });
                if (session is null)
                {
                    return result;
                }
                data = _mapper.Map(session, data);

                result = await service.RemoveImage(data);
                await _dataManager.RemoveUserImage(data, result);

                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);
                return result;
            }
        }

        public async Task<string> ScheduleTask(List<Data> data, string id)
        {
            var planFactory = new PlanFactory();
            var plan = new Plan();
            try
            {
                foreach (var service in data)
                {
                    plan = planFactory.GetPlan(service.Likes, service.Repeat, service.Plan);
                    await _scheduler.Schedule(new Message
                    {
                        Likes = plan.Likes,
                        Price = plan.Price,
                        Service = service.Service,
                        UserId = id,
                        MessageId = Guid.NewGuid(),
                        Repeat = plan.Repeat
                    });
                }
                return plan.ToString();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);

                return e.Message;
            }
        }

        public async Task SeenTutorial(Data data)
        {
            var user = await _dataManager.GetUserById(data);
            data = _mapper.Map(user, data);
            await _dataManager.UpdateUser(data);
        }

        public async Task<string> UpdateAboutMe(Data data)
        {
            try
            {
                IService service = _factory.GetService(data.Service);
                await LoginToService(data);
                await service.UpdateAboutMe(data);
                return data.About;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);

                return "unable to update";
            }
        }

        public async Task<IDictionary<string, string>> UploadImage(Data data)
        {
            IDictionary<string, string> images = new Dictionary<string, string>();

            try
            {
                await LoginToService(data);

                IService service = _factory.GetService(data.Service);
                var result = await service.UploadImage(data);
                var resultObject = JsonConvert.DeserializeObject<PhotosResultModel>(result);

                var tempImages = await service.GetImages(data);
                images.Add(resultObject.PhotoId, resultObject.PhotoUrl);
                await _dataManager.SetUserImages(data, tempImages);

                return images;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);
                return images;
            }
        }
        private async Task GetServicesSessions(List<Service> servicesList, Data data, UserServiceCredentials singleService, IService service)
        {

            try
            {
                var chachedSession = await _dataManager.GetServiceSession(data);

                if (chachedSession is null)
                {
                    // No session, log in to user and update session.
                    data = await service.AppStartUp(data);
                    await TryGetAndUpdateSession(data);
                    servicesList.Add(singleService.Service);
                }
                else
                {
                    servicesList.Add(singleService.Service);
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
