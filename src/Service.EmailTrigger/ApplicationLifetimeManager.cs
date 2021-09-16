using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyJetWallet.Sdk.Service;
using MyServiceBus.TcpClient;

namespace Service.EmailTrigger
{
    public class ApplicationLifetimeManager : ApplicationLifetimeManagerBase
    {
        private readonly ILogger<ApplicationLifetimeManager> _logger;
        private readonly MyServiceBusTcpClient[] _serviceBusTcpClients;

        public ApplicationLifetimeManager(IHostApplicationLifetime appLifetime, ILogger<ApplicationLifetimeManager> logger, MyServiceBusTcpClient[] serviceBusTcpClients)
            : base(appLifetime)
        {
            _logger = logger;
            _serviceBusTcpClients = serviceBusTcpClients;
        }

        protected override void OnStarted()
        {
            _logger.LogInformation("OnStarted has been called.");
            foreach (var client in _serviceBusTcpClients)
            {
                client.Start();    
            }
        }

        protected override void OnStopping()
        {
            _logger.LogInformation("OnStopping has been called.");
            foreach (var client in _serviceBusTcpClients)
            {
                client.Stop();    
            }
        }

        protected override void OnStopped()
        {
            _logger.LogInformation("OnStopped has been called.");
        }
    }
}
