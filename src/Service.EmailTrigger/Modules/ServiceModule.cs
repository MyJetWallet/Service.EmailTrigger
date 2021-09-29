using Autofac;
using MyJetWallet.Sdk.Authorization.ServiceBus;
using MyJetWallet.Sdk.Grpc;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.ServiceBus;
using MyServiceBus.Abstractions;
using Service.EmailSender.Client;
using Service.EmailTrigger.Jobs;
using SimpleTrading.PersonalData.Grpc;

namespace Service.EmailTrigger.Modules
{
    public class ServiceModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var queueName = "Spot-EmailTrigger";

            var authServiceBus = MyServiceBusTcpClientFactory.Create(
                Program.ReloadedSettings(e => e.AuthServiceBusHostPort), ApplicationEnvironment.HostName,
                Program.LogFactory.CreateLogger("AuthServiceBus"));

            builder.RegisterMyServiceBusSubscriberBatch<SessionAuditEvent>(authServiceBus, SessionAuditEvent.TopicName, queueName, TopicQueueType.Permanent);

            builder.RegisterInstance(authServiceBus).SingleInstance();
            
            builder.RegisterEmailSenderClient(Program.Settings.EmailSenderGrpcServiceUrl);
            
            var personalDataClientFactory = new MyGrpcClientFactory(Program.Settings.PersonalDataServiceUrl);
            builder
                .RegisterInstance(personalDataClientFactory.CreateGrpcService<IPersonalDataServiceGrpc>())
                .As<IPersonalDataServiceGrpc>()
                .SingleInstance();
            
            builder
                .RegisterType<LoginEmailNotificator>()
                .AutoActivate()
                .SingleInstance();
        }
    }
}