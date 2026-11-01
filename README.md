# BaldiLauncher

Неофициальный лаунчер для игр Baldi's Basics. Показывает все версии в одном окне и запускает их одной кнопкой.

Возможности
- Автопоиск Steam-версий (Classic Remastered, Plus)
- Ручное добавление остальных версий через выбор пути
- Переключатель версий
- Учёт времени в игре
- Кнопки «Открыть папку» и «Вики»
- Настройки сохраняются в `config.json` рядом с лаунчером

Установка
1. Скачай архив из раздела [Releases](../../releases).
2. Распакуй и запусти `BaldiLauncher.exe`.
3. Нужен [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

Сборка из исходников
Visual Studio 2022 с .NET 8 или командой:
```
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```


Проект неофициальный и не связан с Basically Games и mystman12. Файлы игр и их материалы в репозиторий не входят.

Лицензия: MIT.
