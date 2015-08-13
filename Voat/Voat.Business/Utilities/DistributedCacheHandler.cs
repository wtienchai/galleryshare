/*
This source file is subject to version 3 of the GPL license, 
that is bundled with this package in the file LICENSE, and is 
available online at http://www.gnu.org/licenses/gpl.txt; 
you may not use this file except in compliance with the License. 

Software distributed under the License is distributed on an "AS IS" basis,
WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
the specific language governing rights and limitations under the License.

All portions of the code written by Voat are Copyright (c) 2014 Voat
All Rights Reserved.

This layer is experimental.
*/

using System;
using Newtonsoft.Json;
using StackExchange.Redis;
using Voat.Configuration;

namespace Voat.Business.Utilities
{
    public static class DistributedCacheHandler
    {
        private static readonly Lazy<ConnectionMultiplexer> LazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            return ConnectionMultiplexer.Connect(Settings.DistributedCacheServer+ ",abortConnect=false,ssl=true,password="+Settings.DistributedCacheServerKey);
        });

        public static ConnectionMultiplexer Connection
        {
            get
            {
                return LazyConnection.Value;
            }
        }

        #region helper methods for storing objects
        public static T Get<T>(this IDatabase cache, string key)
        {
            return DeserializeObject<T>(cache.StringGet(key));
        }

        public static object Get(this IDatabase cache, string key)
        {
            return DeserializeObject<object>(cache.StringGet(key));
        }

        public static void Set(this IDatabase cache, string key, object value)
        {
            cache.StringSet(key, SerializeObject(value));
        }
        #endregion

        static string SerializeObject(object o)
        {
            if (o == null)
            {
                return null;
            }

            return JsonConvert.SerializeObject(o);
        }

        static T DeserializeObject<T>(string stream)
        {
            if (stream == null)
            {
                return default(T);
            }

            return JsonConvert.DeserializeObject<T>(stream);
        }
    }
}
