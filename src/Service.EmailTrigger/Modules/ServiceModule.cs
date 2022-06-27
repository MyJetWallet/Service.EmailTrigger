using Autofac;
using MyJetWallet.Sdk.Authorization.ServiceBus;
using MyJetWallet.Sdk.NoSql;
using MyJetWallet.Sdk.ServiceBus;
using MyNoSqlServer.DataReader;
using MyServiceBus.Abstractions;
using Service.Bitgo.DepositDetector.Domain.Models;
using Service.Bitgo.WithdrawalProcessor.Domain.Models;
using Service.ClientBlocker.Domain.Models;
using Service.ClientProfile.Domain.Models;
using Service.EmailSender.Client;
using Service.EmailTrigger.Jobs;
using Service.HighYieldEngine.Domain.Models.Messages;
using Service.InternalTransfer.Domain.Models;
using Service.KYC.Domain.Models.Messages;
using Service.MessageTemplates.Client;
using Service.PersonalData.Client;
using Service.Registration.Client;
using Service.VerificationCodes.Client;

namespace Service.EmailTrigger.Modules
{
    public class ServiceModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            const string queueName = "Spot-EmailTrigger";
            var spotServiceBusClient = builder.RegisterMyServiceBusTcpClient(Program.ReloadedSettings(e => e.SpotServiceBusHostPort), Program.LogFactory);
            builder.RegisterClientRegisteredSubscriber(spotServiceBusClient, queueName);
            builder.RegisterClientRegisterFailAlreadyExistsSubscriber(spotServiceBusClient, queueName);
            builder.RegisterMyServiceBusSubscriberBatch<Withdrawal>(spotServiceBusClient, Withdrawal.TopicName, queueName, TopicQueueType.PermanentWithSingleConnection);
            builder.RegisterMyServiceBusSubscriberBatch<Deposit>(spotServiceBusClient, Deposit.TopicName, queueName, TopicQueueType.PermanentWithSingleConnection);
            builder.RegisterMyServiceBusSubscriberBatch<Transfer>(spotServiceBusClient, Transfer.TopicName, queueName, TopicQueueType.PermanentWithSingleConnection);
            builder.RegisterMyServiceBusSubscriberBatch<KycProfileUpdatedMessage>(spotServiceBusClient, KycProfileUpdatedMessage.TopicName, queueName, TopicQueueType.PermanentWithSingleConnection);
            builder.RegisterMyServiceBusSubscriberBatch<ClientProfileUpdateMessage>(spotServiceBusClient, ClientProfileUpdateMessage.TopicName, queueName, TopicQueueType.PermanentWithSingleConnection);
            builder.RegisterMyServiceBusSubscriberBatch<ClientBlockerMessage>(spotServiceBusClient, ClientBlockerMessage.TopicName, queueName, TopicQueueType.PermanentWithSingleConnection);
            builder.RegisterMyServiceBusSubscriberBatch<ClientOfferTerminate>(spotServiceBusClient, ClientOfferTerminate.TopicName, queueName, TopicQueueType.PermanentWithSingleConnection);

            var authServiceBus = builder.RegisterMyServiceBusTcpClient(Program.ReloadedSettings(e => e.AuthServiceBusHostPort), Program.LogFactory);
            builder.RegisterMyServiceBusSubscriberBatch<SessionAuditEvent>(authServiceBus, SessionAuditEvent.TopicName, queueName, TopicQueueType.Permanent);

            builder.RegisterEmailSenderClient(Program.Settings.EmailSenderGrpcServiceUrl);
            builder.RegisterPersonalDataClient(Program.Settings.PersonalDataServiceUrl);
            builder.RegisterVerificationCodesClient(Program.Settings.VerificationCodesGrpcUrl);

            IMyNoSqlSubscriber myNosqlClient = builder.CreateNoSqlClient(Program.Settings.MyNoSqlReaderHostPort, Program.LogFactory);
            builder.RegisterMessageTemplatesCachedClient(Program.Settings.MessageTemplatesGrpcServiceUrl, myNosqlClient);

            builder
                .RegisterType<EmailNotificator>()
                .AutoActivate()
                .SingleInstance();
        }
    }
}