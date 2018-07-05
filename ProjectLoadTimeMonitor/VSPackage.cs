using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Shell.Interop;

namespace ProjectLoadTimeMonitor
{
    [Guid("d074fa83-c523-495c-bcb3-ca8db1fed88b")]
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]

    //
    // The following line will schedule the package to be initialized when a
    // solution is being opened
    //
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string)]

    //
    // Not using AsyncPackage as such an extension gets loaded too late,
    // regardless of what UI context is specified.
    //
    public sealed class VSPackage : Package
    {
        private const string k_ExtensionName = "Project Load Time Monitor";
        private readonly Dictionary<Guid, long> m_LoadingTimes = new Dictionary<Guid, long>();
        private IVsOutputWindowPane m_OutputWindow;

        public VSPackage()
        {
            //
            // Subscribe interesting events.
            //
            SolutionEvents.OnBeforeOpenProject += HandleBeforeOpenProjectEvent;
            SolutionEvents.OnAfterOpenProject += HandleAfterOpenProjectEvent;

            //
            // FIXME: I was unable to make this pattern work:
            //  ThreadHelper.JoinableTaskFactory.Run(async delegate {
            //      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            //      DoSomething();
            //  });
            // The DoSomething part did not get executed for some reasons.
            //
            //ThreadHelper.ThrowIfNotOnUIThread(k_ExtensionName);

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

            OutputLine("Initialization completed.");
            OutputLine("---------------------------------------------");
            OutputLine(string.Format("{0,-50}, {1,-20},", "Project Name", "Elapsed Time (ms)"));
        }

        private void OutputLine(string Message)
        {
            m_OutputWindow.OutputString(Message + Environment.NewLine);
            Trace.WriteLine(Message, k_ExtensionName);
        }

        private void HandleBeforeOpenProjectEvent(object sender, BeforeOpenProjectEventArgs e)
        {
            //
            // A project is being loaded. Store the current time.
            //
            m_LoadingTimes.Add(e.Project, DateTimeOffset.Now.ToUnixTimeMilliseconds());
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
            if (!m_LoadingTimes.TryGetValue(projectGuid, out long startTime))
            {
                //
                // The project called "Miscellaneous Files" is reported as an
                // empty GUID during HandleBeforeOpenProjectEvent. Check if this
                // completion is for the project.
                //
                projectGuid = Guid.Empty;
                if (!m_LoadingTimes.TryGetValue(projectGuid, out startTime))
                {
                    OutputLine(string.Format("TryGetValue failed : {0}", projectName));
                    return;
                }
            }

            m_LoadingTimes.Remove(projectGuid);

            long elapsedTime = finishTime - startTime;
            OutputLine(string.Format("{0,-50}, {1,20},", projectName, elapsedTime));
        }
    }
}
