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

This repository contains a customized v2rayN build focused on subscription automation, XKeen export, more reliable TUN startup, custom subscription HTTP headers, improved diagnostics, and selectable Windows packages.

### Differences from upstream

Compared with the upstream `2dust/v2rayN` project, this repository adds:

- subscription URL import from a plain-text file;
- deterministic subscription names derived from domains;
- automatic VLESS link export after subscription updates;
- XKeen configuration-set export, including ZIP archives;
- subscription HTTP headers loaded from `agent_v` and diagnostic header logging;
- delayed, observed, and retried Windows TUN startup with fallback to a normal connection;
- the `7.24.1.alex` product/informational version shown in the window title and file properties;
- manual Windows x64 Light, Medium, and Full build workflows;
- repository maintenance, build, and comparison scripts in `ps_scripts`.

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

### Subscription import and VLESS export

The subscription settings window can import subscription URLs from a plain-text file. Put one URL on each line. Blank lines and comments are skipped.

Subscription names are derived from the main domain label. For example, `ent.xtls.win` produces `xtls`. If that name already exists, the next names are `xtls-2`, `xtls-3`, and so on; the suffix `-1` is not used.

After every successful subscription update, the application creates or refreshes:

```text
subs_links/<subscription-name>.txt
```

The file contains only VLESS share links from that subscription, sorted alphabetically. Existing files are replaced. The `subs_links` directory and local subscription input/download files are runtime data and are not intended to be committed to Git.

### XKeen configuration export

The former **Promotion** command is replaced by **XKeen** in both supported user interfaces. In the Windows toolbar it uses a router icon.

The application reads `xkeen_sets.ini` strictly from the directory containing `v2rayN.exe`. The tracked [xkeen_sets.ini](xkeen_sets.ini) file is an example:

```ini
[set.1]
content = xtls(vk, de, es, nl, ch, fr, hk)
content = un1c4d3(sw3)
content = arza(se1)
```

Each section creates `set_N`. Each server name in parentheses creates the next numbered directory in declaration order. A reference such as `xtls(de)` selects subscription `xtls` and a profile whose server address starts with `de.`. Matching is case-insensitive. If a subscription or server is not found, its numbered directory remains empty and the problem is included in the final report.

The export recreates the `xkeen_sets` directory next to the application:

```text
xkeen_sets/
├── set_1/
│   ├── 1/04_outbounds.json
│   ├── 2/04_outbounds.json
│   └── readme.txt
├── set_1.zip
├── set_2/
└── set_2.zip
```

Every `04_outbounds.json` is generated from the current v2rayN profile through the Xray configuration generator. Each ZIP contains the numbered directories, including empty ones, and the set's `readme.txt`. When all entries are exported without errors, the application reports `Экспорт конфигов успешно завершен!`; otherwise it displays a combined error report after processing the available entries.

### Custom version

The application uses `7.24.1.alex` as its informational/product version. This value is visible in the main window title and in Windows file properties. The numeric file and assembly version remains `7.24.1.0` for compatibility.

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

### Отличия от оригинального v2rayN

По сравнению с исходным проектом [2dust/v2rayN](https://github.com/2dust/v2rayN) в этой сборке добавлены:

- импорт адресов подписок из текстового файла;
- формирование имён подписок из доменов;
- автоматический экспорт VLESS-ссылок после обновления подписок;
- экспорт наборов конфигураций для XKeen вместе с ZIP-архивами;
- пользовательские HTTP-заголовки подписок из `agent_v` и их диагностическое журналирование;
- отложенный и контролируемый запуск TUN с повторными попытками и резервным обычным подключением;
- версия `7.24.1.alex` в заголовке программы и свойствах файлов;
- ручные варианты сборки Windows x64 Light, Medium и Full;
- PowerShell-скрипты обслуживания, сборки и сравнения в `ps_scripts`.
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

Файл `agent_v` по умолчанию читается рядом с `v2rayN.exe`. Альтернативный путь можно задать переменной окружения `V2RAYN_AGENT_V_PATH`. Поддерживаются UTF-8, пустые строки, комментарии `;` и `#`, пробелы вокруг ключей и повторяющиеся ключи; используется последнее значение.

### Импорт и экспорт подписок

В окне настройки подписок можно импортировать текстовый файл, содержащий по одному URL в строке. Пустые строки и комментарии пропускаются.

Имя подписки создаётся из основной части домена. Например, для `ent.xtls.win` используется имя `xtls`. При совпадении имён создаются `xtls-2`, `xtls-3` и далее; имя `xtls-1` не используется.

После каждого успешного обновления подписки рядом с программой создаётся или обновляется:

```text
subs_links/<имя-подписки>.txt
```

Файл содержит только VLESS-ссылки этой подписки, отсортированные по алфавиту. Старое содержимое перезаписывается.

### Экспорт для XKeen

Команда «Продвижение» заменена кнопкой **XKeen**. Функция доступна в WPF и Avalonia; в панели Windows используется значок роутера.

Программа ищет `xkeen_sets.ini` строго рядом с `v2rayN.exe`. Корневой файл [xkeen_sets.ini](xkeen_sets.ini) служит примером формата:

```ini
[set.1]
content = xtls(vk, de, es, nl, ch, fr, hk)
content = un1c4d3(sw3)
content = arza(se1)
```

Каждая секция создаёт набор `set_N`. Для каждого имени сервера в скобках последовательно создаётся отдельная нумерованная папка. Запись `xtls(de)` означает: найти подписку `xtls`, затем найти в ней профиль, адрес сервера которого начинается с `de.`. Регистр не учитывается.

Перед экспортом старая папка `xkeen_sets` полностью очищается. В найденных позициях создаётся `04_outbounds.json` на основе актуального профиля и штатного генератора Xray. Если подписка или сервер не найдены, соответствующая папка остаётся пустой, а ошибка добавляется в итоговый отчёт.

В корне каждого `set_N` создаётся `readme.txt`. Рядом создаётся архив `set_N.zip`, содержащий `readme.txt` и все нумерованные папки, включая пустые.

Если ошибок нет, программа выводит:

```text
Экспорт конфигов успешно завершен!
```

При ошибках обработка остальных записей продолжается, после чего показывается общий отчёт.

### Версия сборки

Информационная и продуктовая версия изменена на `7.24.1.alex`. Она отображается в заголовке главного окна и свойствах EXE/DLL. Числовая версия файлов и сборок сохранена как `7.24.1.0` для совместимости.

### Сборки Windows

В [GitHub Releases](https://github.com/PrtUsr1976/v2rayN/releases) опубликована лёгкая Windows x64-сборка без встроенного .NET и без прокси-ядер. Ручные workflow в [GitHub Actions](https://github.com/PrtUsr1976/v2rayN/actions) позволяют также собрать варианты Medium и Full.

**Ключевые слова:** `v2rayN TUN не запускается`, `v2rayN зависает TUN`, `v2rayN TUN запускается только со второго раза`, `v2rayN agent_v`, `v2rayN пользовательские HTTP-заголовки`.

## Upstream project, license, and credits

- Upstream project: [2dust/v2rayN](https://github.com/2dust/v2rayN)
- Upstream documentation: [v2rayN Wiki](https://github.com/2dust/v2rayN/wiki)
- Proxy cores: [Xray-core](https://github.com/XTLS/Xray-core) and [sing-box](https://github.com/SagerNet/sing-box)
- License: [GNU General Public License v3.0](LICENSE)

Thanks to the upstream v2rayN maintainers, contributors, translators, and the developers of the supported proxy cores.
