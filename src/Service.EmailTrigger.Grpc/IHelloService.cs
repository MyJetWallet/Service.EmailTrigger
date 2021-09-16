using System.ServiceModel;
using System.Threading.Tasks;
using Service.EmailTrigger.Grpc.Models;

namespace Service.EmailTrigger.Grpc
{
    [ServiceContract]
    public interface IHelloService
    {
        [OperationContract]
        Task<HelloMessage> SayHelloAsync(HelloRequest request);
    }
}