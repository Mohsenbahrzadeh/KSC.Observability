<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="KSC.Sample.WebApp.Default" %>
<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <title>KSC.Observability sample</title>
    <style>
        body { font-family: Segoe UI, sans-serif; max-width: 720px; margin: 3rem auto; line-height: 1.6; }
        code { background: #f2f2f2; padding: .1rem .3rem; border-radius: 3px; }
        a { color: #2563eb; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <h1>KSC.Observability sample app</h1>
        <p>Server time: <strong><asp:Label ID="TimeLabel" runat="server" /></strong></p>
        <p>
            This request was timed and counted. Metrics are exposed at
            <a href="/metrics"><code>/metrics</code></a>.
        </p>
        <p>Refresh a few times, then open the metrics endpoint to watch the counters move.</p>
    </form>
</body>
</html>
