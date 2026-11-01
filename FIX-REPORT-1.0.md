# گزارش تغییرات NEXUS 1.0 — بررسی ایستا

## اصلاحات اعمال‌شده
- رفع side effect در `LoadAppearance`: بارگذاری تنظیمات دیگر مقدار `theme` را ناخواسته بازنویسی نمی‌کند.
- افزودن بررسی محافظه‌کارانه‌ی reparse point برای مسیر انتخابی و والدهای موجود پیش از عملیات تغییردهنده‌ی File Manager. این بررسی ریسک را کاهش می‌دهد اما race/TOCTOU را کامل حذف نمی‌کند.
- افزودن تست برای ذخیره، به‌روزرسانی و استقلال کلیدهای settings.
- هماهنگ‌سازی نسخه‌ی هدف `1.0` در metadata، manifest و UI.
- افزودن اسکریپت انتشار ویندوز، پروفایل publish، فایل Inno Setup، workflowهای CI و release در GitHub Actions، راهنمای فارسی و پرامپت حرفه‌ای Codex.

## بررسی‌های انجام‌شده
- بررسی ایستا و مقایسه‌ی تغییرات متنی.
- اعتبارسنجی XML پروژه و manifest.
- بررسی سلامت ZIP پس از بسته‌بندی.
- محیط فعلی فاقد .NET SDK است و دریافت بسته‌ها از شبکه در دسترس نیست؛ بنابراین C# compile و تست‌های .NET در این محیط اجرا نشده‌اند.

## موارد باقی‌مانده
- اجرای واقعی restore/build/test روی Windows یا CI ویندوزی.
- smoke test واقعی روی Windows 10/11.
- پیاده‌سازی handle-based filesystem operations برای حذف race/TOCTOU به‌صورت ریشه‌ای؛ تا آن زمان عملیات برگشت‌ناپذیر fail-closed هستند.
- پیاده‌سازی broker جداگانه با isolation/ACL اعمال‌شده توسط سیستم‌عامل؛ تا آن زمان سرویس مجوزدهی مستقل محسوب نمی‌شود و عملیات برگشت‌ناپذیر مسدودند.
- audit log tamper-evidence قوی ندارد.
- تست‌های بیشتر برای restore/rollback و edge caseهای File Manager.
- بررسی accessibility، scaling و keyboard navigation.

**نتیجه:** این تغییرات آماده‌سازی و اصلاحات هستند؛ بررسی ایستا به‌تنهایی اثبات نمی‌کند نسخه برای انتشار عمومی آماده است.


## Security containment update
- Irreversible operations now fail closed in `LocalPermissionService`, even when the UI constructs a matching in-process approval grant. This blocks destructive file operations, task deletion and database restore in this candidate.
- Security UI and model documentation now state that this is containment, not an independent security boundary.
- Added regression tests asserting irreversible requests are denied even with a fresh matching grant.
- This does **not** claim the final architecture fix is complete: the isolated broker and handle-based filesystem mutation implementation still need to be built and validated on Windows.
