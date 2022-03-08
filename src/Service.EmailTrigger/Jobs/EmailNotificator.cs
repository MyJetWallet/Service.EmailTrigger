using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetCoreDecorators;
using Microsoft.Extensions.Logging;
using MyJetWallet.Sdk.Authorization.ServiceBus;
using Service.Bitgo.DepositDetector.Domain.Models;
using Service.Bitgo.WithdrawalProcessor.Domain.Models;
using Service.EmailSender.Grpc;
using Service.InternalTransfer.Domain.Models;
using Service.KYC.Domain.Models.Enum;
using Service.KYC.Domain.Models.Messages;
using Service.PersonalData.Grpc;
using Service.PersonalData.Grpc.Contracts;
using Service.Registration.Domain.Models;
using Service.VerificationCodes.Grpc;

namespace Service.EmailTrigger.Jobs
{
    public class EmailNotificator
    {
        private readonly IEmailSenderService _emailSender;
        private readonly ILogger<EmailNotificator> _logger;
        private readonly IPersonalDataServiceGrpc _personalDataService;
        private readonly IEmailVerificationCodes _verificationCodes;

        public EmailNotificator(ILogger<EmailNotificator> logger,
            ISubscriber<IReadOnlyList<SessionAuditEvent>> sessionAudit, 
            ISubscriber<IReadOnlyList<ClientRegisterMessage>> registerSubscriber, 
            ISubscriber<IReadOnlyList<ClientRegisterFailAlreadyExistsMessage>> failSubscriber,
            ISubscriber<IReadOnlyList<Deposit>> depositSubscriber,
            ISubscriber<IReadOnlyList<Withdrawal>> withdrawalSubscriber,
            ISubscriber<IReadOnlyList<Transfer>> transferSubscriber,
            IEmailSenderService emailSender, 
            IPersonalDataServiceGrpc personalDataService, 
            IEmailVerificationCodes verificationCodes,
            ISubscriber<IReadOnlyList<KycProfileUpdatedMessage>> kycSubscriber)
        {
            _logger = logger;
            _emailSender = emailSender;
            _personalDataService = personalDataService;
            _verificationCodes = verificationCodes;

            sessionAudit.Subscribe(HandleEvent);
            registerSubscriber.Subscribe(HandleEvent);
            failSubscriber.Subscribe(HandleEvent);
            depositSubscriber.Subscribe(HandleEvent);
            withdrawalSubscriber.Subscribe(HandleEvent);
            transferSubscriber.Subscribe(HandleEvent);
            kycSubscriber.Subscribe(HandleEvent);
        }

        private async ValueTask HandleEvent(IReadOnlyList<KycProfileUpdatedMessage> profileUpdates)
        {
            var taskList = new List<Task>();
            foreach (var profileUpdate in profileUpdates)
            {
                if (string.IsNullOrWhiteSpace(profileUpdate.OldProfile.BlockingReason) &&
                    !string.IsNullOrWhiteSpace(profileUpdate.NewProfile.BlockingReason))
                {
                    var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                    {
                        Id = profileUpdate.ClientId
                    });
                    if (pd.PersonalData != null)
                    {
                        var task = _emailSender.SendKycBannedEmailAsync(new ()
                        {
                            Brand = pd.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pd.PersonalData.PlatformType,
                            Email = pd.PersonalData.Email,
                        }).AsTask();
                        taskList.Add(task);
                        _logger.LogInformation("Sending KycBannedEmail to userId {userId}", profileUpdate.ClientId);
                    }
                }

                if (profileUpdate.OldProfile.DepositStatus == KycOperationStatus.KycInProgress &&
                    profileUpdate.OldProfile.DepositStatus == KycOperationStatus.KycRequired)
                {
                    var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                    {
                        Id = profileUpdate.ClientId
                    });
                    if (pd.PersonalData != null)
                    {
                        var task = _emailSender.SendKycDocumentsDeclinedEmailAsync(new ()
                        {
                            Brand = pd.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pd.PersonalData.PlatformType,
                            Email = pd.PersonalData.Email,
                        }).AsTask();
                        taskList.Add(task);
                        _logger.LogInformation("Sending KycDocumentsDeclinedEmail to userId {userId}", profileUpdate.ClientId);
                    }
                }

