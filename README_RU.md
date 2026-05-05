# DonConnect для Streamer.bot

DonConnect - мост донатов и чаевых для Streamer.bot. Он принимает события от donation/tip-сервисов и передает их в Streamer.bot в едином формате.

Проект сделан как новая самостоятельная реализация: без старых авторских строк, старых ссылок, старого update-checker и без чужих встроенных OAuth-ключей.

## Что уже работает

Текущая публичная сборка: `0.8.1-beta`.

- Тестовый донат без подключения к сервисам.
- DonationAlerts через OAuth и WebSocket.
- StreamElements через Astro WebSocket Gateway и `channel.tips`.
- DonatePay RU и DonatePay EU через polling `/api/v1/transactions`.
- DonateX.gg через polling API.
- Streamlabs через polling API.
- Donate.Stream, deStream и Generic API.
- Единые переменные для Streamer.bot.
- Настройки через Actions, без ручного редактирования C# обычным пользователем.
- Дедупликация событий.
- Маскирование секретов в логах.
- Титры Streamer.bot Credits для донатов.
- Общий донатный гол и донатный таймер/марафон.
- Отдельные OBS overlay для гола и таймера.

## Статус беты

Это beta-версия. Перед использованием на важном эфире лучше проверить ее на копии профиля Streamer.bot и на тестовых донатах. Donate.Stream/deStream могут зависеть от актуального API конкретного кабинета, поэтому для них диагностика особенно важна.

## Файлы

- `DonConnect.cs` - основной C# код.
- `DonConnect.install.sb` - готовый импорт для Streamer.bot.
- `README_RU.md` - инструкция.

## Установка

1. Откройте Streamer.bot.
2. Нажмите **Import**.
3. Откройте `DonConnect.install.sb` в Блокноте.
4. Скопируйте всё содержимое файла.
5. Вставьте в поле **Import String**.
6. Нажмите **Import**.

После импорта появится группа Actions `DonConnect`.

Custom-триггеры DonConnect регистрируются автоматически при первом запуске любого action DonConnect: setup, status, test donation, start или auto start.

## Быстрый тест

Запустите Action:

```text
DonConnect - Test Donation
```

Основной способ без чата: выберите Action `DonConnect - Test Donation`, нажмите ПКМ и выберите **Test Trigger**.

Запасной способ через команду, если вы сами включили команды:

```text
!testDC
```

Если всё работает, DonConnect вызовет Custom Code Event:

```text
Custom -> DonConnect -> Donations -> Any donation
```

Чтобы увидеть реакцию в чате, создайте свой Action и добавьте к нему этот trigger.

Для этого теста не нужны Client ID, Client Secret или токены.

## Основные Actions

- `DonConnect - Setup DonationAlerts` - простая настройка DonationAlerts через общее приложение.
- `DonConnect - Advanced - DonationAlerts Own App` - расширенная настройка через свое OAuth-приложение.
- `DonConnect - Authorize DonationAlerts` - авторизация DonationAlerts в браузере.
- `DonConnect - Start` - запуск прослушивания.
- `DonConnect - Stop` - остановка.
- `DonConnect - Status` - статус настроек в лог Streamer.bot и popup-окно.
- `DonConnect - Test Donation` - тестовое событие.
- `DonConnect - How To Use Donation Triggers` - справка по custom triggers и переменным.

## DonationAlerts: простой режим

Этот режим предназначен для обычных пользователей. Автор сборки заранее создает одно приложение DonationAlerts, а пользователь просто логинится в браузере.

В DonationAlerts при создании приложения укажите Redirect URI:

```text
http://127.0.0.1:8597/donconnect/donationalerts/callback/
```

Scopes:

```text
oauth-user-show oauth-donation-subscribe oauth-donation-index
```

В Streamer.bot откройте Action:

```text
DonConnect - Setup DonationAlerts
```

Вставьте:

