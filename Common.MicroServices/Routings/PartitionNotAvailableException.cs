using System;

namespace Common.MicroServices.Routings
{
    public class PartitionNotAvailableException: Exception
    {
        public PartitionNotAvailableException(string msg):base(msg)
        {
            
        }
    }
}
