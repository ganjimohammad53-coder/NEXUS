# NEXUS 1.0 — گزارش تست‌ها

تاریخ اجرا: 2026-10-20

## خلاصه

- بررسی‌های ایستا: **13 از 13 موفق**.
- تست‌های واقعی C#/.NET: **اجرا نشدند (BLOCKED)**؛ در محیط این اجرا `dotnet`، `csc`، `msbuild` و Mono نصب نیستند.
- بنابراین تعداد تست‌های Runtime موفق/ناموفق فعلاً **قابل گزارش نیست**. در سورس، 23 ویژگی `[Fact]`/`[Theory]` و 8 ردیف `[InlineData]` شناسایی شد؛ در صورت اجرای همهٔ موارد، انتظار می‌رود 29 test case اجرا شود. این عدد نتیجهٔ اجرا نیست.
- تست دستی Windows 10/11 و smoke test رابط کاربری اجرا نشده است.

## نتایج ایستا

| بررسی | نتیجه | توضیح |
|---|---|---|
| XML/XAML و فایل‌های پروژه | PASS | 6 فایل parse شدند؛ خطای XML پیدا نشد |
| YAML گردش‌کارهای GitHub Actions | PASS | 2 فایل parse شدند |
| نسخهٔ پروژه | PASS | Version و InformationalVersion برابر 1.0؛ Assembly/File Version برابر 1.0.0.0 |
| نسخهٔ manifest | PASS | فیلدهای نسخه بررسی شدند |
| برچسب نسخه در UI | PASS | `NEXUS 1.0` در MainWindow.axaml پیدا شد |
| آیکون برنامه | PASS | `Assets/nexus.ico` موجود است |
| فایل‌های تست | PASS | 2 فایل تست؛ 23 ویژگی Fact/Theory و 8 InlineData |
| رد کردن عملیات برگشت‌ناپذیر | PASS (ایستا) | شرط fail-closed در LocalPermissionService وجود دارد؛ runtime اثبات نشده |
| بررسی مجوز مسیرهای تغییر فایل | PASS (ایستا) | فراخوانی‌های مجوزدهی در مسیرهای فعلی UI بررسی شدند |
| بررسی مجوز حذف وظیفه | PASS (ایستا) | مسیر UI قبل از حذف، درخواست برگشت‌ناپذیر و تصمیم مجوز را بررسی می‌کند |
| سلامت ZIP ورودی | PASS | همهٔ 64 ورودی آرشیو خوانده شدند |

## مواردی که هنوز حل یا اثبات نشده‌اند

1. **TOCTOU به‌صورت ریشه‌ای حل نشده است.** بررسی Reparse Point و بازبینی مسیر، جایگزین عملیات امن مبتنی بر handle در Windows نیست. عملیات تغییر فایل در مسیرهای فعلی UI به دلیل fail-closed بودن سرویس مجوز باید متوقف شوند؛ این فقط containment است و تست runtime هم انجام نشده است.
2. **سرویس مجوز مستقل نیست.** LocalPermissionService در همان فرایند اجرا می‌شود. broker جداگانه با ACL و جداسازی اعمال‌شده توسط سیستم‌عامل پیاده‌سازی و تست نشده است.
3. تست‌های SQLite، تست‌های مجوز، Build/Restore، تست یکپارچه و اجرای واقعی برنامه به علت نبود .NET SDK اجرا نشده‌اند.
4. بسته هنوز تأییدیهٔ انتشار Production ندارد.

## گام بعدی لازم

روی Windows با .NET 8 SDK اجرا شود:

```powershell
dotnet --info
dotnet restore .\NEXUS.sln
dotnet build .\NEXUS.sln -c Release --no-restore
dotnet test .\NEXUS.sln -c Release --no-build --logger "trx;LogFileName=tests.trx"
```

سپس اجرای دستی برنامه و بررسی File Manager، Permission gate، Backup/Restore و ناوبری UI لازم است.
