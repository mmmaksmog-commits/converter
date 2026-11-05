# Converter by mysik

Простой конвертер: перетащите в окно файлы или целую папку и выберите формат. Исходники не меняются.

**Страница программы и скачивание:** https://mysik-portfolio.vercel.app/soft/converter.html

## Что умеет

- **Видео** — в MP4, WebM, GIF или только звук: MP3, M4A, WAV, FLAC.
- **Музыка** — mP3, M4A, OGG, Opus, FLAC, WAV.
- **Картинки** — jPG, PNG, WEBP, BMP, ICO. Понимает HEIC с iPhone.
- **Сжатие** — «Высокое», «Среднее» или «Сжать» — чтобы видео пролезло в мессенджер.
- **Уменьшение размера** — 1080p, 720p, 480p для видео, ширина для GIF, длинная сторона для картинок.
- **Прогресс** — сколько осталось и насколько уменьшился файл.

## Как начать

Нужна только Windows 10 или 11 — компилятор .NET Framework 4.x уже встроен в систему.

1. Скачайте репозиторий: зелёная кнопка **Code → Download ZIP**, и распакуйте.
2. Запустите `build.bat` — через пару секунд рядом появится `Converter.exe`.
3. Откройте `Converter.exe`. Права администратора не нужны.
4. **Перетащите файлы в окно.** Можно сразу целую папку.
5. **Выберите формат и качество.** Готовые файлы появятся рядом с исходными или в выбранной папке.

## Полезно знать

- Конвертацию делает ffmpeg. Если установлен Video Loader, берётся его ffmpeg, иначе программа предложит скачать его один раз (~140 МБ).

Файлы: `Converter.cs`, `MainWindow.xaml`, `README.txt`, `Shell.cs`, `Theme.xaml`, `app.ico`, `app.manifest`, `build.bat`.

## Другие программы by mysik

- [Zapret GUI](https://mysik-zapret.netlify.app) — Возвращает Discord, YouTube и Steam одной кнопкой. Без VPN: скорость и IP остаются вашими.
- [DNS Switch](https://mysik-portfolio.vercel.app/soft/dns-switch.html) — Меняет DNS в один клик и сам проверяет, какой быстрее именно у вас.
- [Пинг](https://mysik-portfolio.vercel.app/soft/ping.html) — Следит за интернетом каждую секунду и подсказывает, где проблема: Wi-Fi, роутер или провайдер.
- [Передача](https://mysik-portfolio.vercel.app/soft/peredacha.html) — Файлы, фото и тексты между компьютером и телефоном по QR-коду. Без проводов и облаков.
- [Video Loader](https://mysik-portfolio.vercel.app/soft/video-loader.html) — Скачивает видео и музыку с YouTube, VK Видео, RuTube и TikTok: до 4K, плейлисты, MP3 с обложкой.
- [Снимок](https://mysik-portfolio.vercel.app/soft/snimok.html) — Скриншот области по PrintScreen, стрелки, размытие и нумерация. Сразу в буфер.
- [Win Cleaner](https://mysik-portfolio.vercel.app/soft/win-cleaner.html) — Чистит мусор, убирает рекламу и Copilot, наводит порядок в автозагрузке. Всё можно вернуть.
- [Диск](https://mysik-portfolio.vercel.app/soft/disk.html) — Карта диска за минуту: видно, какие папки и файлы съели всё место.
- [Раскладка](https://mysik-portfolio.vercel.app/soft/raskladka.html) — Напечатали «ghbdtn»? Одна клавиша — и слово исправлено, раскладка переключена.
- [Буфер](https://mysik-portfolio.vercel.app/soft/bufer.html) — Помнит всё, что вы копировали: текст, ссылки, картинки. Ctrl+Shift+V — и вставлено.
- [Визитка](https://mysik-portfolio.vercel.app/soft/vizitka.html) — Конструктор сайтов для мастеров: маникюр, барбер, массаж. Сайт под ключ за 15–30 минут.

Все программы — в портфолио: https://mysik-portfolio.vercel.app/#programs  
Сделано [mysik](https://mysik-portfolio.vercel.app).
