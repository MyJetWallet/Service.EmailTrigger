using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetCoreDecorators;
using Microsoft.Extensions.Logging;
using MyJetWallet.Sdk.Authorization.ServiceBus;
using Service.EmailSender.Grpc;
using Service.PersonalData.Grpc;
using Service.PersonalData.Grpc.Contracts;

namespace Service.EmailTrigger.Jobs
{
    public class LoginEmailNotificator
    {
        private readonly IEmailSenderService _emailSender;
        private readonly ILogger<LoginEmailNotificator> _logger;
        private readonly IPersonalDataServiceGrpc _personalDataService;

        public LoginEmailNotificator(ILogger<LoginEmailNotificator> logger,
            ISubscriber<IReadOnlyList<SessionAuditEvent>> sessionAudit, 
            IEmailSenderService emailSender, 
            IPersonalDataServiceGrpc personalDataService)
        {
            _logger = logger;
            _emailSender = emailSender;
            _personalDataService = personalDataService;

            sessionAudit.Subscribe(HandleEvent);
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
    }
}