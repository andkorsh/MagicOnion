using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace MagicOnion.HttpGateway
{
    public static class Utils
    {
        public static bool IsNullable(this Type type)
        {
            return type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static object ParseParameter(ParameterInfo p, StringValues stringValues)
        {
            if (p.ParameterType == typeof(string))
            {
                return (string)stringValues;
            }
            else if (p.ParameterType.GetTypeInfo().IsEnum)
            {
                return Enum.Parse(p.ParameterType, (string)stringValues);
            }
            else
            {
                var collectionType = GetCollectionType(p.ParameterType);
                if (stringValues.Count == 1 || collectionType == null)
                {
                    var values = (string)stringValues;
                    if (p.ParameterType == typeof(DateTime) || p.ParameterType == typeof(DateTimeOffset) || p.ParameterType == typeof(DateTime?) || p.ParameterType == typeof(DateTimeOffset?))
                    {
                        values = "\"" + values + "\"";
                    }

                    return JsonConvert.DeserializeObject(values, p.ParameterType);
                }
                else
                {
                    string serializeTarget;
                    if (collectionType == typeof(string))
                    {
                        serializeTarget = "[" + string.Join(", ", stringValues.Select(x => JsonConvert.SerializeObject(x))) + "]"; // escape serialzie
                    }
                    else if (collectionType.GetTypeInfo().IsEnum || collectionType == typeof(DateTime) || collectionType == typeof(DateTimeOffset) || collectionType == typeof(DateTime?) || collectionType == typeof(DateTimeOffset?))
                    {
                        serializeTarget = "[" + string.Join(", ", stringValues.Select(x => "\"" + x + "\"")) + "]";
                    }
                    else
                    {
                        serializeTarget = "[" + (string)stringValues + "]";
                    }

                    return JsonConvert.DeserializeObject(serializeTarget, p.ParameterType);
                }
            }
        }

        public static Type GetCollectionType(Type type)
        {
            if (type.IsArray) return type.GetElementType();

            if (type.GetTypeInfo().IsGenericType)
            {
                var genTypeDef = type.GetGenericTypeDefinition();
                if (genTypeDef == typeof(IEnumerable<>)
                    || genTypeDef == typeof(ICollection<>)
                    || genTypeDef == typeof(IList<>)
                    || genTypeDef == typeof(List<>)
                    || genTypeDef == typeof(IReadOnlyCollection<>)
                    || genTypeDef == typeof(IReadOnlyList<>))
                {
                    return genTypeDef.GetGenericArguments()[0];
                }
            }

            return null; // not collection
        }

        public static string GetMediaType(string path)
        {
            var extension = path.Split('.').Last();

            switch (extension)
            {
                case "css":
                    return "text/css";
                case "js":
                    return "text/javascript";
                case "json":
                    return "application/json";
                case "gif":
                    return "image/gif";
                case "png":
                    return "image/png";
                case "eot":
                    return "application/vnd.ms-fontobject";
                case "woff":
                    return "application/font-woff";
                case "woff2":
                    return "application/font-woff2";
                case "otf":
                    return "application/font-sfnt";
                case "ttf":
                    return "application/font-sfnt";
                case "svg":
                    return "image/svg+xml";
                case "ico":
                    return "image/x-icon";
                default:
                    return "text/html";
            }
        }
    }
}