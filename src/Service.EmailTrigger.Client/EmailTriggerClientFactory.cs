using JetBrains.Annotations;
using MyJetWallet.Sdk.Grpc;
using Service.EmailTrigger.Grpc;

namespace Service.EmailTrigger.Client
{
    [UsedImplicitly]
    public class EmailTriggerClientFactory: MyGrpcClientFactory
    {
        public EmailTriggerClientFactory(string grpcServiceUrl) : base(grpcServiceUrl)
        {
        }

        public IHelloService GetHelloService() => CreateGrpcService<IHelloService>();
    }
}
