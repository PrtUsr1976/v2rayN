# v2rayN — custom Windows build

<p align="center">
  <a href="#custom-windows-build">English</a> ·
  <a href="#русский">Русский</a>
</p>

<p align="center">
  <a href="https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-framework-dependent.yml"><img src="https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-framework-dependent.yml/badge.svg" alt="Windows Light build"></a>
  <a href="https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-self-contained.yml"><img src="https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-self-contained.yml/badge.svg" alt="Windows Medium build"></a>
  <a href="https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-full.yml"><img src="https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-full.yml/badge.svg" alt="Windows Full build"></a>
</p>

> [!NOTE]
> This is an unofficial customized build based on [2dust/v2rayN](https://github.com/2dust/v2rayN). It is not the official upstream repository.

## Custom Windows build

This repository contains a customized v2rayN build focused on more reliable TUN startup, custom subscription HTTP headers, improved diagnostics, and lightweight Windows packages.

### TUN startup reliability

This build addresses cases where v2rayN TUN mode does not start reliably after Windows logon or on the first application launch and begins working only after restarting v2rayN.

The custom startup flow supports delayed TUN activation:

```text
v2rayN.exe -tundelay <seconds>
```

A positive delay starts v2rayN with TUN disabled and enables TUN after the specified number of seconds. With `-tundelay 0`, TUN is not enabled automatically.

For Windows TUN sessions using Xray or sing-box, the custom startup logic also:

- observes the newly started core for a configurable period;
- retries startup up to three times when the core exits during that period;
- waits five seconds between attempts;
- disables TUN and starts the selected server without TUN if every attempt fails.

The observation period is stored in `finetunes.ini`:

```ini
TunStartObservationSeconds=20
```

Valid values are 20–300 seconds. These changes improve startup recovery but do not guarantee that every possible Windows, driver, network, Xray, or sing-box TUN problem is fixed.

See [TUN startup fix and diagnostics](docs/TUN_STARTUP_FIX.md) for details.

### Custom subscription HTTP headers

Subscription requests can use custom HTTP headers loaded from the `agent_v` file. The application logs the effective subscription request headers for diagnostics.

Example `agent_v`:

```ini
user_agent=Throne/1.1.6
x_hwid=00000000-0000-0000-0000-000000000000
x_device_os=Windows
x_ver_os=10.0.17763
x_device_model=VirtualBox
```

| `agent_v` key | HTTP header |
|---|---|
| `user_agent` | `User-Agent` |
| `x_hwid` | `x-hwid` |
| `x_device_os` | `x-device-os` |
| `x_ver_os` | `x-ver-os` |
| `x_device_model` | `x-device-model` |

By default, `agent_v` is read from the application directory. A different path can be supplied through the `V2RAYN_AGENT_V_PATH` environment variable.

The parser accepts UTF-8, blank lines, comments beginning with `;` or `#`, whitespace around keys and values, and duplicate keys where the last value wins.

### Windows packages

A lightweight Windows x64 package is available in [GitHub Releases](https://github.com/PrtUsr1976/v2rayN/releases). It does not bundle .NET or proxy cores, so the required .NET Desktop Runtime and cores must be installed or added separately.

The following manual GitHub Actions workflows are also available:

| Build | Contents | Requirements |
|---|---|---|
| [Light](https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-framework-dependent.yml) | Application and libraries; no bundled .NET; no proxy cores | Install .NET Desktop Runtime and add the required cores |
| [Medium](https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-self-contained.yml) | Application and bundled .NET; no proxy cores | Add the required cores |
| [Full](https://github.com/PrtUsr1976/v2rayN/actions/workflows/build-windows-full.yml) | Application, bundled .NET, and proxy cores | Ready-to-use package |

All three custom workflows target Windows x64 and include `agent_v` next to `v2rayN.exe`.

### Search terms

`v2rayN TUN does not start`, `v2rayN TUN startup problem`, `v2rayN TUN first launch`, `v2rayN TUN hangs`, `v2rayN TUN freeze`, `v2rayN agent_v`, `v2rayN custom HTTP headers`.

---

<a id="русский"></a>

## Русский

### Исправление запуска TUN

Эта модификация предназначена для случаев, когда режим TUN в v2rayN не запускается после входа в Windows, не работает при первом запуске или начинает работать только после повторного запуска программы.

Параметр:

```text
v2rayN.exe -tundelay <секунды>
```

позволяет сначала запустить программу без TUN, а затем включить TUN с заданной задержкой. При `-tundelay 0` автоматическое включение TUN отключено.

Для Xray и sing-box в Windows также добавлены наблюдение за запуском TUN, до трёх попыток с паузой пять секунд и переход к запуску выбранного сервера без TUN, если все попытки завершились неудачно. Время наблюдения задаётся параметром `TunStartObservationSeconds` в файле `finetunes.ini`.

Изменения повышают надёжность запуска и упрощают диагностику, но не гарантируют устранение всех возможных проблем TUN, драйверов или сети.

Подробности: [docs/TUN_STARTUP_FIX.md](docs/TUN_STARTUP_FIX.md).

### Пользовательские заголовки подписки

Добавлена загрузка пользовательских HTTP-заголовков запросов подписки из файла `agent_v` и их диагностическое логирование. Поддерживаются `User-Agent`, `x-hwid`, `x-device-os`, `x-ver-os` и `x-device-model`.

### Сборки Windows

В [GitHub Releases](https://github.com/PrtUsr1976/v2rayN/releases) опубликована лёгкая Windows x64-сборка без встроенного .NET и без прокси-ядер. Ручные workflow в [GitHub Actions](https://github.com/PrtUsr1976/v2rayN/actions) позволяют также собрать варианты Medium и Full.

**Ключевые слова:** `v2rayN TUN не запускается`, `v2rayN зависает TUN`, `v2rayN TUN запускается только со второго раза`, `v2rayN agent_v`, `v2rayN пользовательские HTTP-заголовки`.

## Upstream project, license, and credits

- Upstream project: [2dust/v2rayN](https://github.com/2dust/v2rayN)
- Upstream documentation: [v2rayN Wiki](https://github.com/2dust/v2rayN/wiki)
- Proxy cores: [Xray-core](https://github.com/XTLS/Xray-core) and [sing-box](https://github.com/SagerNet/sing-box)
- License: [GNU General Public License v3.0](LICENSE)

Thanks to the upstream v2rayN maintainers, contributors, translators, and the developers of the supported proxy cores.
