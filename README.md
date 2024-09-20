# Restart currently running applications after Windows reboot

## Description

The program makes at windows shutting down proccess a list of currently running 
user applications which have a main window, excludes applications from startup 
sections (registry, task schedule, startup folders), and writes the result 
to registry's RunOnce value.

## Usage

Build and place it into any startup place like registry or startup folder.

## System requirements

The program was tested only in Windows 11 with .NET 6.
