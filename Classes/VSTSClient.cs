using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using WorkItem = Microsoft.TeamFoundation.WorkItemTracking.Client.WorkItem;

namespace ConsoleApplication2.Classes
{
    public class VSTSClient : IDisposable
    {
        private static string _collectionUri = "https://carebook.visualstudio.com/DefaultCollection";
        private static string _teamProjectName = "CHMS";
        private readonly NetworkCredential _networkCredential = new NetworkCredential("decease", "Test123$");

        private readonly TfsTeamProjectCollection _tpc;
        private readonly WorkItemStore _workItemStore;

        private readonly List<string> _allowTestCaseFields = new List<string>
        {
            "System.AreaPath",
            "System.TeamProject",
            "System.Title",
            "Microsoft.VSTS.TCM.Steps"
        };

        private readonly List<string> _allowTestPlanFields = new List<string>
        {
            "System.AreaPath",
            "System.TeamProject",
            "System.Title"
        };

        private readonly List<string> _allowTestSuiteFields = new List<string>
        {
            "System.AreaPath",
            "System.TeamProject",
            "System.Title",
            "Microsoft.VSTS.TCM.TestSuiteType", // Static || Dynamic
            // Query
        };

        public VSTSClient()
        {
            var tfsUri = new Uri("https://carebook.visualstudio.com/DefaultCollection/");
            var basicCredential = new BasicAuthCredential(_networkCredential);
            var tfsCredentials = new TfsClientCredentials(basicCredential) { AllowInteractive = false };
            _tpc = new TfsTeamProjectCollection(tfsUri, tfsCredentials);
            _tpc.EnsureAuthenticated();

            _workItemStore = _tpc.GetService<WorkItemStore>();
        }


        public void CreateTestPlan(Dictionary<string, string> fields)
        {
            var wiType = _workItemStore.Projects[_teamProjectName].WorkItemTypes["Test Plan"];

            CreateWorkItem(wiType, fields, _allowTestPlanFields);
        }

        public void CreateTestCase(Dictionary<string, string> fields)
        {
            var wiType = _workItemStore.Projects[_teamProjectName].WorkItemTypes["Test Case"];

            CreateWorkItem(wiType, fields, _allowTestCaseFields);
        }

        private void CreateWorkItem(WorkItemType wiType, Dictionary<string, string> fields, List<string> allowFields)
        {
            var newWorkItem = new WorkItem(wiType);

            foreach (var field in fields)
            {
                if (allowFields.Contains(field.Key))
                {
                    newWorkItem[field.Key] = field.Value;
                }
            }

            newWorkItem["System.IterationPath"] = "CHMS\\Iteration 39";

            if (newWorkItem.Validate().Count == 0)
            {
                newWorkItem.Save();
            }
            else
            {
                throw new Exception(string.Join("\n", newWorkItem.Validate()));
            }
        }

        public void Dispose()
        {
            _tpc.Dispose();
        }
    }
}