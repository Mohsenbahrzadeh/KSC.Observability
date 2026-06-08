<div dir="rtl">

# راهنمای آموزشی KSC.Observability

> **نسخهٔ سند: 0.2.0** — متناظر با نسخهٔ پکیج‌ها (افزودن پشتیبانی .NET 8 / ASP.NET Core).

> مانیتورینگ و متریک آمادهٔ نصب برای اپلیکیشن‌های **.NET Framework** (ASP.NET Web Forms / MVC)
> **و .NET 8 / ASP.NET Core**، بر پایهٔ **Prometheus** و **Grafana**.

این سند هم **توضیح می‌دهد چه چیزی ساخته شد و چرا**، و هم **گام‌به‌گام یاد می‌دهد چطور استفاده کنید**.

> **کدام پکیج را نصب کنم؟** اپ‌های **.NET Framework** → `KSC.Observability.AspNet`؛
> اپ‌های **.NET 8 / ASP.NET Core** → `KSC.Observability.AspNetCore`. هستهٔ متریک بین هر دو مشترک است.

---

## فهرست

1. [مسئله چه بود؟](#۱-مسئله-چه-بود)
2. [مفاهیم پایه (قبل از شروع بخوانید)](#۲-مفاهیم-پایه)
3. [معماری پروژه](#۳-معماری-پروژه)
4. [متریک‌هایی که جمع می‌شوند](#۴-متریکهایی-که-جمع-میشوند)
5. [پشت صحنه: چطور کار می‌کند؟](#۵-پشت-صحنه-چطور-کار-میکند)
6. [نصب در اپلیکیشن واقعی (گام‌به‌گام)](#۶-نصب-در-اپلیکیشن-واقعی)
7. [تنظیمات](#۷-تنظیمات)
8. [راه‌اندازی استک مانیتورینگ](#۸-راهاندازی-استک-مانیتورینگ)
9. [اجرای دمو با یک دستور](#۹-اجرای-دمو-با-یک-دستور)
10. [کار با Grafana](#۱۰-کار-با-grafana)
11. [کوئری‌های کاربردی PromQL](#۱۱-کوئریهای-کاربردی-promql)
12. [امنیت endpoint متریک](#۱۲-امنیت-endpoint-متریک)
13. [عیب‌یابی](#۱۳-عیبیابی)
14. [Build و انتشار پکیج](#۱۴-build-و-انتشار-پکیج)
15. [سؤالات متداول](#۱۵-سؤالات-متداول)

---

## ۱. مسئله چه بود؟

شما کلی اپلیکیشن **.NET Framework** دارید، اما **هیچ دیدی نسبت به وضعیت آن‌ها ندارید**:

- نمی‌دانید همین الان چند کاربر همزمان از یک سیستم استفاده می‌کنند.
- نمی‌دانید یک سیستم چقدر CPU و RAM مصرف می‌کند.
- نمی‌دانید اصلاً یک اپ بالا هست یا پایین.
- نمی‌دانید کندی از کجاست و نرخ خطا چقدر است.

راه‌حل استاندارد صنعتی برای این مسئله، **Observability** (رصدپذیری) با Prometheus + Grafana است.
ما این را به‌شکل یک **پکیج NuGet آمادهٔ نصب** پیاده کردیم: نصبش می‌کنید، تمام.

```
┌─────────────────────┐   هر ۱۵ ثانیه       ┌────────────┐      ┌──────────┐
│   اپ ASP.NET شما    │ ◀── scrape /metrics ─│ Prometheus │ ───▶ │ Grafana  │
│ + KSC.Observability │                      │ (ذخیره)    │      │ (نمودار) │
└─────────────────────┘                      └────────────┘      └──────────┘
```

---

## ۲. مفاهیم پایه

برای اینکه ادامه برایتان واضح باشد، چند مفهوم را سریع مرور کنیم:

### Prometheus چیست؟
یک پایگاه‌دادهٔ سری‌زمانی (Time-Series DB) که **خودش به‌صورت دوره‌ای** (مثلاً هر ۱۵ ثانیه) به اپ شما
سر می‌زند و یک صفحهٔ متنی به نام `/metrics` را می‌خواند و مقادیر را ذخیره می‌کند. به این مدل
**Pull** می‌گویند (برخلاف مدل Push که اپ خودش داده می‌فرستد). مزیت Pull: اپ شما ساده می‌ماند و
Prometheus می‌داند هر هدف بالا هست یا نه (اگر scrape نشد، یعنی down).

### Grafana چیست؟
ابزار نمایش داشبورد. به Prometheus وصل می‌شود و با زبان **PromQL** کوئری می‌زند و نمودار/هشدار می‌سازد.

### فرمت متریک Prometheus
یک متن ساده است. هر خط یک نمونه است:

```
ksc_active_users{service="billing",instance="SRV01",env="production"} 24
└──── نام متریک ────┘ └──────────── لیبل‌ها ───────────────────────┘ └مقدار┘
```

### انواع متریک
| نوع | یعنی چه | مثال در پروژه |
|-----|---------|----------------|
| **Counter** | فقط زیاد می‌شود (شمارنده) | تعداد کل ریکوئست‌ها |
| **Gauge** | بالا/پایین می‌رود (عقربه‌ای) | کاربران فعال، مصرف RAM |
| **Histogram** | توزیع مقادیر در بازه‌ها | زمان پاسخ ریکوئست‌ها |

### لیبل و «Cardinality»
لیبل‌ها (مثل `method`, `code`) به متریک بُعد می‌دهند. اما **مراقب باشید**: اگر یک لیبل مقادیر
بی‌نهایت بگیرد (مثلاً مسیر کامل URL با id داخلش)، تعداد سری‌ها منفجر می‌شود و Prometheus کند/پر می‌شود.
به این مشکل **High Cardinality** می‌گویند. به همین دلیل ما لیبل `path` را **به‌صورت پیش‌فرض خاموش**
گذاشتیم.

---

## ۳. معماری پروژه

پروژه با **Clean Architecture** ساخته شده: لایه‌ها از داخل به بیرون، و وابستگی‌ها فقط رو به داخل.

```
KSC.Observability/
├── src/
│   ├── KSC.Observability.Abstractions   ← لایهٔ Core: قراردادها و Options (بدون هیچ وابستگی)
│   ├── KSC.Observability.Metrics        ← لایهٔ Infrastructure: prometheus-net (net472 + net8.0)
│   ├── KSC.Observability.AspNet         ← یکپارچه‌سازی .NET Framework (System.Web)
│   └── KSC.Observability.AspNetCore     ← یکپارچه‌سازی .NET 8 (Middleware + DI)
├── samples/
│   ├── KSC.Sample.SelfHost              ← دموی کنسولی (بدون IIS) برای تست سریع
│   ├── KSC.Sample.WebApp                ← نمونهٔ ASP.NET Framework (Visual Studio)
│   └── KSC.Sample.WebApi                ← نمونهٔ ASP.NET Core / .NET 8 (Minimal API)
├── tests/KSC.Observability.Tests        ← تست‌های واحد (xUnit)
├── deploy/                              ← Prometheus + Grafana (docker-compose) + داشبورد
├── build/pack.ps1                       ← ساخت پکیج‌های NuGet
├── up.cmd / down.cmd                    ← بالا/پایین آوردن کل محیط با یک دستور
└── .github/workflows/ci.yml             ← بیلد/تست/پکیج خودکار
```

| لایه | پروژه | Target | چرا؟ |
|------|-------|--------|------|
| Core | Abstractions | netstandard2.0 | فقط قرارداد و interface؛ به هیچ کتابخانه‌ای وابسته نیست تا قابل‌تعویض بماند |
| Infrastructure | Metrics | net472 + net8.0 | پیاده‌سازی واقعی متریک‌ها با `prometheus-net` (هستهٔ مشترک هر دو دنیا) |
| Integration | AspNet | net472 | اتصال به System.Web (HttpModule، endpoint، ثبت خودکار) |
| Integration | AspNetCore | net8.0 | اتصال به ASP.NET Core (Middleware، DI، endpoint) |

**جهت وابستگی:** `AspNet / AspNetCore → Metrics → Abstractions`. لایهٔ بیرونی همه را می‌شناسد؛ لایه‌های
داخلی هیچ‌چیزِ بیرونی را نمی‌شناسند. به همین خاطر **افزودن پشتیبانی .NET 8 فقط یک لایهٔ یکپارچه‌سازی
جدید بود** و هستهٔ متریک دست‌نخورده ماند — این دقیقاً فایدهٔ Clean Architecture است.

---

## ۴. متریک‌هایی که جمع می‌شوند

همهٔ متریک‌ها لیبل‌های `service`، `instance` و `env` را دارند (پیشوند `ksc` قابل‌تغییر است).

| متریک | نوع | لیبل اضافه | یعنی چه |
|-------|-----|-----------|---------|
| `ksc_active_users` | Gauge | — | تعداد کاربران فعالِ همزمان در بازهٔ زمانی تعیین‌شده 👥 |
| `ksc_http_requests_total` | Counter | `method`, `code` | تعداد کل ریکوئست‌ها |
| `ksc_http_requests_in_flight` | Gauge | — | ریکوئست‌هایی که همین الان در حال پردازش‌اند 🔄 |
| `ksc_http_request_duration_seconds` | Histogram | `method` | زمان پاسخ ریکوئست‌ها ⏱️ |
| `ksc_process_cpu_usage_percent` | Gauge | — | درصد CPU (نسبت به یک هسته) 🧠 |
| `ksc_process_working_set_bytes` | Gauge | — | حافظهٔ فیزیکی در حال استفاده |
| `ksc_process_private_memory_bytes` | Gauge | — | حافظهٔ خصوصی پراسس |
| `ksc_process_managed_memory_bytes` | Gauge | — | حجم heap مدیریت‌شدهٔ GC |
| `ksc_process_threads` | Gauge | — | تعداد threadها |
| `ksc_process_handles` | Gauge | — | تعداد handleهای سیستم‌عامل |
| `ksc_process_uptime_seconds` | Gauge | — | چند ثانیه است که پراسس بالاست |
| `ksc_gc_collections_total` | Counter | `generation` | تعداد GCها به‌تفکیک نسل ♻️ |
| `ksc_build_info` | Gauge | `version` | همیشه ۱؛ نسخهٔ کتابخانه را به‌عنوان لیبل دارد |

---

## ۵. پشت صحنه: چطور کار می‌کند؟

### ۵.۱ ثبت خودکار ماژول (بدون دستکاری web.config)
وقتی پکیج را نصب می‌کنید، این خط در اسمبلی وجود دارد:

```csharp
[assembly: PreApplicationStartMethod(typeof(ObservabilityHttpModule), "OnPreApplicationStart")]
```

ASP.NET این متد را **قبل از `Application_Start`** صدا می‌زند و ماژول با
`DynamicModuleUtility.RegisterModule` به‌صورت پویا ثبت می‌شود. به همین خاطر **نیازی به افزودن
`<modules>` در web.config ندارید** — فقط نصب کافی است.

### ۵.۲ شمارش کاربران همزمان
در هر ریکوئست (مرحلهٔ `PostAcquireRequestState`)، یک «کلید کاربر» استخراج می‌شود:
ابتدا `SessionID`، اگر نبود نام کاربر احرازهویت‌شده، و در نهایت IP. این کلید با زمانِ فعلی در یک
دیکشنری ذخیره می‌شود. یک تایمر پس‌زمینه هر چند ثانیه کلیدهای قدیمی‌تر از «پنجرهٔ زمانی» (پیش‌فرض ۵
دقیقه) را پاک می‌کند و تعداد باقی‌مانده را در گاج `ksc_active_users` می‌نویسد. نتیجه: تعداد کاربرانی
که در ۵ دقیقهٔ اخیر فعال بوده‌اند.

### ۵.۳ متریک‌های HTTP
- در ابتدای ریکوئست: گاج `in_flight` یک واحد زیاد می‌شود و یک `Stopwatch` شروع می‌شود.
- در انتهای ریکوئست: `in_flight` کم می‌شود، شمارندهٔ کل با لیبل‌های `method`/`code` زیاد می‌شود و
  مدت‌زمان در هیستوگرام ثبت می‌شود.

### ۵.۴ محاسبهٔ CPU
به‌جای `PerformanceCounter` (که روی IIS با نام‌گذاری instance شکننده است)، از اختلاف
`Process.TotalProcessorTime` در بازهٔ زمانی استفاده می‌کنیم:

```
CPU% = (Δ زمان پردازنده) / (Δ زمان واقعی × تعداد هسته) × ۱۰۰
```

این روش پایدار و دقیق است و به دسترسی خاصی نیاز ندارد.

### ۵.۵ endpoint متریک
خود ماژول، مسیر `/metrics` را تشخیص می‌دهد و خروجی Prometheus را می‌نویسد و درخواست را همان‌جا
کامل می‌کند (نیازی به ثبت Handler جداگانه نیست).

---

## ۶. نصب در اپلیکیشن واقعی

### گام ۱ — انتشار پکیج روی فید داخلی
پکیج‌ها در این مخزن ساخته می‌شوند (`build/pack.ps1`) و در پوشهٔ `artifacts` قرار می‌گیرند. آن‌ها را
روی فید داخلی شرکت (Azure Artifacts، BaGet، یا یک پوشهٔ اشتراکی) بگذارید تا اپ‌ها بتوانند نصب کنند.

### گام ۲ — نصب در اپ
در کنسول NuGet اپ ASP.NET:

```powershell
Install-Package KSC.Observability.AspNet
```

همین پکیج به‌صورت زنجیره‌ای `Metrics`، `Abstractions`، `prometheus-net` و
`Microsoft.Web.Infrastructure` را هم می‌آورد.

### گام ۳ — (اختیاری) تنظیم نام سرویس
در `Global.asax.cs`:

```csharp
protected void Application_Start(object sender, EventArgs e)
{
    KscObservability.Initialize(options =>
    {
        options.ServiceName = "billing-portal";
        options.Environment = "production";
    });
}
```

> اگر این کار را هم نکنید، ماژول در اولین ریکوئست خودش از روی web.config (یا پیش‌فرض‌ها) مقداردهی می‌شود.

### گام ۴ — تست
اپ را اجرا کنید و آدرس `/metrics` را باز کنید. باید خروجی متنی متریک‌ها را ببینید. تمام —
از این لحظه اپ شما قابل‌مانیتور است.

### ۶.۵ نصب در اپ‌های .NET 8 (ASP.NET Core)

برای اپ‌های مدرن، چون `System.Web` وجود ندارد، از پکیج جداگانهٔ **`KSC.Observability.AspNetCore`**
استفاده می‌کنید. هستهٔ متریک همان است؛ فقط نحوهٔ اتصال به پایپ‌لاین فرق دارد (Middleware + DI).

```powershell
Install-Package KSC.Observability.AspNetCore
```

سپس فقط **دو خط** در `Program.cs`:

```csharp
builder.Services.AddKscObservability(o => o.ServiceName = "billing-api");
var app = builder.Build();
app.UseKscObservability();   // متریک ریکوئست + کاربر فعال + endpoint /metrics
```

تنظیمات هم می‌تواند از `appsettings.json` بیاید (همان نام‌های Options زیر بخش `KSC.Observability`):

```json
{
  "KSC.Observability": {
    "ServiceName": "billing-api",
    "Environment": "production",
    "TrackRequestPath": true,
    "ActiveUserWindow": "00:05:00"
  }
}
```

نکته‌ها:
- اگر از Endpoint Routing استفاده می‌کنید، به‌جای اتکا به middleware می‌توانید
  `app.MapKscMetrics();` را هم صدا بزنید.
- لیبل `path` در Core از **route template** ساخته می‌شود (مثلاً `/orders/{id}`) که ذاتاً
  cardinality پایینی دارد.
- شمارش کاربر فعال از این ترتیب استفاده می‌کند: هویت احرازهویت‌شده → Session (اگر فعال باشد) → IP.

> **خلاصه:** اپ Framework؟ `KSC.Observability.AspNet`. اپ .NET 8؟ `KSC.Observability.AspNetCore`.
> بقیهٔ این راهنما (متریک‌ها، Prometheus، Grafana، PromQL) برای هر دو **یکسان** است.

---

## ۷. تنظیمات

دو راه دارید: **کد** (در `Initialize`) یا **بدون کد** (در `web.config`). اگر هر دو باشند، کد برنده است.

### از طریق web.config (بدون کد)

```xml
<appSettings>
  <add key="KSC.Observability:ServiceName" value="billing-portal" />
  <add key="KSC.Observability:Environment" value="production" />
  <add key="KSC.Observability:MetricsPath" value="/metrics" />
  <add key="KSC.Observability:ActiveUserWindowSeconds" value="300" />
  <!-- <add key="KSC.Observability:MetricsAccessToken" value="یک-توکن-محرمانه" /> -->
</appSettings>
```

### جدول کامل تنظیمات

| کلید (با پیشوند `KSC.Observability:`) | پیش‌فرض | معنی |
|----------------------------------------|---------|------|
| `ServiceName` | `dotnet-app` | لیبل `service` |
| `InstanceId` | نام ماشین | لیبل `instance` |
| `Environment` | `production` | لیبل `env` |
| `MetricPrefix` | `ksc` | پیشوند نام متریک‌ها |
| `MetricsPath` | `/metrics` | مسیر endpoint |
| `EnableSystemMetrics` | `true` | متریک‌های CPU/RAM/GC/... |
| `EnableHttpMetrics` | `true` | متریک‌های ریکوئست |
| `EnableActiveUserTracking` | `true` | شمارش کاربران فعال |
| `SystemMetricsIntervalSeconds` | `5` | بازهٔ نمونه‌برداری سیستم |
| `ActiveUserWindowSeconds` | `300` | پنجرهٔ «فعال بودن» کاربر |
| `TrackRequestPath` | `false` | افزودن لیبل `path` (مراقب cardinality!) |
| `MetricsAccessToken` | — | محافظت endpoint با Bearer Token |

---

## ۸. راه‌اندازی استک مانیتورینگ

پوشهٔ [`deploy/`](../deploy/) یک استک آمادهٔ Prometheus + Grafana دارد.

```bash
cd deploy
docker compose up -d
```

| سرویس | آدرس | ورود |
|-------|------|------|
| Grafana | http://localhost:3000 | admin / admin |
| Prometheus | http://localhost:9090 | — |

### وصل کردن Prometheus به اپ‌های شما
فایل `deploy/prometheus/prometheus.yml` را ویرایش کنید و آدرس `/metrics` هر اپ را زیر
`ksc-dotnet-apps` اضافه کنید:

```yaml
static_configs:
  - targets:
      - app-server-01:80
      - app-server-02:80
    labels:
      team: billing
```

سپس Prometheus را بدون قطعی reload کنید:

```bash
curl -X POST http://localhost:9090/-/reload
```

سلامت هدف‌ها را در http://localhost:9090/targets ببینید (باید `up` باشند).

---

## ۹. اجرای دمو با یک دستور

برای دیدن کل چرخه به‌صورت زنده (بدون نیاز به IIS)، از ریشهٔ پروژه و با **Docker روشن**:

```powershell
.\up.cmd
```

این یک دستور: اپ دمو را build می‌کند، استک را بالا می‌آورد، اپ نمونه را اجرا می‌کند، صبر می‌کند تا
همه healthy شوند و مرورگر را روی داشبورد باز می‌کند.

| دستور | کاربرد |
|------|--------|
| `.\up.cmd` | کل دمو (استک + اپ نمونه) |
| `.\up.cmd -NoDemo` | فقط استک مانیتورینگ (برای محیط واقعی) |
| `.\up.cmd -NoBrowser` | بدون باز کردن مرورگر |
| `.\down.cmd` | توقف اپ + استک |
| `.\down.cmd -Volumes` | توقف + پاک کردن دیتای ذخیره‌شده |

---

## ۱۰. کار با Grafana

۱. به http://localhost:3000 بروید (admin / admin؛ بار اول رمز را عوض کنید).
۲. داشبورد **KSC.Observability — Overview** به‌صورت خودکار provision شده (پوشهٔ KSC.Observability).
۳. از منوی **Service** بالای داشبورد، اپ موردنظر را انتخاب کنید (یا All).
۴. بازهٔ زمانی بالا-راست را روی **Last 15 minutes** و auto-refresh را روشن کنید.

پنل‌های داشبورد: کاربران فعال، ریکوئست در حال پردازش، نرخ ریکوئست/خطا، latency (p50/p95/p99)،
CPU، حافظه، thread/handle و GC.

> داشبورد از فایل ساخته می‌شود (`deploy/grafana/dashboards/ksc-overview.json`). برای تغییر دائمی،
> همین فایل را ویرایش کنید (تا با restart از بین نرود).

---

## ۱۱. کوئری‌های کاربردی PromQL

```promql
# کاربران همزمان هر اپ
sum by (service) (ksc_active_users)

# نرخ ریکوئست (در ثانیه)
sum by (service) (rate(ksc_http_requests_total[5m]))

# نسبت خطا (5xx)
sum by (service) (rate(ksc_http_requests_total{code=~"5.."}[5m]))
  / sum by (service) (rate(ksc_http_requests_total[5m]))

# صدک ۹۵ زمان پاسخ
histogram_quantile(0.95, sum by (le) (rate(ksc_http_request_duration_seconds_bucket[5m])))

# آیا اپ پایین است؟ (scrape نشده)
up{job="ksc-dotnet-apps"} == 0

# مصرف حافظه (مگابایت)
ksc_process_working_set_bytes / 1024 / 1024
```

---

## ۱۲. امنیت endpoint متریک

به‌صورت پیش‌فرض `/metrics` باز است. اگر اپ روی شبکهٔ غیرامن قرار دارد، یک توکن تعیین کنید:

```xml
<add key="KSC.Observability:MetricsAccessToken" value="یک-توکن-محرمانه" />
```

سپس در Prometheus همان توکن را بدهید:

```yaml
authorization:
  type: Bearer
  credentials: "یک-توکن-محرمانه"
```

از این به بعد هر درخواست بدون هدر `Authorization: Bearer ...` با کد ۴۰۱ رد می‌شود.

> توصیهٔ دیگر: endpoint را در سطح شبکه/فایروال فقط برای IPِ سرور Prometheus باز بگذارید.

---

## ۱۳. عیب‌یابی

| نشانه | علت محتمل | راه‌حل |
|-------|-----------|--------|
| در `/targets` هدف `down` با خطای *connection refused* است | اپ بالا نیست یا پورت اشتباه است | آدرس و پورت `/metrics` را بررسی کنید |
| هدف `down` با *unexpected EOF* | پاسخ ناقص از سرور | معمولاً اپ کرش کرده؛ لاگ اپ را ببینید |
| `host.docker.internal` از داخل کانتینر کار نمی‌کند | شبکهٔ Docker | در docker-compose از `extra_hosts: host-gateway` استفاده شده؛ روی لینوکس بررسی کنید |
| متریک‌ها در `/metrics` هست ولی Grafana خالی است | datasource/زمان | بازهٔ زمانی Grafana و `up` بودن هدف را چک کنید |
| `active_users` صفر می‌ماند | تازه استارت خورده | تایمر هر ۱۵ ثانیه گاج را منتشر می‌کند؛ کمی صبر کنید |
| `cpu_usage_percent` صفر است | اپ واقعاً بی‌کار است | زیر بار واقعی مقدار می‌گیرد؛ این درست است |

---

## ۱۴. Build و انتشار پکیج

```powershell
# Restore + Test + Pack در پوشهٔ artifacts
.\build\pack.ps1

# یا دستی
dotnet build  KSC.Observability.sln -c Release
dotnet test   KSC.Observability.sln -c Release
dotnet pack   KSC.Observability.sln -c Release
```

برای انتشار روی فید داخلی:

```powershell
dotnet nuget push artifacts/*.nupkg --source <آدرس-فید> --api-key <کلید>
```

> **نکته دربارهٔ نسخهٔ .NET SDK:** نسخه در `global.json` پین شده تا بیلد پایدار بماند. اگر روی
> سیستمی چند نسخهٔ dotnet نصب است (مثلاً یک نسخهٔ x86 خراب)، از hostِ ۶۴-بیتی
> (`C:\Program Files\dotnet\dotnet.exe`) استفاده کنید — اسکریپت‌های `up.ps1`/`pack.ps1` این کار را
> خودکار انجام می‌دهند.

### CI
فایل `.github/workflows/ci.yml` روی هر push به `main` (روی ویندوز) restore/build/test/pack می‌کند و
پکیج‌ها و نتایج تست را به‌عنوان artifact آپلود می‌کند.

---

## ۱۵. سؤالات متداول

**چرا net472؟** چون اپ‌های هدف شما .NET Framework هستند. کتابخانهٔ Abstractions روی
netstandard2.0 است تا حداکثر سازگاری داشته باشد.

**آیا روی Windows Service یا WCF هم کار می‌کند؟** هستهٔ متریک (Metrics) بله؛ اما لایهٔ AspNet مخصوص
System.Web است. برای Windows Service می‌توان مثل `KSC.Sample.SelfHost` با `PrometheusObservabilityRuntime`
به‌صورت self-host استفاده کرد. (در صورت نیاز می‌توان یک پکیج جدا برایش ساخت.)

**مدل Push می‌خواهم نه Pull.** Prometheus ذاتاً Pull است؛ برای اپ‌های پشت فایروال می‌توان از
Pushgateway یا بک‌اند Push مثل InfluxDB استفاده کرد (تغییر در لایهٔ Metrics).

**چقدر سربار دارد؟** بسیار کم: چند گاج/شمارنده در حافظه و یک تایمر سبک. متریک‌ها فقط هنگام scrape
سریالایز می‌شوند.

**آیا روی کاردینالیتی باید نگران باشم؟** فقط اگر `TrackRequestPath` را روشن کنید و مسیرها id داشته
باشند. در آن صورت مسیرها را normalize کنید (مثلاً `/orders/{id}` به‌جای `/orders/12345`).

---

## ۱۶. ضمیمه: بازتولید پروژه از صفر (برای تیم‌های دیگر)

این بخش طوری نوشته شده که اگر **فقط همین داکیومنت** را در اختیار یک تیم دیگر بگذارید، بتوانند کل
پروژه را از صفر بسازند. ترتیب کار دقیقاً همان ۱۰ مرحله‌ای است که پروژه ساخته شد.

### ۱۶.۰ پیش‌نیازها
- **.NET SDK** (نسخهٔ 8 یا 9). در `global.json` پین می‌شود.
- **.NET Framework 4.7.2 Targeting Pack** (روی ویندوز؛ برای بیلد net472).
- **Git** و یک مخزن خالی.
- **Docker Desktop** (برای استک مانیتورینگ).
- (اختیاری) **Visual Studio 2019/2022** برای اپ نمونهٔ Web Forms.

### ۱۶.۱ ساختار نهایی که قرار است بسازیم
```
KSC.Observability/
├── global.json, Directory.Build.props, NuGet.config, .gitignore, .gitattributes
├── KSC.Observability.sln
├── src/{Abstractions, Metrics, AspNet}
├── tests/KSC.Observability.Tests
├── samples/{KSC.Sample.SelfHost, KSC.Sample.WebApp}
├── deploy/{docker-compose.yml, prometheus/, grafana/}
├── build/pack.ps1 , up.ps1 , down.ps1 , up.cmd , down.cmd
└── .github/workflows/ci.yml
```

### مرحله ۱ — اسکلت و فایل‌های پایه

`global.json` (SDK را پین می‌کند تا بیلد پایدار بماند):
```json
{ "sdk": { "version": "9.0.314", "rollForward": "latestMinor", "allowPrerelease": false } }
```

`Directory.Build.props` (تنظیمات مشترک همهٔ پروژه‌ها + متادیتای پکیج):
```xml
<Project>
  <PropertyGroup>
    <Product>KSC.Observability</Product>
    <VersionPrefix>0.1.0</VersionPrefix>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageOutputPath>$(MSBuildThisFileDirectory)artifacts</PackageOutputPath>
  </PropertyGroup>
  <ItemGroup Condition="'$(IsPackable)' == 'true'">
    <None Include="$(MSBuildThisFileDirectory)README.md" Pack="true" PackagePath="\" Visible="false" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All" />
  </ItemGroup>
</Project>
```

سپس solution بسازید: `dotnet new sln -n KSC.Observability`.

### مرحله ۲ — لایهٔ Core (Abstractions)
پروژه: `src/KSC.Observability.Abstractions` با `TargetFramework=netstandard2.0` و `RootNamespace=KSC.Observability`.

**`ObservabilityOptions.cs`** — تمام تنظیمات با مقدار پیش‌فرض:
```csharp
public sealed class ObservabilityOptions
{
    public string ServiceName { get; set; } = "dotnet-app";
    public string InstanceId { get; set; } = System.Environment.MachineName;
    public string Environment { get; set; } = "production";
    public string MetricPrefix { get; set; } = "ksc";
    public string MetricsPath { get; set; } = "/metrics";
    public bool EnableSystemMetrics { get; set; } = true;
    public bool EnableHttpMetrics { get; set; } = true;
    public bool EnableActiveUserTracking { get; set; } = true;
    public TimeSpan SystemMetricsInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan ActiveUserWindow { get; set; } = TimeSpan.FromMinutes(5);
    public bool TrackRequestPath { get; set; } = false;
    public double[] RequestDurationSecondsBuckets { get; set; }
        = { 0.005,0.01,0.025,0.05,0.1,0.25,0.5,1,2.5,5,10 };
    public string? MetricsAccessToken { get; set; }
    public void Validate() { /* بررسی خالی‌نبودن ServiceName، شروع MetricsPath با '/'، مثبت بودن بازه‌ها */ }
}
```

**چهار interface کلیدی** (همگی در namespace `KSC.Observability`):
```csharp
public interface ISystemMetricsCollector : IDisposable { void Start(); void Stop(); }

public interface IHttpMetricsRecorder {
    void RequestStarted();
    void RequestCompleted(string method, string? path, int statusCode, double elapsedSeconds);
}

public interface IActiveUserTracker : IDisposable { void Touch(string userKey); int CurrentCount { get; } }

public interface IObservabilityRuntime : IDisposable {
    ObservabilityOptions Options { get; }
    IHttpMetricsRecorder Http { get; }
    IActiveUserTracker Users { get; }
    void WriteMetrics(System.IO.Stream output);
}
```

**`ObservabilityHost.cs`** — یک نگه‌دارندهٔ سراسری (process-wide) برای runtime فعال:
```csharp
public static class ObservabilityHost
{
    static IObservabilityRuntime? _current; static readonly object Gate = new object();
    public static bool IsInitialized => _current != null;
    public static IObservabilityRuntime Current => _current ?? throw new InvalidOperationException("not initialized");
    public static IObservabilityRuntime? TryGet() => _current;
    public static void SetRuntime(IObservabilityRuntime rt) {
        lock (Gate) { if (_current == null) _current = rt; else rt.Dispose(); } // اولین برنده است
    }
}
```
به‌علاوه ثابت‌های `LabelNames` (service, instance, env, method, code, path, generation) و
`MetricSuffixes` (نام متریک‌ها بدون پیشوند).

### مرحله ۳ — لایهٔ Infrastructure (Metrics)
پروژه: `src/KSC.Observability.Metrics` با `TargetFramework=net472`، ارجاع به Abstractions و
`PackageReference: prometheus-net 8.2.1`.

**نکتهٔ کلیدی محاسبهٔ CPU** (در `PrometheusSystemMetricsCollector`): یک تایمر هر چند ثانیه نمونه می‌گیرد:
```csharp
_process.Refresh();
var nowUtc = DateTime.UtcNow;
var cpuNow = _process.TotalProcessorTime;
var wall   = (nowUtc - _lastSampleUtc).TotalSeconds;
if (wall > 0) {
    var cpu = (cpuNow - _lastCpuTime).TotalSeconds;
    var cores = Math.Max(1, Environment.ProcessorCount);
    _cpu.Set(Math.Round(cpu / (wall * cores) * 100.0, 2));
}
_lastSampleUtc = nowUtc; _lastCpuTime = cpuNow;
_workingSet.Set(_process.WorkingSet64);
_managedMemory.Set(GC.GetTotalMemory(false));
_threads.Set(_process.Threads.Count);
_uptime.Set((nowUtc - _startedUtc).TotalSeconds);
for (int g = 0; g <= GC.MaxGeneration; g++)
    _gcCollections.WithLabels(g.ToString()).IncTo(GC.CollectionCount(g)); // Counter یکنواخت
```
گاج‌ها/شمارنده‌ها با `IMetricFactory` ساخته می‌شوند؛ نام = `prefix + "_" + suffix`.

**`PrometheusHttpMetricsRecorder`** — سه متریک:
```csharp
_requestsTotal = factory.CreateCounter(name("http_requests_total"),
    "...", new CounterConfiguration { LabelNames = new[]{ "method","code" } });   // + "path" اگر فعال
_inFlight = factory.CreateGauge(name("http_requests_in_flight"), "...");
_duration = factory.CreateHistogram(name("http_request_duration_seconds"), "...",
    new HistogramConfiguration { Buckets = opts.RequestDurationSecondsBuckets, LabelNames = new[]{ "method" } });
// RequestStarted: _inFlight.Inc();
// RequestCompleted: _inFlight.Dec(); _requestsTotal.WithLabels(...).Inc(); _duration.WithLabels(method).Observe(sec);
```

**`PrometheusActiveUserTracker`** — منطق پنجرهٔ کشویی:
```csharp
ConcurrentDictionary<string,long> _lastSeen;       // userKey -> ticks
public void Touch(string k){ if(!string.IsNullOrEmpty(k)) _lastSeen[k] = DateTime.UtcNow.Ticks; }
public void Prune(){                                 // تایمر هر window/4 صدا می‌زند
    var cutoff = DateTime.UtcNow.Ticks - _windowTicks;
    foreach (var p in _lastSeen) if (p.Value < cutoff) _lastSeen.TryRemove(p.Key, out _);
    _activeUsers.Set(_lastSeen.Count);
}
```

**`PrometheusObservabilityRuntime`** — composition root:
```csharp
_registry = Metrics.NewCustomRegistry();                       // رجیستری ایزوله
_registry.SetStaticLabels(new Dictionary<string,string>{        // لیبل‌های ثابت روی همهٔ متریک‌ها
    ["service"]=opts.ServiceName, ["instance"]=opts.InstanceId, ["env"]=opts.Environment });
var factory = Metrics.WithCustomRegistry(_registry);
// build_info, Http, Users و در صورت فعال بودن SystemCollector ساخته و Start می‌شوند.
public void WriteMetrics(Stream s) => _registry.CollectAndExportAsTextAsync(s).GetAwaiter().GetResult();
```

**`ObservabilityBootstrapper`** — مقداردهی idempotent:
```csharp
public static IObservabilityRuntime Initialize(ObservabilityOptions o){
    var ex = ObservabilityHost.TryGet(); if (ex != null) return ex;
    lock (Gate){ ex = ObservabilityHost.TryGet(); if (ex != null) return ex;
        ObservabilityHost.SetRuntime(new PrometheusObservabilityRuntime(o)); return ObservabilityHost.Current; }
}
```

### مرحله ۴ — لایهٔ Integration (AspNet)
پروژه: `src/KSC.Observability.AspNet` با `net472`، ارجاع به Abstractions و Metrics،
`PackageReference: Microsoft.Web.Infrastructure 2.0.0`، و `Reference: System.Web, System.Configuration`.

**ثبت خودکار ماژول** — فایل `PreApplicationStart.cs`:
```csharp
[assembly: PreApplicationStartMethod(typeof(ObservabilityHttpModule),
    nameof(ObservabilityHttpModule.OnPreApplicationStart))]
```
و در ماژول:
```csharp
public static void OnPreApplicationStart() =>
    Microsoft.Web.Infrastructure.DynamicModuleHelper.DynamicModuleUtility
        .RegisterModule(typeof(ObservabilityHttpModule));
```

**هستهٔ `ObservabilityHttpModule`**:
```csharp
public void Init(HttpApplication ctx){
    KscObservability.EnsureInitialized();                 // اگر مقداردهی نشده، با web.config/پیش‌فرض
    ctx.BeginRequest            += OnBeginRequest;
    ctx.PostAcquireRequestState += OnPostAcquireRequestState; // اینجا Session در دسترس است
    ctx.EndRequest              += OnEndRequest;
}
// OnBeginRequest: اگر مسیر == MetricsPath → ServeMetrics و CompleteRequest؛ وگرنه Http.RequestStarted + Stopwatch
// OnPostAcquireRequestState: کلید کاربر (SessionID → نام کاربر → IP) را Users.Touch می‌کند
// OnEndRequest: Stopwatch را می‌خواند و Http.RequestCompleted(method, path?, status, sec)
```
سرو متریک:
```csharp
response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
runtime.WriteMetrics(response.OutputStream);
app.CompleteRequest();
```
به‌علاوه `KscObservability.Initialize(Action<ObservabilityOptions>?)` (façade) و
`AppSettingsOptionsBinder` که کلیدهای `KSC.Observability:*` را از web.config می‌خواند.

### مرحله ۴.۵ — لایهٔ Integration برای .NET 8 (AspNetCore)
برای پشتیبانی از .NET 8، ابتدا `Metrics` را چندهدفه کنید تا روی هر دو دنیا قابل‌استفاده باشد:
```xml
<TargetFrameworks>net472;net8.0</TargetFrameworks>
```
سپس پروژهٔ `src/KSC.Observability.AspNetCore` با `TargetFramework=net8.0`،
`<FrameworkReference Include="Microsoft.AspNetCore.App" />` و ارجاع به Abstractions و Metrics بسازید.
سه فایل کلیدی:
```csharp
// AddKscObservability: ساخت runtime به‌عنوان singleton، با bind از بخش "KSC.Observability"
services.AddSingleton<IObservabilityRuntime>(sp => {
    var o = new ObservabilityOptions();
    sp.GetService<IConfiguration>()?.GetSection("KSC.Observability").Bind(o);
    configure?.Invoke(o);
    return ObservabilityBootstrapper.Initialize(o);
});

// ObservabilityMiddleware.Invoke: اگر مسیر == MetricsPath → سرو متریک؛ وگرنه:
if (opts.EnableActiveUserTracking) TouchUser(ctx);     // identity → session → ip
runtime.Http.RequestStarted();
var ts = Stopwatch.GetTimestamp();
try { await _next(ctx); }
finally {
    var sec = Stopwatch.GetElapsedTime(ts).TotalSeconds;
    string? path = opts.TrackRequestPath ? RouteOf(ctx) : null;   // از RouteEndpoint.RoutePattern
    runtime.Http.RequestCompleted(ctx.Request.Method, path, ctx.Response.StatusCode, sec);
}

// UseKscObservability: GetRequiredService<IObservabilityRuntime>() (اجباری‌سازی ساخت) + UseMiddleware
```
استفادهٔ نهایی در `Program.cs`: `AddKscObservability(...)` و `app.UseKscObservability();`.

### مرحله ۵ — تست‌ها
پروژه: `tests/KSC.Observability.Tests` (net472، xUnit). تست‌های کلیدی: اعتبارسنجی Options،
شمارش/پاکسازی کاربران فعال، و خروجی exposition (شمارنده/گاج/هیستوگرام و لیبل‌های ثابت). اجرا با
`dotnet test`.

### مرحله ۶ — بسته‌بندی NuGet
در سه پروژهٔ `src/*` مقادیر `IsPackable=true` و `PackageId` را ست کنید. چون پروژه‌ها به‌هم
`ProjectReference` دارند و همگی packable‌اند، NuGet آن‌ها را به‌صورت **وابستگی پکیج** تبدیل می‌کند؛
یعنی نصب `KSC.Observability.AspNet` بقیه را هم می‌آورد. تولید با `dotnet pack -c Release`.

### مرحله ۷ — استقرار (Prometheus + Grafana)
`deploy/docker-compose.yml`:
```yaml
services:
  prometheus:
    image: prom/prometheus:v2.54.1
    ports: ["9090:9090"]
    volumes:
      - ./prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus-data:/prometheus
    command: ["--config.file=/etc/prometheus/prometheus.yml","--storage.tsdb.retention.time=30d"]
    extra_hosts: ["host.docker.internal:host-gateway"]
  grafana:
    image: grafana/grafana:11.2.0
    ports: ["3000:3000"]
    environment: { GF_SECURITY_ADMIN_USER: admin, GF_SECURITY_ADMIN_PASSWORD: admin }
    volumes:
      - ./grafana/provisioning:/etc/grafana/provisioning:ro
      - ./grafana/dashboards:/var/lib/grafana/dashboards:ro
      - grafana-data:/var/lib/grafana
    depends_on: [prometheus]
volumes: { prometheus-data: {}, grafana-data: {} }
```
`deploy/prometheus/prometheus.yml` (هدف = اپ‌های شما)، و در
`deploy/grafana/provisioning/` دو فایل: datasource (به `http://prometheus:9090` با `uid: PROMETHEUS`)
و dashboard provider (مسیر `/var/lib/grafana/dashboards`). داشبورد را به‌صورت یک فایل JSON در
`deploy/grafana/dashboards/ksc-overview.json` بسازید (پنل‌ها: کاربران فعال، in-flight، نرخ ریکوئست/خطا،
p50/p95/p99، CPU، حافظه، thread/handle، GC؛ با متغیر `service`). ساده‌ترین راه: داشبورد را در Grafana
طراحی و سپس **Export → Save to file** کنید.

### مرحله ۸ — CI (GitHub Actions)
`.github/workflows/ci.yml` روی `windows-latest` (به‌خاطر net472): `setup-dotnet` با
`global-json-file: global.json`، سپس `restore` → `build -c Release` → `test` → `pack` و آپلود
artifactها.

### مرحله ۹ — نمونه‌ها
- `samples/KSC.Sample.SelfHost`: کنسول net472 که با `TcpListener` روی `0.0.0.0:9184` یک سرور
  HTTP کوچک می‌سازد و `runtime.WriteMetrics` را روی `/metrics` سرو می‌کند و خودش ترافیک تولید می‌کند.
  **نکتهٔ مهم:** قبل از بستن سوکت، کل هدرهای درخواست را بخوانید (drain) تا ویندوز به‌جای FIN یک RST
  نفرستد و Prometheus خطای *unexpected EOF* نگیرد.
- `samples/KSC.Sample.WebApp`: اپ کلاسیک ASP.NET Web Forms که پکیج را با `PackageReference` نصب
  می‌کند (دقیقاً مثل مصرف واقعی).

### مرحله ۱۰ — اسکریپت یک‌دستوری
`up.ps1` (+ wrapper `up.cmd`): بررسی Docker → build دمو → `docker compose up -d` → اجرای اپ به‌صورت
پراسس detached → انتظار تا سلامت → باز کردن مرورگر. `down.ps1` معکوسش را انجام می‌دهد. نکته: hostِ
۶۴-بیتی dotnet را ترجیح دهید (`$env:ProgramFiles\dotnet\dotnet.exe`) تا با نسخهٔ x86 احتمالی تداخل نکند.

### نقشهٔ کامیت مرحله‌ای (استاندارد، Conventional Commits)
پروژه دقیقاً با این ترتیب کامیت شد — همین الگو را دنبال کنید:
```
1.  chore: scaffold solution structure and core abstractions
2.  feat(metrics): add Prometheus-based collectors and composition root
3.  feat(aspnet): add System.Web integration with auto-registered HttpModule
4.  test: add unit tests for options, active users and exposition
5.  build(nuget): enable packaging with symbols, readme and Source Link
6.  feat(deploy): add Prometheus + Grafana stack with provisioned dashboard
7.  feat(sample): add ASP.NET Web Forms sample and local NuGet feed
8.  ci: add GitHub Actions build/test/pack workflow
9.  docs: write full usage guide, metrics reference and changelog
10. feat: one-command launcher for the demo environment
```

### چک‌لیست تأیید (وقتی تمام شد)
- [ ] `dotnet build KSC.Observability.sln -c Release` بدون خطا.
- [ ] `dotnet test` همهٔ تست‌ها سبز.
- [ ] `dotnet pack -c Release` سه فایل `.nupkg` در `artifacts` می‌سازد.
- [ ] `.\up.cmd` همه‌چیز را بالا می‌آورد و در `http://localhost:9090/targets` هدف `up` است.
- [ ] در Grafana، داشبورد با داده پر می‌شود.

---

## جمع‌بندی

| می‌خواهید… | این کار را بکنید |
|-----------|------------------|
| یک اپ را مانیتور کنید | `Install-Package KSC.Observability.AspNet` |
| استک مانیتورینگ را بالا بیاورید | `.\up.cmd -NoDemo` و ویرایش `prometheus.yml` |
| همه‌چیز را زنده ببینید | `.\up.cmd` |
| پکیج بسازید | `.\build\pack.ps1` |
| داشبورد را ببینید | http://localhost:3000 |

موفق باشید! 🚀

</div>