                if (profileUpdate.OldProfile.DepositStatus == KycOperationStatus.KycInProgress &&
                    profileUpdate.OldProfile.DepositStatus == KycOperationStatus.Allowed)
                {
                    var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                    {
                        Id = profileUpdate.ClientId
                    });
                    if (pd.PersonalData != null)
                    {
                        var task = _emailSender.SendKycDocumentsApprovedEmailAsync(new ()
                        {
                            Brand = pd.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pd.PersonalData.PlatformType,
                            Email = pd.PersonalData.Email,
                        }).AsTask();
                        taskList.Add(task);
                        _logger.LogInformation("Sending KycDocumentsApprovedEmail to userId {userId}", profileUpdate.ClientId);
                    }
                }
            }
            
            await Task.WhenAll(taskList);
        }

        private async ValueTask HandleEvent(IReadOnlyList<SessionAuditEvent> events)
        {
            var taskList = new List<Task>();
            
            foreach (var auditEvent in events.Where(e => e.Action == SessionAuditEvent.SessionAction.Login && !e.Session.Description.Contains("Registration")))
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
                    _logger.LogInformation("Sending LoginEmail to userId {userId}", auditEvent.Session.TraderId);
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
                    var task = _verificationCodes.SendEmailVerificationCodeAsync(new ()
                    {
                        ClientId = message.TraderId,
                        Brand = pd.PersonalData.BrandId,
                        DeviceType = "Unknown",
                        Lang = "En",
                    }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending EmailVerificationCode to userId {userId}", message.TraderId);

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
                    _logger.LogInformation("Sending AlreadyRegisteredEmail to userId {userId}", message.TraderId);
                }
            }
            await Task.WhenAll(taskList);
        }
        
        private async ValueTask HandleEvent(IReadOnlyList<Withdrawal> messages)
        {
            var taskList = new List<Task>();
            
            foreach (var message in messages.Where(t => t.Status == WithdrawalStatus.Cancelled))
            {
                var pdSender = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                {
                    Id = message.ClientId
                });
                if (pdSender.PersonalData != null)
                {
                    var task = _emailSender.SendWithdrawalCancelledEmailAsync(new ()
                    {
                        Brand = pdSender.PersonalData.BrandId,
                        Lang = "En",
                        Platform = pdSender.PersonalData.PlatformType,
                        Email = pdSender.PersonalData.Email,
                        AssetSymbol = message.AssetSymbol,
                        Amount = message.Amount.ToString(),
                    }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending WithdrawalSuccessfulEmail to userId {userId}", message.ClientId);
                }
            }
            
            foreach (var message in messages.Where(t=>t.Status == WithdrawalStatus.Success))
            {
                var pdSender = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                {
                    Id = message.ClientId
                });
                if (pdSender.PersonalData != null)
                {
                    var task = _emailSender.SendWithdrawalSuccessfulEmailAsync(new ()
                    {
                        Brand = pdSender.PersonalData.BrandId,
                        Lang = "En",
                        Platform = pdSender.PersonalData.PlatformType,
                        Email = pdSender.PersonalData.Email,
                        AssetSymbol = message.AssetSymbol,
                        Amount = message.Amount.ToString(),
                        FullName = pdSender.PersonalData.FirstName,
                    }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending WithdrawalSuccessfulEmail to userId {userId}", message.ClientId);
                }
                
                if(!message.IsInternal)
                    continue;
                
                var pdReceiver = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                {
                    Id = message.DestinationClientId
                });
                if (pdReceiver.PersonalData != null)
                {
                    var task = _emailSender.SendDepositSuccessfulEmailAsync(new ()
                    {
                        Brand = pdReceiver.PersonalData.BrandId,
                        Lang = "En",
                        Platform = pdReceiver.PersonalData.PlatformType,
                        Email = pdReceiver.PersonalData.Email,
                        AssetSymbol = message.AssetSymbol,
                        Amount = message.Amount.ToString(),
                    }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending DepositSuccessfulEmail to userId {userId}", message.ClientId);
                }
            }
            await Task.WhenAll(taskList);
        }
        
        private async ValueTask HandleEvent(IReadOnlyList<Deposit> messages)
        {
            var taskList = new List<Task>();
            
            foreach (var message in messages.Where(t=>t.Status == DepositStatus.Processed))
            {
                var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                {
                    Id = message.ClientId
                });
                if (pd.PersonalData != null)
                {
                    var task = _emailSender.SendDepositSuccessfulEmailAsync(new ()
                    {
                        Brand = pd.PersonalData.BrandId,
                        Lang = "En",
                        Platform = pd.PersonalData.PlatformType,
                        Email = pd.PersonalData.Email,
                        AssetSymbol = message.AssetSymbol,
                        Amount = message.Amount.ToString(),
                    }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending DepositSuccessfulEmail to userId {userId}", message.ClientId);
                }
            }
            await Task.WhenAll(taskList);
        }
        
        private async ValueTask HandleEvent(IReadOnlyList<Transfer> messages)
        {
            var taskList = new List<Task>();

            foreach (var message in messages.Where(t => t.Status == TransferStatus.Cancelled))
            {
                var pdSender = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                {
                    Id = message.ClientId
                });
                if (pdSender.PersonalData != null)
                {
                    var task = _emailSender.SendWithdrawalCancelledEmailAsync(new ()
                    {
                        Brand = pdSender.PersonalData.BrandId,
                        Lang = "En",
                        Platform = pdSender.PersonalData.PlatformType,
                        Email = pdSender.PersonalData.Email,
                        AssetSymbol = message.AssetSymbol,
                        Amount = message.Amount.ToString(),
                    }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending WithdrawalSuccessfulEmail to userId {userId}", message.ClientId);
                }
            }

            foreach (var message in messages.Where(t=>t.Status == TransferStatus.Completed))
            {
                var pdSender = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                {
                    Id = message.ClientId
                });
                if (pdSender.PersonalData != null)
                {
                    var task = _emailSender.SendWithdrawalSuccessfulEmailAsync(new ()
                    {
                        Brand = pdSender.PersonalData.BrandId,
                        Lang = "En",
                        Platform = pdSender.PersonalData.PlatformType,
                        Email = pdSender.PersonalData.Email,
                        AssetSymbol = message.AssetSymbol,
                        Amount = message.Amount.ToString(),
                        FullName = pdSender.PersonalData.FirstName,
                    }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending WithdrawalSuccessfulEmail to userId {userId}", message.ClientId);
                }
                
                var pdReceiver = await _personalDataService.GetByIdAsync(new GetByIdRequest()
                {
                    Id = message.DestinationClientId
                });
                if (pdReceiver.PersonalData != null)
                {
                    var task = _emailSender.SendDepositSuccessfulEmailAsync(new ()
                    {
                        Brand = pdReceiver.PersonalData.BrandId,
                        Lang = "En",
                        Platform = pdReceiver.PersonalData.PlatformType,
                        Email = pdReceiver.PersonalData.Email,
                        AssetSymbol = message.AssetSymbol,
                        Amount = message.Amount.ToString(),
                    }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending DepositSuccessfulEmail to userId {userId}", message.ClientId);
                }
            }
            await Task.WhenAll(taskList);
        }
        
    }
}