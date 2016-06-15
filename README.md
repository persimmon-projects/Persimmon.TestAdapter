# Visual Studio test explorer for Persimmon
![Persimmon.VisualStudio.TestExplorer](https://raw.githubusercontent.com/persimmon-projects/Persimmon.VisualStudio.TestExplorer/master/Images/banner.png)

* Still under construction...
* Continuous integration: [![Build status](https://ci.appveyor.com/api/projects/status/yum3a2eybr7s7ven?svg=true)](https://ci.appveyor.com/project/kekyo/persimmon-visualstudio-testexplorer)
* Latest "unstable" CI build binary: https://ci.appveyor.com/api/buildjobs/qhrkfuh0rvfj8fxy/artifacts/Persimmon.VisualStudio.TestExplorer.Setup/bin/Release/Persimmon.VisualStudio.TestExplorer.Setup.msi

# What is this?
* Integrate unit test explorer (adapter) on Visual Studio for F# computation-based unit test framework "Persimmon". http://persimmon-projects.github.io/Persimmon/

![Test explorer screen shot (ja)](https://raw.githubusercontent.com/persimmon-projects/Persimmon.VisualStudio.TestExplorer/master/Images/screenshot_ja.png)

# Environment
* Visual Studio 2013/2015
* Persimmon-v2. testable branch current not merged, will be soon :)

# License
* Under MIT https://raw.githubusercontent.com/persimmon-projects/Persimmon.VisualStudio.TestExplorer/master/LICENSE.txt

# Debugging information

1. If you are debuging test explorer, switch to "Debug" solution-configuration and require administrative-mode on Visual Studio 2013.
2. Run Persimmon.VisualStudio.TestExplorer on debugger.
3. Will execute Visual Studio on experimental-mode and test explorer vsix installed.
4. Open test explorer on Visual Studio, move progress bar and raise message box dialog "Waiting for attach debugger ...".
5. Attach your debugger onto "vstest.discoveryengine.x86.exe" (Dialog owned).
6. Set breakpoints/exception settings/more...
7. Click message box dialog's button, continue execution with debugger.

* Currently, not merged persimmon-v2 codes in official repository. So if you are using test explorer, please use pre-v2 core code from https://github.com/kekyo/Persimmon .
