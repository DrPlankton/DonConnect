# DonConnect для Streamer.bot

DonConnect - мост донатов и чаевых для Streamer.bot. Он принимает события от donation/tip-сервисов и передает их в Streamer.bot в едином формате.

Проект сделан как новая самостоятельная реализация: без старых авторских строк, старых ссылок, старого update-checker и без чужих встроенных OAuth-ключей.

## Что уже работает

- Тестовый донат без подключения к сервисам.
- DonationAlerts через OAuth и WebSocket.
- Единые переменные для Streamer.bot.
- Настройки через Actions, без ручного редактирования C# обычным пользователем.
- Дедупликация событий.
- Маскирование секретов в логах.
- Generic API provider для простого polling endpoint.
- Setup-actions для StreamElements, Streamlabs и DonatePay.

## Что пока заготовка

- StreamElements.
- Streamlabs.
- DonatePay.
- Donate.Stream / DonateStream.
- DonateX.gg.

Для этих сервисов уже есть отдельные классы и setup-actions, но реальные адаптеры нужно включать только после проверки актуальной API-схемы. DonConnect не должен притворяться, что поддержка готова, если endpoint или realtime API не подтвержден.

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
- `DonConnect - Status` - статус настроек в лог Streamer.bot.
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

Setup сам запустит авторизацию DonationAlerts, покажет окно с просьбой авторизоваться в браузере и после успешной авторизации сразу начнет слушать донаты.

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
| `tipAmount` | То же, что `donationAmount` |
| `tipCurrency` | То же, что `donationCurrency` |
| `tipMessage` | То же, что `donationMessage` |

## Как сделать свою реакцию на донат

DonConnect не заставляет использовать один общий обработчик. Вместо этого создайте любой свой Action и добавьте trigger:

```text
Add -> Custom -> DonConnect -> Donations
```

Варианты trigger:

```text
Any donation
DonationAlerts donation
DonatePay donation
StreamElements donation
Streamlabs donation
Generic API donation
Donate.Stream donation
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

## StreamElements, Streamlabs, DonatePay

Actions:

```text
DonConnect - Setup StreamElements
DonConnect - Setup Streamlabs
DonConnect - Setup DonatePay
```

Ожидаемые аргументы:

```text
streamElementsAccountId
streamElementsJwtToken
streamlabsToken
donatePayApiKey
```

Для StreamElements используйте:

- `Account ID` - можно считать публичным идентификатором канала.
- `JWT Token` - секретный токен, не показывайте его зрителям и не публикуйте.

`Overlay Token` для DonConnect сейчас не нужен.

StreamElements уже подключается через официальный Astro WebSocket Gateway и topic `channel.tips`. Streamlabs и DonatePay пока сохраняют токены/API-ключи, но реальные адаптеры для них остаются заготовками до финальной проверки API.

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
| `!statusDC` | Показать статус в логах |
| `!testDC` | Проверить DonConnect тестовым донатом |
| `!setupSE` | Настройка StreamElements, если нужен этот сервис |
| `!setupSL` | Настройка Streamlabs, если нужен этот сервис |
| `!setupDP` | Настройка DonatePay, если нужен этот сервис |
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
