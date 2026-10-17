# ClickTamer 1.0

ClickTamer is a small Windows utility that helps prevent clicks and pops from a DAC when audio playback starts or stops.

Some DACs click when an audio source becomes active or stops playing. When there is no sound, the audio stream may become inactive and the DAC can switch to an idle state. When playback starts again, the stream becomes active, and this transition can cause an audible click in headphones or speakers.

ClickTamer keeps a silent WASAPI audio stream running in the background, so the DAC can stay active even when nothing is playing.

I use a xDuoo XD-05 Basic, and ClickTamer completely eliminated the clicks on my setup.

## Features

- Keeps a silent audio stream running in the background.
- Restarts the stream if it stops working.
- Updates the device list automatically when Windows detects a change.
- Includes a button to refresh the device list manually.
- Supports light and dark themes.
- Has a Russian and English interface.
- Can start with Windows or run minimized to the system tray.
- Remembers the selected audio device and suppression settings.

## Requirements

- Windows 10 or later.
- .NET 10 SDK to build the project.

## Build

Open the project folder in a terminal and run:

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The executable will be created in `bin\Release\net10.0-windows\win-x64\publish\ClickTamer.exe`.

## Files and settings

- Settings: `%APPDATA%\ClickTamer\config.json`
- Log: `%LOCALAPPDATA%\ClickTamer\startup.log`
- Application icon: `ClickTamer.ico`
- Source images: `assets/ClickTamer.png` and `assets/ClickTamer-1024.png`

On the first launch, click suppression and both startup options are disabled. ClickTamer saves your settings when you change them and uses its own configuration and log files.

## Startup behavior

When enabled, the startup option creates a shortcut in your Windows Startup folder. It does not copy the executable there. If you move ClickTamer to another folder, run it once from the new location to update the shortcut.

The "Start minimized" option works for both manual launches and Windows startup. When enabled, the main window stays hidden until you open it from the system tray.

## Version

The current application version is `1.0`. Version information is stored in `ClickTamer.csproj`.

## Theme

On the first launch, ClickTamer follows your Windows app theme. This continues until you switch the theme manually. After that, your choice is saved and will not change when you change the Windows theme.

---

# ClickTamer 1.0

ClickTamer это небольшая утилита для Windows, которая помогает избавиться от щелчков в наушниках или колонках при запуске и остановке воспроизведения звука через ЦАП.

Некоторые ЦАП издают щелчок, когда звуковой источник начинает или прекращает воспроизведение. Когда звука нет, аудиопоток может переходить в неактивное состояние, а сам ЦАП может переключаться в режим ожидания. При следующем запуске звука поток снова активируется, и этот переход иногда сопровождается щелчком.

ClickTamer постоянно поддерживает беззвучный аудиопоток WASAPI. Благодаря этому ЦАП может оставаться активным даже тогда, когда ничего не воспроизводится.

Я использую ЦАП xDuoo XD-05 Basic. В моей системе ClickTamer полностью устранил щелчки.

## Возможности

- Поддерживает беззвучный аудиопоток в фоновом режиме.
- Восстанавливает поток, если он перестаёт работать.
- Автоматически обновляет список устройств при изменениях со стороны Windows.
- Позволяет обновить список устройств вручную.
- Поддерживает светлую и тёмную темы.
- Имеет русский и английский интерфейсы.
- Может запускаться вместе с Windows или сворачиваться в системный трей.
- Запоминает выбранное аудиоустройство и настройки подавления щелчков.

## Требования

- Windows 10 или новее.
- .NET 10 SDK для сборки проекта.

## Сборка

Открой папку проекта в терминале и выполни:

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Готовый файл появится в `bin\Release\net10.0-windows\win-x64\publish\ClickTamer.exe`.

## Файлы и настройки

- Настройки: `%APPDATA%\ClickTamer\config.json`
- Журнал: `%LOCALAPPDATA%\ClickTamer\startup.log`
- Иконка приложения: `ClickTamer.ico`
- Исходные изображения: `assets/ClickTamer.png` и `assets/ClickTamer-1024.png`

При первом запуске подавление щелчков и обе настройки автозапуска выключены. ClickTamer сохраняет изменения настроек и использует только собственные файлы конфигурации и журнала.

## Автозапуск

При включении автозапуска программа создаёт ярлык в папке автозагрузки Windows. Сам EXE туда не копируется. Если переместить ClickTamer в другую папку, запусти его один раз из нового расположения, чтобы обновить ярлык.

Настройка «Запускать свёрнутым» действует и при ручном запуске, и при запуске Windows. Если она включена, главное окно остаётся скрытым до открытия через значок в системном трее.

## Версия

Текущая версия приложения: `1.0`. Информация о версии находится в `ClickTamer.csproj`.

## Тема

При первом запуске ClickTamer использует тему приложений Windows. Это продолжается до первого ручного переключения темы. После этого программа запоминает выбор, и изменение темы Windows больше на неё не влияет.