```text
sharedClientId = Client ID общего приложения
sharedClientSecret = Client Secret общего приложения
```

Запустите этот Action один раз при подготовке сборки. Потом обычный пользователь делает так:

1. Выбирает Action `DonConnect - Setup DonationAlerts`.
2. Нажимает ПКМ.
3. Выбирает **Test Trigger**.

Запасной способ через команду, если команды включены:

```text
!setupDC
```

Setup сам зарегистрирует триггеры, запустит авторизацию DonationAlerts, покажет окно с просьбой авторизоваться в браузере и после успешной авторизации сразу начнет слушать донаты.

Важно: если `Client Secret` находится внутри `.sb` или C# кода, его нельзя считать полностью скрытым. Это удобно, но менее безопасно, чем auth-сервер.

## DonationAlerts: Advanced Own App

Этот режим для продвинутых пользователей. Каждый пользователь создает свое приложение DonationAlerts и вставляет свои ключи.

В Streamer.bot откройте:

```text
DonConnect - Advanced - DonationAlerts Own App
```

Вставьте:

```text
clientId = ваш Client ID
clientSecret = ваш Client Secret
```

Запустите Action через **Test Trigger**. Запасной способ через команду:

```text
!ownDC
```

После успешной авторизации DonConnect сразу начнет слушать донаты.

## Переменные Streamer.bot

DonConnect передает эти аргументы во все donation triggers. Их можно использовать в сообщениях, OBS actions, звуках, условиях и любых sub-actions Streamer.bot:

| Переменная | Значение |
| --- | --- |
| `donationSource` | Источник события |
| `donationProvider` | Имя провайдера |
| `donationEventType` | Тип события, обычно `donation` |
| `donationUser` | Имя донатера |
| `donationAmount` | Сумма |
| `donationCurrency` | Валюта |
| `donationMessage` | Сообщение |
| `donationId` | ID доната |
| `donationTimestamp` | Время события UTC |
| `donationRawJson` | Исходный JSON |
| `donationIsAnonymous` | `True` или `False` |

Aliases:

| Переменная | Значение |
| --- | --- |
| `tipSource` | То же, что `donationSource` |
| `tipUser` | То же, что `donationUser` |
| `tipUsername` | То же, что `donationUser`. Алиас для старых шаблонов сообщений. |
| `tipName` | То же, что `donationUser`. |
| `tipAmount` | То же, что `donationAmount` |
| `tipCurrency` | То же, что `donationCurrency` |
| `tipMessage` | То же, что `donationMessage` |

## Как сделать свою реакцию на донат

DonConnect не заставляет использовать один общий обработчик. Вместо этого создайте любой свой Action и добавьте trigger:

```text
Add -> Custom -> DonConnect -> Donations
```

Если сразу после импорта подменю `DonConnect` еще не видно, запустите setup нужного сервиса или `DonConnect - Test Donation` один раз. Это ограничение Streamer.bot: меню custom triggers появляется после того, как код зарегистрировал события.

Варианты trigger:

```text
Any donation
DonationAlerts donation
DonatePay donation
DonatePay RU donation
DonatePay EU donation
StreamElements donation
Streamlabs donation
Generic API donation
Donate.Stream donation
deStream donation
DonateX.gg donation
```

Пример: если нужен отдельный alert только для DonationAlerts, создайте Action и добавьте:

```text
Custom -> DonConnect -> Donations -> DonationAlerts donation
```

Если нужен общий alert для всех сервисов:

```text
Custom -> DonConnect -> Donations -> Any donation
```

`DonatePay RU` и `DonatePay EU` также вызывают общий trigger `Any donation`. Если нужен один общий обработчик для всех донатов и одного сбора, используйте именно `Any donation`.

Пример сообщения в чат:

```text
Спасибо, %donationUser%, за донат %donationAmount% %donationCurrency%! %donationMessage%
```

Другие шаблоны:

