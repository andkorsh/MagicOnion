using Grpc.Core;
using System.Reflection;
using MagicOnion.Server;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Newtonsoft.Json;

namespace MagicOnion.HttpGateway
{
    public class MagicOnionHttpGatewayMiddleware : OwinMiddleware
    {
        readonly OwinMiddleware next;
        readonly IDictionary<string, MethodHandler> handlers;
        readonly Channel channel;

        public MagicOnionHttpGatewayMiddleware(OwinMiddleware next, IReadOnlyList<MethodHandler> handlers, Channel channel) : base(next)
        {
            this.next = next;
            this.handlers = handlers.ToDictionary(x => "/" + x.ToString());
            this.channel = channel;
        }

        public override async Task Invoke(IOwinContext httpContext)
        {
            try
            {
                var path = httpContext.Request.Path.Value;

                MethodHandler handler;
                if (!handlers.TryGetValue(path, out handler))
                {
                    await next.Invoke(httpContext);
                    return;
                }

                // from form...
                object deserializedObject;
                if (httpContext.Request.ContentType == "application/x-www-form-urlencoded")
                {
                    //object parameters
                    var args = new List<object>();
                    var typeArgs = new List<Type>();

                    var formValues = await httpContext.Request.ReadFormAsync();

                    foreach (var p in handler.MethodInfo.GetParameters())
                    {
                        typeArgs.Add(p.ParameterType);

                        var values = formValues.GetValues(p.Name);
                        
                        if (values != null && values.Any())
                        {
                            args.Add(Utils.ParseParameter(p, new StringValues(values.ToArray())));
                        }
                        else
                        {
                            if (p.HasDefaultValue)
                            {
                                args.Add(p.DefaultValue);
                            }
                            else
                            {
                                args.Add(null);
                            }
                        }
                    }

                    deserializedObject = typeArgs.Count == 1 ?
                        args[0] : MagicOnionMarshallers.InstantiateDynamicArgumentTuple(typeArgs.ToArray(), args.ToArray());
                }
                else
                {
                    string body;
                    using (var sr = new StreamReader(httpContext.Request.Body, Encoding.UTF8))
                    {
                        body = sr.ReadToEnd();
                    }

                    if (handler.RequestType == typeof(byte[]) && string.IsNullOrWhiteSpace(body))
                    {
                        body = "[]";
                    }
                    deserializedObject = Newtonsoft.Json.JsonConvert.DeserializeObject(body, handler.RequestType);
                }

                // JSON to C# Object to MessagePack
                var requestObject = handler.BoxedSerialize(deserializedObject);

                var method = new Method<byte[], byte[]>(MethodType.Unary, handler.ServiceName, handler.MethodInfo.Name, MagicOnionMarshallers.ThroughMarshaller, MagicOnionMarshallers.ThroughMarshaller);

                // create header
                var metadata = new Metadata();
                foreach (var header in httpContext.Request.Headers)
                {
                    foreach (var value in header.Value)
                    {
                        metadata.Add(header.Key, value);
                    }
                }

                var invoker = new DefaultCallInvoker(channel);
                var rawResponse = await invoker.AsyncUnaryCall(method, null, default(CallOptions).WithHeaders(metadata), requestObject);

                // MessagePack -> Object -> Json
                var obj = handler.BoxedDeserialize(rawResponse);
                var v = JsonConvert.SerializeObject(obj, new[] { new Newtonsoft.Json.Converters.StringEnumConverter() });
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(v);
            }
            catch (Exception ex)
            {
                httpContext.Response.StatusCode = 500;
                httpContext.Response.ContentType = "text/plain";
                await httpContext.Response.WriteAsync(ex.ToString());
            }
        }
    }
}