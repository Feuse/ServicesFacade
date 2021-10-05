using AutoMapper;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ServicesInterfaces;
using ServicesInterfaces.Facades;
using ServicesInterfaces.Scheduler;
using ServicesModels;
using ServicesModels.Global;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicesFacade
{
    public class ActionsFacade : IActionsFacade
    {
        private readonly IUserServicesFacade _servicesFacade;
        private readonly IServicesFactory _factory;
        private readonly IDataAccessManager _dataManager;
        private readonly IMapper _mapper;
        private readonly ILogger<ActionsFacade> _logger;
        private readonly IScheduler _scheduler;

        public ActionsFacade(IServicesFactory factory, IDataAccessManager dataManager, IMapper mapper, ILogger<ActionsFacade> logger, IScheduler scheduler, IUserServicesFacade servicesFacade)
        {
            _factory = factory;
            _dataManager = dataManager;
            _mapper = mapper;
            _logger = logger;
            _scheduler = scheduler;
            _servicesFacade = servicesFacade;
        }
        public async Task<Data> UpdateAboutMe(Data data)
        {
            try
            {
                IService service = _factory.GetService(data.Service);
                var user = await _dataManager.GetUserById(data);
                data = _mapper.Map(user, data);
                await _servicesFacade.LoginToService(data);
                await service.UpdateAboutMe(data);
                await _dataManager.UpdateUser(data);
                return data;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);

                return default;
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
                    await _servicesFacade.LoginToService(data);
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
        public async Task<List<string>> ScheduleTask(List<Data> data, string id)
        {
            var planFactory = new PlanFactory();
            var plan = new Plan();
            var plans = new List<string>();
            try
            {
                foreach (var service in data)
                {
                    service.Id = id;
                    var singleService = await _dataManager.GetUserServiceByServiceNameAndId(service);
                    //For testing uncomment Premium = true
                    //singleService.Premium = true;
                    if (singleService.Premium)
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
                        plans.Add(plan.ToString());
                    }
                }
                return plans;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogTrace(e.StackTrace);

                return default;
            }
        }
        public async Task<IDictionary<string, string>> UploadImage(Data data)
        {
            IDictionary<string, string> images = new Dictionary<string, string>();

            try
            {
                await _servicesFacade.LoginToService(data);

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

                await _servicesFacade.LoginToService(data);

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
    }
}