```text
%tipUser% поддержал канал на %tipAmount% %tipCurrency%!
```

```text
Новый донат с %donationSource%: %donationUser% - %donationAmount% %donationCurrency%. Сообщение: %donationMessage%
```

## Общий сбор донатов без отдельного оверлея

DonConnect может вести общий сбор по всем подключенным сервисам: DonationAlerts, DonatePay RU/EU, DonateX, StreamElements и другим. Это работает через обычные persisted Global Variables Streamer.bot, без отдельного HTTP и без отдельного overlay.

Настройка:

1. Откройте Action `DonConnect - Setup Donation Goal`.
2. Заполните:

```text
donConnectGoalEnabled=true
donConnectGoalTitle=Сбор на новый ПК
donConnectGoalTarget=100000
donConnectGoalStartAmount=0
donConnectGoalCurrency=RUB
donConnectCurrencyConversion=auto
donConnectCurrencyOnError=skip
donConnectCurrencyCacheMinutes=60
donConnectCurrencyApiUrl=https://open.er-api.com/v6/latest/{FROM}
donConnectCurrencyRates=RUB=1;USD=90;EUR=100;UAH=2.2;PLN=23
```

3. Запустите Action через **Test Trigger**.
4. После этого каждый новый донат будет прибавляться к общей сумме.

Если нужно обнулить сбор, запустите `DonConnect - Reset Donation Goal`.

Переменные, которые DonConnect обновляет:

| Переменная | Что показывает |
| --- | --- |
| `donConnectGoalTitle` | Название сбора, например `Сбор на новый ПК`. |
| `donConnectGoalCurrent` | Текущая сумма числом, например `2500`. |
| `donConnectGoalTarget` | Цель числом, например `100000`. |
| `donConnectGoalRemaining` | Сколько осталось до цели. |
| `donConnectGoalPercent` | Процент числом без знака `%`, например `25.5`. |
| `donConnectGoalCurrency` | Валюта сбора. |
| `donConnectGoalCurrentText` | Готовая строка текущей суммы, например `2500 RUB`. |
| `donConnectGoalTargetText` | Готовая строка цели, например `100000 RUB`. |
| `donConnectGoalRemainingText` | Готовая строка остатка. |
| `donConnectGoalPercentText` | Готовый процент, например `25.5%`. |
| `donConnectGoalSummary` | Готовая строка `2500 RUB / 100000 RUB (2.5%)`. |
| `donConnectLastDonationUser` | Последний донатер. |
| `donConnectLastDonationAmount` | Сумма последнего доната. |
| `donConnectLastDonationCurrency` | Валюта последнего доната. |
| `donConnectLastDonationPlatform` | Площадка последнего доната, например `DonatePay RU`. |

Внутри donation trigger эти переменные доступны как обычные args, например:

```text
%donConnectGoalSummary%
%donConnectGoalPercentText%
%donConnectGoalRemainingText%
```

Для OBS text/source или progress bar используйте штатные средства Streamer.bot: возьмите persisted Global Variable и подставьте её в нужный sub-action. Для полоски обычно удобно брать `donConnectGoalPercent` как число от `0` до `100`.

Важно: DonConnect складывает сбор и таймер в одной выбранной валюте. Если донат пришёл в другой валюте, DonConnect сначала конвертирует сумму, а потом прибавляет её к сбору/таймеру.

Конвертация валют:

| Настройка | Значение |
| --- | --- |
| `donConnectCurrencyConversion=auto` | Донат в любой валюте конвертируется через актуальный курс API. |
| `donConnectCurrencyApiUrl=https://open.er-api.com/v6/latest/{FROM}` | Бесплатный no-key endpoint курсов. `{FROM}` заменяется на валюту доната: `USD`, `EUR`, `UAH`, `PLN` и т.д. |
| `donConnectCurrencyCacheMinutes=60` | Курс пары кешируется на 60 минут. |
| `donConnectCurrencyOnError=skip` | Если курс не получен, не прибавлять донат к сбору/таймеру, чтобы не испортить сумму. Alert при этом всё равно сработает. |
| `donConnectCurrencyRates=...` | Ручной fallback, если API недоступен или валюты нет в API. |

