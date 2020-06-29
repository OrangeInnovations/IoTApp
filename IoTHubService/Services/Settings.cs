using System.Fabric;
using System.Fabric.Description;

namespace IoTHubService.Services
{
    public class Settings
    {
        private readonly StatefulServiceContext _context;
        private readonly ConfigurationSection _routerServiceConfigSection;

        public Settings(StatefulServiceContext serviceContext)
        {
            _context = serviceContext;
            var configurationPackage = _context.CodePackageActivationContext.GetConfigurationPackageObject("Config");
            _routerServiceConfigSection = configurationPackage.Settings.Sections["RouterServiceConfigSection"];
        }




        public string EventHubConnectionString
        {
            get
            {
                string connectionString = _routerServiceConfigSection.Parameters["EventHubListenConnectionString"].Value;
                return connectionString;
            }
        }

        public string EventHubName => _routerServiceConfigSection.Parameters["EventHubName"].Value;

        public bool EmailBugInformation
        {
            get
            {
                string str = _routerServiceConfigSection.Parameters["EmailBugInformation"].Value;
                return bool.TryParse(str, out var save) && save;
            }
        }

        public int OffsetInterval
        {
            get
            {
                string str = _routerServiceConfigSection.Parameters["OffsetInterval"].Value;
                if (int.TryParse(str, out int result))
                {
                    return result;
                }

                return 5;
            }
        }
        public int RouterServiceBackupFrequentSeconds
        {
            get
            {
                string str = _routerServiceConfigSection.Parameters["RouterServiceBackupFrequentSeconds"].Value;
                if (int.TryParse(str, out int result))
                {
                    return result;
                }

                return 10;
            }
        }

        //public int ExternalBackupInterval
        //{
        //    get
        //    {
        //        string str = _routerServiceConfigSection.Parameters["ExternalBackupInterval"].Value;
        //        if (int.TryParse(str, out int result))
        //        {
        //            return result;
        //        }

        //        return 10;
        //    }
        //}
        //public int EventBatchSize
        //{
        //    get
        //    {
        //        string str = _routerServiceConfigSection.Parameters["EventBatchSize"].Value;
        //        if (int.TryParse(str, out int result))
        //        {
        //            return result;
        //        }

        //        return 5;
        //    }
        //}

        public string SqlDBConnectionString
        {
            get
            {
                string connectionString = _routerServiceConfigSection.Parameters["SqlDBConnectionString"].Value;
                return connectionString;
            }
        }


    }
}
