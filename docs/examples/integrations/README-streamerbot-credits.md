# Титры Streamer.bot для OBS

Этот проект содержит один готовый файл:

- `streamerbot-credits-overlay.html` - браузерный оверлей, который берет данные из встроенной системы Credits в Streamer.bot и прокручивает их как титры.

## Как это работает

Streamer.bot сам собирает события стрима в `Settings -> Credits`: фолловеры, сабы, рейды, награды канала, активные зрители и другие выбранные пункты. Оверлей просто запрашивает эти данные через локальный HTTP Server Streamer.bot:

- `/GetCredits` - реальные титры текущего стрима.
- `/TestCredits` - тестовые данные, чтобы проверить оформление до стрима.
- `/ClearCredits` - очистить накопленные титры.

## Настройка Streamer.bot

1. Открой Streamer.bot.
2. Перейди в `Settings -> Credits`.
3. Отметь события, которые хочешь показывать в конце стрима.
4. Перейди в `Servers/Clients -> HTTP Server`.
5. Включи `Auto Start`.
6. Проверь, что:
   - `Host` = `127.0.0.1`
   - `Port` = `7474`
7. В `Mappings` добавь папку с этим файлом:
   - `Path`: `credits`
   - `Folder`: путь к папке проекта, например `C:\Users\Strat\OneDrive\Документы\New project`
8. Запусти HTTP Server кнопкой `Start`, если он еще не запущен.

## Подключение в OBS

1. В OBS добавь новый источник `Browser`.
2. Выбери `URL`.
3. Для реальных титров вставь:

```text
http://127.0.0.1:7474/credits/streamerbot-credits-overlay.html
```

4. Для теста вставь:

```text
http://127.0.0.1:7474/credits/streamerbot-credits-overlay.html?test=1
```

5. Рекомендуемый размер источника:
   - Width: `1920`
   - Height: `1080`

## Быстрая настройка внешнего вида

Параметры можно добавлять в конец URL:

```text
?duration=85s&title=Спасибо за стрим&subtitle=Сегодня сияли&outro=До встречи
```

Если уже есть `?test=1`, следующие параметры добавляй через `&`:

```text
http://127.0.0.1:7474/credits/streamerbot-credits-overlay.html?test=1&duration=45s
```

Полезные параметры:

- `test=1` - взять тестовые данные.
- `duration=70s` - длительность прокрутки; больше число = медленнее.
- `title=...` - большой заголовок в начале.
- `subtitle=...` - текст под заголовком.
- `outro=...` - финальная строка.

## Как запускать в конце стрима

Самый простой вариант:

1. Сделай отдельную OBS-сцену `Конец стрима`.
2. Добавь туда Browser Source с URL оверлея.
3. В конце стрима переключись на эту сцену.

Более автоматический вариант:

1. В Streamer.bot создай Action `Show Credits`.
2. Добавь OBS sub-action, который переключает сцену на `Конец стрима`.
3. Повесь этот Action на кнопку Stream Deck, hotkey или команду.

## Как добавить свои кастомные титры

Если Streamer.bot не собирает какое-то событие сам, его можно добавить в титры через C# sub-action.

Пример C# кода для Action:

```csharp
using System;

public class CPHInline
{
    public bool Execute()
    {
        CPH.AddToCredits("Отдельное спасибо", "ИмяЗрителя", false);
        return true;
    }
}
```

Первый аргумент - название секции в титрах. Второй - текст, который появится в этой секции. Третий аргумент `false` говорит Streamer.bot, что мы передаем обычный текст, а не JSON.

Например, можно сделать команду `!thanks Имя`, которая будет добавлять человека в секцию `Отдельное спасибо`.

## Как добавить донаты из нашего донат-трекера

Да, донаты с разных площадок можно добавить в эти же титры. Для этого используется связка из двух файлов:

- `streamerbot-add-donation-to-credits.cs` - C# код для Streamer.bot Action.
- `donation-to-streamerbot-credits.js` - JS-хелпер для проекта, который отслеживает донаты.

### 1. Создай Action в Streamer.bot

1. В Streamer.bot создай новый Action.
2. Назови его строго:

```text
Add Donation To Credits
```

3. Добавь sub-action `C# -> Execute C# Code`.
4. Вставь туда код из файла `streamerbot-add-donation-to-credits.cs`.
5. Сохрани и скомпилируй код.

Этот Action принимает аргументы:

- `donorName` - имя донатера.
- `amount` - сумма.
- `currency` - валюта.
- `platform` - площадка, например `DonationAlerts`, `Boosty`, `Ko-fi`.
- `message` - сообщение доната.
- `creditsSection` - название секции в титрах, по умолчанию `Донаты`.

### 2. Вызови Action из проекта донатов

Когда твой проект получает новый донат, нужно отправить HTTP POST в Streamer.bot `/DoAction`.

Пример:

```js
const { sendDonationToStreamerbotCredits } = require("./donation-to-streamerbot-credits");

await sendDonationToStreamerbotCredits({
  donorName: "ViewerName",
  amount: 500,
  currency: "RUB",
  platform: "DonationAlerts",
  message: "Спасибо за стрим!"
});
```

Если проект не на Node.js, логика та же самая: отправить JSON на `http://127.0.0.1:7474/DoAction`.

```json
{
  "action": {
    "name": "Add Donation To Credits"
  },
  "args": {
    "donorName": "ViewerName",
    "amount": "500",
    "currency": "RUB",
    "platform": "DonationAlerts",
    "message": "Спасибо за стрим!",
    "creditsSection": "Донаты"
  }
}
```

После этого донат попадет в секцию `Донаты`, а `streamerbot-credits-overlay.html` покажет его в конце стрима.

## Очистка перед новым стримом

В `Settings -> Credits` можно нажать `Reset` вручную.

Лучше сделать автоматом: на событие `Stream Online` добавь sub-action `Reset Credits`. Тогда каждый новый стрим начнется с пустого списка.

## Если ничего не показывается

- Убедись, что HTTP Server в Streamer.bot запущен.
- Открой в браузере `http://127.0.0.1:7474/TestCredits`. Должен появиться JSON.
- Проверь, что OBS URL начинается с `http://127.0.0.1:7474/credits/`, а не с `file:///`.
- Для реального режима нужны события, накопленные после последнего Reset.
