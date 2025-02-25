﻿using System;
using System.IO;
using Newtonsoft.Json;

namespace Xpand.Extensions.JsonExtensions {
    public static partial class JsonExtensions {
        public static T[] DeserializeJson<T>(this string json) {
            using var stringReader = new StringReader(json);
            using var reader = new JsonTextReader(stringReader);
            return reader.DeserializeSingleOrList<T>();
        }

        public static object[] FromJson(this Type type,string json) {
            using var stringReader = new StringReader(json);
            using var reader = new JsonTextReader(stringReader);
            return reader.DeserializeSingleOrList(type);
        }
    }
}