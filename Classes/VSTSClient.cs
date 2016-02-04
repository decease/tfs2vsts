using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.TestManagement.Client;

namespace ConsoleApplication2.Classes
{
    public class VSTSClient : IDisposable
    {
        private static string _sourceCollectionUri = Constants.SOURCE_TFS_URL;
        private readonly NetworkCredential _sourceCredentials = new NetworkCredential(Constants.SOURCE_USER_NAME, Constants.SOURCE_PASSWORD);

        private static string _targetCollectionUri = Constants.TARGET_TFS_URL;
        private static string _targetTeamProjectName = Constants.TARGET_PROJECT_NAME;
        private readonly NetworkCredential _targetCredentials = new NetworkCredential(Constants.TARGET_USER_NAME, Constants.TARGET_PASSWORD);

        private readonly TfsTeamProjectCollection _targetTfs;
        private readonly WorkItemStore _targetWorkItemStore;
        private readonly ITestManagementTeamProject _targetProject;
        private readonly Project _vsProject;

        private ITestPlan _currentTestPlan;
        private ICommonStructureService _css;

        /// <summary>
        /// Relations between TFS wi id and VSTS wi id
        /// Key - TFS id
        /// Value - VSTS id
        /// </summary>
        private Dictionary<int, int> _wiIdRealtions = new Dictionary<int, int>();

        private WorkItemStore _sourceWorkItemStore;
        private ITestManagementTeamProject _sourceProject;


        public VSTSClient()
        {
            // Target connections
            var targetTfsUri = new Uri(_targetCollectionUri);
            var targetBasicCredential = new BasicAuthCredential(_targetCredentials);
            var targetTfsCredentials = new TfsClientCredentials(targetBasicCredential) { AllowInteractive = false };
            _targetTfs = new TfsTeamProjectCollection(targetTfsUri, targetTfsCredentials);
            _targetTfs.EnsureAuthenticated();

            _targetWorkItemStore = _targetTfs.GetService<WorkItemStore>();
            _targetProject = _targetTfs.GetService<ITestManagementService>().GetTeamProject(_targetTeamProjectName);

            // Source connections
            var sourceTfsUri = new Uri(_sourceCollectionUri);
            var sourceTfs = new TfsTeamProjectCollection(sourceTfsUri, _sourceCredentials);
            sourceTfs.EnsureAuthenticated();

            _sourceWorkItemStore = sourceTfs.GetService<WorkItemStore>();
            _sourceProject = sourceTfs.GetService<ITestManagementService>().GetTeamProject(_targetTeamProjectName);
        }


        public void CopyAreas()
        {
            // SOURCE TFS
            var targetCss = _targetTfs.GetService<ICommonStructureService>();
            var targetProject = targetCss.GetProjectFromName(_targetTeamProjectName);
            var targetNodes = targetCss.ListStructures(targetProject.Uri);

            var targetAreas = targetNodes.FirstOrDefault(node => node.StructureType == "ProjectModelHierarchy");

            var sourceProject = _sourceWorkItemStore.Projects.GetById(Constants.SOURCE_PROJECT_ID);

            foreach (Node areaRootNode in sourceProject.AreaRootNodes)
            {
                CreateArea(targetCss, areaRootNode, targetAreas);
            }
        }

        private void CreateArea(ICommonStructureService targetCss, Node areaRootNode, NodeInfo parentArea)
        {
            NodeInfo newNode;
            var nodeName = areaRootNode.Name.Replace('+', ' ');
            try
            {
                var newUri = targetCss.CreateNode(nodeName, parentArea.Uri);
                newNode = targetCss.GetNode(newUri);
            }
            catch (CommonStructureSubsystemException e)
            {
                Logger.Warn($"Area \"{nodeName}\" already exists.");
                newNode = targetCss.GetNodeFromPath(parentArea.Path + "\\" + nodeName);
            }
            catch (Exception e)
            {
                Logger.Error($"Some error while creating \"{nodeName}\" area.");
                Logger.Error(e.Message);

                return;
            }

            if (areaRootNode.HasChildNodes)
            {
                foreach (Node childNode in areaRootNode.ChildNodes)
                {
                    CreateArea(targetCss, childNode, newNode);
                }
            }
        }

