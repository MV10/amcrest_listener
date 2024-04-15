# amcrest_listener

A very bare-bones console program which connects to Amcrest IP cameras and outputs the details of any events communicated by the devices.

## Usage
Set up your camera details in the user secrets file (see _Configuration_ below).

Run `listen.exe` to connect to all cameras, or `listen [name]` to connect to a single specific camera.

Press <kbd>ESC</kbd> to exit.

Tested with the [Amcrest IP5M-T1277EB-AI](https://amcrest.com/5mp-poe-camera-turret-ai-ip5m-t1277eb-ai.html).

## Configuration
Since camera login passwords are required, these are stored in the local User Secrets file. In Visual Studio, right-click the project and choose _Manage User Secrets_ or directly open the JSON file stored at the path below (the GUID in the path is in the project file).

`%APPDATA%\Microsoft\UserSecrets\803dc381-023f-4d0c-a940-8cc00d645c1a\secrets.json`

The configuration is a standard JSON hierarchical key-value-pair config file:

```json
{
	"cameras":[
		{
			"name":"test1",
			"ip":"192.168.1.110",
			"user":"username",
			"pass":"password"
		},
		{
			"name":"test2",
			"ip":"192.168.1.111",
			"user":"username",
			"pass":"password"
		}
	]
}
```
