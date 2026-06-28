# DonConnect Beta 3: референс для облачной версии

Этот документ фиксирует фактический состав DonConnect Beta 3 как референс для будущей облачной версии DonConnect. Его задача - помочь перенести в DonConnect Cloud не только список функций, но и пользовательскую логику: как стример подключает площадки, настраивает виджеты, тестирует донаты, управляет повторами и сохраняет свои пресеты.

Документ описывает текущий проект как Streamer.bot extension. Для cloud-версии это не инструкция "копировать архитектуру один в один"; это карта функций, UX и бизнес-логики, которые уже проверяются тестерами.

## 1. Что такое текущая Beta 3

DonConnect Beta 3 - это монолитное C#-расширение для Streamer.bot с локальным браузерным редактором и набором overlay-виджетов для OBS.

Главная идея Beta 3:

- стример импортирует один файл `DonConnect.Beta3.sb` в Streamer.bot;
- расширение само запускает локальный HTTP-сервер на `127.0.0.1`;
- пользователь открывает браузерный редактор без Node.js, npm, Python, Electron, Docker и отдельных EXE;
- донаты из разных площадок приводятся к единой модели события;
- Streamer.bot получает нормализованные переменные доната;
- OBS получает Browser Source URL для алерта, цели, таймера, титров, лидерборда и док-панели.

Текущая версия кода: `0.13.0-beta.3.15`.

В этот hotfix входит:

- таймерный виджет больше не должен показывать строку последнего донатера;
- стартовая раскладка элементов алерта уплотнена вокруг центра;
- старые provider-значения в setup actions очищены: `Client ID`, `Client Secret`, JWT, API keys, access tokens и custom endpoint;
- встроенные данные приложения DonationAlerts в Beta 3 не используются.

## 2. Технологии

- C# inline code внутри Streamer.bot action.
- .NET Framework API, совместимый со средой Streamer.bot.
- `TcpListener` как простой локальный HTTP-сервер.
- Сервер привязан к `127.0.0.1`, порт по умолчанию `3987`.
- HTML/CSS/JavaScript встроены строками в C#.
- Хранение настроек в JSON-файлах.
- `Newtonsoft.Json` / `JObject` для настроек и состояния.
- `System.Speech` для озвучки доната голосами Windows.
- Browser Source в OBS для отображения виджетов.
- Streamer.bot Credits API / HTTP Server для интеграции титров.

Важное ограничение: текущая реализация intentionally zero-install для стримера, но из-за этого весь web UI, API и runtime живут в одном большом C# файле.

## 3. Файлы проекта

- `DonConnect.cs` - основной исходник расширения: провайдеры, runtime, локальный сервер, HTML/CSS/JS редактора, виджеты, API, настройки, дедупликация, титры, лидерборд, док.
- `DonConnect.Beta3.sb` - импортируемый пакет Streamer.bot.
- `README_RU.md` - русская инструкция для пользователей.
- `README.md` - английская инструкция.
- `CHANGELOG_RU.md` / `CHANGELOG.md` - история изменений.
- `PATCH_NOTES_RU.md` - заметки текущей Beta 3.
- `version.json` - feed проверки обновлений.
- `LICENSE` - лицензия.
- `BETA3_CLOUD_REFERENCE_RU.md` - этот референс.

## 4. Действия Streamer.bot в пакете

В экспортированном `.sb` есть следующие группы действий:

- `DonConnect Internal`
  - `DonConnect - Code`
  - `DonConnect - Auto Start`
  - `Add Donation To Credits`

- `DonConnect Providers`
  - `DonConnect - Подключение площадок`
  - `DonConnect - Advanced - DonationAlerts Own App`
  - `DonConnect - Authorize DonationAlerts`
  - `DonConnect - Setup StreamElements`
  - `DonConnect - Setup Streamlabs`
  - `DonConnect - Setup DonatePay RU`
  - `DonConnect - Setup DonatePay EU`
  - `DonConnect - Setup DonateStream`
  - `DonConnect - Setup deStream`
  - `DonConnect - Setup DonateX`
  - `DonConnect - Setup ODA`
  - `DonConnect - Setup Generic API`

