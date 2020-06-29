using IoTHubService.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace IoTHubService.Services
{
    public interface IMessageParser
    {
        CoDevMessage Parse(string jsonMessage);
    }
}
