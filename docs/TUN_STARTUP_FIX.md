# v2rayN TUN startup fix

## Problem

On some Windows systems, v2rayN TUN mode may fail to start after Windows logon or on the first application launch. TUN may begin working only after restarting v2rayN.

Typical symptoms:

- TUN mode is enabled in settings but the TUN interface is not created;
- the first launch does not route traffic through TUN;
- restarting v2rayN makes TUN work;
- the proxy core exits during TUN initialization;
- startup succeeds only after Windows networking and related services have finished initializing.

## Delayed TUN activation

This repository includes a delayed TUN startup option:

```text
-tundelay <seconds>
```

Examples:

```text
v2rayN.exe -tundelay 10
v2rayN.exe -tundelay 20
v2rayN.exe -tundelay 0
v2rayN.exe -tundelay=30
```

Behavior:

- the presence of `-tundelay` suppresses TUN during initial application startup;
- a positive value enables TUN after the specified delay;
- `-tundelay 0` leaves TUN disabled until it is enabled manually;
- an invalid or missing value is logged and also leaves TUN disabled;
- the delay allows the proxy core and Windows network environment to initialize before TUN activation.

## TUN startup retry and fallback

On Windows, when TUN is enabled with Xray or sing-box, the custom startup logic observes the newly started core process.

- The default observation period is 20 seconds.
- If the core exits during the observation period, startup is retried.
- A maximum of three attempts is made.
- The pause between attempts is five seconds.
- If TUN is disabled manually during observation or the retry delay, the pending sequence is cancelled.
- If all attempts fail, TUN is disabled and the selected server is started without TUN as a fallback.

The observation period is configured in `finetunes.ini` next to the application:

```ini
TunStartObservationSeconds=20
```

Accepted values are 20–300 seconds. Missing or invalid values are replaced with the default value of 20 seconds.

## Diagnostics

Check the v2rayN log for messages related to:

- application startup and the parsed `-tundelay` value;
- delayed TUN activation;
- proxy-core start and stop operations;
- TUN attempt numbers and observation results;
- retry delays and cancellation;
- fallback startup without TUN;
- TUN initialization or process-exit errors;
- subscription request headers.

## Custom subscription headers

The application can load HTTP headers for subscription requests from the `agent_v` file. This is useful when a subscription service requires device or client headers.

Supported keys include:

```ini
user_agent=Throne/1.1.6
x_hwid=00000000-0000-0000-0000-000000000000
x_device_os=Windows
x_ver_os=10.0.17763
x_device_model=VirtualBox
```

By default, the file is read from the application directory. The `V2RAYN_AGENT_V_PATH` environment variable can specify another path.

## Scope and status

These changes are intended to improve recovery from specific TUN startup failures. They do not guarantee a fix for every Windows networking, Wintun, Xray, sing-box, driver, security-software, or configuration problem.

This behavior is included in the custom builds published by this repository. It is not an official upstream v2rayN feature unless accepted separately by the upstream project.
