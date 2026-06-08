<div dir="rtl">

# راهنمای آموزشی KSC.Observability

> مانیتورینگ و متریک آمادهٔ نصب برای اپلیکیشن‌های **.NET Framework** (ASP.NET Web Forms / MVC)
> بر پایهٔ **Prometheus** و **Grafana**.

این سند هم **توضیح می‌دهد چه چیزی ساخته شد و چرا**، و هم **گام‌به‌گام یاد می‌دهد چطور استفاده کنید**.

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
│   ├── KSC.Observability.Metrics        ← لایهٔ Infrastructure: پیاده‌سازی با prometheus-net
│   └── KSC.Observability.AspNet         ← لایهٔ Integration: همان پکیجی که نصب می‌کنید
├── samples/
│   ├── KSC.Sample.SelfHost              ← دموی کنسولی (بدون IIS) برای تست سریع
│   └── KSC.Sample.WebApp                ← نمونهٔ واقعی ASP.NET (برای Visual Studio)
├── tests/KSC.Observability.Tests        ← تست‌های واحد (xUnit)
├── deploy/                              ← Prometheus + Grafana (docker-compose) + داشبورد
├── build/pack.ps1                       ← ساخت پکیج‌های NuGet
├── up.cmd / down.cmd                    ← بالا/پایین آوردن کل محیط با یک دستور
└── .github/workflows/ci.yml             ← بیلد/تست/پکیج خودکار
```

| لایه | پروژه | Target | چرا؟ |
|------|-------|--------|------|
| Core | Abstractions | netstandard2.0 | فقط قرارداد و interface؛ به هیچ کتابخانه‌ای وابسته نیست تا قابل‌تعویض بماند |
| Infrastructure | Metrics | net472 | پیاده‌سازی واقعی متریک‌ها با `prometheus-net` |
| Integration | AspNet | net472 | اتصال به System.Web (HttpModule، endpoint، ثبت خودکار) |

**جهت وابستگی:** `AspNet → Metrics → Abstractions`. لایهٔ بیرونی (AspNet) همه را می‌شناسد؛ لایه‌های
داخلی هیچ‌چیزِ بیرونی را نمی‌شناسند. نتیجه: اگر فردا خواستید به‌جای Prometheus از سیستم دیگری استفاده
کنید، فقط لایهٔ Metrics را عوض می‌کنید و قراردادها (Abstractions) و کد اپ شما دست‌نخورده می‌ماند.

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
