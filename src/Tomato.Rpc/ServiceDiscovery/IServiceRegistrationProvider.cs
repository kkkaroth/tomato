using System.Threading.Tasks;
using Tomato.Rpc.Client;

namespace Tomato.Rpc.ServiceDiscovery
{
    public interface IServiceRegistrationProvider
    {
        /// <summary>
        /// Register Service Async
        /// </summary>
        /// <param name="serviceId"></param>
        /// <param name="serviceName"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        Task RegisterServiceAsync(string serviceId,string serviceName, IRouterPoint point);

        /// <summary>
        /// Deregister Service Async
        /// </summary>
        /// <param name="serviceId"></param>
        /// <returns></returns>
        Task DeregisterServiceAsync(string serviceId);
    }
}