- `DonConnect Widgets`
  - `DonConnect - Widget Editor`

- `DonConnect Tools`
  - `DonConnect - Diagnostics`
  - `DonConnect - Status`
  - `DonConnect - Stream Start Reset`
  - `DonConnect - Test Donation`

- `DonConnect Notifications`
  - `DonConnect - Chat Notification`

Для cloud-версии эти actions нужно заменить onboarding flow, background worker и web dashboard. При этом нужно сохранить простоту: "открыл DonConnect, подключил площадки, скопировал OBS URL".

## 5. Локальные URL и API

Базовый адрес: `http://127.0.0.1:3987/donconnect`

Основные страницы:

- `/editor` - главный редактор виджетов.
- `/providers` - подключение донатных площадок.
- `/widget` - алерт доната.
- `/goal` - цель / полоса сбора.
- `/timer` - таймер донатов.
- `/goal-timer` - комбинированная цель + таймер сбора.
- `/credits` - титры.
- `/leaderboard` - лидерборд.
- `/dock` - OBS-док для управления последними донатами.

Основные API:

- `GET /api/ping` - состояние сервера.
- `GET/POST /api/settings` - настройки алерта доната.
- `GET /api/settings-export` - экспорт профиля настроек.
- `POST /api/settings-import` - импорт профиля настроек.
- `GET/POST /api/provider-settings` - настройки подключений.
- `POST /api/provider-authorize-donationalerts` - авторизация DonationAlerts.
- `GET/POST /api/overlay-settings` - цель и таймер.
- `GET /api/goal-state` - состояние цели/таймера.
- `GET/POST /api/credits-settings` - настройки титров.
- `GET /api/credits-state` - состояние титров.
- `GET/POST /api/leaderboard-settings` - настройки лидерборда.
- `GET /api/leaderboard-state` - состояние лидерборда.
- `POST /api/leaderboard-reset` - сброс лидерборда.
- `POST /api/leaderboard-entry` - ручное добавление/редактирование строки.
- `GET/POST /api/content-filter-settings` - запрет ников и слов.
- `POST /api/test-donation` - тестовый донат/алерт.
- `POST /api/timer-test` - ручное добавление времени в таймер.
- `POST /api/speech-test` - проверка озвучки.
- `POST /api/goal-reset` - сброс цели.
- `GET /api/recent-donations` - последние донаты для повторов/дока.
- `POST /api/replay-donation` - повторить алерт без влияния на цель/таймер/титры/лидерборд.
- `POST /api/delete-recent-donation` - удалить донат из локальной истории повторов.
- `POST /api/credits-test` - тестовые титры.
- `POST /api/credits-reset` - сброс титров через Streamer.bot.
- `GET /api/fonts` - каталог системных и локальных шрифтов.
- `GET /api/speech-voices` - голоса Windows.
- `GET /api/alert-media` - медиатека алертов.
- `POST /api/alert-media-upload` - загрузка медиа.
- `POST /api/alert-media-delete` - удаление медиа.
- `POST /api/alert-media-open` - открыть папку медиатеки.
- `POST /api/donation-logs-open` - открыть папку логов донатов.
- `/media/...` - отдача медиафайлов.
- `/font/...` - отдача локальных шрифтов.

## 6. Хранение данных

DonConnect пытается хранить данные рядом с окружением Streamer.bot:

- сначала используется настроенная папка `widget.dataDirectory` или `donconnect.dataDirectory`;
- если она не задана, используется папка `DonConnect` рядом с базовой директорией приложения;
- затем fallback на текущую директорию;
- затем fallback на `%APPDATA%/DonConnect`.

Основные файлы:

- `bridge-settings.json` - резервное хранилище настроек bridge/runtime.
- `widget-settings.json` - настройки алерта доната.
- `goal-timer-settings.json` - цель, таймер, deadline-таймер сбора.
- `credits-settings.json` - титры.
- `leaderboard-settings.json` - внешний вид лидерборда.
- `leaderboard-data.json` - накопленные данные лидерборда.
- `content-filter-settings.json` - запрет ников/слов.
- `alert-media/` - медиафайлы алертов и декоративные картинки.
- `donation-logs/` - дневные текстовые логи донатов.

