using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using RestSharp;

namespace ConsoleApplication2.Classes
{
    public class TFSRestClient
    {
        private readonly NetworkCredential _credentials = new NetworkCredential(Constants.SOURCE_USER_NAME, Constants.SOURCE_PASSWORD);
        private readonly RestClient _aClient = new RestClient(Constants.SOURCE_TFS_URL);

        private int _getWorkItemsRetryCount = Constants.RetryCount;
        public ICollection<WorkItemJson> GetWorkItems(params int[] ids)
        {
            var path = $"/_apis/wit/workitems/?ids={string.Join(",", ids)}&$expand=all&api-version=1.0";
            var aRequest = new RestRequest(path, Method.GET)
            {
                Credentials = _credentials
            };

            var retryLabel = _getWorkItemsRetryCount < Constants.RetryCount ? $"(RETRY #{Constants.RetryCount - _getWorkItemsRetryCount})" : "";
            Logger.Log($"{retryLabel} Excecute request to: {path}");
            var aResponse = _aClient.Execute(aRequest);

            if (aResponse.StatusCode == 0 && _getWorkItemsRetryCount > 0)
            {
                _getWorkItemsRetryCount--;
                return GetWorkItems(ids);
            }

            if (aResponse.StatusCode != HttpStatusCode.OK)
            {
                Logger.Error(aResponse.ErrorMessage);
                throw new Exception(aResponse.ErrorMessage);
            }

            _getWorkItemsRetryCount = Constants.RetryCount;
            return Utils.GetItemCollectionFormJson<WorkItemJson>(aResponse.Content);
        }

        public ICollection<TestPlanJson> GetTestPlans(int retryCount = Constants.RetryCount)
        {
            var apiString = $"/{Constants.SOURCE_PROJECT_NAME}/_apis/test/plans?api-version=1.0";

            var aRequest = new RestRequest(apiString, Method.GET)
            {
                Credentials = _credentials
            };

            var retryLabel = retryCount < Constants.RetryCount ? $"(RETRY #{Constants.RetryCount - retryCount})" : "";
            Logger.Log($"{retryLabel} Excecute request to: {apiString}");
            var aResponse = _aClient.Execute(aRequest);

            if (aResponse.StatusCode == 0 && retryCount > 0)
            {
                return GetTestPlans(--retryCount);
            }

            if (aResponse.StatusCode != HttpStatusCode.OK)
            {
                Logger.Error(aResponse.ErrorMessage);
                throw new Exception(aResponse.ErrorMessage);
            }

            return Utils.GetItemCollectionFormJson<TestPlanJson>(aResponse.Content);
        }

        public ICollection<TestSiuteJson> GetTestSuites(int planId, int retryCount = Constants.RetryCount)
        {
            var apiString = $"/{Constants.SOURCE_PROJECT_NAME}/_apis/test/plans/{planId}/suites?api-version=1.0";

            var aRequest = new RestRequest(apiString, Method.GET)
            {
                Credentials = _credentials
            };

            var retryLabel = retryCount < Constants.RetryCount ? $"(RETRY #{Constants.RetryCount - retryCount})" : "";
            Logger.Log($"{retryLabel} Excecute request to: {apiString}");
            var aResponse = _aClient.Execute(aRequest);

            if (aResponse.StatusCode == 0 && retryCount > 0)
            {
                return GetTestSuites(planId, --retryCount);
            }

            if (aResponse.StatusCode != HttpStatusCode.OK)
            {
                Logger.Error(aResponse.ErrorMessage);
                throw new Exception(aResponse.ErrorMessage);
            }

            return Utils.GetItemCollectionFormJson<TestSiuteJson>(aResponse.Content);
        }

        public ICollection<TestCaseJson> GetTestCases(int planId, int suiteId, int retryCount = Constants.RetryCount)
        {
            var apiString = $"/{Constants.SOURCE_PROJECT_NAME}/_apis/test/plans/{planId}/suites/{suiteId}/testcases?api-version=1.0";

            var aRequest = new RestRequest(apiString, Method.GET)
            {
                Credentials = _credentials
            };

            var retryLabel = retryCount < Constants.RetryCount ? $"(RETRY #{Constants.RetryCount - retryCount})" : "";
            Logger.Log($"{retryLabel} Excecute request to: {apiString}");
            var aResponse = _aClient.Execute(aRequest);

            if (aResponse.StatusCode == 0 && retryCount > 0)
            {
                return GetTestCases(planId, suiteId, --retryCount);
            }

            if (aResponse.StatusCode != HttpStatusCode.OK)
            {
                Logger.Error(aResponse.ErrorMessage);
                throw new Exception(aResponse.ErrorMessage);
            }

            return Utils.GetItemCollectionFormJson<TestCaseJson>(aResponse.Content);
        }
    }

    public class TestCaseJson
    {
        public IdItem testCase { get; set; }
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

        [JsonIgnore]
        public string assignedTo;

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

        [JsonIgnore]
        public string assignedTo;
    }

    public class WorkItemCollectionJson<T>
    {
        public int count { get; set; }
        public ICollection<T> value { get; set; }
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
