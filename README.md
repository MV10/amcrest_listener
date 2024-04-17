# amcrest_listener

A very bare-bones console program which connects to Amcrest IP cameras, parses any event data communicated by the devices, and outputs the event.

## Usage
Set up your camera details in the user secrets file (see _Configuration_ below).

Run `listen.exe` to connect to all cameras, or `listen [name]` to connect to a single specific camera.

Press <kbd>ESC</kbd> to exit.

Tested with the [Amcrest IP5M-T1277EB-AI](https://amcrest.com/5mp-poe-camera-turret-ai-ip5m-t1277eb-ai.html) as well as a 2015-era Amcrest IPM-721W device.

## Configuration
Since camera login passwords are required, these are stored in the local User Secrets file. In Visual Studio, right-click the project and choose _Manage User Secrets_ or directly open the JSON file stored at the path below (the GUID in the path is in the project file).

`%APPDATA%\Microsoft\UserSecrets\803dc381-023f-4d0c-a940-8cc00d645c1a\secrets.json`

HTTP is assumed (HTTPS not supported). The configuration is a standard JSON hierarchical key-value config file:

```json
{
  "Cameras":[
    {
      "Name":"test1",
      "Addr":"192.168.1.110",
      "User":"username",
      "Pass":"password"
    },
    {
      "Name":"test2",
      "Addr":"192.168.1.111",
      "User":"username",
      "Pass":"password"
    }
  ]
}
```

## Raw Event Stream

To manually connect to your camera to see the raw event stream, open a terminal prompt and run this (replacing USER, PASS, and the IP address for your device parameters). Use <kbd>Ctrl</kbd><kbd>C</kbd> to disconnect.

```
curl -v --digest -u USER:PASS http://192.168.1.100/cgi-bin/eventManager.cgi?action=attach%26codes=%5BAll%5D
```

The un-escaped action parameter at the end requests all possible event codes:

```
?action=attach&codes=[All]
```
