using System.Collections.Generic;
using Newtonsoft.Json;

namespace ConsoleApplication2.Classes
{

    public class RestWorkItem
    {
        public Dictionary<string, string> fields { get; set; }
    }

    public class TestCaseItem
    {
        public RestItem testCase { get; set; }
    }

    public class RestItem
    {
        public int id { get; set; }
        public string name { get; set; }

    }
    public class RestTestSuite: RestItem
    {
        public int testCaseCount { get; set; }
        public string queryString { get; set; }
        public string suiteType { get; set; }
}

    internal class RestWorkItemCollection<T>
    {
        public int count { get; set; }

        public T[] value { get; set; }
    }

    public static class Utils
    {
        public static IEnumerable<int> GetItemIds(string result)
        {
            var data = JsonConvert.DeserializeObject<RestWorkItemCollection<RestItem>>(result);

            var fieldsCollection = new List<int>();

            for (var i = 0; i < data.count; i++)
            {
                if (data.value[i] != null)
                {
                    fieldsCollection.Add(data.value[i].id);
                }
            }

            return fieldsCollection;
        }

        public static IEnumerable<int> GetTestSuiteItemIds(string result)
        {
            var data = JsonConvert.DeserializeObject<RestWorkItemCollection<RestTestSuite>>(result);

            var fieldsCollection = new List<int>();

            for (var i = 0; i < data.count; i++)
            {
                if (data.value[i] != null && data.value[i].testCaseCount > 0)
                {
                    fieldsCollection.Add(data.value[i].id);
                }
            }

            return fieldsCollection;
        }

        public static IEnumerable<int> GetTestCaseItemIds(string cases)
        {
            var data = JsonConvert.DeserializeObject<RestWorkItemCollection<TestCaseItem>>(cases);

            var fieldsCollection = new List<int>();

            for (var i = 0; i < data.count; i++)
            {
                if (data.value[i] != null)
                {
                    fieldsCollection.Add(data.value[i].testCase.id);
                }
            }

            return fieldsCollection;
        }

        public static List<Dictionary<string, string>> Transform(string testCase)
        {
            var data = JsonConvert.DeserializeObject<RestWorkItemCollection<RestWorkItem>>(testCase);
            var fieldsCollection = new List<Dictionary<string, string>>();

            for (var i = 0; i < data.count; i++)
            {
                if (data.value[i] != null)
                {
                    fieldsCollection.Add(data.value[i].fields);
                }
            }

            return fieldsCollection;
        }
    }
}
