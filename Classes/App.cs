using System.Collections.Generic;
using System.Linq;

namespace ConsoleApplication2.Classes
{
    struct Relation
    {
        public int OldId { get; set; }
        public int NewId { get; set; }

        public Relation(int oldId, int newId)
        {
            OldId = oldId;
            NewId = newId;
        }
    }

    class App
    {
        private readonly TFSClient _tfsClient = new TFSClient();
        private readonly VSTSClient _vstsRest = new VSTSClient();

        private List<Relation> planRelations { get; set; } = new List<Relation>();
        private List<Relation> suiteRelations { get; set; } = new List<Relation>();
        private List<Relation> caseRelations { get; set; } = new List<Relation>();

        public void Run()
        {
            var testPlanJsons = _tfsClient.GetTestPlans();
            
            foreach (var plan in testPlanJsons.Take(1))
            {
                SaveTestPlan(plan);
            }
        }

        private void SaveTestPlan(TestPlanJson testPlan)
        {
            var suites = _tfsClient.GetTestSuites(testPlan.id);

            // Create test plan and save relation
            var planItem  = _tfsClient.GetWorkItems(testPlan.id).First();
            var newId = _vstsRest.CreateTestPlan(planItem);
            planRelations.Add(new Relation(testPlan.id, newId));

            // LABEL A
            foreach (var suite in suites)
            {
                SaveTestSuite(testPlan, suite);
            }
        }

        private void SaveTestSuite(TestPlanJson testPlan, TestSiuteJson suite)
        {
            // Recursion (Get children)
            var suiteExt = _tfsClient.GetTestSuiteWithChildren(testPlan.id, suite.id);

            if (suiteExt.suites != null && suiteExt.suites.Count != 0)
            {
                foreach (var childSuiteInfo in suiteExt.suites)
                {
                    var childSuite = _tfsClient.GetTestSuiteWithChildren(testPlan.id, childSuiteInfo.id);
                    SaveTestSuite(testPlan, childSuite);
                }
            }

            // Create test suite and save relation
            var suiteItem = _tfsClient.GetWorkItems(suite.id).First();
            var newId = _vstsRest.CreateTestSuite(suiteItem);
            suiteRelations.Add(new Relation(suite.id, newId));

            var testCases = _tfsClient.GetTestCases(testPlan.id, suite.id);

            foreach (var testCaseInfo in testCases)
            {
                var testCaseItem = _tfsClient.GetWorkItems(testCaseInfo.id).First();
                var newCaseId = _vstsRest.CreateTestCase(testCaseItem.fields);
                caseRelations.Add(new Relation(testCaseItem.id, newCaseId));

                Utils.Log($"Create TestCase with id({testCaseItem.id}) name({testCaseInfo.name})");
            }
        }
    }
}
