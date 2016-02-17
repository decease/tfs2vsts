using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApplication2.Classes
{
    internal class App
    {
        private readonly TFSRestClient _tfsRestClient = new TFSRestClient();
        private readonly VSTSMigrationManager _vstsMigrationManager = new VSTSMigrationManager();
        private List<TestSiuteJson> _currentPlanSuites = new List<TestSiuteJson>();
        private List<TestSiuteJson> _remainedElements;

        public void Run()
        {
            Logger.Log("Migration was started...");

            _vstsMigrationManager.MigrateAreas();
            _vstsMigrationManager.MigrateIterations();

            _vstsMigrationManager.MigrateWorkItems();

            
            _vstsMigrationManager.MigrateTestCases();

            var testPlanJsons = _tfsRestClient.GetTestPlans();

            foreach (var plan in testPlanJsons)
            {
                SaveTestPlan(plan);
            }
            

            Logger.Log("Migration was finished...");
        }

        private void SaveTestPlan(TestPlanJson testPlan)
        {
            // Create test plan and save relation
            var wiTestPlan = _tfsRestClient.GetWorkItems(testPlan.id);
            testPlan.assignedTo = wiTestPlan.FirstOrDefault()?.fields["System.AssignedTo"];
            _vstsMigrationManager.CreateTestPlan(testPlan);

            _currentPlanSuites = _tfsRestClient.GetTestSuites(testPlan.id).Where(s => s.parent != null).ToList();
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
            var parentSuiteId = _vstsMigrationManager.GetSuiteId(suite.parent.name);
            if (!parentSuiteId.HasValue)
            {
                var parentSuite = _currentPlanSuites.FirstOrDefault(s => s.id == suite.parent.id);
                parentSuiteId = SaveTestSuite(testPlan, parentSuite);
            }

            var wiTestSuite = _tfsRestClient.GetWorkItems(suite.id);
            suite.assignedTo = wiTestSuite.FirstOrDefault()?.fields["System.AssignedTo"];

            var suiteId = _vstsMigrationManager.CreateTestSuite(suite, parentSuiteId.Value);


            // Create test cases
            if (suite.suiteType == TestSiuteJson.StaticType)
            {
                var testCases = _tfsRestClient.GetTestCases(testPlan.id, suite.id);
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
            _vstsMigrationManager.RelateTestCaseToSuite(int.Parse(testCaseInfo.testCase.id), suiteId);
        }
    }
}
