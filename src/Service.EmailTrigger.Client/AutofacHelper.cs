using Autofac;
using Service.EmailTrigger.Grpc;

// ReSharper disable UnusedMember.Global

namespace Service.EmailTrigger.Client
{
    public static class AutofacHelper
    {
        public static void RegisterEmailTriggerClient(this ContainerBuilder builder, string grpcServiceUrl)
        {
            var factory = new EmailTriggerClientFactory(grpcServiceUrl);

            builder.RegisterInstance(factory.GetHelloService()).As<IHelloService>().SingleInstance();
        }
    }
}
