using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using RestSharp;

namespace ConsoleApplication2
{
    public class TFSClient
    {
        private const string TFS_URL = "http://vancouver.copemanhealthcare.com:8899/tfs/DefaultCollection";
        private readonly NetworkCredential Credentials = new NetworkCredential(@"COPEMAN\omishenkin", "Password123");
        private readonly RestClient _aClient = new RestClient(TFS_URL);

        public string GetWorkItem(params int[] ids)
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

            return aResponse.Content;
        }

        public string GetTestPlans()
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

            return aResponse.Content;
        }

        public string GetTestSuites(int planId)
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

            return aResponse.Content;
        }

        public string GetTestCases(int planId, int suiteId)
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

            return aResponse.Content;
        }
    }
}
