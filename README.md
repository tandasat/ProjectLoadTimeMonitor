Project Load Time Monitor
==========================

Introduction
-------------

Project Load Time Monitor is a Visual Studio extension that measures load time of each project when a solution file is opened. This extension aims to help developers identify projects slowing down solution load.

Developers often experience unacceptable slowness when opening solution files with 100+ projects on Visual Studio. While one of the easiest solutions to the issue is to separate or unload some rarely used projects, this cannot be done efficiently because there is no built-in way to measure which projects are making solution load slow.

This extension measures time needed to complete load of each project, and outputs them in the CSV format to assist further manual analysis.


Examples
---------

The results are displayed on the Output Window as show below once all projects are loaded.

![OutputWindow](/Images/ExampleOutput.png)

Here is another example results taken from a real-world solution which consisted of 400+ projects. The below pie chart was created with Excel with the result of this extension and shows that the only top five projects are responsible for one third of the entire solution time. I was able to save 30% of load time by just unloading four of those projects from the solution.

![SlowProjects](/Images/SlowProjects.png)


On Visual Studio 2019
----------------------

On Visual Studio 2019, the extension is unable to measure time for the solution that is loaded at the first time for the Visual Studio instance. An user must open any solution first, and then, open the target solution to measure its load time.

This is due to the changes of the requirements Microsoft made for the Visual Studio extensions. For more details of this change, see a blog article at The Visual Studio Blog below:
  - [Improving the responsiveness of critical scenarios by updating auto load behavior for extensions](https://blogs.msdn.microsoft.com/visualstudio/2018/05/16/improving-the-responsiveness-of-critical-scenarios-by-updating-auto-load-behavior-for-extensions/)


Supported Platforms
--------------------
- Visual Studio 2017 ([Download](https://marketplace.visualstudio.com/items?itemName=SatoshiTanda.ProjectLoadTimeMonitor))
- Visual Studio 2019 ([Download](https://marketplace.visualstudio.com/items?itemName=SatoshiTanda.ProjectLoadTimeMonitor2))