Пример: валюта сбора `RUB`, пришло `10 USD`. DonConnect запросит курс `USD -> RUB`, прибавит пересчитанную сумму к `donConnectGoalCurrent`, а оригинальный донат останется в `%donationAmount% %donationCurrency%`.

Для редких валют можно расширить ручной fallback:

```text
donConnectCurrencyRates=RUB=1;USD=95;EUR=105;UAH=2.4;PLN=24;KZT=0.2
```

Новые переменные конвертации:

| Переменная | Что показывает |
| --- | --- |
| `donConnectLastDonationOriginalAmount` | Оригинальная сумма доната. |
| `donConnectLastDonationOriginalCurrency` | Оригинальная валюта доната. |
| `donConnectLastDonationConvertedAmount` | Сумма после конвертации. |
| `donConnectLastDonationConvertedCurrency` | Валюта сбора/таймера. |
| `donConnectLastDonationConversionRate` | Использованный курс. |
| `donConnectLastDonationConversionStatus` | `same`, `auto`, `cached`, `manualFallback`, либо текст ошибки. |

### Быстрый OBS overlay для сбора

Если стример не хочет собирать свой UI в OBS/Streamer.bot, используйте готовый overlay:

1. Запустите `DonConnect - Setup Goal Overlay`.
2. В Streamer.bot откройте `Servers/Clients -> HTTP Server`.
3. В `Static File Servers` добавьте mapping:

```text
Path: donconnect-overlays
Folder: D:\SBBOTcodex\DonConnect\overlays
```

4. В OBS Browser Source откройте:

```text
http://127.0.0.1:7474/donconnect-overlays/donconnect-goal-overlay.html
```

Все цвета/шрифт/масштаб меняются в Action `DonConnect - Setup Goal Overlay`.

## Донатный таймер для марафона

DonConnect может добавлять время за донаты со всех подключенных сервисов. Это тоже работает через обычные persisted Global Variables Streamer.bot.

Настройка:

1. Откройте Action `DonConnect - Setup Donation Timer`.
2. Заполните:

```text
donConnectTimerEnabled=true
donConnectTimerTitle=Марафон
donConnectTimerStartSeconds=0
donConnectTimerUnitAmount=100
donConnectTimerSecondsPerUnit=60
donConnectTimerMaxSeconds=0
donConnectTimerCurrency=RUB
donConnectCurrencyConversion=auto
donConnectCurrencyOnError=skip
donConnectCurrencyCacheMinutes=60
donConnectCurrencyApiUrl=https://open.er-api.com/v6/latest/{FROM}
donConnectCurrencyRates=RUB=1;USD=90;EUR=100;UAH=2.2;PLN=23
```

3. Запустите Action через **Test Trigger**.

Пример выше означает: каждые `100 RUB` добавляют `60` секунд. Донат `500 RUB` добавит `5` минут. `donConnectTimerMaxSeconds=0` означает без лимита. Если поставить `28800`, таймер не поднимется выше 8 часов.

Переменные таймера:

| Переменная | Что показывает |
| --- | --- |
| `donConnectTimerTitle` | Название таймера. |
| `donConnectTimerSeconds` | Сколько секунд осталось на момент последнего обновления. |
| `donConnectTimerText` | Готовое время `HH:MM:SS`. |
| `donConnectTimerEndsAt` | UTC-время окончания таймера в ISO-формате. |
| `donConnectTimerAddedSeconds` | Сколько секунд добавил последний донат. |
| `donConnectTimerAddedText` | Сколько времени добавил последний донат в формате `HH:MM:SS`. |
| `donConnectTimerSummary` | Готовая строка `Марафон: 01:30:00`. |
| `donConnectTimerLastDonationUser` | Кто последним добавил время. |
| `donConnectTimerLastDonationAmount` | Сумма последнего доната. |
| `donConnectTimerLastDonationPlatform` | Площадка последнего доната. |

