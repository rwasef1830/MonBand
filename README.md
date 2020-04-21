MonBand
========
What is MonBand ?
-------------------
MonBand is a bandwidth monitoring application that adds a toolbar to the Windows taskbar (also known as a "deskband") that renders bandwidth graphs. It was written as a lightweight alternative for the same functionality in NetBalancer for those who don't need any of its other features.

It is written in C# and requires the .NET Core 3.1 SDK to build and the .NET Core 3.1 runtime to run. You can install it from https://dot.net. Since it integrates with the shell, it requires Windows to run. It should work on all Windows platforms that .NET Core runs on.

It is preferable to compile from a git working copy to get changeset ids embedded into the version number.

FAQ
---
### What is this project licensed under ?
This project is licensed under the [MIT license](http://opensource.org/licenses/MIT). You are free to use it for personal or commercial use, though it would be nice if you contributed new features or bug fixes if it helped you out during your day :-).

### How to install ?
1. Unzip the latest release to a folder, maybe `C:\Program Files\MonBand`
2. Open a command prompt as administrator.
3. Execute `regsvr32 "C:\Program Files\MonBand\MonBand.Windows.ComHost.comhost.dll"`
4. Run `MonBand.Windows.Standalone.exe` to add monitors and configure the logging level.
5. Right-click taskbar > Toolbars > MonBand, you will probably want to unlock it and resize it and relock it.
6. Enjoy

### How to update ?
1. Open Task Manager and kill explorer.exe
2. Overwrite the old build files with the new files in the updated zip.
3. CTRL+ALT+DELETE > Open Task Manager > File > Run > explorer.exe

### How to uninstall ?
1. Open a command prompt as administrator.
2. Execute `regsvr32 /u "C:\Program Files\MonBand\MonBand.Windows.ComHost.comhost.dll"`
3. Open Task Manager and kill explorer.exe
4. Delete `C:\Program Files\MonBand`
5. CTRL+ALT+DELETE > Open Task Manager > File > Run > explorer.exe
6. If you don't wish to preserve configuration, you may also delete `%AppData%\MonBand`.

### Where are the configuration and logs stored ?
Application configuration and logs are stored in `%AppData%\MonBand`
**Note:** Deskbands in Windows are installed for all users. But the configuration of MonBand is user-specific and stored in the location above.

Features
--------
* Deskband with monitoring of multiple interfaces simultaneously with an autoscaling graph for each one.
* Standalone configuration application that administers the configuration and communicates with the deskband when configuration is updated without needing to restart the shell.
* Deskband standalone test mode (without installation in Windows) by running `MonBand.Windows.Standalone.exe deskband-test`.
* Bandwidth monitoring of any local network interface traffic rate using performance counters.
* Bandwidth monitoring of a remote network interface using SNMPv1 counters: 
	* GUI to query remote interface names and pick which one to monitor. To monitor multiple interfaces, create another SNMP monitor to the same server.
	* Configurable poll interval with automatic compensation for poll latency to reduce rate calculation inaccuracy.
	* 32-bit counter wrap-around is transparently handled.
	* Anomalous rate spikes (due to slight irregularities of polling interval and multiple counter updates in case of slow updating counters) are smoothed using ZScore algorithm.
