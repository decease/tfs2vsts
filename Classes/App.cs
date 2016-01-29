using System.Collections.Generic;
using System.Linq;

namespace ConsoleApplication2.Classes
{
    class App
    {
        private readonly TFSClient _tfsClient = new TFSClient();
        private readonly VSTSClient _vstsRest = new VSTSClient();

        public void Run()
        {
//            var id = 33373;
            //var res = _tfsClient.GetTestSuites(id);

            //return;

            var testCaseIds = new List<int>();

            var resultString = _tfsClient.GetTestPlans();
            var testPlanIds = Utils.GetItemIds(resultString);

            foreach (var planId in testPlanIds)
            {
                var suitesString = _tfsClient.GetTestSuites(planId);
                var testSuiteIds = Utils.GetTestSuiteItemIds(suitesString);

                foreach (var suiteId in testSuiteIds)
                {
                    // Recursion (Get children)

                    // if Dynamic => Set Query : vvvv
                    var casesString = _tfsClient.GetTestCases(planId, suiteId);
                    var ids = Utils.GetTestCaseItemIds(casesString);

                    testCaseIds.AddRange(ids);
                }
            }

            testCaseIds = testCaseIds.Distinct()
                .Take(10)
                .ToList();

            // Get all Test Cases
            var testCase = _tfsClient.GetWorkItem(testCaseIds.ToArray());

            // Get collections of all needed fields
            var allFields = Utils.Transform(testCase);

            // Create Test Cases in VSTS
            foreach (var fields in allFields)
            {
                //vstsRest.CreateTestCase(fields);
            }
        }
    }
}