Логи донатов пишутся по дням, тестовые донаты туда не попадают. Формат строки:

`dd/mm/yy - ник - сумма валюта - площадка - сообщение`

Для cloud-версии это нужно разделить на persistent database, object storage для медиа и export/import профилей.

## 7. Единая модель доната

Основная модель: `UnifiedDonationEvent`.

Ключевые поля:

- `Source`
- `ProviderName`
- `UserName`
- `Amount`
- `Currency`
- `Message`
- `DonationId`
- `Timestamp`
- `RawJson`
- `IsAnonymous`

Нормализованные переменные для Streamer.bot:

- `donationSource`
- `donationProvider`
- `donationUser`
- `donationAmount`
- `donationCurrency`
- `donationMessage`
- `donationId`
- `donationTimestamp`
- `donationRawJson`
- `donationIsAnonymous`

Также сохраняются совместимые `tip*` переменные.

Для cloud-версии эту модель стоит оставить как canonical event contract. Именно вокруг неё должны строиться алерты, цели, таймеры, титры, лидерборд и API.

## 8. Провайдеры донатов

Поддержанные/заведённые провайдеры:

- DonationAlerts
- StreamElements
- Streamlabs
- DonatePay RU
- DonatePay EU
- Donate.Stream
- deStream
- DonateX.gg
- ODA
- Generic API

### DonationAlerts

Статус: реализован OAuth flow. В Beta 3 общие данные приложения удалены: пользователь должен создать своё приложение DonationAlerts, вставить `Client ID` и `Client Secret`, затем авторизоваться.

UX для cloud:

- кнопка "Создать приложение";
- подсказка Redirect URL;
- поля Client ID / Client Secret;
- запрет авторизации, если ключи не заполнены;
- статус подключения.

### StreamElements

Статус: реализована настройка через account/JWT, используется как источник донатов. Есть риск дублей, если StreamElements проксирует оплату через DonatePay EU и DonatePay EU подключен отдельно.

Для cloud нужно отдельное правило дедупликации cross-provider.

### Streamlabs

Статус: провайдер есть в коде и настройках. Нужно перепроверять актуальность API при переносе в cloud.

### DonatePay RU / EU

Статус: реализованы отдельные провайдеры RU и EU. В UX есть отдельные настройки. Возможны дубли с площадками, которые используют DonatePay как платёжный backend.

### Donate.Stream / deStream

Статус: провайдеры заведены. Для cloud нужен отдельный актуальный аудит API и стабильности.

### DonateX.gg

Статус: провайдер заведён, хранит access token и дополнительные параметры.

### ODA

Статус: добавлена интеграция Open Donation Assistant. Важно предупреждать пользователя о возможных дублях, потому что ODA может агрегировать DA, DonatePay, DonateX и другие площадки.

### Generic API

Статус: кастомный провайдер для ручного endpoint. Для cloud это может стать webhook/custom connector.

## 9. Дедупликация и риски дублей

В Beta 3 есть локальная дедупликация на уровне runtime:

- локальный `DonationDeduplicator`;
- стабильный ключ события;
- proxy-signature для случаев, когда одна площадка передаёт донат через другую;
- отдельные last-id/seen-id для некоторых провайдеров;
- защита от тестовых событий в логах и статистике.

Бизнес-проблема для cloud:

- StreamElements может отправить донат, а DonatePay EU потом отправит тот же платёж как отдельное событие;
- ODA может агрегировать те же площадки, что пользователь подключил напрямую;
- часть площадок не всегда передаёт нормальный ник, иногда приходит номер операции.

Cloud-версии нужен нормальный dedupe engine:

- fingerprint по сумме, валюте, времени, сообщению, operation id, provider raw id;
- configurable merge rules;
- warnings при подключении конфликтующих источников;
- ручное "это дубль" / "это разные донаты" в админке.

## 10. Редактор виджетов

