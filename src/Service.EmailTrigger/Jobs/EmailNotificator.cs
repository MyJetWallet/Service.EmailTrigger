using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetCoreDecorators;
using Microsoft.Extensions.Logging;
using MyJetWallet.Sdk.Authorization.ServiceBus;
using Service.Bitgo.DepositDetector.Domain.Models;
using Service.Bitgo.WithdrawalProcessor.Domain.Models;
using Service.ClientBlocker.Domain.Models;
using Service.ClientProfile.Domain.Models;
using Service.EmailSender.Grpc;
using Service.EmailSender.Grpc.Models;
using Service.HighYieldEngine.Domain.Models;
using Service.InternalTransfer.Domain.Models;
using Service.KYC.Domain.Models.Enum;
using Service.KYC.Domain.Models.Messages;
using Service.MessageTemplates.Client;
using Service.PersonalData.Grpc;
using Service.PersonalData.Grpc.Contracts;
using Service.PersonalData.Grpc.Models;
using Service.Registration.Domain.Models;
using Service.VerificationCodes.Grpc;
using Service.VerificationCodes.Grpc.Models;

namespace Service.EmailTrigger.Jobs
{
    public class EmailNotificator
    {
        private readonly IEmailSenderService _emailSender;
        private readonly ILogger<EmailNotificator> _logger;
        private readonly IPersonalDataServiceGrpc _personalDataService;
        private readonly IEmailVerificationCodes _verificationCodes;
        private readonly ITemplateClient _templateClient;

        public EmailNotificator(ILogger<EmailNotificator> logger,
            ISubscriber<IReadOnlyList<SessionAuditEvent>> sessionAudit,
            ISubscriber<IReadOnlyList<ClientRegisterFailAlreadyExistsMessage>> failSubscriber,
            ISubscriber<IReadOnlyList<Deposit>> depositSubscriber,
            ISubscriber<IReadOnlyList<Withdrawal>> withdrawalSubscriber,
            ISubscriber<IReadOnlyList<Transfer>> transferSubscriber,
            ISubscriber<IReadOnlyList<ClientProfileUpdateMessage>> clientProfileSubscriber,
            ISubscriber<IReadOnlyList<ClientOfferTerminate>> highYieldTerminateClientOffer,
            IEmailSenderService emailSender,
            IPersonalDataServiceGrpc personalDataService,
            IEmailVerificationCodes verificationCodes,
            ISubscriber<IReadOnlyList<KycProfileUpdatedMessage>> kycSubscriber,
            ISubscriber<IReadOnlyList<ClientBlockerMessage>> clientSubscriber, 
			ITemplateClient templateClient)
        {
            _logger = logger;
            _emailSender = emailSender;
            _personalDataService = personalDataService;
            _verificationCodes = verificationCodes;
	        _templateClient = templateClient;

	        sessionAudit.Subscribe(HandleEvent);
            failSubscriber.Subscribe(HandleEvent);
            depositSubscriber.Subscribe(HandleEvent);
            withdrawalSubscriber.Subscribe(HandleEvent);
            transferSubscriber.Subscribe(HandleEvent);
            kycSubscriber.Subscribe(HandleEvent);
            clientProfileSubscriber.Subscribe(HandleEvent);
            clientSubscriber.Subscribe(HandleEvent);
            highYieldTerminateClientOffer.Subscribe(HandleEvent);
        }

