# BaldiLauncher

Неофициальный лаунчер для игр Baldi's Basics. Показывает все версии в одном окне и запускает их одной кнопкой.

Возможности
- Автопоиск Steam-версий (Classic Remastered, Plus)
- Ручное добавление остальных версий через выбор пути
- Переключатель версий
- Учёт времени в игре
- Кнопки «Открыть папку» и «Вики»
- Настройки сохраняются в `config.json` рядом с лаунчером

**Установка**
1. Скачай `BaldiLauncher.exe`.
2. Установи [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) иначе всё сдохнет и твой пк взорвётся.
3. Запусти эту срань. Настройки сохранятся в `baldiconfig.json` рядом с exe.

Сборка из исходников
Visual Studio 2022 с .NET 8 или командой:
```
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```


Проект неофициальный и не связан с Basically Games и mystman12. Файлы игр и их материалы в репозиторий не входят.

Лицензия: MIT.
