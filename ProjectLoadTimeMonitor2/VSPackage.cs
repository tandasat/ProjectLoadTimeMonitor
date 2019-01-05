using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Shell.Interop;

namespace ProjectLoadTimeMonitor2
{
    [Guid("0E9EA39D-49AB-4890-9A1F-73714D820960")]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]

    //
    // The following line will schedule the package to be initialized when a
    // solution is being opened
    //
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string,
                     PackageAutoLoadFlags.BackgroundLoad)]

    public sealed class VSPackage : AsyncPackage
    {
        private const string k_ExtensionName = "Project Load Time Monitor 2";
        private readonly Dictionary<Guid, LoadEntry> m_LoadingTimes =
            new Dictionary<Guid, LoadEntry>();
        private IVsOutputWindowPane m_OutputWindow;

        //
        // Represents a record for a project being loaded.
        //
        private class LoadEntry
        {
            public long LoadOrder;
            public long StartTime;
            public long ElapsedTime;
            public string Name;

            public LoadEntry(long LoadOrder, long StartTime)
            {
                this.LoadOrder = LoadOrder;
                this.StartTime = StartTime;
            }
        }

        public VSPackage()
        {
            //
            // Subscribe interesting events.
            //
            SolutionEvents.OnBeforeOpenProject += HandleBeforeOpenProjectEvent;
            SolutionEvents.OnAfterOpenProject += HandleAfterOpenProjectEvent;
            SolutionEvents.OnAfterOpenSolution += HandleAfterOpenSolution;

            //
            // Get the OutputWindow for logging.
            //
            var paneGuid = VSConstants.OutputWindowPaneGuid.GeneralPane_guid;
            var outputWindow = (IVsOutputWindow)GetGlobalService(typeof(SVsOutputWindow));
            int result = outputWindow.CreatePane(paneGuid, k_ExtensionName, 1, 0);
            if (ErrorHandler.Failed(result))
            {
                Trace.WriteLine(string.Format("CreatePane failed : {0:X8}", result),
                                k_ExtensionName);
                return;
            }

            result = outputWindow.GetPane(paneGuid, out m_OutputWindow);
            if (ErrorHandler.Failed(result))
            {
                Trace.WriteLine(string.Format("GetPane failed : {0:X8}", result),
                                k_ExtensionName);
                return;
            }
        }

        private void OutputLine(string Message)
        {
            m_OutputWindow.OutputString(Message + Environment.NewLine);
            Trace.WriteLine(Message, k_ExtensionName);
        }

        private void HandleBeforeOpenProjectEvent(object sender, BeforeOpenProjectEventArgs e)
        {
            //
            // A project is being loaded. Store the current time and the order.
            //
            var entry = new LoadEntry(m_LoadingTimes.Count + 1,
                                      DateTimeOffset.Now.ToUnixTimeMilliseconds());
            m_LoadingTimes.Add(e.Project, entry);
        }

        private void HandleAfterOpenProjectEvent(object sender, OpenProjectEventArgs e)
        {
            //
            // A project has been loaded. Save the current time.
            //
            long finishTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            //ThreadHelper.ThrowIfNotOnUIThread(k_ExtensionName);

            //
            // Get the project name.
            //
            int result = e.Hierarchy.GetProperty(VSConstants.VSITEMID_ROOT,
                                                 (int)__VSHPROPID.VSHPROPID_Name,
                                                 out Object nameAsObject);
            if (ErrorHandler.Failed(result))
            {
                OutputLine(string.Format("GetProperty failed : {0:X8}", result));
                return;
            }
            var projectName = (string)nameAsObject;

            //
            // Get the project GUID.
            //
            result = e.Hierarchy.GetGuidProperty(VSConstants.VSITEMID_ROOT,
                                                 (int)__VSHPROPID.VSHPROPID_ProjectIDGuid,
                                                 out Guid projectGuid);
            if (ErrorHandler.Failed(result))
            {
                OutputLine(string.Format("GetGuidProperty failed : {0:X8}", result));
                return;
            }

            //
            // Pop the start time for the project and log it.
            //
            if (!m_LoadingTimes.TryGetValue(projectGuid, out LoadEntry entry))
            {
                //
                // The project called "Miscellaneous Files" is reported as an
                // empty GUID during HandleBeforeOpenProjectEvent. Check if this
                // completion is for the project.
                //
                projectGuid = Guid.Empty;
                if (!m_LoadingTimes.TryGetValue(projectGuid, out entry))
                {
                    OutputLine(string.Format("TryGetValue failed : {0}", projectName));
                    return;
                }
            }

            entry.ElapsedTime = finishTime - entry.StartTime;
            entry.Name = projectName;
        }

        private void HandleAfterOpenSolution(object sender, EventArgs e)
        {

            OutputLine("---------------------------------------------");
            OutputLine(string.Format("{0,-50}, {1,-20}, {2,-20},",
                                     "Project Name",
                                     "Elapsed Time (ms)",
                                     "Load Order"));

            //
            // Sort the entries by the elapsed time.
            //
            long totalTime = 0;
            var ordered = m_LoadingTimes.OrderByDescending(entry => entry.Value.ElapsedTime);
            foreach (var entry in ordered)
            {
                //
                // Skip any entries that encountered error and being incomplete.
                //
                if (string.IsNullOrEmpty(entry.Value.Name))
                {
                    continue;
                }

                //
                // Print out the entry.
                //
                OutputLine(string.Format("{0,-50}, {1,20}, {2,20},",
                                         entry.Value.Name,
                                         entry.Value.ElapsedTime,
                                         entry.Value.LoadOrder));
                totalTime += entry.Value.ElapsedTime;
            }
            OutputLine("---------------------------------------------");
            OutputLine(string.Format("{0,-50}, {1,20},", "Total", totalTime));

            //
            // Clear the all entries for the next solution load.
            //
            m_LoadingTimes.Clear();
        }
    }
}