        public void CreateTestCases()
        {
            var testCases = _sourceProject.TestCases.Query("SELECT [Id] FROM WorkItems");//.ToList();
            var total = testCases.Count();
            var left = total;
            foreach (var testCase in testCases)
            {
                var id = testCase.Id;
                var testCaseWi = _sourceWorkItemStore.GetWorkItem(id);
                var vstsId = CreateTestCase(testCaseWi.Fields);

                _wiIdRealtions[id] = vstsId;

                Logger.Log($"{--left}/{total} test cases migrated.");
            }
        }

        public void CreateTestPlan(TestPlanJson plan)
        {
            _currentTestPlan = _targetProject.TestPlans.Create();
            _currentTestPlan.AreaPath = plan.area.name;
            _currentTestPlan.Name = plan.name;
            _currentTestPlan.Description = $"Assigned to: {plan.assignedTo}";

            _currentTestPlan.Save();
            Logger.Success($"Test plan was created with id({_currentTestPlan.Id}) name({_currentTestPlan.Name})");
        }

        public int? GetSuiteId(string suiteName)
        {
            _currentTestPlan.Refresh();
            return Utils.GetSuiteIdRecursive(_currentTestPlan.RootSuite, suiteName);
        }

        public int CreateTestSuite(TestSiuteJson suite, int parentId)
        {
            ITestSuiteBase testSuite;
            if (suite.suiteType == TestSiuteJson.StaticType)
            {
                testSuite = _targetProject.TestSuites.CreateStatic();
                
            }
            else
            {
                testSuite = _targetProject.TestSuites.CreateDynamic();
                ((IDynamicTestSuite)testSuite).Query = _targetProject.CreateTestQuery(suite.queryString);
            }

            testSuite.Title = suite.name;
            testSuite.Description = $"Assigned to: {suite.assignedTo}";

            _currentTestPlan.Refresh();

            if (parentId == _currentTestPlan.RootSuite.Id)
            {
                _currentTestPlan.RootSuite.Entries.Add(testSuite);
            }
            else
            {
                var parentSuite = Utils.FindSuiteRecursive(_currentTestPlan.RootSuite, parentId);
                if (parentSuite != null)
                {
                    parentSuite.Entries.Add(testSuite);
                }
                else
                {
                    Logger.Error($"Can't found suite with id ({parentId})");
                }
            }

            _currentTestPlan.Save();
            Logger.Success($"Test suite was created with id({testSuite.Id}) name({testSuite.Title})");

            return testSuite.Id;
        }

        /// <summary>
        /// Create new unrelated test case
        /// </summary>
        /// <param name="fields"></param>
        private int CreateTestCase(FieldCollection fields)
        {
            var testCase = _targetProject.TestCases.Create();
            testCase.Title = fields["System.Title"].Value.ToString();
            testCase.Area = fields["System.AreaPath"].Value.ToString();

            if (!string.IsNullOrEmpty(fields["System.AssignedTo"].Value.ToString()))
            {
                testCase.Description = $"Assigned to: {fields["System.AssignedTo"].Value}";
            } else if (!string.IsNullOrEmpty(fields["System.CreatedBy"].Value.ToString()))
            {
                testCase.Description = $"Created by: {fields["System.CreatedBy"].Value}";
            }

            testCase.Save();

            var wiTestCase = _targetWorkItemStore.GetWorkItem(testCase.Id);
            wiTestCase["Microsoft.VSTS.TCM.Steps"] = fields["Microsoft.VSTS.TCM.Steps"].Value.ToString();
            wiTestCase.Save();

            Logger.Success($"Test case was created with id('{testCase.Id}') name('{testCase.Title}')");

            return testCase.Id;
        }

        /// <summary>
        /// Create relation between test case and suite
        /// </summary>
        /// <param name="testCaseId"></param>
        /// <param name="suiteId"></param>
        public void RelateTestCaseToSuite(int testCaseId, int suiteId)
        {
            var testCase = _targetProject.TestCases.Find(_wiIdRealtions[testCaseId]);
            if (testCase == null)
            {
                Logger.Error($"Can't find test case with Id = '{_wiIdRealtions[testCaseId]}'");
                return;
            }

            _currentTestPlan.Refresh();
            var parentSuite = Utils.FindSuiteRecursive(_currentTestPlan.RootSuite, suiteId);
            parentSuite.Entries.Add(testCase);

            _currentTestPlan.Save();

            Logger.Success($"Test case (id: \"{testCase.Id}\" name: \"{testCase.Title}\") was related to suite (id: \"{suiteId}\")");
        }

        public void Dispose()
        {
            _targetTfs.Dispose();
        }
    }
}