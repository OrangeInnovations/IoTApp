using System;
using System.Collections.Generic;
using System.Text;

namespace IoTHubService.Models
{
    public static class Names
    {

        #region service and service type names

        public const string RouterServiceTypeName = "RouterServiceType";
        public const string RouterServiceName = "RouterService";

        public const string MessageServiceTypeName = "MessageServiceType";
        public const string MessageServiceName = "MessageService";


        #endregion

        #region Specific Router Service constants

        public const string EventHubOffsetDictionaryName = "EventHubOffsetDictionary";
        public const string HubStreamOffSetKey = "HubStreamOffSetKey";

        public const string RouterServiceInfoDict = "store://StreamDataRouterService/dictionary";
        public const string RouterServiceTotalProcessedMessage = "StreamDataRouterServiceTotalProcessedMessage";

        #endregion

        #region Specific Keyout message Service constants

        public const string MessageQueueName = "store://devicemessages/queue";
        public const string MessageServiceInfoDict = "store://MessageService/dictionary";
        public const string MessageServiceTotalProcessedMessage = "MessageServiceTotalProcessedMessage";
        public const string MessageServiceTotalGeofenceEvents = "MessageServiceTotalGeofenceEvents";

        #endregion
    }
}
