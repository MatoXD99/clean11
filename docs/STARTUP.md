# Sign-in startup

Clean11 uses one Task Scheduler logon task named `Clean11 Startup <user SID>`.
The action runs the installed `Clean11.exe --background --sign-in` in the
current user's interactive session, with normal permissions and no stored
password. It does not depend on Explorer processing the Registry Run list.
Battery power, network availability and idle detection do not prevent launch.

The launcher waits up to 20 seconds for the Windows desktop, starts missing
components, then waits up to 15 seconds for the dock's first composition frame,
the overview's initialized hotkey/message loop, and the widgets process.
Failures are logged and return exit code 1; Task Scheduler retries up to three
times at one-minute intervals. Sign-in failures never open a blocking dialog.
Repeated task invocations reuse existing components.

Settings > System > Start with Windows creates/removes this same task. MSI
install, repair and upgrade register it even when the user does not open the
app at the end of setup. Uninstall removes it before deleting the executable.
Legacy Clean11 Run entries and separate Startup shortcuts are removed only
after task registration succeeds. Clean11 does not modify StartupApproved or
other apps' startup settings. A task disabled manually in Task Scheduler stays
disabled on ordinary app launches and unrelated Settings saves.

## Diagnostics

Inspect the task action, user, enabled state, Last Run Time and Last Run Result
in Task Scheduler. The task must use InteractiveToken and LeastPrivilege;
Last Run Result 0 means the launcher observed startup readiness. A direct
manual app launch alone is not evidence that the registered task works.

Private timing/readiness/failure logs are in
`%LOCALAPPDATA%\Clean11\Dock\dock.log` and
`%LOCALAPPDATA%\Programs\Clean11\Overview\native.log`.

Launcher maintenance commands:

- `--register-startup`: enable the current user's logon task.
- `--unregister-startup`: remove it and legacy Clean11 startup entries.
- `--sync-startup`: apply the installer choice or existing user preference.
- `--check-install`: check the three installed module executables.

`src/Clean11.Dock/Install.ps1 -NoLaunch` installs and registers startup without
opening the UI. This permits a real task-action smoke test with
`Start-ScheduledTask`, followed by readiness and duplicate-process checks,
without signing out or rebooting. It does not exercise the next logon trigger.
`installer/Test-Sandbox.ps1` checks startup registration, task execution,
unchecked startup and uninstall cleanup in a disposable Windows account.

## Regression check

Run `tools/Test-Startup.ps1` with Start with Windows enabled. It executes its
assertions in a temporary task under the real interactive user token, then
removes diagnostic tasks and leaves the enabled production task in place.
This matters when a tool host exposes different registry or file state from
the normal desktop process: identical paths alone do not establish that the
desktop sees the same startup registration or log contents.

Validated on 25 October 2026 with the installed 0.3.2 build:

- Registered sign-in action returned 0 after actual dock composition,
  initialized overview hotkey/message loop, and widget startup.
- Repeated execution preserved every module PID without duplicates.
- Startup removal, recreation and explicit enable passed.
- An independently disabled task stayed disabled during ordinary registration.
- Unrelated startup approval records remained byte-for-byte unchanged.
- A separate missing-component fixture returned task result 1 without a dialog.
- Overview and Dock builds, PowerShell syntax checks, MSI build and MSI custom
  action metadata checks passed. The existing framework assembly warning remains.

The machine was not restarted or signed out. The physical next-logon trigger
and complete MSI lifecycle in Windows Sandbox were not exercised in this run.