Главная страница: `/donconnect/editor`.

Секции редактора:

- Donation
- Goal
- Timer
- Credits
- Leaderboard
- Blocked
- OBS Dock / повторы

UX:

- слева настройки;
- справа live preview;
- iframe preview обновляется сразу;
- есть сетка и прилипание;
- элементы можно выделять, двигать, растягивать и вращать;
- слои отображаются справа в окне предпросмотра;
- элементы поддерживают порядок слоёв;
- есть reset для текущего редактора/раздела;
- есть export/import профиля;
- есть copy OBS URL;
- язык интерфейса: английский, русский, украинский.

Для cloud нужно сохранить общий mental model: "выбрал виджет -> редактируешь слева -> видишь live preview справа -> копируешь OBS URL".

## 11. Алерт доната

URL: `/donconnect/widget`

Возможности:

- пресеты;
- размер алерта;
- фон, цвет текста, акцент;
- отдельные шрифты для донатера, суммы, сообщения, площадки;
- отдельные размеры текста для каждого элемента;
- шаблоны:
  - донатер;
  - сумма/валюта;
  - сообщение;
  - площадка;
- включение/выключение донатера, суммы, сообщения, площадки, фона, медиа, декоративной картинки;
- X/Y/Width/Height/Rotation для каждого элемента;
- слои:
  - background;
  - decor;
  - media;
  - donor;
  - amount;
  - message;
  - platform;
- drag/drop медиа;
- поддержка PNG/JPG/WebP/GIF/MP4/WebM/MP3/WAV/OGG;
- preview медиа;
- удаление медиа;
- открытие папки медиатеки;
- правила по суммам: разные медиа/звуки для разных диапазонов доната;
- анимация появления и исчезновения;
- текстовые анимации;
- длительность показа алерта;
- громкость основного звука и звука текста;
- озвучка доната голосом Windows;
- галочки, какие строки озвучивать:
  - донатер;
  - сумма;
  - площадка;
  - сообщение;
- кастомный тестовый алерт.

После текущей правки стартовые элементы алерта должны быть расположены компактнее вокруг центра и не выглядеть съехавшими на первом запуске.

## 12. Цель / прогресс сбора

URL: `/donconnect/goal`

Возможности:

- пресеты цели;
- заголовок 1 и заголовок 2;
- текущая сумма, цель, валюта;
- формат отображения: сумма, процент, summary;
- длина и высота полосы;
- радиус контейнера и радиус полосы отдельно;
- opacity фона и opacity полосы отдельно;
- горизонтальное и вертикальное заполнение;
- обычная полоса;
- картинка как заполняемый объект;
- режимы картинки:
  - reveal из ч/б;
  - силуэт;
  - прозрачное появление;
  - инвертированное исчезновение;
- декоративная картинка;
- список подключённых площадок;
- скрытие отдельных площадок;
- строка последнего доната;
- слои:
  - background;
  - decor;
  - goalBar;
  - goalImage;
  - goalText;
  - goalMeta;
  - goalDeadline;
  - services;
  - last;
  - title;
- отдельный deadline-таймер сбора:
  - дата и время окончания;
  - дни/часы/минуты/секунды;
  - подпись;
  - текст после окончания;
  - автоотключение после окончания;
  - сохранение времени в настройках.

Для cloud важно разделить обычный donation timer и goal deadline timer. Это разные сущности.

## 13. Таймер донатов

URL: `/donconnect/timer`

Возможности:

- отдельный widget mode;
- countdown: донаты добавляют время;
- countup-reset: таймер идёт вперёд и сбрасывается до нуля при событии;
- конвертация суммы в секунды по формуле `сумма = секунды`;
- поддержка валюты таймера и конвертации в RUB через runtime;
- строка конвертации для зрителя;
- включение/выключение площадок;
- отдельные шрифты для заголовка, подзаголовка, значения, мета-строки и конвертации;
- декоративная картинка;
- фон и основные элементы редактируются в preview;
- слои:
  - background;
  - decor;
  - title;
  - timerBlock;
  - timerTitle;
  - timerSubtitle;
  - timerValue;
  - timerMeta;
  - timerConversion;
  - services.