Для сообщения после доната можно использовать:

```text
%donationUser% добавил %donConnectTimerAddedText%! Осталось: %donConnectTimerText%
```

Для OBS text source можно вывести:

```text
%donConnectTimerText%
```

Важно: `donConnectTimerText` обновляется при setup/reset и при донате. Для живого обратного отсчета каждую секунду используйте штатный Timer в Streamer.bot: он может раз в секунду пересчитывать остаток от `donConnectTimerEndsAt` и обновлять текст/OBS source. DonConnect здесь отвечает за главное: добавляет время от донатов и хранит `endsAt`.

### Быстрый OBS overlay для таймера

Для таймера используйте отдельный setup:

1. Запустите `DonConnect - Setup Timer Overlay`.
2. В OBS Browser Source откройте:

```text
http://127.0.0.1:7474/donconnect-overlays/donconnect-timer-overlay.html
```

Если нужно показать и сбор, и таймер сразу:

```text
http://127.0.0.1:7474/donconnect-overlays/donconnect-goal-timer-overlay.html?mode=both
```

Параметры в `DonConnect - Setup Goal Overlay` и `DonConnect - Setup Timer Overlay`:

| Настройка | Пример | Что делает |
| --- | --- | --- |
| `DONCONNECT_OVERLAY_MODE` | `both`, `goal`, `timer` | Что показывать по умолчанию. |
| `DONCONNECT_OVERLAY_TITLE` | `DonConnect` | Заголовок overlay. |
| `DONCONNECT_OVERLAY_FONT` | `Segoe UI, Arial, sans-serif` | Шрифт. |
| `DONCONNECT_OVERLAY_BG` | `rgba(10, 12, 18, 0.72)` | Фон панели. |
| `DONCONNECT_OVERLAY_TEXT` | `#ffffff` | Основной текст. |
| `DONCONNECT_OVERLAY_MUTED` | `#b8c0cc` | Вторичный текст. |
| `DONCONNECT_OVERLAY_ACCENT` | `#35d07f` | Цвет прогресс-бара и акцентов. |
| `DONCONNECT_OVERLAY_BAR_BG` | `rgba(255,255,255,0.18)` | Фон прогресс-бара. |
| `DONCONNECT_OVERLAY_RADIUS` | `8px` | Скругление. |
| `DONCONNECT_OVERLAY_SCALE` | `1` | Масштаб всего overlay. |

URL тоже может переопределять часть настроек:

```text
...?mode=timer&accent=#ffcc00&scale=1.2
```

## StreamElements, Streamlabs, DonatePay, DonateX

Actions в импорте разложены по группам, чтобы стримеру было проще:

| Группа | Что внутри |
| --- | --- |
| `DonConnect Providers` | Подключение сервисов донатов. |
| `DonConnect Widgets` | Титры, сбор, таймер и overlay. |
| `DonConnect Tools` | Диагностика, старт/стоп, тестовый донат. |
| `DonConnect Internal` | Служебный код и auto start, обычно не трогать. |

Основные provider-actions:

```text
DonConnect - Setup StreamElements
DonConnect - Setup Streamlabs
DonConnect - Setup DonatePay RU
DonConnect - Setup DonatePay EU
DonConnect - Setup DonateX
```

Ожидаемые аргументы:

```text
streamElementsAccountId
streamElementsJwtToken
streamlabsToken
donatePayApiKey
donatePayApiHost
donatePayPollSeconds
donateXAccessToken
donateXApiBase
donateXPollSeconds
```

Для StreamElements используйте:

