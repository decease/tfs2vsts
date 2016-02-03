using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApplication2.Classes
{
    internal class App
    {
        private readonly TFSClient _tfsClient = new TFSClient();
        private readonly VSTSClient _vstsClient = new VSTSClient();
        private List<TestSiuteJson> _currentPlanSuites = new List<TestSiuteJson>();
        private List<TestSiuteJson> _remainedElements;

        public void Run()
        {
            Logger.Log("Migration was started...");

            //TODO: [OM] Turn it on
            //_vstsRest.CopyAreas();

            var testPlanJsons = _tfsClient.GetTestPlans();

            // TODO: [OM] remove .Take(1)
            foreach (var plan in testPlanJsons.Take(1))
            {
                SaveTestPlan(plan);
            }

            Logger.Log("Migration was finished...");
        }

        private void SaveTestPlan(TestPlanJson testPlan)
        {
            // Create test plan and save relation
            var wiTestPlan = _tfsClient.GetWorkItems(testPlan.id);
            testPlan.assignedTo = wiTestPlan.FirstOrDefault()?.fields["System.AssignedTo"];
            _vstsClient.CreateTestPlan(testPlan);

            _currentPlanSuites = _tfsClient.GetTestSuites(testPlan.id).Where(s => s.parent != null).ToList();
            _remainedElements = new List<TestSiuteJson>(_currentPlanSuites);
            while (_remainedElements.Any())
            {
                Logger.Log($"{_remainedElements.Count} suite items left.", ConsoleColor.Green);

                var suite = _remainedElements.FirstOrDefault();
                SaveTestSuite(testPlan, suite);
            }
        }

        private int SaveTestSuite(TestPlanJson testPlan, TestSiuteJson suite)
        {
            // If parent exists and does not created => create it first
            var parentSuiteId = _vstsClient.GetSuiteId(suite.parent.name);
            if (!parentSuiteId.HasValue)
            {
                var parentSuite = _currentPlanSuites.FirstOrDefault(s => s.id == suite.parent.id);
                parentSuiteId = SaveTestSuite(testPlan, parentSuite);
            }

            var wiTestSuite = _tfsClient.GetWorkItems(suite.id);
            suite.assignedTo = wiTestSuite.FirstOrDefault()?.fields["System.AssignedTo"];

            var suiteId = _vstsClient.CreateTestSuite(suite, parentSuiteId.Value);


            // Create test cases
            if (suite.suiteType == TestSiuteJson.StaticType)
            {
                var testCases = _tfsClient.GetTestCases(testPlan.id, suite.id);
                foreach (var testCaseInfo in testCases)
                {
                    SaveTestCase(testCaseInfo, suiteId);
                }
            }

            _remainedElements.Remove(suite);

            return suiteId;
        }

        private void SaveTestCase(TestCaseJson testCaseInfo, int suiteId)
        {
            var testCaseItem = _tfsClient.GetWorkItems(int.Parse(testCaseInfo.testCase.id)).First();
            _vstsClient.CreateTestCase(testCaseItem.fields, suiteId);
        }
    }
}
