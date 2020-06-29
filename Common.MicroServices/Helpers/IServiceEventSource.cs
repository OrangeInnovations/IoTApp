using System.Fabric;

namespace Common.MicroServices.Helpers
{
    public interface IServiceEventSource
    {
        void LoggingServiceMessage(ServiceContext serviceContext, string message, params object[] args);
    }
}