- `Account ID` - можно считать публичным идентификатором канала.
- `JWT Token` - секретный токен, не показывайте его зрителям и не публикуйте.

`Overlay Token` для DonConnect сейчас не нужен.

StreamElements подключается через официальный Astro WebSocket Gateway и topic `channel.tips`. DonateX опрашивается чаще, по умолчанию раз в `5` секунд (`donateXPollSeconds=5`), но реальная скорость зависит от того, как быстро DonateX отдаст донат в API. DonatePay по умолчанию опрашивается раз в `20` секунд (`donatePayPollSeconds=20`); если у конкретного аккаунта появляются `429 Too Many Requests`, увеличьте интервал.

Для DonatePay важен домен API. В импорте есть два отдельных action, чтобы не путаться:

- `DonConnect - Setup DonatePay RU` - для ключей с `donatepay.ru`.
- `DonConnect - Setup DonatePay EU` - для ключей с `donatepay.eu`.

В каждом из этих actions уже выставлен правильный домен:

```text
donatePayApiHost=https://donatepay.ru
```

или

```text
donatePayApiHost=https://donatepay.eu
```

## Generic API

Action:

```text
DonConnect - Setup Generic API
```

Ожидаемые аргументы:

```text
genericEndpoint
genericToken
```

Минимальный JSON:

```json
[
  {
    "id": "abc-123",
    "user": "Viewer",
    "amount": 100,
    "currency": "RUB",
    "message": "Привет!"
  }
]
```

## Статус

Запустите Action или напишите в чат:

```text
!statusDC
```

Он напишет в лог Streamer.bot, какие провайдеры включены и сохранены ли токены.

## Команды чата

Команды уже добавлены в импорт, но Streamer.bot может импортировать их выключенными. Это нормально. Основной способ настройки DonConnect - **Test Trigger**, чтобы не писать служебные команды в чат во время стрима.

Если вы всё же включаете команды, оставьте права только для `Broadcaster`. В импорт уже заложено ограничение `Broadcaster`, чтобы модераторы и зрители не могли запускать настройку.

| Команда | Что делает |
| --- | --- |
| `!setupDC` | Простая настройка и авторизация DonationAlerts |
| `!ownDC` | Advanced: настройка DonationAlerts через свое приложение |
| `!authDC` | Повторная авторизация DonationAlerts и автоматический запуск |
| `!startDC` | Служебный ручной запуск, обычно не нужен |
| `!stopDC` | Остановить прослушивание |
| `!resetDC` | Перезапустить DonConnect. Включено для стримера и модераторов |
| `!statusDC` | Показать статус в логах |
| `!testDC` | Проверить DonConnect тестовым донатом |
| `!setupSE` | Настройка StreamElements, если нужен этот сервис |
| `!setupSL` | Настройка Streamlabs, если нужен этот сервис |
| `!setupDPRU` | Настройка DonatePay RU |
| `!setupDPEU` | Настройка DonatePay EU |
| `!setupDCGoalOverlay` | Настройка overlay донатного гола |
| `!setupDCTimerOverlay` | Настройка overlay донатного таймера |
| `!setupGenericDC` | Настройка Generic API |

Если сервис вам не нужен, его setup-action можно не открывать и не запускать.

После setup DonConnect запускается сам. При перезапуске Streamer.bot action `DonConnect - Auto Start` тихо поднимает прослушивание снова, если токены уже сохранены. Он не открывает браузер.

## Безопасность

- Не хардкодьте личные токены без необходимости.
- DonConnect маскирует секреты в логах.
- Глобальные переменные Streamer.bot удобны, но это не полноценное защищенное хранилище секретов.
- Shared DonationAlerts mode удобен для пользователей, но `Client Secret` внутри `.sb` технически можно извлечь.

## Troubleshooting

### Тестовый донат не срабатывает

Проверьте, что Action `DonConnect - Test Donation` скомпилирован и запускается через trigger.

### DonationAlerts не авторизуется

