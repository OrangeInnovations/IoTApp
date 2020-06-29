using System;
using System.Fabric;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Common.MicroServices.Routings
{
    public class HttpServiceClientHandler : HttpClientHandler
    {
        private const int MaxRetries = 5;
        private const int InitialRetryDelayMs = 25;
        private readonly Random _random = new Random();
        private readonly JsonSerializerSettings _jsonSerializerSettings =  new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        public HttpServiceClientHandler()
        {
        }

        private string GetServiceEndpointJson(ResolvedServicePartition partition, HttpServiceUriBuilder uriBuilder)
        {
            string serviceEndpointJson;

            switch (uriBuilder.Target)
            {
                case HttpServiceUriTarget.Default:
                case HttpServiceUriTarget.Primary:
                    serviceEndpointJson = partition.GetEndpoint().Address;
                    break;
                case HttpServiceUriTarget.Secondary:
                    serviceEndpointJson = partition.Endpoints.ElementAt(this._random.Next(1, partition.Endpoints.Count)).Address;
                    break;
                default:
                    serviceEndpointJson = partition.Endpoints.ElementAt(this._random.Next(0, partition.Endpoints.Count)).Address;
                    break;
            }

            return serviceEndpointJson;
        }

        private Uri GetRequestUri(string serviceEndpointJson, HttpServiceUriBuilder uriBuilder)
        {
            //need more reliable
            JToken obj = JObject.Parse(serviceEndpointJson)["Endpoints"];

            string endpointUrl = ((JContainer)obj).Select(b => ((JProperty)b).Value.Value<string>())
                .FirstOrDefault(r => r.StartsWith("http"));
            //string endpointUrl=((JProperty)obj.First).Value.Value<string>();
            //string endpointUrl = JObject.Parse(serviceEndpointJson)["Endpoints"][uriBuilder.EndpointName].Value<string>();

            return new Uri($"{endpointUrl.TrimEnd('/')}/{uriBuilder.ServicePathAndQuery.TrimStart('/')}", UriKind.Absolute);
        }

        private Uri GetResolvedRequestUri(ResolvedServicePartition partition, HttpServiceUriBuilder uriBuilder)
        {
            string serviceEndpointJson = GetServiceEndpointJson(partition, uriBuilder);
            return GetRequestUri(serviceEndpointJson, uriBuilder);
        }

        /// <summary>
        /// http://fabric/app/service/#/partitionkey/any|primary|secondary/endpoint-name/api-path
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int retries = MaxRetries;
            int retryDelay = InitialRetryDelayMs;

            ResponseData responseData=null;

            ServicePartitionResolver resolver = ServicePartitionResolver.GetDefault();
            ResolvedServicePartition partition = null;
            HttpServiceUriBuilder uriBuilder = null;
            try
            {
                uriBuilder = new HttpServiceUriBuilder(request.RequestUri);
            }
            catch (Exception)
            {
                string serializedJson = JsonConvert.SerializeObject(request.RequestUri, Formatting.None, _jsonSerializerSettings);

                throw new RequestUriArgumentException($"HttpServiceUriBuilder create failed due to RequestUri={serializedJson}");
            }
            

            while (retries --> 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                partition = partition != null
                    ? await resolver.ResolveAsync(partition, cancellationToken)
                    : await resolver.ResolveAsync(uriBuilder.ServiceName, uriBuilder.PartitionKey, cancellationToken);

                Uri resolvedRequestUri= GetResolvedRequestUri(partition, uriBuilder);

                responseData = await SingleSendAsync(request, resolvedRequestUri, cancellationToken);

                if (responseData.HttpResponseMessage != null)
                {
                    return responseData.HttpResponseMessage;
                }
                await Task.Delay(retryDelay, cancellationToken);

                retryDelay += retryDelay;
            }
            
            throw responseData.LastException;
        }

        class ResponseData
        {
            public HttpResponseMessage HttpResponseMessage { get; set; }
            public Exception LastException { get; set; }
        }
        private async Task<ResponseData> SingleSendAsync(HttpRequestMessage request, Uri resolvedRequestUri, CancellationToken cancellationToken)
        {
            ResponseData responseData=new ResponseData();
            Exception lastException = null;

            try
            {
                request.RequestUri = resolvedRequestUri;
                var lastResponse = await base.SendAsync(request, cancellationToken);

                if (!(lastResponse.StatusCode == HttpStatusCode.NotFound ||
                      lastResponse.StatusCode == HttpStatusCode.ServiceUnavailable))
                {
                    responseData.HttpResponseMessage = lastResponse;
                }
            }
            catch (TimeoutException te)
            {
                lastException = te;
            }
            catch (SocketException se)
            {
                lastException = se;
            }
            catch (HttpRequestException hre)
            {
                lastException = hre;
            }
            catch (Exception ex)
            {
                lastException = ex;
                WebException we = ex as WebException;

                if (we == null)
                {
                    we = ex.InnerException as WebException;
                }

                if (we != null)
                {
                    HttpWebResponse errorResponse = we.Response as HttpWebResponse;

                    // the following assumes port sharing
                    // where a port is shared by multiple replicas within a host process using a single web host (e.g., http.sys).
                    if (we.Status == WebExceptionStatus.ProtocolError)
                    {
                        if (!(errorResponse.StatusCode == HttpStatusCode.NotFound ||
                              errorResponse.StatusCode == HttpStatusCode.ServiceUnavailable))
                        {
                            // On any other HTTP status codes, re-throw the exception to the caller.
                            throw;
                        }
                    }
                }
                else
                {
                    throw;
                }
            }
            responseData.LastException = lastException;

            return responseData;
        }
    }
}