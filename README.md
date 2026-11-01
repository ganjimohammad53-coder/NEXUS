# NEXUS Personal Control Center — 1.0 (Readiness Candidate)

> **وضعیت انتشار:** شماره نسخه‌ی هدف 1.0 در metadata تنظیم شده است؛ اما تا وقتی build/test واقعی روی Windows و smoke test دستی موفق نشده‌اند، این پروژه را انتشار عمومیِ تأییدشده تلقی نکنید. این نسخه هنوز محدودیت‌های امنیتی و قابلیت‌های ناقص دارد.

NEXUS یک برنامه‌ی دسکتاپ local-first برای ویندوز است که با C#، .NET 8، Avalonia 11 و SQLite ساخته شده است. این مخزن شامل پوسته‌ی رابط کاربری و چند workflow محلی واقعی است؛ تمام بخش‌های UI به معنی قابلیت کامل و production-ready نیستند.

## وضعیت ماژول‌ها

| ماژول | وضعیت فعلی | محدودیت مهم |
|---|---|---|
| Dashboard | پایه‌ای | اطلاعات task و runtime؛ از نمایش آمار ساختگی خودداری می‌شود. |
| AI Center | غیرفعال | اجرای مدل و اتصال آنلاین در این نسخه پیاده‌سازی/فعال نشده است. |
| System Center | پایه‌ای | اطلاعات عمومی سیستم و runtime؛ پایش پیشرفته سخت‌افزار کامل نیست. |
| Productivity | محلی | taskها در SQLite ذخیره می‌شوند. |
| File Manager | محدود | فهرست حداکثر 200 ورودی، ساخت پوشه، rename و حذف فایل با تأیید؛ حذف بازگشتی پوشه غیرفعال است. بررسی reparse point ریسک را کاهش می‌دهد اما race/TOCTOU را کامل حذف نمی‌کند. |
| Security / Backup | پایه‌ی محافظت‌شده، نه محصول امنیتی نهایی | snapshot دیتابیس، manifest، SHA-256 و integrity check؛ محدودیت‌های audit و restore در ادامه آمده‌اند. |
| Automation | محدود | recipeهای محلی برای ناوبری؛ shell/script اجرا نمی‌شود. |
| Gaming | پایه‌ای | تشخیص محدود launcher/process؛ مدیریت یا tuning بازی‌ها کامل نیست. |
| Network | پایه‌ای | مشاهده‌ی اطلاعات adapter و برخی تست‌های اتصال؛ اسکن پنهان شبکه انجام نمی‌شود. |
| Customization / Settings | جزئی | accent، density، navigation و startup page ذخیره می‌شوند؛ تم این نسخه تیره است. |

## پیش‌نیازها

- Windows 10/11 x64
- .NET SDK سازگار با `global.json` (نسخه‌ی هدف 8.0.426 یا SDK سازگار بر اساس `rollForward`)
- اتصال به NuGet برای restore اولیه، مگر اینکه تمام پکیج‌ها در cache باشند.

## Build و Test

در PowerShell از پوشه‌ی اصلی پروژه اجرا کنید:

```powershell
dotnet --info
dotnet restore .\NEXUS.sln
dotnet build .\NEXUS.sln -c Release --no-restore
dotnet test .\NEXUS.sln -c Release --no-build
dotnet run --project .\src\Nexus.App\Nexus.App.csproj
```

برای ساخت بسته‌ی Windows x64:

```powershell
.\RELEASE-WINDOWS.ps1
```

اسکریپت restore، build، test و publish را اجرا می‌کند و در اولین شکست متوقف می‌شود. خروجی portable در `artifacts\NEXUS-1.0-win-x64-portable.zip` قرار می‌گیرد. اگر Inno Setup 6 و `ISCC.exe` در PATH باشند، نصب‌کننده نیز ساخته می‌شود.

## محل ذخیره‌ی داده‌ها

داده‌های برنامه در `%LOCALAPPDATA%\NEXUS` ذخیره می‌شوند:

- `nexus.db` — taskها و تنظیمات
- `Backups\` — snapshotهای دیتابیس و manifestها
- `security-audit.log` — رخدادهای حداقلی محلی

حذف پوشه‌ی نصب نباید پوشه‌ی داده‌ی کاربر را حذف کند. قبل از restore یا دست‌کاری دستی دیتابیس، نسخه‌ی پشتیبان جداگانه تهیه کنید.

## مدل امنیتی و محدودیت‌ها

- سرویس مجوز فعلی داخل همان process اجرا می‌شود و **مرز امنیتی مستقل در برابر process compromise نیست**.
- audit log ساده است و tamper-evident قوی یا زنجیره‌ی رمزنگاری‌شده‌ی قابل‌اعتماد ارائه نمی‌کند.
- hash در manifest می‌تواند دست‌کاری تصادفی را آشکار کند؛ به‌تنهایی اصالت فایل را در برابر مهاجمی که هم backup و هم manifest را تغییر می‌دهد ثابت نمی‌کند.
- بررسی reparse point در File Manager محافظه‌کارانه است، ولی دفاع handle-based در برابر race/TOCTOU هنوز تکمیل نشده است.
- plugin/script execution، online AI، administrator elevation و حذف بازگشتی پوشه غیرفعال‌اند.
- قبل از انتشار عمومی، restore/rollback، چندنمونه‌ای بودن برنامه، تست‌های Windows 10/11، دسترس‌پذیری و ارزیابی dependencyها باید تکمیل شوند.

## معیار انتشار

تا وقتی CI ویندوزی، Release build، همه‌ی تست‌ها و smoke test دستی روی Windows 10/11 موفق نشده‌اند، وضعیت را **نیازمند تأیید نهایی** نگه دارید. راهنمای کامل در `docs/راهنمای-انتشار-1.0.md` و گزارش تغییرات ایستا در `FIX-REPORT-1.0.md` است.

## پرامپت توسعه

برای ادامه‌ی کار با ابزارهای کدنویسی هوش مصنوعی از `PROMPT-CODEX-RELEASE-1.0.md` استفاده کنید. این پرامپت بر شواهد واقعی، تست، امنیت و اعلام صریح محدودیت‌ها تأکید می‌کند.

## License

برای این مخزن مجوز توزیع مشخصی تعریف نشده است. پیش از انتشار عمومی، مجوز مناسب را تعیین کنید.

## Multi-game live AI (2026-10-27 patch)
See `MULTI-GAME-LIVE-AI-REPORT.md` for current support boundaries. CS2 and Dota 2 use local GSI snapshot receivers; Minecraft Java can analyze a user-triggered local `latest.log` snapshot. R6 and Rust are listed honestly as needing a supported telemetry bridge; they are not claimed to have automatic live-state analysis in this patch.


## Online accounts, web admin, and cloud sync (Phase 1 foundation)

A separate ASP.NET Core backend and PostgreSQL deployment scaffold are now included under `src/Nexus.Server` and `deploy`. The web control panel is served at `/` when the backend runs. Registration/login, server sessions, revision-aware JSON sync endpoints, admin summary/users endpoints, rate limiting, and health endpoints are implemented in the server source. `CloudSyncClient` provides an optional desktop-side HTTP client, but the current desktop UI has not yet been wired to the login/sync flows.

**Capacity status: not verified.** The repository includes k6 scripts to ramp readiness checks to 1,000 virtual users and authenticated sync requests using 1,000 distinct test tokens. These scripts have not been executed against a live server here. Do not claim production support for 1,000 simultaneous users until the full load test and server monitoring have passed. Setup and limitations: `ONLINE-ARCHITECTURE-PHASE1.md`.
