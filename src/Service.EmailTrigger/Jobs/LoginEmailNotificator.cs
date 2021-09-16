using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DotNetCoreDecorators;
using Microsoft.Extensions.Logging;
using MyJetWallet.Sdk.Authorization.ServiceBus;
using SimpleTrading.EmailSender.Grpc;
using SimpleTrading.EmailSender.Grpc.Contracts.Spot;
using SimpleTrading.PersonalData.Grpc;

namespace Service.EmailTrigger.Jobs
{
    public class LoginEmailNotificator
    {
        private readonly ISpotEmailSenderApi _emailSender;
        private readonly ILogger<LoginEmailNotificator> _logger;
        private readonly IPersonalDataServiceGrpc _personalDataService;

        public LoginEmailNotificator(ILogger<LoginEmailNotificator> logger,
            ISubscriber<IReadOnlyList<SessionAuditEvent>> sessionAudit, 
            ISpotEmailSenderApi emailSender, 
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
                var pd = await _personalDataService.GetByIdAsync(auditEvent.Session.TraderId);
                if (pd.PersonalData != null)
                {
                    var task = _emailSender.SendLoginEmailAsync(new SpotLoginEmailGrpcRequestContract
                    {
                        Brand = auditEvent.Session.BrandId,
                        Lang = "En",
                        Platform = pd.PersonalData.PlatformType,
                        Email = pd.PersonalData.Email,
                        Ip = auditEvent.Session.IP,
                        LoginTime = auditEvent.Session.CreateTime.ToString(CultureInfo.InvariantCulture)
                    }).AsTask();
                    taskList.Add(task);
                }
            }
            await Task.WhenAll(taskList);
        }
    }
}