Проверьте Redirect URI:

```text
http://127.0.0.1:8597/donconnect/donationalerts/callback/
```

Он должен совпадать точно.

### Донат пришел, но мой сценарий не запускается

Проверьте, что в вашем Action добавлен trigger:

```text
Custom -> DonConnect -> Donations -> Any donation
```

### Нужны подробные логи

Создайте глобальную переменную:

```text
udb_debug = True
```

Затем перезапустите:

```text
DonConnect - Stop
DonConnect - Start
```

## Титры Streamer.bot Credits

В импорт `DonConnect.install.sb` теперь входят два Action в группе `DonConnect`:

- `DonConnect - Credits` - включает и настраивает отправку донатов в Credits.
- `Add Donation To Credits` - пример служебного Action для ручной/HTTP-интеграции. Сам DonConnect теперь пишет донаты в Credits напрямую через `CPH.AddToCredits(...)`.

Как включить:

1. В Streamer.bot включите `Servers/Clients -> HTTP Server`.
2. Проверьте `Host = 127.0.0.1`, `Port = 7474`.
3. Запустите `DonConnect - Credits` через **Test Trigger**.
4. После этого новые донаты из DonConnect будут напрямую попадать в Credits.

Настройки в Action `DonConnect - Credits`:

```text
STREAMERBOT_CREDITS_ENABLED=true
STREAMERBOT_HTTP_URL=http://127.0.0.1:7474
STREAMERBOT_CREDITS_ACTION=Add Donation To Credits
STREAMERBOT_CREDITS_SECTION=Донаты
STREAMERBOT_CREDITS_FIELDS=name,amount
```

HTTP Server нужен оверлею OBS для `/GetCredits` и статического HTML. Добавление донатов в Credits происходит внутри Streamer.bot напрямую.

### Таблица настроек титров

Актуальная схема: DonConnect добавляет в Streamer.bot Credits только донаты. Если у стримера уже есть свой оверлей титров, он может продолжать использовать его: донаты DonConnect появятся в штатном `/GetCredits -> custom`. Встроенный оверлей DonConnect нужен только как готовый вариант для OBS и настраивается через Action `DonConnect - Credits`.

Разделы `Follows`, `Raided`, `Subs`, `Users`, `Top` и другие по-прежнему собирает сам Streamer.bot в `Settings -> Credits`. В оверлее DonConnect можно дополнительно скрывать лишние разделы через `STREAMERBOT_CREDITS_HIDE_SECTIONS`, не меняя Streamer.bot и не ломая существующие титры.

OBS URL можно оставить простым:

```text
http://127.0.0.1:7474/credits/streamerbot-credits-overlay.html?v=7
```

Основные настройки внешнего вида и названий теперь меняются в Action `DonConnect - Credits`, затем этот Action нужно запустить. Он создаёт файл:

```text
D:\SBBOTcodex\DonConnect\credits\credits-config.json
```

Настройки `DonConnect - Credits` управляют тем, что DonConnect записывает в Streamer.bot Credits:

