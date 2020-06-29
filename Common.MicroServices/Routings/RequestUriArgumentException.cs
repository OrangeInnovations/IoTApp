using System;

namespace Common.MicroServices.Routings
{
    public class RequestUriArgumentException : ArgumentException
    {
        public RequestUriArgumentException(string msg) : base(msg)
        {

        }
    }
}