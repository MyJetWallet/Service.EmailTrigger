﻿using MyJetWallet.Sdk.Service;
using MyYamlParser;

namespace Service.EmailTrigger.Settings
{
    public class SettingsModel
    {
        [YamlProperty("EmailTrigger.SeqServiceUrl")]
        public string SeqServiceUrl { get; set; }

        [YamlProperty("EmailTrigger.ZipkinUrl")]
        public string ZipkinUrl { get; set; }

        [YamlProperty("EmailTrigger.ElkLogs")]
        public LogElkSettings ElkLogs { get; set; }
        
        [YamlProperty("EmailTrigger.AuthServiceBusHostPort")]
        public string AuthServiceBusHostPort { get; set; }

        [YamlProperty("EmailTrigger.EmailSenderGrpcServiceUrl")]
        public string EmailSenderGrpcServiceUrl { get; set; }
        
        [YamlProperty("EmailTrigger.PersonalDataServiceUrl")]
        public string PersonalDataServiceUrl { get; set; }
    }
}