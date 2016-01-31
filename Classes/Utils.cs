using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ConsoleApplication2.Classes
{

    public static class Utils
    {
        public static T GetItemFormJson<T>(string json)
        {
            var data = JsonConvert.DeserializeObject<T>(json);
            return data;
        }

        public static List<T> GetItemCollectionFormJson<T>(string json)
        {
            var data = JsonConvert.DeserializeObject<WorkItemCollectionJson<T>>(json);

            return data.value;
        }

        public static void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now}] => {message}");
        }
    }
}
