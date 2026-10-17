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

## Files and settings

- Settings: `%APPDATA%\ClickTamer\config.json`
- Log: `%LOCALAPPDATA%\ClickTamer\startup.log`

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

## Файлы и настройки

- Настройки: `%APPDATA%\ClickTamer\config.json`
- Журнал: `%LOCALAPPDATA%\ClickTamer\startup.log`
