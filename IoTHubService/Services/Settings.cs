using System.Fabric;
using System.Fabric.Description;

namespace IoTHubService.Services
{
    public class Settings
    {
        private readonly StatefulServiceContext _context;
        private readonly ConfigurationSection _serviceConfigSection;

        public Settings(StatefulServiceContext serviceContext)
        {
            _context = serviceContext;
            var configurationPackage = _context.CodePackageActivationContext.GetConfigurationPackageObject("Config");
            _serviceConfigSection = configurationPackage.Settings.Sections["IoTHubServiceConfigSection"];
        }

        public string EventHubConnectionString
        {
            get
            {
                string connectionString = _serviceConfigSection.Parameters["EventHubListenConnectionString"].Value;
                return connectionString;
            }
        }

        public string EventHubName => _serviceConfigSection.Parameters["EventHubName"].Value;

       

        public int OffsetInterval
        {
            get
            {
                string str = _serviceConfigSection.Parameters["OffsetInterval"].Value;
                if (int.TryParse(str, out int result))
                {
                    return result;
                }

                return 5;
            }
        }
        public int IotHubServiceBackupFrequentSeconds
        {
            get
            {
                string str = _serviceConfigSection.Parameters["IotHubServiceBackupFrequentSeconds"].Value;
                if (int.TryParse(str, out int result))
                {
                    return result;
                }

                return 10;
            }
        }

       

        public string SqlDBConnectionString
        {
            get
            {
                string connectionString = _serviceConfigSection.Parameters["SqlDBConnectionString"].Value;
                return connectionString;
            }
        }


    }
}
