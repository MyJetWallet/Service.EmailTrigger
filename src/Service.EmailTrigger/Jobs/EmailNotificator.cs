using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetCoreDecorators;
using Microsoft.Extensions.Logging;
using MyJetWallet.Sdk.Authorization.ServiceBus;
using Service.EmailSender.Grpc;
using Service.PersonalData.Grpc;
using Service.PersonalData.Grpc.Contracts;
using Service.Registration.Domain.Models;

namespace Service.EmailTrigger.Jobs
{
    public class EmailNotificator
    {
        private readonly IEmailSenderService _emailSender;
        private readonly ILogger<EmailNotificator> _logger;
        private readonly IPersonalDataServiceGrpc _personalDataService;

        public EmailNotificator(ILogger<EmailNotificator> logger,
            ISubscriber<IReadOnlyList<SessionAuditEvent>> sessionAudit, 
            ISubscriber<IReadOnlyList<ClientRegisterMessage>> registerSubscriber, 
            ISubscriber<IReadOnlyList<ClientRegisterFailAlreadyExistsMessage>> failSubscriber,
            IEmailSenderService emailSender, 
            IPersonalDataServiceGrpc personalDataService)
        {
            _logger = logger;
            _emailSender = emailSender;
            _personalDataService = personalDataService;

            sessionAudit.Subscribe(HandleEvent);
            registerSubscriber.Subscribe(HandleEvent);
            failSubscriber.Subscribe(HandleEvent);
        }

        private async ValueTask HandleEvent(IReadOnlyList<SessionAuditEvent> events)
        {
            var taskList = new List<Task>();
            
            foreach (var auditEvent in events.Where(e => e.Action == SessionAuditEvent.SessionAction.Login))
            {
                var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                {
                    Id = auditEvent.Session.TraderId
                });
                if (pd.PersonalData != null)
                {
                    var task = _emailSender.SendLoginEmailAsync(new ()
                    {
                        Brand = auditEvent.Session.BrandId,
                        Lang = "En",
                        Platform = pd.PersonalData.PlatformType,
                        Email = pd.PersonalData.Email,
                        Ip = auditEvent.Session.IP,
                        LoginTime = auditEvent.Session.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
                    }).AsTask();
                    taskList.Add(task);
                }
            }
            await Task.WhenAll(taskList);
        }
        
        private async ValueTask HandleEvent(IReadOnlyList<ClientRegisterMessage> messages)
        {
            var taskList = new List<Task>();
            
            foreach (var message in messages)
            {
                var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                {
                    Id = message.TraderId
                });
                if (pd.PersonalData != null)
                {
                    var task = _emailSender.SendRegistrationConfirmAsync(new ()
                    {
                        Brand = pd.PersonalData.BrandId,
                        Lang = "En",
                        Platform = pd.PersonalData.PlatformType,
                        Email = pd.PersonalData.Email,
                        TraderId = message.TraderId,
                    }).AsTask();
                    taskList.Add(task);
                }
            }
            await Task.WhenAll(taskList);
        }
        
        private async ValueTask HandleEvent(IReadOnlyList<ClientRegisterFailAlreadyExistsMessage> messages)
        {
            var taskList = new List<Task>();
            
            foreach (var message in messages)
            {
                var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                {
                    Id = message.TraderId
                });
                if (pd.PersonalData != null)
                {
                    var task = _emailSender.SendAlreadyRegisteredEmailAsync(new ()
                    {
                        Brand = pd.PersonalData.BrandId,
                        Lang = "En",
                        Platform = pd.PersonalData.PlatformType,
                        Email = pd.PersonalData.Email,
                        TraderId = message.TraderId,
                        Ip = message.IpAddress,
                        UserAgent = message.UserAgent
                    }).AsTask();
                    taskList.Add(task);
                }
            }
            await Task.WhenAll(taskList);
        }
    }
}