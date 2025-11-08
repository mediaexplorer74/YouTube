# Overview
Проект — UWP-клиент YouTube с воспроизведением видео. Основной плеер — `LibVLCSharp` (`VlcView`) с резервным `MediaPlayerElement`. В репозитории присутствует библиотека `VLCMediaElement` с кастомным `MediaElement` и `MediaTransportControls` для более богатого управления и событий.

# Goals
- Перейти от `MediaPlayerElement`/кастомных контролов к `VLC.MediaElement` + `VLC.MediaTransportControls`.
- Сохранить/восстанавливать позицию, скорость и состояние при смене качества.
- Минимизировать изменения и риски, аккуратно интегрируя новый плеер.
- Обеспечить стабильность: корректные обработчики событий (открытие, окончание, ошибки, треки, позиция, время).

# Progress
- Изучены `VLCMediaElement\MediaElement.cs` (стр. 500–800): обработка ошибок, диалогов, событий (добавление треков, изменение позиции/времени), открытие/конец медиа, обновление состояния и масштаба, установка аудиоустройства, очистка и смена источника.
- Просмотрен конец `MediaElement.cs` (стр. 800–953): создание `Media` и `MediaPlayer`, добавление опций (HW accel, mobile), внешние субтитры; привязка множества событий; публичные методы управления (`Play/Pause/Stop`, полноэкранный режим), установка треков и позиции.
- Проанализирован `Video.xaml.cs` (стр. 1–250): инициализация `LibVLC`/`MediaPlayer`, `VlcView`, обработчики жизненного цикла страницы, изменение размера окна, навигация, выход из полноэкранного режима; `CustomMediaTransportControls` и событие `SettingsClicked`.
- Найдена и изучена реализация `ApplyAndPlayCurrentUrlWithQuality` (стр. 1180–1380): выбор URL по качеству, вызов `StartPlaybackVlc`; `ChangeVideoQuality` сохраняет состояние (позиция, скорость, статус), меняет качество и пытается восстановить воспроизведение.
- Просмотрены соседние блоки (стр. 1380–1580): восстановление состояния `MediaPlayerElement` через `MediaOpened`, переключение видимости `VlcView`/`VideoPlayer`, логика `StartPlaybackVlc` с первичным запуском VLC и откатом на `MediaPlayerElement` при ошибке.
- Изучен `CustomMediaTransportControls.cs` (стр. 1–200): расширение стандартного `MediaTransportControls`, добавление кнопки настроек и её событие `SettingsClicked`.
- Проверены template parts в `VLC.MediaTransportControls` (`GetTemplateChild`): есть `RootGrid`, `ControlPanelGrid` и др., что важно для потенциальной встройки кнопки настроек.
- Исправлена синтаксическая ошибка в `Video.xaml.cs` на ~1173 строке (лишние скобки и устаревший лог), метод `ChangeVideoQuality` корректно завершён; проект успешно пересобран.
- Добавлено публичное свойство `Duration` в `VLC.MediaElement`, обновляется из события `OnLengthChanged`.
- В `Video.xaml.cs` реализован полноэкранный режим: `ToggleFullScreen()` делегирует в `VlcMediaElement.ToggleFullscreen()`, `BackRequested` сначала выходит из FullScreen через `ApplicationView.IsFullScreenMode`.
- Обновлена логика перемотки по двойному тапу: ограничение по фактической длительности (`VlcMediaElement.Duration`), с безопасным фолбэком на `_videoDuration`.
- Добавлен `Debug.WriteLine` с диагностикой перед стартом видео (`DisplayVideoInfo`).
- Сборка решения `YouTube.sln` выполнена успешно (`msbuild /t:Restore,Build`).

# Pending
- Реализовать кнопку «Настройки» как оверлей
- Реализовать управление скоростью воспроизведения через API `VLC.MediaElement`

# Выполнено
- Заменен `MediaPlayerElement` на `VLC.MediaElement`
- Адаптированы обращения к API плеера
- Обновлен `StartPlaybackVlc` для использования `VLC.MediaElement`
- Упрощены сценарии смены качества с сохранением позиции