| Настройка | Где писать | Значение по умолчанию | Что делает |
| --- | --- | --- | --- |
| `STREAMERBOT_CREDITS_ENABLED` | Action `DonConnect - Credits` | `true` | Включает/выключает добавление донатов в титры. |
| `STREAMERBOT_CREDITS_SECTION` | Action `DonConnect - Credits` | `Донаты` | Название секции в `/GetCredits -> custom`. |
| `STREAMERBOT_CREDITS_FIELDS` | Action `DonConnect - Credits` | `name,amount` | Какие части доната показывать в оверлее. |
| `STREAMERBOT_CREDITS_TITLE` | Action `DonConnect - Credits` | `Спасибо за стрим` | Главный заголовок титров. |
| `STREAMERBOT_CREDITS_SUBTITLE` | Action `DonConnect - Credits` | `Сегодня с нами были` | Подзаголовок. |
| `STREAMERBOT_CREDITS_OUTRO` | Action `DonConnect - Credits` | `Увидимся на следующем стриме` | Финальная строка. |
| `STREAMERBOT_CREDITS_DURATION` | Action `DonConnect - Credits` | `90s` | Скорость прокрутки. Больше число - медленнее. |
| `STREAMERBOT_CREDITS_ACCENT` | Action `DonConnect - Credits` | `#ffcf5a` | Цвет заголовков секций. |
| `STREAMERBOT_CREDITS_TEXT` | Action `DonConnect - Credits` | `#f7f4ec` | Цвет основного текста. |
| `STREAMERBOT_CREDITS_MUTED` | Action `DonConnect - Credits` | `#b9d8d2` | Цвет деталей под ником. |
| `STREAMERBOT_CREDITS_BG` | Action `DonConnect - Credits` | `transparent` | Фон оверлея. |
| `STREAMERBOT_CREDITS_FONT` | Action `DonConnect - Credits` | `Segoe UI, Arial, sans-serif` | Шрифт. Можно писать любой установленный шрифт. |
| `STREAMERBOT_CREDITS_SECTION_LABELS` | Action `DonConnect - Credits` | список `Key=Название` | Переименовывает разделы. Можно писать на русском, английском, японском и т.д. |
| `STREAMERBOT_CREDITS_HIDE_SECTIONS` | Action `DonConnect - Credits` | `Users,allBits,monthBits,weekBits` | Скрывает разделы только во встроенном оверлее DonConnect. Донаты в native Credits остаются. |
| `STREAMERBOT_CREDITS_SHOW_SECTIONS` | Action `DonConnect - Credits` | пусто | Если заполнить, оверлей покажет только перечисленные разделы. Обычно оставляйте пустым. |
| `STREAMERBOT_CREDITS_CONFIG_PATH` | Action `DonConnect - Credits` | `D:\SBBOTcodex\DonConnect\credits\credits-config.json` | Куда сохраняется конфиг оверлея. |

Поля для `STREAMERBOT_CREDITS_FIELDS`:

| Поле | Что показывает |
| --- | --- |
| `name` | Ник донатера. Ник всегда остается основной строкой. |
| `amount` | Сумма и валюта, например `100 RUB`. |
| `platform` | Сервис доната, например `DonateX.gg` или `StreamElements`. |
| `message` | Текст сообщения к донату. |

Примеры:

```text
name,amount
```

Покажет ник и сумму. Это текущий рекомендованный вариант.

```text
name,amount,message
```

Покажет ник, сумму и сообщение.

```text
name
```

Покажет только ник.

Формат `STREAMERBOT_CREDITS_SECTION_LABELS`:

```text
Follows=フォロー;Raided=レイド;Moderator=Модераторы;Users=Зрители;донаты=Донаты
```

Слева пишется ключ из `/GetCredits`, справа любое название, которое должно быть видно в титрах. Разделы включаются/выключаются не здесь, а штатными галочками Streamer.bot в `Settings -> Credits`.

Формат `STREAMERBOT_CREDITS_HIDE_SECTIONS`:

```text
Users,allBits,monthBits,weekBits
```

Это уберет зрителей в чате и топы Bits из встроенного оверлея DonConnect. Чтобы вернуть зрителей, удалите `Users` из строки и снова запустите `DonConnect - Credits`. Чтобы показывать только донаты, можно написать в `STREAMERBOT_CREDITS_SHOW_SECTIONS` название своей секции, например:

```text
донаты
```

Рекомендованный URL для OBS теперь простой. Все обычные настройки делаются в Action `DonConnect - Credits`:

```text
http://127.0.0.1:7474/credits/streamerbot-credits-overlay.html?v=7
```

Файлы старого мини-пакета с оверлеем и примерами перенесены сюда:

```text
docs/examples/integrations/
```
