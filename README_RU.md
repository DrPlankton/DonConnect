# DonConnect Beta 2

DonConnect - расширение для Streamer.bot, которое принимает донаты с разных площадок и приводит их к одному формату для Streamer.bot, OBS-виджетов, цели, таймера, титров и лидерборда.

Патчноут Beta 2: [PATCH_NOTES_RU.md](PATCH_NOTES_RU.md)

## Самая короткая установка для новичков

1. Скачайте файл `DonConnect.Beta2.sb` из релиза.
2. В Streamer.bot нажмите **Import**.
3. Перетащите `DonConnect.Beta2.sb` в окно импорта или откройте файл как текст и вставьте его содержимое в **Import String**.
4. Нажмите **Import**.
5. В списке actions откройте группу **DonConnect Widgets**.
6. Выберите `DonConnect - Widget Editor`.
7. В панели **Triggers** нажмите правой кнопкой по **Command Trigger** и выберите **Test Trigger**.
8. Откроется браузерный редактор. В нём можно скопировать ссылки для OBS.

Можно импортировать Beta 2 поверх прошлой версии, но перед тестами лучше удалить старые группы/actions DonConnect и импортировать заново. Так меньше шанс поймать старые хвосты от предыдущих бет.

Горячую клавишу удобно назначить вручную: добавьте к `DonConnect - Widget Editor` Hot Key trigger, например `Ctrl+Alt+D`. После этого редактор можно открывать этой комбинацией.

## Быстрый старт: где взять токены и API

### DonationAlerts

Для тестеров Beta 2 DonationAlerts подключается самым простым способом.

1. Импортируйте `DonConnect.Beta2.sb` в Streamer.bot.
2. Найдите action `DonConnect - DonationAlerts Shared Auth`.
3. В панели **Triggers** нажмите правой кнопкой по **Command Trigger** и выберите **Test Trigger**.
4. В браузере откроется страница входа DonationAlerts.
5. Войдите в свой аккаунт DonationAlerts и разрешите доступ.
6. Вернитесь в Streamer.bot и запустите `DonConnect - Widget Editor`.

В этой бете не нужно создавать свое API-приложение DonationAlerts. Общие тестовые данные приложения уже встроены специально для тестеров, чтобы они могли сразу подключиться к своему аккаунту.

Полезные ссылки:

- Кабинет DonationAlerts: https://www.donationalerts.com/dashboard
- Документация API DonationAlerts: https://www.donationalerts.com/apidoc

### Другие площадки

Откройте setup-action нужного провайдера в Streamer.bot и вставьте токен/API-ключ из личного кабинета сервиса.

- StreamElements: настройки аккаунта/канала и JWT token, https://streamelements.com/dashboard/account/channels
- Помощь StreamElements: https://support.streamelements.com/hc/en-us/categories/10474362906642-Getting-Started
- DonatePay RU: https://donatepay.ru/
- DonatePay EU: https://donatepay.eu/
- Документация DonatePay/DonationPay API: https://api.donationpay.org/documentation/
- DonateX.gg: https://donatex.gg/
- Документация ODA: https://opendonationassistant.mintlify.app/auth
- Donate.Stream: https://donate.stream/

Не вставляйте токены в чат, публичные скриншоты, GitHub issues или текстовые источники OBS.

## Как установить

Нужен один файл:

```text
DonConnect.Beta2.sb
```

В Streamer.bot:

1. Откройте **Import**.
2. Перетащите `DonConnect.Beta2.sb` в окно импорта или откройте файл как текстовый файл.
3. Если открывали как текст, скопируйте всё содержимое и вставьте его в **Import String**.
4. Нажмите **Import**.

После импорта в Streamer.bot появятся actions DonConnect.

## Как открыть редактор виджетов

Запустите один action:

```text
DonConnect - Widget Editor
```

В Streamer.bot выберите этот action, нажмите правой кнопкой по его **Command Trigger** и выберите **Test Trigger**. Action сам запускает встроенный локальный сервер и открывает редактор:

