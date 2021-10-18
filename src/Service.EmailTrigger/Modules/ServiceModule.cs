using Autofac;
using MyJetWallet.Sdk.Authorization.ServiceBus;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.ServiceBus;
using MyServiceBus.Abstractions;
using Service.EmailSender.Client;
using Service.EmailTrigger.Jobs;
using Service.PersonalData.Client;
using Service.Registration.Client;
using Service.VerificationCodes.Client;

namespace Service.EmailTrigger.Modules
{
    public class ServiceModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var queueName = "Spot-EmailTrigger";
            var spotServiceBusClient = builder.RegisterMyServiceBusTcpClient(Program.ReloadedSettings(e => e.SpotServiceBusHostPort), Program.LogFactory);
            builder.RegisterClientRegisteredSubscriber(spotServiceBusClient, queueName);
            builder.RegisterClientRegisterFailAlreadyExistsSubscriber(spotServiceBusClient, queueName);

            var authServiceBus =
                builder.RegisterMyServiceBusTcpClient(Program.ReloadedSettings(e => e.AuthServiceBusHostPort), Program.LogFactory);
            
            builder.RegisterMyServiceBusSubscriberBatch<SessionAuditEvent>(authServiceBus, SessionAuditEvent.TopicName, queueName, TopicQueueType.Permanent);

            builder.RegisterEmailSenderClient(Program.Settings.EmailSenderGrpcServiceUrl);
            builder.RegisterPersonalDataClient(Program.Settings.PersonalDataServiceUrl);
            builder.RegisterVerificationCodesClient(Program.Settings.VerificationCodesGrpcUrl);
            
            builder
                .RegisterType<EmailNotificator>()
                .AutoActivate()
                .SingleInstance();
        }
    }
}