        private async ValueTask HandleEvent(IReadOnlyList<ClientBlockerMessage> blockers)
        {
            var taskList = new List<Task>();
            foreach (var blocker in blockers)
            {
                if (blocker.BlockerType == BlockerType.Login1st)
                {
                    var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest
                    {
                        Id = blocker.ClientId
                    });
                    if (pd.PersonalData != null)
                    {
                        var task = _emailSender.SendSignInFailed1HEmailAsync(new ()
                        {
                            Brand = pd.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pd.PersonalData.PlatformType,
                            Email = pd.PersonalData.Email
                        }).AsTask();
                        taskList.Add(task);
                        _logger.LogInformation("Sending SignInFailed1HEmail to userId {userId}", blocker.ClientId);
                    }
                }
                if (blocker.BlockerType == BlockerType.Login2nd)
                {
                    var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest
                    {
                        Id = blocker.ClientId
                    });
                    if (pd.PersonalData != null)
                    {
                        var task = _emailSender.SendSignInFailed24HEmailAsync(new ()
                        {
                            Brand = pd.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pd.PersonalData.PlatformType,
                            Email = pd.PersonalData.Email
                        }).AsTask();
                        taskList.Add(task);
                        _logger.LogInformation("Sending SignInFailed24HEmail to userId {userId}", blocker.ClientId);
                    }
                }
                if (blocker.BlockerType is BlockerType.Login2FA1st or BlockerType.Resend2FA1st)
                {
                    var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest
                    {
                        Id = blocker.ClientId
                    });
                    if (pd.PersonalData != null)
                    {
                        var task = _emailSender.SendSignInFailed2Fa1HEmailAsync(new ()
                        {
                            Brand = pd.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pd.PersonalData.PlatformType,
                            Email = pd.PersonalData.Email
                        }).AsTask();
                        taskList.Add(task);
                        _logger.LogInformation("Sending SignInFailed2Fa1HEmail to userId {userId}", blocker.ClientId);
                    }
                }
                if (blocker.BlockerType is BlockerType.Login2FA2nd or BlockerType.Resend2FA2nd)
                {
                    var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest
                    {
                        Id = blocker.ClientId
                    });
                    if (pd.PersonalData != null)
                    {
                        var task = _emailSender.SendSignInFailed2Fa24HEmailAsync(new ()
                        {
                            Brand = pd.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pd.PersonalData.PlatformType,
                            Email = pd.PersonalData.Email
                        }).AsTask();
                        taskList.Add(task);
                        _logger.LogInformation("Sending SignInFailed2Fa24HEmail to userId {userId}", blocker.ClientId);
                    }
                }
                if (blocker.BlockerType is BlockerType.PhoneNumberUpdate)
                {
                    var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest
                    {
                        Id = blocker.ClientId
                    });
                    if (pd.PersonalData != null)
                    {
                        var task = _emailSender.SendSuspiciousActivityEmailAsync(new ()
                        {
                            Brand = pd.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pd.PersonalData.PlatformType,
                            Email = pd.PersonalData.Email
                        }).AsTask();
                        taskList.Add(task);
                        _logger.LogInformation("Sending SuspiciousActivityEmail to userId {userId}", blocker.ClientId);
                    }
                }
            }
            await Task.WhenAll(taskList);
        }

        private async ValueTask HandleEvent(IReadOnlyList<ClientProfileUpdateMessage> profileUpdates)
        {
            var taskList = new List<Task>();
            foreach (var profileUpdate in profileUpdates)
            {
                if (profileUpdate.OldProfile.Status2FA != profileUpdate.NewProfile.Status2FA)
                {
                    var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest
                    {
                        Id = profileUpdate.NewProfile.ClientId
                    });
                    if (pd.PersonalData != null)
                    {
                        var task = _emailSender.Send2FaSettingsChangedEmailAsync(new ()
                        {
                            Brand = pd.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pd.PersonalData.PlatformType,
                            Email = pd.PersonalData.Email
                        }).AsTask();
                        taskList.Add(task);
                        _logger.LogInformation("Sending 2FaSettingsChangedEmail to userId {userId}", profileUpdate.NewProfile.ClientId);
                    }
                }
            }
            await Task.WhenAll(taskList);
        }

        private async ValueTask HandleEvent(IReadOnlyList<KycProfileUpdatedMessage> profileUpdates)
        {
            var taskList = new List<Task>();
            foreach (var profileUpdate in profileUpdates)
            {
                if (profileUpdate.OldProfile.DepositStatus != KycOperationStatus.Blocked &&
                    profileUpdate.NewProfile.DepositStatus == KycOperationStatus.Blocked ||
                    profileUpdate.OldProfile.WithdrawalStatus != KycOperationStatus.Blocked &&
                    profileUpdate.NewProfile.WithdrawalStatus == KycOperationStatus.Blocked ||
                    profileUpdate.OldProfile.TradeStatus != KycOperationStatus.Blocked &&
                    profileUpdate.NewProfile.TradeStatus == KycOperationStatus.Blocked)
                {
                    var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest
                    {
                        Id = profileUpdate.ClientId
                    });
                    if (pd.PersonalData != null)
                    {
                        var task = _emailSender.SendKycBannedEmailAsync(new KycBannedEmailGrpcRequestContract
                        {
                            Brand = pd.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pd.PersonalData.PlatformType,
                            Email = pd.PersonalData.Email
                        }).AsTask();
                        taskList.Add(task);
                        _logger.LogInformation("Sending KycBannedEmail to userId {userId}", profileUpdate.ClientId);
                    }
                }

                if (profileUpdate.OldProfile.DepositStatus != KycOperationStatus.KycRequired &&
                    profileUpdate.NewProfile.DepositStatus == KycOperationStatus.KycRequired ||
                    profileUpdate.OldProfile.WithdrawalStatus != KycOperationStatus.KycRequired &&
                    profileUpdate.NewProfile.WithdrawalStatus == KycOperationStatus.KycRequired ||
                    profileUpdate.OldProfile.TradeStatus != KycOperationStatus.KycRequired &&
                    profileUpdate.NewProfile.TradeStatus == KycOperationStatus.KycRequired)
                {
                    var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest
                    {
                        Id = profileUpdate.ClientId
                    });
                    if (pd.PersonalData != null)
                    {
                        var task = _emailSender.SendKycDocumentsDeclinedEmailAsync(
                            new KycDeclinedEmailGrpcRequestContract
                            {
                                Brand = pd.PersonalData.BrandId,
                                Lang = "En",
                                Platform = pd.PersonalData.PlatformType,
                                Email = pd.PersonalData.Email
                            }).AsTask();
                        taskList.Add(task);
                        _logger.LogInformation("Sending KycDocumentsDeclinedEmail to userId {userId}",
                            profileUpdate.ClientId);
                    }
                }

                if (profileUpdate.OldProfile.DepositStatus != KycOperationStatus.Allowed &&
                    profileUpdate.NewProfile.DepositStatus == KycOperationStatus.Allowed ||
                    profileUpdate.OldProfile.WithdrawalStatus != KycOperationStatus.Allowed &&
                    profileUpdate.NewProfile.WithdrawalStatus == KycOperationStatus.Allowed ||
                    profileUpdate.OldProfile.TradeStatus != KycOperationStatus.Allowed &&
                    profileUpdate.NewProfile.TradeStatus == KycOperationStatus.Allowed)
                {
                    var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest
                    {
                        Id = profileUpdate.ClientId
                    });
                    if (pd.PersonalData != null)
                    {
                        var task = _emailSender.SendKycDocumentsApprovedEmailAsync(
                            new KycApprovedEmailGrpcRequestContract
                            {
                                Brand = pd.PersonalData.BrandId,
                                Lang = "En",
                                Platform = pd.PersonalData.PlatformType,
                                Email = pd.PersonalData.Email
                            }).AsTask();
                        taskList.Add(task);
                        _logger.LogInformation("Sending KycDocumentsApprovedEmail to userId {userId}",
                            profileUpdate.ClientId);
                    }
                }
            }

            await Task.WhenAll(taskList);
        }

        private async ValueTask HandleEvent(IReadOnlyList<SessionAuditEvent> events)
        {
            var taskList = new List<Task>();

            foreach (var auditEvent in events.Where(e => e.Action == SessionAuditEvent.SessionAction.Login))
            {
                var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest
                {
                    Id = auditEvent.Session.TraderId
                });
                
                if (pd.PersonalData == null)
                    continue;

	            var platform = auditEvent.Session.PlatformType.ToString();

	            if (pd.PersonalData.Confirm == null)
                {
                    var task = _verificationCodes.SendEmailVerificationCodeAsync(new SendVerificationCodeRequest
                    {
                        ClientId = pd.PersonalData.Id,
                        Lang = "En",
                        Brand = auditEvent.Session.BrandId,
                        PlatformType = platform,
                        DeviceType = "Unknown"
                    });
                    taskList.Add(task);
                }
                else
                {
                    var task = _emailSender.SendLoginEmailAsync(new LoginEmailGrpcRequestContract
                    {
                        Brand = auditEvent.Session.BrandId,
                        Lang = "En",
                        Platform = platform,
                        Email = pd.PersonalData.Email,
                        Ip = auditEvent.Session.IP,
                        LoginTime = auditEvent.Session.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        Location = auditEvent.Session.Location,
                        PhoneModel = auditEvent.Session.PhoneModel
                    }).AsTask();
                    taskList.Add(task);
                }
                
                _logger.LogInformation("Sending LoginEmail to userId {userId}", auditEvent.Session.TraderId);
            }

            await Task.WhenAll(taskList);
        }

        private async ValueTask HandleEvent(IReadOnlyList<ClientRegisterFailAlreadyExistsMessage> messages)
        {
            var taskList = new List<Task>();

            foreach (var message in messages)
            {
                var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest
                {
                    Id = message.TraderId
                });
                if (pd.PersonalData != null)
                {
                    var task = _emailSender.SendAlreadyRegisteredEmailAsync(
                        new AlreadyRegisteredEmailGrpcRequestContract
                        {
                            Brand = pd.PersonalData.BrandId,
                            Lang = "En",
                            Platform = message.PlatformType,
                            Email = pd.PersonalData.Email,
                            TraderId = message.TraderId,
                            Ip = message.IpAddress,
                            UserAgent = message.UserAgent,
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
                var pdSender = await _personalDataService.GetByIdAsync(new GetByIdRequest
                {
                    Id = message.ClientId
                });
                if (pdSender.PersonalData != null)
                {
                    var task = message.IsInternal
                        ? _emailSender.SendTransferCancelledEmailAsync(
                            new()
                            {
                                Brand = pdSender.PersonalData.BrandId,
                                Lang = "En",
                                Platform = pdSender.PersonalData.PlatformType,
                                Email = pdSender.PersonalData.Email,
                                AssetSymbol = message.AssetSymbol,
                                Amount = message.Amount.ToString(),
                                OperationId = message.Id.ToString(),
                                Timestamp = message.EventDate.ToString("f"),
                            }).AsTask()
                        : _emailSender.SendWithdrawalCancelledEmailAsync(
                            new()
                            {
                                Brand = pdSender.PersonalData.BrandId,
                                Lang = "En",
                                Platform = pdSender.PersonalData.PlatformType,
                                Email = pdSender.PersonalData.Email,
                                AssetSymbol = message.AssetSymbol,
                                Amount = message.Amount.ToString(),
                                OperationId = message.Id.ToString(),
                                Timestamp = message.EventDate.ToString("f"),
                            }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending WithdrawalSuccessfulEmail to userId {userId}",
                        message.ClientId);
                }
            }

            foreach (var message in messages.Where(t => t.Status == WithdrawalStatus.Success))
            {
                var pdSender = await _personalDataService.GetByIdAsync(new GetByIdRequest
                {
                    Id = message.ClientId
                });
                if (pdSender.PersonalData != null)
                {
                    var task = message.IsInternal
                        ? _emailSender.SendTransferSuccessfulEmailAsync(new()
                        {
                            Brand = pdSender.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pdSender.PersonalData.PlatformType,
                            Email = pdSender.PersonalData.Email,
                            AssetSymbol = message.AssetSymbol,
                            Amount = message.Amount.ToString(),
                            FullName = pdSender.PersonalData.FirstName,
                            OperationId = message.Id.ToString(),
                            Timestamp = message.EventDate.ToString("f"),
                        }) .AsTask()
                        : _emailSender.SendWithdrawalSuccessfulEmailAsync(
                        new ()
                        {
                            Brand = pdSender.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pdSender.PersonalData.PlatformType,
                            Email = pdSender.PersonalData.Email,
                            AssetSymbol = message.AssetSymbol,
                            Amount = message.Amount.ToString(),
                            FullName = pdSender.PersonalData.FirstName,
                            OperationId = message.Id.ToString(),
                            Timestamp = message.EventDate.ToString("f"),

                        }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending WithdrawalSuccessfulEmail to userId {userId}", message.ClientId);
                }

                if (!message.IsInternal)
                    continue;

                var pdReceiver = await _personalDataService.GetByIdAsync(new GetByIdRequest
                {
                    Id = message.DestinationClientId
                });
                if (pdReceiver.PersonalData != null)
                {
                    var task = _emailSender.SendTransferReceivedEmailAsync(new ()
                    {
                        Brand = pdReceiver.PersonalData.BrandId,
                        Lang = "En",
                        Platform = pdReceiver.PersonalData.PlatformType,
                        Email = pdReceiver.PersonalData.Email,
                        AssetSymbol = message.AssetSymbol,
                        Amount = message.Amount.ToString(),
                        OperationId = message.Id.ToString(),
                        Timestamp = message.EventDate.ToString("f"),
                    }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending TransferReceivedEmail to userId {userId}", message.ClientId);
                }
            }

            await Task.WhenAll(taskList);
        }

        private async ValueTask HandleEvent(IReadOnlyList<Deposit> messages)
        {
            var taskList = new List<Task>();

            foreach (var message in messages.Where(t => t.Status == DepositStatus.Processed))
            {
                var pd = await _personalDataService.GetByIdAsync(new GetByIdRequest
                {
                    Id = message.ClientId
                });
                if (pd.PersonalData != null)
                {
                    var task = _emailSender.SendDepositSuccessfulEmailAsync(new()
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
                var pdSender = await _personalDataService.GetByIdAsync(new GetByIdRequest
                {
                    Id = message.ClientId
                });
                if (pdSender.PersonalData != null)
                {
                    var task = _emailSender.SendTransferCancelledEmailAsync(new ()
                    {
                        Brand = pdSender.PersonalData.BrandId,
                        Lang = "En",
                        Platform = pdSender.PersonalData.PlatformType,
                        Email = pdSender.PersonalData.Email,
                        AssetSymbol = message.AssetSymbol,
                        Amount = message.Amount.ToString(),
                        OperationId = message.Id.ToString(),
                        Timestamp = message.EventDate.ToString("f"),
                    }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending TransferSuccessfulEmail to userId {userId}", message.ClientId);
                }
            }

            foreach (var message in messages.Where(t => t.Status == TransferStatus.Completed))
            {
                var pdSender = await _personalDataService.GetByIdAsync(new GetByIdRequest
                {
                    Id = message.ClientId
                });
                if (pdSender.PersonalData != null)
                {
                    var task = _emailSender.SendTransferSuccessfulEmailAsync(
                        new ()
                        {
                            Brand = pdSender.PersonalData.BrandId,
                            Lang = "En",
                            Platform = pdSender.PersonalData.PlatformType,
                            Email = pdSender.PersonalData.Email,
                            AssetSymbol = message.AssetSymbol,
                            Amount = message.Amount.ToString(),
                            FullName = pdSender.PersonalData.FirstName,
                            OperationId = message.Id.ToString(),
                            Timestamp = message.EventDate.ToString("f"),
                        }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending TransferSuccessfulEmail to userId {userId}", message.ClientId);
                }

                var pdReceiver = await _personalDataService.GetByIdAsync(new GetByIdRequest
                {
                    Id = message.DestinationClientId
                });
                if (pdReceiver.PersonalData != null)
                {
                    var task = _emailSender.SendTransferReceivedEmailAsync(new ()
                    {
                        Brand = pdReceiver.PersonalData.BrandId,
                        Lang = "En",
                        Platform = pdReceiver.PersonalData.PlatformType,
                        Email = pdReceiver.PersonalData.Email,
                        AssetSymbol = message.AssetSymbol,
                        Amount = message.Amount.ToString(),
                        OperationId = message.Id.ToString(),
                        Timestamp = message.EventDate.ToString("f"),
                    }).AsTask();
                    taskList.Add(task);
                    _logger.LogInformation("Sending TransferReceivedEmail to userId {userId}", message.ClientId);
                }
            }

            await Task.WhenAll(taskList);
        }

	    private async ValueTask HandleEvent(IReadOnlyList<ClientOfferTerminate> messages)
	    {
		    var taskList = new List<Task>();

		    foreach (var message in messages)
		    {
                if (message.Amount <= 0)
                    continue;
                
			    PersonalDataGrpcResponseContract personalDataResponse = await _personalDataService.GetByIdAsync(new GetByIdRequest
			    {
				    Id = message.ClientId
			    });

			    PersonalDataGrpcModel personalData = personalDataResponse.PersonalData;
			    if (personalData == null) 
					continue;

                string title = await _templateClient.GetTemplateBody(message.OfferNameTemplateId, personalData.BrandId, "En");

			    var task = _emailSender.SendClientOfferTerminateEmailAsync(new()
			    {
				    Brand = personalData.BrandId,
				    Lang = "En",
				    Platform = personalData.PlatformType,
				    Email = personalData.Email,
				    AssetSymbol = message.AssetSymbol,
				    Amount = message.Amount.ToString(),
				    SubscriptionName = title,
				    InterestEarn = message.InterestEarn
			    }).AsTask();

			    taskList.Add(task);

			    _logger.LogInformation("Sending ClientOfferTerminate to userId {userId}", message.ClientId);
		    }

		    await Task.WhenAll(taskList);
	    }
    }
}