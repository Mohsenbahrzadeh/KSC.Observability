# KSC.Observability

پروژه محلی برای راه‌اندازی **Observability Stack** مبتنی بر OpenTelemetry و Prometheus، احتمالاً برای مانیتورینگ **Kaspersky Security Center (KSC)**.

این پروژه شامل ابزارهای لازم برای جمع‌آوری، پردازش و نمایش متریک‌های سیستم (به ویژه متریک‌های صادرشده توسط KSC روی پورت Prometheus) است و امکان مانیتورینگ پیشرفته را فراهم می‌کند.

## ویژگی‌ها
- اجرای **OpenTelemetry Collector** برای جمع‌آوری و پردازش telemetry data (metrics, traces, logs)
- اجرای **Prometheus** برای scraping و ذخیره‌سازی متریک‌ها
- اسکریپت PowerShell ساده برای راه‌اندازی همزمان سرویس‌ها
- مناسب برای محیط‌های محلی/تست یا مانیتورینگ KSC با استفاده از exporter داخلی Prometheus آن (پورت 13296)

## پیش‌نیازها
- Windows (به دلیل فایل‌های .exe و اسکریپت PowerShell)
- فایل‌های باینری دانلودشده:
  - `otelcol.exe` (OpenTelemetry Collector Contrib)
  - `prometheus.exe`
  - `promtool.exe` (اختیاری)

## ساختار پروژه
