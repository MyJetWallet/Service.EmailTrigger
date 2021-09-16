using System.Runtime.Serialization;
using Service.EmailTrigger.Domain.Models;

namespace Service.EmailTrigger.Grpc.Models
{
    [DataContract]
    public class HelloMessage : IHelloMessage
    {
        [DataMember(Order = 1)]
        public string Message { get; set; }
    }
}