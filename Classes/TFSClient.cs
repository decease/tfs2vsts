using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ConsoleApplication2.Classes;
using RestSharp;

namespace ConsoleApplication2
{
    public class TFSClient
    {
        private const string TFS_URL = "http://vancouver.copemanhealthcare.com:8899/tfs/DefaultCollection";
        private readonly NetworkCredential Credentials = new NetworkCredential(@"COPEMAN\omishenkin", "Password123");
        private readonly RestClient _aClient = new RestClient(TFS_URL);

        public List<WorkItemJson> GetWorkItems(params int[] ids)
        {
            var aRequest = new RestRequest($"/_apis/wit/workitems/?ids={string.Join(",", ids)}&$expand=all&api-version=1.0", Method.GET)
            {
                Credentials = Credentials
            };

            var aResponse = _aClient.Execute(aRequest);

            if (aResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception(aResponse.ErrorMessage);
            }

            return Utils.GetItemCollectionFormJson<WorkItemJson>(aResponse.Content);
        }

        public List<TestPlanJson> GetTestPlans()
        {
            var apiString = "/CHMS/_apis/test/plans?api-version=1.0";

            var aRequest = new RestRequest(apiString, Method.GET)
            {
                Credentials = Credentials
            };

            var aResponse = _aClient.Execute(aRequest);

            if (aResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception(aResponse.ErrorMessage);
            }

            return Utils.GetItemCollectionFormJson<TestPlanJson>(aResponse.Content);
        }

        public List<TestSiuteJson> GetTestSuites(int planId)
        {
            var apiString = $"/CHMS/_apis/test/plans/{planId}/suites?api-version=1.0";

            var aRequest = new RestRequest(apiString, Method.GET)
            {
                Credentials = Credentials
            };

            var aResponse = _aClient.Execute(aRequest);

            if (aResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception(aResponse.ErrorMessage);
            }

            return Utils.GetItemCollectionFormJson<TestSiuteJson>(aResponse.Content);
        }

        public TestSiuteJson GetTestSuiteWithChildren(int planId, int suiteId)
        {
            var apiString = $"/CHMS/_apis/test/plans/{planId}/suites/{suiteId}?includeChildSuites=true&api-version=1.0";

            var aRequest = new RestRequest(apiString, Method.GET)
            {
                Credentials = Credentials
            };

            var aResponse = _aClient.Execute(aRequest);

            if (aResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception(aResponse.ErrorMessage);
            }

            return Utils.GetItemFormJson<TestSiuteJson>(aResponse.Content);
        }

        public List<TestCaseJson> GetTestCases(int planId, int suiteId)
        {
            var apiString = $"/CHMS/_apis/test/plans/{planId}/suites/{suiteId}/testcases?api-version=1.0";

            var aRequest = new RestRequest(apiString, Method.GET)
            {
                Credentials = Credentials
            };

            var aResponse = _aClient.Execute(aRequest);

            if (aResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception(aResponse.ErrorMessage);
            }

            return Utils.GetItemCollectionFormJson<TestCaseJson>(aResponse.Content);
        }
    }

    public class TestCaseJson
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class TestSiuteJson
    {
        public const string DynamicType = "DynamicTestSuite";
        public const string StaticType = "StaticTestSuite";

        public int id { get; set; }
        public string name { get; set; }
        public string url { get; set; }
        public NamedItem<string> project { get; set; }
        public NamedItem<int> plan { get; set; }
        public NamedItem<int> parent { get; set; }

        /// <summary>
        /// Filled only for Dynamic suite type
        /// </summary>
        public string queryString { get; set; }

        public int testCaseCount { get; set; }
        public string suiteType { get; set; }
        public List<NamedItem<int>> suites { get; set; }
    }

    public class TestPlanJson
    {
        public int id { get; set; }
        public string name { get; set; }
        public string url { get; set; }
        public NamedItem<string> project { get; set; }
        public NamedItem<int> area { get; set; }
        public string iteration { get; set; }
        public string state { get; set; }
        public IdItem rootSuite { get; set; }
        public string clientUrl { get; set; }
    }

    public class WorkItemCollectionJson<T>
    {
        public int count { get; set; }
        public List<T> value { get; set; }
    }

    public class WorkItemJson
    {
        public int id { get; set; }
        public Dictionary<string, string> fields { get; set; }
    }

    public class NamedItem<T>
    {
        public T id { get; set; }
        public string name { get; set; }
    }

    public class IdItem
    {
        public string id { get; set; }
    }
}