```text
http://127.0.0.1:3987/donconnect/editor
```

Ничего дополнительно устанавливать не нужно. Пользователю не нужны Node.js, npm, Python, Docker, Electron или отдельный сервер.

## URL для OBS

В редакторе есть кнопка копирования URL текущего виджета. Также можно вставить URL вручную.

```text
Донат-алерт:    http://127.0.0.1:3987/donconnect/widget
Цель:           http://127.0.0.1:3987/donconnect/goal
Таймер:         http://127.0.0.1:3987/donconnect/timer
Титры:          http://127.0.0.1:3987/donconnect/credits
Лидерборд:      http://127.0.0.1:3987/donconnect/leaderboard
Док OBS:        http://127.0.0.1:3987/donconnect/dock
```

Рекомендуемые размеры Browser Source в OBS:

```text
Донат-алерт:    1280 x 720
Цель:           1280 x 520
Таймер:         1280 x 420
Титры:          1920 x 1080
Лидерборд:      1280 x 720
Док OBS:        добавляется как Custom Browser Dock в OBS
```

## Что есть в Beta 2

- Встроенный браузерный редактор, который запускается прямо из расширения Streamer.bot.
- Донат-алерт с медиатекой, кастомными тестами, повторами, озвучкой текста и фильтром запретных слов.
- Цель с пресетами, горизонтальным и вертикальным прогрессом, режимами заполнения картинки, площадками, последним донатером и подробной настройкой расположения.
- Таймер с пресетами, режимом отсчета вперед, конвертацией доната во время и ручной тестовой суммой.
- Титры с данными Streamer.bot, паузой, отключением секций, пресетами и живым редактированием стиля.
- Лидерборд с пресетами, редактируемыми строками, фильтром запретов и историей донатов.
- Док-панель OBS для последних донатов и повтора алертов.
- Локальные JSON-настройки в папке `DonConnect` рядом со Streamer.bot, а не в AppData.
- Проверка новых версий при запуске.

## Уведомления о новых версиях

При запуске DonConnect проверяет файл:

```text
https://raw.githubusercontent.com/DrPlankton/DonConnect/main/version.json
```

Если установленная версия устарела, DonConnect один раз напишет короткое сообщение в чат с новой версией и ссылкой на скачивание. Токены, настройки, сообщения донатов и личные данные никуда не отправляются.

## Важно про дубли донатов

Некоторые сервисы могут передавать донаты через другую площадку. Например, StreamElements может прислать донат, который также появится через DonatePay EU, а ODA может собирать DonationAlerts, DonatePay, DonateX и другие источники.

Если вы включаете и агрегатор, и исходную площадку одновременно, обязательно проверьте тестовыми донатами. В DonConnect есть дедупликация, но разные API не всегда отдают одинаковый ник или id операции.

## Переменные Streamer.bot

После доната остаются доступными нормализованные переменные:

```text
donationSource
donationUser
donationAmount
donationCurrency
donationMessage
donationId
donationTimestamp
donationRawJson
donationIsAnonymous
```

Также сохранены совместимые alias-переменные:

```text
tipUser
tipAmount
tipCurrency
tipMessage
```

## Безопасность

Локальный сервер редактора запускается только на:

```text
127.0.0.1
```

Он не доступен из локальной сети, не требует прав администратора и не требует HTTPS-сертификатов. Токены и секреты не печатаются в лог целиком.

## Частые проблемы

Если редактор не открылся, запустите `DonConnect - Widget Editor` еще раз и посмотрите URL в логе Streamer.bot.

Если порт `3987` занят, закройте другую локальную программу, которая использует этот порт, или перезапустите Streamer.bot.

Если OBS не обновляет виджет, убедитесь, что Streamer.bot запущен, action `DonConnect - Widget Editor` был запущен хотя бы один раз, а URL в Browser Source начинается с `http://127.0.0.1:3987/donconnect/`.

Если площадка не подключается, запустите диагностику DonConnect и проверьте, включен ли провайдер и подключен ли он.