Важно: таймерный виджет не должен показывать последнего донатера. После текущей правки блок `last donation` принудительно отключается для режима `/timer`.

## 14. Титры / Credits

URL: `/donconnect/credits`

Возможности:

- интеграция со штатными Credits Streamer.bot;
- DonConnect добавляет отдельную секцию донатов;
- видимость native-секций должна определяться Streamer.bot, а не дублирующими галочками в DonConnect;
- можно менять:
  - общий шрифт;
  - шрифт заголовка;
  - шрифт заголовков секций;
  - шрифт деталей;
  - названия секций;
  - показ имени/суммы/площадки/сообщения для донатов;
  - фон, цвета, акцент;
  - прозрачный фон через галочку;
  - скорость;
  - поведение длинных титров;
- есть пауза предпросмотра;
- тестовые титры;
- reset титров через Streamer.bot HTTP Server.

Зависимость от Streamer.bot здесь самая сильная. Для cloud нужно либо:

- построить собственный credits collector;
- либо сделать Streamer.bot integration optional.

## 15. Лидерборд

URL: `/donconnect/leaderboard`

Возможности:

- пресеты;
- режимы:
  - общий топ;
  - топ за месяц;
  - топ за неделю;
  - топ за стрим;
  - слайды по площадкам;
  - последние донаты;
- количество строк до 10;
- показ/скрытие рангов, сумм, площадок;
- выравнивание текста;
- слайд-анимации: fade, slide, none;
- длительность слайдов;
- декоративная картинка;
- ручное добавление строки;
- редактирование имени/суммы/валюты/площадки;
- удаление строки с подъёмом следующих мест;
- сброс лидерборда;
- автоочистка раз в день на старте, чтобы перезапуск стрима после аварии не сбрасывал всё повторно.

Для cloud стоит хранить leaderboard как отдельную aggregate projection поверх donation events.

## 16. OBS Dock

URL: `/donconnect/dock`

Назначение: компактная док-панель для OBS/браузера, чтобы стример видел последние донаты и мог повторить алерт.

Возможности:

- список последних донатов;
- ник, сумма, валюта, сообщение, площадка;
- повторить алерт без влияния на:
  - цель;
  - таймер;
  - титры;
  - лидерборд;
- удалить донат из истории повторов;
- обновить;
- открыть логи донатов;
- мини-индикатор цели;
- мини-таймер;
- сброс цели;
- сброс титров.

Для cloud это может стать отдельным streamer control panel.

## 17. Фильтр / запрет

Секция `Blocked`.

Возможности:

- список запрещённых ников;
- список запрещённых слов;
- replacement nickname;
- replacement text;
- кастомный тест запрета.

Фильтр применяется к браузерным виджетам и не должен ломать исходные переменные доната в Streamer.bot. Для cloud фильтр должен быть отдельным presentation filter, а не изменением raw event.

## 18. Профили настроек

В Beta 3 есть экспорт/импорт профилей:

- экспортируются настройки виджетов;
- экспортируются используемые медиафайлы;
- provider tokens и secrets не экспортируются;
- импорт восстанавливает настройки и медиа.

Для cloud это важная продуктовая функция:

- marketplace/community presets;
- sharing profiles between streamers;
- backup before update;
- demo profiles for onboarding.

Нужно сохранить принцип: профиль может переносить внешний вид, но не переносит секреты площадок.

## 19. Шрифты

В Beta 3 есть каталог шрифтов:

- базовый fallback;
- Windows/system fonts;
- локальная отдача файлов через `/font/...`;
- отдельные font controls для разных элементов;
- sanitizing font-family в CSS.

Для cloud:

- нельзя рассчитывать на шрифты Windows на сервере;
- для OBS/browser лучше поддерживать uploaded fonts или web-safe/font bundles;
- профиль должен переносить кастомный шрифт как asset, если лицензия позволяет.

## 20. Озвучка доната

В Beta 3 озвучка работает локально через Windows voices:

