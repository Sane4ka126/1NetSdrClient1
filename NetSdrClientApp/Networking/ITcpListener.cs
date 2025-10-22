using System.Threading.Tasks;

namespace NetSdrClientApp.Networking.Interfaces
{
    public interface ITcpListener
    {
        void Start();
        void Stop();
        Task<ITcpClient> AcceptTcpClientAsync();
    }
}
