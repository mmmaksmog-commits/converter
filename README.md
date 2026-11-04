# Converter by mysik

Простой конвертер для Windows: перетащите файлы или целую папку в окно и выберите формат. Исходные файлы не меняются.

## Что умеет

- **Видео** → MP4, WebM, GIF или только звук (MP3, M4A, WAV, FLAC)
- **Музыка** → MP3, M4A, OGG, Opus, FLAC, WAV
- **Картинки** → JPG, PNG, WEBP, BMP, ICO — понимает и HEIC с iPhone
- **Качество** — «Высокое», «Среднее» или «Сжать», чтобы уменьшить видео перед отправкой
- **Уменьшение размера** — 1080p, 720p, 480p для видео, ширина для GIF, длинная сторона для картинок
- **Прогресс** — сколько осталось и насколько уменьшился файл
- Готовые файлы сохраняются рядом с исходными или в выбранную папку

## Первый запуск

Права администратора не нужны. Конвертацию делает [ffmpeg](https://github.com/yt-dlp/FFmpeg-Builds): если установлен [Video Loader](https://github.com/mmmaksmog-commits/video-loader), берётся его ffmpeg, иначе программа предложит скачать его один раз (~140 МБ) в `%LOCALAPPDATA%\Converter`.

## Сборка

Нужна только Windows 10 или 11 — компилятор .NET Framework 4.x уже встроен в систему.

1. Скачайте репозиторий: зелёная кнопка **Code → Download ZIP**, и распакуйте.
2. Запустите `build.bat` — за несколько секунд рядом появится `Converter.exe`.
3. Перенесите `Converter.exe` куда удобно и запускайте.

Файлы в репозитории: `Converter.cs`, `Shell.cs`, `MainWindow.xaml`, `Theme.xaml`, `app.manifest`, `app.ico`, `build.bat`.

## Другие программы by mysik

- [Zapret GUI](https://mysik-zapret.netlify.app) — возвращает Discord, YouTube и Steam без VPN
- [Video Loader](https://github.com/mmmaksmog-commits/video-loader) — скачивание видео и музыки
- [Converter](https://github.com/mmmaksmog-commits/converter) — конвертер видео, музыки и картинок
- [Win Cleaner](https://github.com/mmmaksmog-commits/win-cleaner) — очистка и настройка Windows
- [DNS Switch](https://github.com/mmmaksmog-commits/dns-switch) — смена DNS в один клик

Все программы — на сайте **[mysik-zapret.netlify.app](https://mysik-zapret.netlify.app)**.
Сделано [mysik](https://mysik-portfolio.vercel.app).