- включение/выключение TTS;
- выбор голоса;
- скорость;
- тон;
- громкость;
- выбор строк для озвучки;
- тест голоса.

Для cloud это нельзя перенести напрямую без изменения подхода. Возможные варианты:

- браузерный Web Speech API;
- серверный TTS provider;
- локальный companion app;
- интеграция с OBS/browser через Web Audio.

С точки зрения UX нужно сохранить галочки "что зачитывать".

## 21. Автостарт и надёжность

В Beta 3 есть:

- `Init()` расширения;
- `StartFromInit()`;
- watchdog запуска локального widget server;
- попытки восстановить сервер после перезапуска Streamer.bot;
- reset стрим-сессии раз в день, чтобы перезапуск стрима не чистил данные повторно;
- проверка обновлений через `version.json`.

Cloud-версии нужен аналог:

- always-on backend;
- per-user workers для провайдеров;
- health checks;
- reconnection policy;
- status page/provider diagnostics.

## 22. Безопасность

Текущие правила Beta 3:

- локальный сервер слушает только `127.0.0.1`;
- не используется `0.0.0.0`;
- нет HTTPS, чтобы не требовать сертификаты;
- токены не печатаются полностью в лог;
- DonationAlerts больше не использует общий Client ID/Secret автора;
- экспорт профиля не содержит provider tokens/secrets.

Для cloud:

- OAuth credentials должны храниться encrypted at rest;
- нужны scopes и refresh-token rotation;
- публичные overlay URL должны иметь unguessable token;
- editor/dashboard должен быть за авторизацией;
- webhook endpoints должны проверять подписи/секреты там, где площадка это позволяет.

## 23. Что обязательно перенести в cloud

- Единая модель доната.
- Provider connection status.
- Browser editor с live preview.
- Donation alert editor с медиа, звуками, TTS/voice settings и rules by amount.
- Goal editor с bar/image reveal modes, deadline timer, layers, drag/resize/rotate.
- Timer editor с countdown/countup-reset и валютной конвертацией.
- Credits/titles editor как отдельный продуктовый модуль.
- Leaderboard modes: all time, month, week, stream, platform slides, recent.
- OBS Dock как control panel.
- Content filter.
- Profiles export/import без секретов.
- Donation daily logs/history.
- Dedupe engine между агрегаторами и прямыми провайдерами.
- Три языка UI: English, Russian, Ukrainian.

## 24. Что нельзя тащить как есть

- Монолитный C# файл с HTML строками.
- Прямую зависимость от Streamer.bot как обязательного runtime.
- Хранение состояния только в локальных JSON-файлах.
- System.Speech как единственный TTS путь.
- Streamer.bot Credits как единственный источник титров.
- Локальные Windows fonts как единственный механизм кастомных шрифтов.
- Синхронный polling там, где cloud может использовать WebSocket/SSE.

## 25. Минимальный cloud parity checklist

Чтобы cloud-версия ощущалась как тот же DonConnect, первая cloud beta должна уметь:

- подключить хотя бы DonationAlerts через личное приложение/OAuth;
- принять donation event;
- показать donation alert;
- обновить goal;
- обновить timer;
- записать donation history;
- показать leaderboard;
- открыть editor с live preview;
- экспортировать/импортировать профиль виджета;
- дать OBS URL;
- иметь русскую локализацию на уровне текущей Beta 3.

## 26. Короткий prompt для другого проекта

Use DonConnect Beta 3 as the functional and UX reference for DonConnect Cloud. Preserve the current product model: donation providers feed a normalized donation event; widgets are configured in a browser editor with live preview; OBS receives browser-source URLs; streamers can customize alerts, goals, timers, credits, leaderboards, dock, media, fonts, filters, profiles and logs. Do not copy the monolithic C# architecture. Rebuild it as a modern web platform with backend services, database persistence, secure OAuth/token storage, real-time overlay updates, reusable widget configuration models, profile import/export without secrets, and optional Streamer.bot integration. Keep the UI familiar: left settings, right live preview, sections for Donation, Goal, Timer, Credits, Leaderboard, Blocked and OBS Dock, with Russian/English/Ukrainian localization.
